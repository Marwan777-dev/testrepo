// src/components/kpi/CxiSpiderPreview.tsx
import { useKpis } from "@/contexts/kpi-context"

// Mock KPI performance scores used for the radar visualization
const MOCK_SCORES: Record<string, number> = {
  nps: 71,
  csat: 78,
  ces: 55,
  fcr: 58,
  vfm: 72,
  agent: 84,
}
const DEFAULT_SCORE = 65

function perfColor(v: number): string {
  if (v >= 85) return "#1A7A3C"
  if (v >= 75) return "#2EB85C"
  if (v >= 60) return "#E8A020"
  if (v >= 45) return "#E05C1A"
  return "#C01B2A"
}

interface CxiSpiderPreviewProps {
  weights: Record<string, number>
  shortName?: string
}

export default function CxiSpiderPreview({ weights, shortName = "CXI" }: CxiSpiderPreviewProps) {
  const { kpis } = useKpis()

  const activeKpis = kpis.filter((k) => k.isActive && k.id !== "cxi")
  const n = activeKpis.length

  if (n < 3) {
    return (
      <p className="text-sm text-muted-foreground text-center py-8">
        At least 3 active KPIs are required for this visualization.
      </p>
    )
  }

  // Weighted composite score from mock performance data — updates live as weights change
  const totalWeight = activeKpis.reduce((s, k) => s + (weights[k.id] ?? 0), 0)
  const cxiScore = totalWeight > 0
    ? Math.round(
        activeKpis.reduce((s, k) => {
          const w = weights[k.id] ?? 0
          const score = MOCK_SCORES[k.id] ?? DEFAULT_SCORE
          return s + (w / totalWeight) * score
        }, 0),
      )
    : 0

  const SIZE = 280
  const CX = SIZE / 2
  const CY = SIZE / 2
  const R = SIZE * 0.33  // outer boundary radius

  const toAngle = (i: number) => -Math.PI / 2 + (i / n) * 2 * Math.PI

  const verts = activeKpis.map((k, i) => {
    const a = toAngle(i)
    const score = MOCK_SCORES[k.id] ?? DEFAULT_SCORE
    const dr = (score / 100) * R
    return {
      kpi: k, score, a,
      bx: CX + R * Math.cos(a),
      by: CY + R * Math.sin(a),
      dx: CX + dr * Math.cos(a),
      dy: CY + dr * Math.sin(a),
    }
  })

  const dataPoints = verts.map(v => `${v.dx},${v.dy}`).join(" ")
  const boundaryPoints = verts.map(v => `${v.bx},${v.by}`).join(" ")

  const GRIDS = [0.2, 0.4, 0.6, 0.8, 1.0]
  const gridPts = (level: number) =>
    activeKpis.map((_, i) => {
      const a = toAngle(i)
      const r = level * R
      return `${CX + r * Math.cos(a)},${CY + r * Math.sin(a)}`
    }).join(" ")

  const SPOKE_COLOR = "rgba(148,163,184,0.45)"
  const BOUNDARY_COLOR = "rgba(148,163,184,0.7)"

  return (
    <div className="space-y-2">
      <p className="text-xs text-muted-foreground">Current performance vs normalized target</p>

      <div className="relative">
        {/* Live CXI composite score badge — updates as weights change */}
        <div className="absolute top-0 end-0 z-10 flex flex-col items-center justify-center w-[72px] h-[72px] rounded-full bg-nb-navy text-white shadow-lg">
          <span className="text-[8px] font-bold tracking-widest uppercase leading-none mt-1">
            {shortName}
          </span>
          <span className="text-[26px] font-heading font-bold tabular-nums leading-none">
            {cxiScore}
          </span>
          <span className="text-[9px] leading-none mb-1 text-nb-navy-300">
            /100
          </span>
        </div>

        <svg
          width="100%"
          viewBox={`0 0 ${SIZE} ${SIZE}`}
          role="img"
          aria-label={`${shortName} performance radar chart`}
        >
          <defs>
            {/* Radial gradient: red (low performance, center) → green (high, outer boundary) */}
            <radialGradient
              id="cxi-perf-gradient"
              gradientUnits="userSpaceOnUse"
              cx={CX}
              cy={CY}
              r={R}
            >
              <stop offset="0%"   stopColor="#C01B2A" stopOpacity={0.55} />
              <stop offset="35%"  stopColor="#E05C1A" stopOpacity={0.40} />
              <stop offset="65%"  stopColor="#E8A020" stopOpacity={0.28} />
              <stop offset="100%" stopColor="#2EB85C" stopOpacity={0.18} />
            </radialGradient>
          </defs>

          {/* Grid polygons at 20/40/60/80/100% */}
          {GRIDS.map(level => (
            <polygon
              key={level}
              points={gridPts(level)}
              fill="none"
              stroke={SPOKE_COLOR}
              strokeWidth={0.75}
            />
          ))}

          {/* Axis spokes from center to boundary */}
          {verts.map((v, i) => (
            <line
              key={i}
              x1={CX} y1={CY}
              x2={v.bx} y2={v.by}
              stroke={SPOKE_COLOR}
              strokeWidth={0.75}
            />
          ))}

          {/* Data polygon filled with radial performance gradient */}
          <polygon points={dataPoints} fill="url(#cxi-perf-gradient)" />

          {/* Outer boundary (stroke only — card background shows through to white-out the gap) */}
          <polygon points={boundaryPoints} fill="none" stroke={BOUNDARY_COLOR} strokeWidth={1} />

          {/* Per-vertex dots colored by performance level */}
          {verts.map((v, i) => (
            <circle
              key={i}
              cx={v.dx} cy={v.dy}
              r={5}
              fill={perfColor(v.score)}
              stroke="white"
              strokeWidth={1.5}
            />
          ))}

          {/* Score value labels near each dot */}
          {verts.map((v, i) => {
            const lx = CX + ((v.score / 100) * R + 15) * Math.cos(v.a)
            const ly = CY + ((v.score / 100) * R + 15) * Math.sin(v.a)
            return (
              <text
                key={i}
                x={lx} y={ly}
                textAnchor="middle"
                dominantBaseline="middle"
                fontSize={9}
                fontWeight={700}
                fill={perfColor(v.score)}
              >
                {v.score}
              </text>
            )
          })}

          {/* KPI name labels outside the boundary polygon */}
          {verts.map((v, i) => {
            const lx = CX + (R + 22) * Math.cos(v.a)
            const ly = CY + (R + 22) * Math.sin(v.a)
            const cosA = Math.cos(v.a)
            const anchor = cosA > 0.25 ? "start" : cosA < -0.25 ? "end" : "middle"
            return (
              <text
                key={i}
                x={lx} y={ly}
                textAnchor={anchor}
                dominantBaseline="middle"
                fontSize={9}
                fontWeight={600}
                fill={perfColor(v.score)}
              >
                {v.kpi.shortName}
              </text>
            )
          })}
        </svg>
      </div>
    </div>
  )
}
