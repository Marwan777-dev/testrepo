// src/components/kpi/KpiDashboardPreview.tsx
import { TrendingUp, TrendingDown } from "lucide-react"
import { cn } from "@/lib/utils"

// D-scale semantic colors (KPI status only — never decorative)
const D2 = "#2EB85C"
const D3 = "#E8A020"
const D5 = "#C01B2A"

function perfColor(value: number, kpiId: string): string {
  // CES and CHS: inverted — lower score = better
  if (kpiId === "ces" || kpiId === "chs") {
    if (value <= 30) return "#1A7A3C"
    if (value <= 40) return D2
    if (value <= 50) return D3
    if (value <= 60) return "#E05C1A"
    return D5
  }
  if (kpiId === "nps") {
    if (value >= 50) return "#1A7A3C"
    if (value >= 40) return D2
    if (value >= 30) return D3
    if (value >= 20) return "#E05C1A"
    return D5
  }
  if (value >= 85) return "#1A7A3C"
  if (value >= 75) return D2
  if (value >= 60) return D3
  if (value >= 45) return "#E05C1A"
  return D5
}

// ── Dual-Ring Gauge SVG ───────────────────────────────────
// Mirrors the design in kpi-flip-card.tsx — outer filled arc + inner zone ring

function DualRingGauge({
  value,
  min = 0,
  max = 100,
  color,
  targetPct,
  label,
  zoneX,
  zoneY,
}: {
  value: number
  min?: number
  max?: number
  color: string
  targetPct?: number
  label: string
  zoneX?: number  // 0-1: fraction of arc where red/amber splits
  zoneY?: number  // 0-1: fraction of arc where amber/green splits
}) {
  const size = 210
  const cx = size / 2
  const cy = size / 2 + 14
  const startAngle = -210
  const totalAngle = 240
  const toRad = (a: number) => (a * Math.PI) / 180

  const rOuter = size * 0.42
  const outerSW = size * 0.06
  const innerSW = Math.max(2.5, size * 0.016)
  const rInner = rOuter - outerSW / 2 - innerSW / 2

  const arcPath = (a1: number, a2: number, r: number) => {
    const x1 = cx + r * Math.cos(toRad(a1))
    const y1 = cy + r * Math.sin(toRad(a1))
    const x2 = cx + r * Math.cos(toRad(a2))
    const y2 = cy + r * Math.sin(toRad(a2))
    const large = a2 - a1 > 180 ? 1 : 0
    return `M ${x1} ${y1} A ${r} ${r} 0 ${large} 1 ${x2} ${y2}`
  }

  const pct = Math.max(0, Math.min(1, (value - min) / (max - min)))
  const valueAngle = startAngle + pct * totalAngle

  // Zone boundaries — live from thresholds when provided, else design defaults
  const z1 = Math.max(0, Math.min(1, zoneX ?? 0.33))
  const z2 = Math.max(z1, Math.min(1, zoneY ?? 0.55))

  const zones = [
    { from: startAngle, to: startAngle + z1 * totalAngle, color: D5 },
    { from: startAngle + z1 * totalAngle, to: startAngle + z2 * totalAngle, color: D3 },
    { from: startAngle + z2 * totalAngle, to: startAngle + totalAngle, color: D2 },
  ]

  const needleX = cx + (rOuter - 4) * Math.cos(toRad(valueAngle))
  const needleY = cy + (rOuter - 4) * Math.sin(toRad(valueAngle))

  let thTx1 = 0, thTy1 = 0, thTx2 = 0, thTy2 = 0, thLx = 0, thLy = 0
  if (targetPct !== undefined) {
    const targetAngle = startAngle + targetPct * totalAngle
    const thRad = toRad(targetAngle)
    const tickLen = 14
    thTx1 = cx + (rOuter - tickLen) * Math.cos(thRad)
    thTy1 = cy + (rOuter - tickLen) * Math.sin(thRad)
    thTx2 = cx + (rOuter + 6) * Math.cos(thRad)
    thTy2 = cy + (rOuter + 6) * Math.sin(thRad)
    thLx = cx + (rOuter + 17) * Math.cos(thRad)
    thLy = cy + (rOuter + 17) * Math.sin(thRad)
  }

  const displayVal = min < 0 ? (value > 0 ? `+${value}` : `${value}`) : `${value}%`

  return (
    <svg
      width="100%"
      viewBox={`0 0 ${size} ${size * 0.72}`}
      role="img"
      aria-label={`${label}: ${value}`}
    >
      {/* Inner ring — zone colors (thin) */}
      <path d={arcPath(startAngle, startAngle + totalAngle, rInner)} fill="none" stroke="#E2E8F0" strokeWidth={innerSW} strokeLinecap="round" />
      {zones.map((z, i) => (
        <path
          key={i}
          d={arcPath(z.from, z.to, rInner)}
          fill="none"
          stroke={z.color}
          strokeWidth={innerSW}
          strokeLinecap={i === 0 || i === zones.length - 1 ? "round" : "butt"}
        />
      ))}

      {/* Outer ring — value arc (thick) */}
      <path d={arcPath(startAngle, startAngle + totalAngle, rOuter)} fill="none" stroke="#E2E8F0" strokeWidth={outerSW} strokeLinecap="round" />
      <path d={arcPath(startAngle, valueAngle, rOuter)} fill="none" stroke={color} strokeWidth={outerSW} strokeLinecap="round" />

      {/* Target marker tick + T label */}
      {targetPct !== undefined && (
        <>
          <line x1={thTx1} y1={thTy1} x2={thTx2} y2={thTy2} stroke="#334155" strokeWidth={2.5} strokeLinecap="round" />
          <text x={thLx} y={thLy} textAnchor="middle" fontSize={8} fontWeight={700} fill="#64748B" dominantBaseline="middle">T</text>
        </>
      )}

      {/* Needle dot at current value */}
      <circle cx={needleX} cy={needleY} r={5} fill={color} stroke="#fff" strokeWidth={2} />
      <circle cx={cx} cy={cy} r={3.5} fill="#CBD5E1" />

      {/* Center: value + label */}
      <text x={cx} y={cy - 6} textAnchor="middle" fontSize={32} fontWeight={800} fill={color}>
        {displayVal}
      </text>
      <text x={cx} y={cy + 14} textAnchor="middle" fontSize={9} fontWeight={600} fill="#94A3B8">
        {label}
      </text>
    </svg>
  )
}

// ── Mock data per KPI ─────────────────────────────────────
// Values match the reference dashboard image exactly.
// CHS mirrors CES design with Happy / Neutral / Unhappy labels.

interface SegmentData {
  label: string
  value: number
  color: string
}

interface KpiPreviewData {
  title: string
  subtitle: string
  value: number
  min: number
  max: number
  targetLabel: string
  targetPct: number
  segments: SegmentData[]
  trend: { label: string; isPositive: boolean }
  responses: number
}

const PREVIEW_DATA: Record<string, KpiPreviewData> = {
  nps: {
    title: "NPS", subtitle: "Net Promoter Score",
    value: 42, min: -100, max: 100,
    targetLabel: "+50", targetPct: (50 + 100) / 200,
    segments: [
      { label: "Promoters", value: 51, color: D2 },
      { label: "Passives",  value: 40, color: D3 },
      { label: "Detractors",value: 9,  color: D5 },
    ],
    trend: { label: "+4 points", isPositive: true },
    responses: 3420,
  },
  csat: {
    title: "CSAT", subtitle: "Customer Satisfaction Score",
    value: 78, min: 0, max: 100,
    targetLabel: "80%", targetPct: 0.80,
    segments: [
      { label: "Satisfied",    value: 78, color: D2 },
      { label: "Neutral",      value: 14, color: D3 },
      { label: "Dissatisfied", value: 8,  color: D5 },
    ],
    trend: { label: "+2%", isPositive: true },
    responses: 4180,
  },
  ces: {
    title: "CES", subtitle: "Customer Effort Score",
    value: 45, min: 0, max: 100,
    targetLabel: "≤50%", targetPct: 0.50,
    segments: [
      { label: "Easy",     value: 53, color: D2 },
      { label: "Moderate", value: 33, color: D3 },
      { label: "Difficult",value: 14, color: D5 },
    ],
    trend: { label: "−0.3", isPositive: true }, // lower effort = improvement
    responses: 2890,
  },
  chs: {
    title: "CHS", subtitle: "Customer Happiness Score",
    value: 45, min: 0, max: 100,
    targetLabel: "≤50%", targetPct: 0.50,
    segments: [
      { label: "Happy",   value: 53, color: D2 },
      { label: "Neutral", value: 33, color: D3 },
      { label: "Unhappy", value: 14, color: D5 },
    ],
    trend: { label: "−0.3", isPositive: true },
    responses: 2890,
  },
  agent: {
    title: "Agent", subtitle: "Agent Performance Score",
    value: 84, min: 0, max: 100,
    targetLabel: "85%", targetPct: 0.85,
    segments: [
      { label: "Satisfied",    value: 84, color: D2 },
      { label: "Neutral",      value: 10, color: D3 },
      { label: "Dissatisfied", value: 6,  color: D5 },
    ],
    trend: { label: "−1 point", isPositive: false },
    responses: 5120,
  },
  vfm: {
    title: "VFM", subtitle: "Value for Money",
    value: 72, min: 0, max: 100,
    targetLabel: "80%", targetPct: 0.80,
    segments: [
      { label: "Good Value", value: 72, color: D2 },
      { label: "Average",    value: 19, color: D3 },
      { label: "Poor Value", value: 9,  color: D5 },
    ],
    trend: { label: "−8 points", isPositive: false },
    responses: 2340,
  },
  fcr: {
    title: "FCR", subtitle: "First Contact Resolution",
    value: 68, min: 0, max: 100,
    targetLabel: "75%", targetPct: 0.75,
    segments: [
      { label: "Resolved",  value: 68, color: D2 },
      { label: "Partial",   value: 21, color: D3 },
      { label: "Escalated", value: 11, color: D5 },
    ],
    trend: { label: "−7 points", isPositive: false },
    responses: 3780,
  },
}

// ── Component ─────────────────────────────────────────────

interface KpiDashboardPreviewProps {
  kpiId: string
  liveTitle?: string
  liveSubtitle?: string
  liveThresholdX?: number | null
  liveThresholdY?: number | null
  liveTarget?: number | null
}

export default function KpiDashboardPreview({
  kpiId,
  liveTitle,
  liveSubtitle,
  liveThresholdX,
  liveThresholdY,
  liveTarget,
}: KpiDashboardPreviewProps) {
  const base = PREVIEW_DATA[kpiId]
  if (!base) return null

  const title = liveTitle || base.title
  const subtitle = liveSubtitle || base.subtitle
  const color = perfColor(base.value, kpiId)
  const range = base.max - base.min

  // Zone percentages derived from live thresholds (inner ring splits)
  const zoneX = liveThresholdX != null
    ? Math.max(0, Math.min(1, (liveThresholdX - base.min) / range))
    : undefined
  const zoneY = liveThresholdY != null
    ? Math.max(0, Math.min(1, (liveThresholdY - base.min) / range))
    : undefined

  // Target marker + label from live target value
  const targetPct = liveTarget != null
    ? Math.max(0, Math.min(1, (liveTarget - base.min) / range))
    : base.targetPct
  const targetLabel = liveTarget != null
    ? (base.min < 0 ? (liveTarget > 0 ? `+${liveTarget}` : `${liveTarget}`) : `${liveTarget}%`)
    : base.targetLabel

  return (
    <div className="flex flex-col items-center text-center">
      <span className="text-sm font-bold" style={{ color }}>{title}</span>
      <p className="text-xs text-muted-foreground mb-1">{subtitle}</p>

      {/* Larger gauge — fills most of the preview card width */}
      <div className="w-full max-w-[260px]">
        <DualRingGauge
          value={base.value}
          min={base.min}
          max={base.max}
          color={color}
          targetPct={targetPct}
          label={title}
          zoneX={zoneX}
          zoneY={zoneY}
        />
      </div>

      <p className="text-xs text-muted-foreground mt-0.5 mb-2">
        Target:{" "}
        <span className="font-bold text-foreground">{targetLabel}</span>
      </p>

      {/* 3-tile segment grid — compact */}
      <div className="grid grid-cols-3 gap-1.5 w-full mb-2">
        {base.segments.map((seg) => (
          <div
            key={seg.label}
            className="rounded-md py-1 px-1 text-center"
            style={{ backgroundColor: `${seg.color}18` }}
          >
            <div className="text-xs font-bold tabular-nums" style={{ color: seg.color }}>
              {seg.value}%
            </div>
            <div className="text-[8px] text-muted-foreground mt-0.5 leading-tight">
              {seg.label}
            </div>
          </div>
        ))}
      </div>

      {/* vs Last Quarter + responses — compact */}
      <div className="flex items-center justify-center gap-5 text-[11px]">
        <div className="text-center">
          <div className="text-muted-foreground">vs Last Quarter</div>
          <div
            className={cn(
              "font-bold tabular-nums mt-0.5 flex items-center justify-center gap-0.5",
              base.trend.isPositive
                ? "text-d2 dark:text-d2-light"
                : "text-d5 dark:text-d5-light",
            )}
          >
            {base.trend.isPositive
              ? <TrendingUp className="size-3" />
              : <TrendingDown className="size-3" />
            }
            {base.trend.label}
          </div>
        </div>
        <div className="text-center">
          <div className="text-muted-foreground">responses</div>
          <div className="font-bold text-foreground tabular-nums mt-0.5">
            {base.responses.toLocaleString("en-US")}
          </div>
        </div>
      </div>
    </div>
  )
}
