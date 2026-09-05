import { useState } from "react"
import { useTranslation } from "react-i18next"
import { PieChart as PieChartIcon, X, TrendingUp, TrendingDown } from "lucide-react"
import { PieChart, Pie, Cell } from "recharts"
import { Card, CardContent } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"

// ─── Types ────────────────────────────────────────────────

interface KpiMetric {
  id: string
  title: string
  subtitle: string
  value: number
  displayValue: string
  gaugePercent: number
  targetLabel: string
  trend: number
  trendUp: boolean
  trendLabel: string
  color: string
  responses: number
}

interface KpiFlipCardProps {
  kpi: KpiMetric
  onDetail: (id: string) => void
  delay: number
  targetWord: string
  targetTooltip: string
  responsesWord: string
  reasonsLabel: string
}

// ─── D1–D5 Semantic KPI Colors ───────────────────────────
// These are the ONLY colors that may signal KPI status.
// Brand colors (Cyan/Mint/Navy) must NEVER be used for status.
const D1 = "#1A7A3C" // Excellent
const D2 = "#2EB85C" // Good
const D3 = "#E8A020" // Caution
const D4 = "#E05C1A" // Warning
const D5 = "#C01B2A" // Critical

// ─── Performance Color Scale ──────────────────────────────
// Universal scale (default) — used for percentages 0-100 where higher = better
// KPI-aware overrides handle metrics with different scales (NPS, CES)

const KPI_THRESHOLDS: Record<string, [number, number, number, number]> = {
  // [great, good, average, belowAvg] — anything below belowAvg is "poor"
  nps:   [50, 40, 30, 20],     // NPS: 50+ great, 40+ good, 30+ avg, 20+ below, <20 poor
  ces:   [30, 40, 50, 60],     // CES: lower is better — 30 great, 60+ poor (INVERTED)
}

function perfColor(value: number, kpiId?: string): string {
  const th = kpiId ? KPI_THRESHOLDS[kpiId] : undefined

  if (th) {
    if (kpiId === "ces") {
      // CES is inverted — lower effort = better
      if (value <= th[0]) return D1
      if (value <= th[1]) return D2
      if (value <= th[2]) return D3
      if (value <= th[3]) return D4
      return D5
    }
    // NPS or other custom scales
    if (value >= th[0]) return D1
    if (value >= th[1]) return D2
    if (value >= th[2]) return D3
    if (value >= th[3]) return D4
    return D5
  }

  // Default scale (percentages 0-100)
  if (value >= 85) return D1
  if (value >= 75) return D2
  if (value >= 60) return D3
  if (value >= 45) return D4
  return D5
}

// ─── KPI Segment Data (3-tile breakdown below gauge) ──────

const KPI_SEGMENTS: Record<string, { key: string; value: number; color: string }[]> = {
  nps: [
    { key: "promoters", value: 51, color: D2 },
    { key: "passives", value: 40, color: D3 },
    { key: "detractors", value: 9, color: D5 },
  ],
  csat: [
    { key: "satisfied", value: 78, color: D2 },
    { key: "neutral", value: 14, color: D3 },
    { key: "dissatisfied", value: 8, color: D5 },
  ],
  ces: [
    { key: "easy", value: 53, color: D2 },
    { key: "moderate", value: 33, color: D3 },
    { key: "difficult", value: 14, color: D5 },
  ],
  agent: [
    { key: "satisfied", value: 84, color: D2 },
    { key: "neutral", value: 10, color: D3 },
    { key: "dissatisfied", value: 6, color: D5 },
  ],
  vfm: [
    { key: "goodValue", value: 72, color: D2 },
    { key: "average", value: 19, color: D3 },
    { key: "poorValue", value: 9, color: D5 },
  ],
  fcr: [
    { key: "resolved", value: 68, color: D2 },
    { key: "partial", value: 21, color: D3 },
    { key: "escalated", value: 11, color: D5 },
  ],
}

const SEGMENT_LABELS: Record<string, { en: string; ar: string }> = {
  promoters: { en: "Promoters", ar: "مروّجون" },
  passives: { en: "Passives", ar: "محايدون" },
  detractors: { en: "Detractors", ar: "منتقدون" },
  satisfied: { en: "Satisfied", ar: "راضٍ" },
  neutral: { en: "Neutral", ar: "محايد" },
  dissatisfied: { en: "Dissatisfied", ar: "غير راضٍ" },
  easy: { en: "Easy", ar: "سهل" },
  moderate: { en: "Moderate", ar: "متوسط" },
  difficult: { en: "Difficult", ar: "صعب" },
  goodValue: { en: "Good Value", ar: "قيمة جيدة" },
  average: { en: "Average", ar: "متوسط" },
  poorValue: { en: "Poor Value", ar: "قيمة ضعيفة" },
  resolved: { en: "Resolved", ar: "تم الحل" },
  partial: { en: "Partial", ar: "جزئي" },
  escalated: { en: "Escalated", ar: "مُصعّد" },
}

// ─── Reasons Data (back face donut) ───────────────────────

// Reasons use BRAND chart palette (non-semantic) — never D1-D5
const CHART_CYAN = "#0D8BBC"
const CHART_MINT = "#13DB9B"
const CHART_NAVY_300 = "#8B90A5"
const CHART_CYAN_700 = "#0087A8"

const REASONS_DATA: Record<string, { key: string; value: number; color: string }[]> = {
  nps: [
    { key: "staffQuality", value: 35, color: CHART_CYAN },
    { key: "digitalExperience", value: 28, color: CHART_MINT },
    { key: "waitTime", value: 22, color: CHART_NAVY_300 },
    { key: "fees", value: 15, color: CHART_CYAN_700 },
  ],
  csat: [
    { key: "serviceSpeed", value: 30, color: CHART_CYAN },
    { key: "staffAttitude", value: 25, color: CHART_MINT },
    { key: "productQuality", value: 25, color: CHART_NAVY_300 },
    { key: "accessibility", value: 20, color: CHART_CYAN_700 },
  ],
  ces: [
    { key: "processComplexity", value: 32, color: CHART_CYAN },
    { key: "channelSwitching", value: 28, color: CHART_MINT },
    { key: "documentRequirements", value: 24, color: CHART_NAVY_300 },
    { key: "responseTime", value: 16, color: CHART_CYAN_700 },
  ],
  agent: [
    { key: "knowledgeBase", value: 30, color: CHART_CYAN },
    { key: "communicationSkills", value: 28, color: CHART_MINT },
    { key: "resolutionSpeed", value: 25, color: CHART_NAVY_300 },
    { key: "empathy", value: 17, color: CHART_CYAN_700 },
  ],
  vfm: [
    { key: "pricingTransparency", value: 35, color: CHART_CYAN },
    { key: "serviceQuality", value: 27, color: CHART_MINT },
    { key: "competitorComparison", value: 22, color: CHART_NAVY_300 },
    { key: "bundleValue", value: 16, color: CHART_CYAN_700 },
  ],
  fcr: [
    { key: "agentSkill", value: 33, color: CHART_CYAN },
    { key: "systemLimitations", value: 27, color: CHART_MINT },
    { key: "escalationProcess", value: 24, color: CHART_NAVY_300 },
    { key: "informationAccess", value: 16, color: CHART_CYAN_700 },
  ],
}

const REASON_LABELS: Record<string, { en: string; ar: string }> = {
  staffQuality: { en: "Staff Quality", ar: "جودة الموظفين" },
  digitalExperience: { en: "Digital Experience", ar: "التجربة الرقمية" },
  waitTime: { en: "Wait Time", ar: "وقت الانتظار" },
  fees: { en: "Fees", ar: "الرسوم" },
  serviceSpeed: { en: "Service Speed", ar: "سرعة الخدمة" },
  staffAttitude: { en: "Staff Attitude", ar: "أسلوب الموظفين" },
  productQuality: { en: "Product Quality", ar: "جودة المنتج" },
  accessibility: { en: "Accessibility", ar: "سهولة الوصول" },
  processComplexity: { en: "Process Complexity", ar: "تعقيد العمليات" },
  channelSwitching: { en: "Channel Switching", ar: "التنقل بين القنوات" },
  documentRequirements: { en: "Document Requirements", ar: "متطلبات المستندات" },
  responseTime: { en: "Response Time", ar: "وقت الاستجابة" },
  knowledgeBase: { en: "Knowledge Base", ar: "قاعدة المعرفة" },
  communicationSkills: { en: "Communication Skills", ar: "مهارات التواصل" },
  resolutionSpeed: { en: "Resolution Speed", ar: "سرعة الحل" },
  empathy: { en: "Empathy", ar: "التعاطف" },
  pricingTransparency: { en: "Pricing Transparency", ar: "شفافية الأسعار" },
  serviceQuality: { en: "Service Quality", ar: "جودة الخدمة" },
  competitorComparison: { en: "Competitor Comparison", ar: "مقارنة بالمنافسين" },
  bundleValue: { en: "Bundle Value", ar: "قيمة الباقات" },
  agentSkill: { en: "Agent Skill", ar: "مهارة الموظف" },
  systemLimitations: { en: "System Limitations", ar: "قيود النظام" },
  escalationProcess: { en: "Escalation Process", ar: "عملية التصعيد" },
  informationAccess: { en: "Information Access", ar: "الوصول للمعلومات" },
}

// ─── Custom SVG Dual-Ring Gauge ───────────────────────────

function DualRingGauge({
  value,
  min = 0,
  max = 100,
  color,
  targetPct,
  label,
  size = 185,
}: {
  value: number
  min?: number
  max?: number
  color: string
  targetPct?: number
  label: string
  size?: number
}) {
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

  // Zone breaks: bad (0-33%), average (33-55%), great (55-100%)
  const zones = [
    { from: startAngle, to: startAngle + 0.33 * totalAngle, color: D5 },
    { from: startAngle + 0.33 * totalAngle, to: startAngle + 0.55 * totalAngle, color: D3 },
    { from: startAngle + 0.55 * totalAngle, to: startAngle + totalAngle, color: D2 },
  ]

  // Needle position
  const needleX = cx + (rOuter - 4) * Math.cos(toRad(valueAngle))
  const needleY = cy + (rOuter - 4) * Math.sin(toRad(valueAngle))

  // Target marker
  let targetAngle = 0
  let thTx1 = 0, thTy1 = 0, thTx2 = 0, thTy2 = 0, thLx = 0, thLy = 0
  if (targetPct !== undefined) {
    targetAngle = startAngle + targetPct * totalAngle
    const thRad = toRad(targetAngle)
    const tickLen = 14
    thTx1 = cx + (rOuter - tickLen) * Math.cos(thRad)
    thTy1 = cy + (rOuter - tickLen) * Math.sin(thRad)
    thTx2 = cx + (rOuter + 6) * Math.cos(thRad)
    thTy2 = cy + (rOuter + 6) * Math.sin(thRad)
    thLx = cx + (rOuter + 17) * Math.cos(thRad)
    thLy = cy + (rOuter + 17) * Math.sin(thRad)
  }

  // Display value
  const displayVal = min < 0 && value > 0 ? `+${value}` : label === "NPS" && value > 0 ? `+${value}` : label === "CES" ? `${value}%` : `${value}%`

  return (
    <svg
      width="100%"
      viewBox={`0 0 ${size} ${size * 0.72}`}
      role="img"
      aria-label={`${label}: ${value}`}
    >
      {/* Inner ring — zone colors (thin) */}
      <path d={arcPath(startAngle, startAngle + totalAngle, rInner)} fill="none" className="stroke-muted/30" strokeWidth={innerSW} strokeLinecap="round" />
      {zones.map((z, i) => (
        <path
          key={i}
          d={arcPath(z.from, z.to, rInner)}
          fill="none"
          stroke={z.color}
          strokeWidth={innerSW}
          strokeLinecap={i === 0 ? "round" : i === zones.length - 1 ? "round" : "butt"}
        />
      ))}

      {/* Outer ring — value arc (thick) */}
      <path d={arcPath(startAngle, startAngle + totalAngle, rOuter)} fill="none" className="stroke-muted/20" strokeWidth={outerSW} strokeLinecap="round" />
      <path d={arcPath(startAngle, valueAngle, rOuter)} fill="none" stroke={color} strokeWidth={outerSW} strokeLinecap="round" />

      {/* Target marker */}
      {targetPct !== undefined && (
        <>
          <line x1={thTx1} y1={thTy1} x2={thTx2} y2={thTy2} className="stroke-foreground" strokeWidth={2.5} strokeLinecap="round" />
          <text x={thLx} y={thLy} textAnchor="middle" fontSize={8} fontWeight={700} className="fill-muted-foreground" dominantBaseline="middle">T</text>
        </>
      )}

      {/* Needle dot */}
      <circle cx={needleX} cy={needleY} r={5} fill={color} className="stroke-card" strokeWidth={2} />
      <circle cx={cx} cy={cy} r={3.5} className="fill-muted-foreground/40" />

      {/* Center value */}
      <text x={cx} y={cy - 6} textAnchor="middle" fontSize={28} fontWeight={800} fill={color}>
        {displayVal}
      </text>
      <text x={cx} y={cy + 12} textAnchor="middle" fontSize={9} fontWeight={600} className="fill-muted-foreground">
        {label}
      </text>
    </svg>
  )
}

// ─── Animations ───────────────────────────────────────────

const flipStyles = `
  @keyframes kpi-flip-fade-in {
    from { opacity: 0; transform: translateY(20px); }
    to   { opacity: 1; transform: translateY(0); }
  }
  .kpi-flip-fade-in {
    opacity: 0;
    animation: kpi-flip-fade-in 0.6s cubic-bezier(0.16, 1, 0.3, 1) forwards;
  }
  @media (prefers-reduced-motion: reduce) {
    .kpi-flip-fade-in { opacity: 1; animation: none; }
  }
`

// ─── KpiFlipCard Component ────────────────────────────────

function KpiFlipCard({
  kpi,
  onDetail,
  delay,
  targetWord,
  targetTooltip,
  responsesWord,
  reasonsLabel,
}: KpiFlipCardProps) {
  const { t, i18n } = useTranslation()
  const [flipped, setFlipped] = useState(false)

  const reasons = REASONS_DATA[kpi.id] ?? REASONS_DATA.nps
  const segments = KPI_SEGMENTS[kpi.id] ?? KPI_SEGMENTS.nps
  const isArabic = i18n.language === "ar"

  // Compute target percentage for gauge marker
  const targetPctMap: Record<string, number> = {
    nps: (50 + 100) / 200,    // +50 on -100 to +100 scale
    csat: 0.80,
    ces: 0.50,
    agent: 0.85,
    vfm: 0.80,
    fcr: 0.75,
  }
  const targetPct = targetPctMap[kpi.id] ?? 0.8

  // NPS has -100 to +100 range
  const gaugeMin = kpi.id === "nps" ? -100 : 0
  const gaugeMax = kpi.id === "nps" ? 100 : 100
  const gaugeValue = kpi.value

  return (
    <>
      <style>{flipStyles}</style>
      <div
        className="perspective-[1000px] kpi-flip-fade-in"
        style={{ animationDelay: `${delay}s` }}
      >
        <div
          className={cn(
            "relative [transform-style:preserve-3d] transition-transform duration-500",
            flipped && "[transform:rotateY(180deg)]",
          )}
        >
          {/* ── FRONT FACE ────────────────────── */}
          <Card
            className={cn(
              "relative overflow-hidden [backface-visibility:hidden] cursor-pointer",
              "motion-safe:transition-all motion-safe:duration-300",
              "hover:-translate-y-1 hover:shadow-lg",
            )}
            onClick={() => onDetail(kpi.id)}
          >
            {/* Flip button */}
            <Button
              variant="ghost"
              size="icon-xs"
              className="absolute top-2 end-2 z-10 text-muted-foreground hover:text-foreground"
              aria-label={reasonsLabel}
              onClick={(e) => {
                e.stopPropagation()
                setFlipped(true)
              }}
            >
              <PieChartIcon className="size-3.5" />
            </Button>

            <CardContent className="flex flex-col items-center text-center">
              <span className="text-sm font-bold" style={{ color: kpi.color }}>
                {kpi.title}
              </span>
              <p className="text-xs text-muted-foreground mb-1">{kpi.subtitle}</p>

              {/* Rich dual-ring gauge */}
              <div className="w-full max-w-[185px]">
                <DualRingGauge
                  value={gaugeValue}
                  min={gaugeMin}
                  max={gaugeMax}
                  color={perfColor(kpi.gaugePercent)}
                  targetPct={targetPct}
                  label={kpi.title}
                />
              </div>

              {/* Target label */}
              <p className="text-xs text-muted-foreground mt-1 mb-3" title={targetTooltip}>
                {targetWord}: <span className="font-bold text-foreground">{kpi.targetLabel}</span>
              </p>

              {/* 3-tile segment grid */}
              <div className="grid grid-cols-3 gap-2 w-full mb-3">
                {segments.map((seg) => {
                  const label = SEGMENT_LABELS[seg.key]
                  return (
                    <div
                      key={seg.key}
                      className="rounded-md py-1.5 px-1 text-center"
                      style={{ backgroundColor: `${seg.color}15` }}
                    >
                      <div className="text-sm font-bold tabular-nums" style={{ color: seg.color }}>
                        {seg.value}%
                      </div>
                      <div className="text-[9px] text-muted-foreground mt-0.5">
                        {label ? (isArabic ? label.ar : label.en) : seg.key}
                      </div>
                    </div>
                  )
                })}
              </div>

              {/* Change + responses row */}
              <div className="flex items-center justify-center gap-4 text-xs">
                <div className="text-center">
                  <div className="text-muted-foreground">{isArabic ? "عن الربع الماضي" : "vs Last Quarter"}</div>
                  <div className={cn(
                    "font-bold tabular-nums mt-0.5",
                    kpi.trendUp ? "text-d2 dark:text-d2-light" : "text-d5 dark:text-d5-light",
                  )}>
                    {kpi.trendUp ? <TrendingUp className="size-3 inline me-1" /> : <TrendingDown className="size-3 inline me-1" />}
                    {kpi.trendLabel}
                  </div>
                </div>
                <div className="text-center">
                  <div className="text-muted-foreground">{responsesWord}</div>
                  <div className="font-bold text-foreground tabular-nums mt-0.5">
                    {kpi.responses.toLocaleString("en-US")}
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* ── BACK FACE ─────────────────────── */}
          <Card className="absolute inset-0 overflow-hidden [backface-visibility:hidden] [transform:rotateY(180deg)]">
            <CardContent className="h-full flex flex-col">
              <div className="flex items-center justify-between mb-3">
                <div>
                  <h3 className="text-sm font-bold">{t("cx.reasonsAnalysis")}</h3>
                  <p className="text-[10px] text-muted-foreground">{kpi.title}</p>
                </div>
                <Button
                  variant="ghost"
                  size="icon-xs"
                  className="text-muted-foreground hover:text-foreground"
                  aria-label={t("common.close")}
                  onClick={() => setFlipped(false)}
                >
                  <X className="size-3.5" />
                </Button>
              </div>

              <div className="flex justify-center mb-3">
                <PieChart width={130} height={130}>
                  <Pie
                    data={reasons}
                    dataKey="value"
                    nameKey="key"
                    cx={65}
                    cy={65}
                    innerRadius={40}
                    outerRadius={60}
                    strokeWidth={2}
                    stroke="var(--color-card)"
                  >
                    {reasons.map((entry) => (
                      <Cell key={entry.key} fill={entry.color} />
                    ))}
                  </Pie>
                </PieChart>
              </div>

              <div className="space-y-2 flex-1 min-h-0 overflow-y-auto">
                {reasons.map((reason) => {
                  const label = REASON_LABELS[reason.key]
                  const displayLabel = label ? (isArabic ? label.ar : label.en) : reason.key
                  return (
                    <div key={reason.key} className="flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="size-2 rounded-full shrink-0" style={{ backgroundColor: reason.color }} />
                        <span className="text-[10px] truncate">{displayLabel}</span>
                      </div>
                      <span className="text-[10px] font-bold tabular-nums shrink-0" style={{ color: reason.color }}>
                        {reason.value}%
                      </span>
                    </div>
                  )
                })}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </>
  )
}

export { KpiFlipCard, perfColor }
export type { KpiMetric }
