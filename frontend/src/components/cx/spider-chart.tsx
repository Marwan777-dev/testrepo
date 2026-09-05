import { perfColor } from "./kpi-flip-card"

interface SpiderKpi {
  label: string
  value: number
}

interface SpiderChartProps {
  kpis: SpiderKpi[]
  className?: string
}

/**
 * Custom SVG spider/radar chart with:
 * - Radial gradient fill (red center → green outer) showing performance zones
 * - White cutout via fill-rule="evenodd" between outer boundary and data polygon
 * - Per-vertex colored dots based on performance level
 * - Value labels next to each dot
 * - Grid levels at 20, 40, 60, 80, 100
 */
export function SpiderChart({ kpis, className }: SpiderChartProps) {
  const N = kpis.length
  const cx = 110
  const cy = 90
  const R = 55

  const toRad = (deg: number) => (deg * Math.PI) / 180
  const angleOf = (i: number) => toRad(-90 + (360 / N) * i)

  const ptOf = (i: number, pct: number) => ({
    x: cx + R * (pct / 100) * Math.cos(angleOf(i)),
    y: cy + R * (pct / 100) * Math.sin(angleOf(i)),
  })

  const gridLevels = [20, 40, 60, 80, 100]

  // Data polygon points
  const polyPts = kpis.map((k, i) => ptOf(i, k.value))
  const polyStr = polyPts.map((p) => `${p.x},${p.y}`).join(" ")

  // Outer boundary points (100%)
  const outerPts = kpis.map((_, i) => ptOf(i, 100))
  const outerPath =
    outerPts.map((p, i) => `${i === 0 ? "M" : "L"} ${p.x.toFixed(2)} ${p.y.toFixed(2)}`).join(" ") + " Z"
  const innerPath =
    polyPts.map((p, i) => `${i === 0 ? "M" : "L"} ${p.x.toFixed(2)} ${p.y.toFixed(2)}`).join(" ") + " Z"

  return (
    <svg
      width="100%"
      viewBox="0 0 220 168"
      preserveAspectRatio="xMidYMid meet"
      role="img"
      aria-label="KPI Overview Radar Chart"
      className={className}
    >
      <defs>
        {/* Radial gradient: red center → green outer (performance zones) */}
        <radialGradient id="spiderGrad" cx={cx} cy={cy} r={R} gradientUnits="userSpaceOnUse">
          <stop offset="0%" stopColor="#C01B2A" stopOpacity={0.6} />
          <stop offset="38%" stopColor="#E05C1A" stopOpacity={0.45} />
          <stop offset="62%" stopColor="#E8A020" stopOpacity={0.35} />
          <stop offset="82%" stopColor="#2EB85C" stopOpacity={0.25} />
          <stop offset="100%" stopColor="#1A7A3C" stopOpacity={0.2} />
        </radialGradient>
      </defs>

      {/* Grid level polygons — use CSS vars so they adapt to light/dark */}
      {gridLevels.map((lvl) => {
        const pts = kpis.map((_, i) => ptOf(i, lvl))
        return (
          <polygon
            key={lvl}
            points={pts.map((p) => `${p.x},${p.y}`).join(" ")}
            fill="none"
            className={lvl === 100 ? "stroke-border" : "stroke-border/40"}
            strokeWidth={lvl === 100 ? 1.5 : 0.8}
          />
        )
      })}

      {/* Grid level numbers along first axis */}
      {gridLevels.map((lvl) => {
        const p = ptOf(0, lvl)
        return (
          <text key={lvl} x={p.x + 4} y={p.y} fontSize={6} className="fill-muted-foreground/50" dominantBaseline="middle">
            {lvl}
          </text>
        )
      })}

      {/* Axis lines from center to each vertex */}
      {kpis.map((_, i) => {
        const outer = ptOf(i, 100)
        return <line key={i} x1={cx} y1={cy} x2={outer.x} y2={outer.y} className="stroke-border/30" strokeWidth={0.8} />
      })}

      {/* Gradient fill on outer polygon */}
      <polygon points={outerPts.map((p) => `${p.x},${p.y}`).join(" ")} fill="url(#spiderGrad)" />

      {/* Card-color cutout: outer boundary minus data polygon — adapts to light/dark */}
      <path d={`${outerPath} ${innerPath}`} fill="var(--card)" fillRule="evenodd" />

      {/* Data polygon outline */}
      <polygon
        points={polyStr}
        fill="none"
        className="stroke-muted-foreground/40"
        strokeWidth={1.5}
        strokeLinejoin="round"
      />

      {/* Per-vertex colored dots */}
      {polyPts.map((p, i) => (
        <circle
          key={i}
          cx={p.x}
          cy={p.y}
          r={4.5}
          fill={perfColor(kpis[i].value)}
          stroke="var(--card)"
          strokeWidth={1.5}
        />
      ))}

      {/* Axis labels (KPI names outside polygon) */}
      {kpis.map((k, i) => {
        const lpt = ptOf(i, 122)
        const anchor = lpt.x < cx - 8 ? "end" : lpt.x > cx + 8 ? "start" : "middle"
        return (
          <text
            key={i}
            x={lpt.x}
            y={lpt.y}
            textAnchor={anchor}
            fontSize={8}
            fontWeight={700}
            fill={perfColor(k.value)}
            dominantBaseline="middle"
          >
            {k.label}
          </text>
        )
      })}

      {/* Value labels next to dots */}
      {polyPts.map((p, i) => {
        const ang = angleOf(i)
        return (
          <text
            key={i}
            x={p.x + 9 * Math.cos(ang)}
            y={p.y + 9 * Math.sin(ang)}
            textAnchor="middle"
            fontSize={7}
            fontWeight={800}
            fill={perfColor(kpis[i].value)}
            dominantBaseline="middle"
          >
            {kpis[i].value}
          </text>
        )
      })}

      {/* Center dot */}
      <circle cx={cx} cy={cy} r={2.5} className="fill-muted-foreground/30" />
    </svg>
  )
}
