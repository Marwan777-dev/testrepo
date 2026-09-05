import { useState, useEffect } from "react"
import { useTranslation } from "react-i18next"
import { cn } from "@/lib/utils"
import { KpiFlipCard, perfColor, type KpiMetric } from "@/components/cx/kpi-flip-card"
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card"
import { Button, buttonVariants } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Switch } from "@/components/ui/switch"
import { Slider } from "@/components/ui/slider"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog"
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip"
import { PieChart, Pie, Cell } from "recharts"
import {
  Plus,
  Pencil,
  Power,
  PowerOff,
  Calendar,
  MessageSquare,
  BarChart3,
  ThumbsUp,
  ThumbsDown,
  Minus,
  Users,
  ChevronDown,
  ChevronUp,
  TrendingUp,
  TrendingDown,
} from "lucide-react"

// ─── D1–D5 semantic tokens ──────────────────────────────
const D1 = "#1A7A3C"
const D2 = "#2EB85C"
const D3 = "#E8A020"
const D5 = "#C01B2A"

// ─── Types ───────────────────────────────────────────────

interface SurveyQuestion {
  id: string
  text: string
  textAr: string
  kpiType: "nps" | "csat" | "ces" | "vfm" | "agent" | "comment"
  targetAnswers: number
  active: boolean
  fontSize: number
}

interface Survey {
  id: string
  name: string
  nameAr: string
  responses: number
  status: "active" | "inactive" | "draft"
  startDate: string
  expiryDate: string
  questions: SurveyQuestion[]
}

// ─── Mock Data ───────────────────────────────────────────

const MOCK_SURVEYS: Survey[] = [
  {
    id: "SRV-001",
    name: "Post-Transaction Experience",
    nameAr: "تجربة ما بعد المعاملة",
    responses: 4820,
    status: "active",
    startDate: "2026-01-15",
    expiryDate: "2026-06-30",
    questions: [
      { id: "q1", text: "How likely are you to recommend us?", textAr: "ما مدى احتمالية أن توصي بنا؟", kpiType: "nps", targetAnswers: 5000, active: true, fontSize: 5 },
      { id: "q2", text: "How satisfied are you with the service?", textAr: "ما مدى رضاك عن الخدمة؟", kpiType: "csat", targetAnswers: 5000, active: true, fontSize: 5 },
      { id: "q3", text: "How easy was the process?", textAr: "ما مدى سهولة العملية؟", kpiType: "ces", targetAnswers: 5000, active: true, fontSize: 4 },
      { id: "q4", text: "Any additional comments?", textAr: "هل لديك أي ملاحظات إضافية؟", kpiType: "comment", targetAnswers: 2000, active: true, fontSize: 4 },
    ],
  },
  {
    id: "SRV-002",
    name: "Branch Visit Feedback",
    nameAr: "تقييم زيارة الفرع",
    responses: 2156,
    status: "active",
    startDate: "2026-02-01",
    expiryDate: "2026-07-31",
    questions: [
      { id: "q5", text: "Rate the agent who served you", textAr: "قيّم الموظف الذي خدمك", kpiType: "agent", targetAnswers: 3000, active: true, fontSize: 5 },
      { id: "q6", text: "Did you get value for money?", textAr: "هل حصلت على قيمة مقابل المال؟", kpiType: "vfm", targetAnswers: 3000, active: true, fontSize: 5 },
      { id: "q7", text: "How likely are you to recommend us?", textAr: "ما مدى احتمالية أن توصي بنا؟", kpiType: "nps", targetAnswers: 3000, active: true, fontSize: 5 },
      { id: "q8", text: "Tell us more about your experience", textAr: "أخبرنا المزيد عن تجربتك", kpiType: "comment", targetAnswers: 1500, active: true, fontSize: 4 },
    ],
  },
  {
    id: "SRV-003",
    name: "Digital Channel Survey",
    nameAr: "استبيان القنوات الرقمية",
    responses: 891,
    status: "inactive",
    startDate: "2025-10-01",
    expiryDate: "2026-03-31",
    questions: [
      { id: "q9", text: "How satisfied are you with the app?", textAr: "ما مدى رضاك عن التطبيق؟", kpiType: "csat", targetAnswers: 2000, active: true, fontSize: 5 },
      { id: "q10", text: "How easy was the digital process?", textAr: "ما مدى سهولة العملية الرقمية؟", kpiType: "ces", targetAnswers: 2000, active: true, fontSize: 5 },
    ],
  },
  {
    id: "SRV-004",
    name: "Q2 Loyalty Assessment",
    nameAr: "تقييم الولاء للربع الثاني",
    responses: 0,
    status: "draft",
    startDate: "2026-06-01",
    expiryDate: "2026-09-30",
    questions: [
      { id: "q11", text: "How likely are you to recommend us?", textAr: "ما مدى احتمالية أن توصي بنا؟", kpiType: "nps", targetAnswers: 4000, active: true, fontSize: 6 },
    ],
  },
]

// ─── Mock Results ─────────────────────────────────────────

interface KpiResult {
  score: number
  displayValue: string
  trend: number
  trendUp: boolean
  responses: number
  segments: { label: string; labelAr: string; value: number; color: string }[]
}

const MOCK_KPI_RESULTS: Record<string, KpiResult> = {
  nps: {
    score: 42, displayValue: "+42", trend: 3, trendUp: true, responses: 4820,
    segments: [
      { label: "Promoters", labelAr: "مروّجون", value: 51, color: D2 },
      { label: "Passives", labelAr: "محايدون", value: 40, color: D3 },
      { label: "Detractors", labelAr: "منتقدون", value: 9, color: D5 },
    ],
  },
  csat: {
    score: 78, displayValue: "78%", trend: 2, trendUp: true, responses: 3210,
    segments: [
      { label: "Satisfied", labelAr: "راضٍ", value: 78, color: D2 },
      { label: "Neutral", labelAr: "محايد", value: 14, color: D3 },
      { label: "Dissatisfied", labelAr: "غير راضٍ", value: 8, color: D5 },
    ],
  },
  ces: {
    score: 45, displayValue: "45%", trend: -2, trendUp: true, responses: 2890,
    segments: [
      { label: "Easy", labelAr: "سهل", value: 53, color: D2 },
      { label: "Moderate", labelAr: "متوسط", value: 33, color: D3 },
      { label: "Difficult", labelAr: "صعب", value: 14, color: D5 },
    ],
  },
  agent: {
    score: 84, displayValue: "84%", trend: 4, trendUp: true, responses: 2156,
    segments: [
      { label: "Excellent", labelAr: "ممتاز", value: 52, color: D1 },
      { label: "Good", labelAr: "جيد", value: 32, color: D2 },
      { label: "Poor", labelAr: "ضعيف", value: 16, color: D5 },
    ],
  },
  vfm: {
    score: 72, displayValue: "72%", trend: -1, trendUp: false, responses: 2156,
    segments: [
      { label: "Good Value", labelAr: "قيمة جيدة", value: 72, color: D2 },
      { label: "Fair", labelAr: "مقبول", value: 19, color: D3 },
      { label: "Poor Value", labelAr: "قيمة ضعيفة", value: 9, color: D5 },
    ],
  },
}

const MOCK_SENTIMENT = {
  positive: 62,
  neutral: 24,
  negative: 14,
  total: 1847,
  topPositive: { en: "Staff friendliness", ar: "ودّ الموظفين" },
  topNegative: { en: "Wait time", ar: "وقت الانتظار" },
}

// ─── Helpers ─────────────────────────────────────────────

const KPI_LABELS: Record<string, { en: string; ar: string }> = {
  nps:     { en: "NPS", ar: "صافي الترويج" },
  csat:    { en: "CSAT", ar: "رضا العملاء" },
  ces:     { en: "CES", ar: "جهد العميل" },
  vfm:     { en: "VFM", ar: "القيمة مقابل المال" },
  agent:   { en: "Agent Score", ar: "أداء الموظف" },
  comment: { en: "Open-Ended", ar: "أسئلة مفتوحة" },
}

const KPI_CHART_COLORS: Record<string, string> = {
  nps:   "var(--chart-1)",
  csat:  "var(--chart-2)",
  ces:   "var(--chart-4)",
  agent: "var(--chart-3)",
  vfm:   "var(--chart-5)",
}

function statusBadge(status: Survey["status"], t: (k: string) => string) {
  switch (status) {
    case "active":
      return (
        <Badge className="bg-d2-light text-d2-dark border-0 dark:bg-d2-dark/20 dark:text-d2-light">
          {t("surveys.active")}
        </Badge>
      )
    case "inactive":
      return (
        <Badge className="bg-d5-light text-d5-dark border-0 dark:bg-d5-dark/20 dark:text-d5-light">
          {t("surveys.inactive")}
        </Badge>
      )
    case "draft":
      return (
        <Badge className="bg-nb-cloud text-nb-stone border-0 dark:bg-nb-dark-3 dark:text-nb-stone-lt">
          {t("surveys.draft")}
        </Badge>
      )
  }
}

// ─── NPS Gauge SVG ────────────────────────────────────────
// Dual-ring semicircular gauge matching the Nabdat KPI gauge spec.
// NPS range is -100 to +100.

function NpsGauge({ value, size = 185 }: { value: number; size?: number }) {
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

  const pct = Math.max(0, Math.min(1, (value + 100) / 200))
  const valueAngle = startAngle + pct * totalAngle
  const color = perfColor(value, "nps")

  const zones = [
    { from: startAngle, to: startAngle + 0.33 * totalAngle, color: D5 },
    { from: startAngle + 0.33 * totalAngle, to: startAngle + 0.55 * totalAngle, color: D3 },
    { from: startAngle + 0.55 * totalAngle, to: startAngle + totalAngle, color: D2 },
  ]

  const needleX = cx + (rOuter - 4) * Math.cos(toRad(valueAngle))
  const needleY = cy + (rOuter - 4) * Math.sin(toRad(valueAngle))

  // Target: NPS +50
  const targetPct = (50 + 100) / 200
  const targetAngle = startAngle + targetPct * totalAngle
  const tRad = toRad(targetAngle)
  const tickLen = 14
  const ttx1 = cx + (rOuter - tickLen) * Math.cos(tRad)
  const tty1 = cy + (rOuter - tickLen) * Math.sin(tRad)
  const ttx2 = cx + (rOuter + 6) * Math.cos(tRad)
  const tty2 = cy + (rOuter + 6) * Math.sin(tRad)
  const tlx = cx + (rOuter + 17) * Math.cos(tRad)
  const tly = cy + (rOuter + 17) * Math.sin(tRad)

  // Min/Max label positions
  const minLx = cx + rOuter * Math.cos(toRad(startAngle))
  const minLy = cy + rOuter * Math.sin(toRad(startAngle)) + 14
  const maxLx = cx + rOuter * Math.cos(toRad(startAngle + totalAngle))
  const maxLy = cy + rOuter * Math.sin(toRad(startAngle + totalAngle)) + 14

  const displayVal = value > 0 ? `+${value}` : `${value}`

  return (
    <svg
      width="100%"
      viewBox={`0 0 ${size} ${size * 0.72}`}
      role="img"
      aria-label={`NPS: ${displayVal}`}
    >
      {/* Inner ring — zone colors */}
      <path d={arcPath(startAngle, startAngle + totalAngle, rInner)} fill="none" stroke="#F1F5F9" strokeWidth={innerSW} strokeLinecap="round" />
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

      {/* Outer ring — value arc */}
      <path d={arcPath(startAngle, startAngle + totalAngle, rOuter)} fill="none" stroke="#F1F5F9" strokeWidth={outerSW} strokeLinecap="round" />
      <path d={arcPath(startAngle, valueAngle, rOuter)} fill="none" stroke={color} strokeWidth={outerSW} strokeLinecap="round" />

      {/* Target marker */}
      <line x1={ttx1} y1={tty1} x2={ttx2} y2={tty2} stroke="#334155" strokeWidth={2.5} strokeLinecap="round" />
      <text x={tlx} y={tly} textAnchor="middle" fontSize={8} fontWeight={700} fill="#64748B" dominantBaseline="middle">T</text>

      {/* Needle dot */}
      <circle cx={needleX} cy={needleY} r={5} fill={color} stroke="#fff" strokeWidth={2} />
      <circle cx={cx} cy={cy} r={3.5} fill="#E2E8F0" />

      {/* Center value */}
      <text x={cx} y={cy - 6} textAnchor="middle" fontSize={28} fontWeight={800} fill={color}>{displayVal}</text>
      <text x={cx} y={cy + 12} textAnchor="middle" fontSize={9} fontWeight={600} fill="#94A3B8">NPS</text>

      {/* Min / Max labels */}
      <text x={minLx} y={minLy} textAnchor="middle" fontSize={7.5} fill="#94A3B8">-100</text>
      <text x={maxLx} y={maxLy} textAnchor="middle" fontSize={7.5} fill="#94A3B8">+100</text>
    </svg>
  )
}

// ─── NPS Result Card ──────────────────────────────────────
// Dedicated card for NPS questions. Shows the semicircular gauge,
// Promoters / Passives / Detractors breakdown, trend and response count.

function NpsResultCard({ question, t, isArabic }: {
  question: SurveyQuestion
  t: (k: string) => string
  isArabic: boolean
}) {
  const result = MOCK_KPI_RESULTS.nps
  const qText = isArabic ? question.textAr : question.text
  const npsColor = perfColor(result.score, "nps")

  return (
    <Card className="overflow-hidden shadow-sm hover:shadow-md motion-safe:transition-shadow">
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between gap-2">
          <CardTitle className="text-sm font-bold" style={{ color: npsColor }}>
            {isArabic ? "صافي نقاط الترويج" : "Net Promoter Score"}
          </CardTitle>
          <Badge variant="outline" className="shrink-0 text-[10px] font-medium uppercase tracking-wide">NPS</Badge>
        </div>
        <CardDescription className="text-xs truncate" title={qText}>{qText}</CardDescription>
      </CardHeader>

      <CardContent className="pt-0 pb-4 flex flex-col items-center text-center">
        {/* Gauge */}
        <div className="w-full max-w-[185px]">
          <NpsGauge value={result.score} />
        </div>

        {/* Target label */}
        <p className="text-xs text-muted-foreground -mt-1 mb-3">
          {t("surveys.target")}: <span className="font-bold text-foreground">+50</span>
        </p>

        {/* 3-tile segment grid — Promoters | Passives | Detractors */}
        <div className="grid grid-cols-3 gap-2 w-full mb-3">
          {result.segments.map((seg) => (
            <div
              key={seg.label}
              className="rounded-lg py-1.5 px-1 text-center"
              style={{ backgroundColor: `${seg.color}18` }}
            >
              <div className="text-sm font-bold tabular-nums" style={{ color: seg.color }}>
                {seg.value}%
              </div>
              <div className="text-[9px] text-muted-foreground mt-0.5 leading-tight">
                {isArabic ? seg.labelAr : seg.label}
              </div>
            </div>
          ))}
        </div>

        {/* Trend + responses row */}
        <div className="flex items-center justify-center gap-6 text-xs">
          <div className="text-center">
            <div className="text-muted-foreground">{isArabic ? "عن الفترة السابقة" : "vs last period"}</div>
            <div className={cn(
              "font-bold tabular-nums mt-0.5 flex items-center justify-center gap-0.5",
              result.trendUp ? "text-d2 dark:text-d2-light" : "text-d5 dark:text-d5-light",
            )}>
              {result.trendUp
                ? <TrendingUp className="size-3" />
                : <TrendingDown className="size-3" />}
              {result.trendUp ? "+" : ""}{result.trend}
            </div>
          </div>
          <div className="text-center">
            <div className="text-muted-foreground">{t("surveys.responses")}</div>
            <div className="font-bold text-foreground tabular-nums mt-0.5">
              {result.responses.toLocaleString("en-US")}
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}

// ─── KPI Result Card — CSAT / CES / VFM / Agent ──────────
// Re-uses the existing KpiFlipCard component (with flip to reasons).

function KpiResultCard({ kpiType, question, t, isArabic }: {
  kpiType: string
  question: SurveyQuestion
  t: (k: string) => string
  isArabic: boolean
}) {
  const result = MOCK_KPI_RESULTS[kpiType]
  if (!result) return null

  const label = isArabic ? KPI_LABELS[kpiType]?.ar : KPI_LABELS[kpiType]?.en
  const subtitle = isArabic ? question.textAr : question.text

  const kpiMetric: KpiMetric = {
    id: kpiType,
    title: label || kpiType.toUpperCase(),
    subtitle,
    value: result.score,
    displayValue: result.displayValue,
    gaugePercent: result.score,
    targetLabel: kpiType === "ces" ? "35%" : "80%",
    trend: result.trend,
    trendUp: result.trendUp,
    trendLabel: `${result.trendUp ? "+" : ""}${result.trend}`,
    color: KPI_CHART_COLORS[kpiType] || "var(--chart-1)",
    responses: result.responses,
  }

  return (
    <KpiFlipCard
      kpi={kpiMetric}
      onDetail={() => {}}
      delay={0}
      targetWord={t("surveys.target")}
      targetTooltip=""
      responsesWord={t("surveys.responses")}
      reasonsLabel={t("surveys.reasons")}
    />
  )
}

// ─── Sentiment Card ───────────────────────────────────────
// Used for open-ended / comment questions. Shows a donut chart
// with positive / neutral / negative sentiment breakdown.

function SentimentCard({ question, t, isArabic }: {
  question: SurveyQuestion
  t: (k: string) => string
  isArabic: boolean
}) {
  const s = MOCK_SENTIMENT
  const qText = isArabic ? question.textAr : question.text
  const donutData = [
    { name: "positive", value: s.positive, color: D2 },
    { name: "neutral",  value: s.neutral,  color: D3 },
    { name: "negative", value: s.negative, color: D5 },
  ]
  const rows = [
    { icon: ThumbsUp,   label: t("surveys.positive"), labelAr: "إيجابي", value: s.positive, color: D2, colorClass: "text-d2 dark:text-d2-light" },
    { icon: Minus,      label: t("surveys.neutral"),  labelAr: "محايد",  value: s.neutral,  color: D3, colorClass: "text-d3 dark:text-d3-light" },
    { icon: ThumbsDown, label: t("surveys.negative"), labelAr: "سلبي",   value: s.negative, color: D5, colorClass: "text-d5 dark:text-d5-light" },
  ]

  return (
    <Card className="overflow-hidden shadow-sm hover:shadow-md motion-safe:transition-shadow">
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between gap-2">
          <div className="min-w-0">
            <CardTitle className="text-sm flex items-center gap-1.5">
              <MessageSquare className="size-3.5 text-nb-cyan shrink-0" />
              {t("surveys.sentimentAnalysis")}
            </CardTitle>
            <CardDescription className="text-xs truncate mt-0.5" title={qText}>{qText}</CardDescription>
          </div>
          <Badge variant="outline" className="shrink-0 text-[10px] font-medium uppercase tracking-wide">
            {t("surveys.openEnded")}
          </Badge>
        </div>
      </CardHeader>

      <CardContent className="pt-0">
        {/* Donut + legend */}
        <div className="flex items-center gap-4">
          <div className="shrink-0">
            <PieChart width={96} height={96}>
              <Pie
                data={donutData}
                cx={48}
                cy={48}
                innerRadius={26}
                outerRadius={42}
                dataKey="value"
                strokeWidth={2}
                stroke="var(--color-card)"
                startAngle={90}
                endAngle={-270}
              >
                {donutData.map((d) => (
                  <Cell key={d.name} fill={d.color} />
                ))}
              </Pie>
            </PieChart>
          </div>

          <div className="flex-1 space-y-2">
            {rows.map((row) => (
              <div key={row.label} className="flex items-center gap-2">
                <span className="size-2 rounded-full shrink-0" style={{ backgroundColor: row.color }} />
                <row.icon className={cn("size-3 shrink-0", row.colorClass)} />
                <span className="text-xs flex-1">{isArabic ? row.labelAr : row.label}</span>
                <span className="text-xs font-bold tabular-nums shrink-0" style={{ color: row.color }}>
                  {row.value}%
                </span>
              </div>
            ))}

            <div className="pt-1.5 border-t border-border">
              <span className="text-[10px] text-muted-foreground flex items-center gap-1">
                <Users className="size-3" />
                <span dir="ltr" className="tabular-nums">{s.total.toLocaleString()}</span>
                <span>{t("surveys.responses")}</span>
              </span>
            </div>
          </div>
        </div>

        {/* Top drivers */}
        <div className="grid grid-cols-2 gap-2 mt-3">
          <div className="rounded-lg bg-d2-light/40 dark:bg-d2-dark/10 px-3 py-2">
            <p className="text-[10px] font-medium text-d2-dark dark:text-d2-light mb-1">
              {t("surveys.topPositive")}
            </p>
            <p className="text-xs leading-snug">{isArabic ? s.topPositive.ar : s.topPositive.en}</p>
          </div>
          <div className="rounded-lg bg-d5-light/40 dark:bg-d5-dark/10 px-3 py-2">
            <p className="text-[10px] font-medium text-d5-dark dark:text-d5-light mb-1">
              {t("surveys.topNegative")}
            </p>
            <p className="text-xs leading-snug">{isArabic ? s.topNegative.ar : s.topNegative.en}</p>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}

// ─── Survey Results Panel ────────────────────────────────
// Shown below the table when a survey row is selected.

function SurveyResults({ survey, t, isArabic }: {
  survey: Survey
  t: (k: string) => string
  isArabic: boolean
}) {
  const activeQuestions = survey.questions.filter((q) => q.active)

  return (
    <div className="pt-2">
      {/* Results header */}
      <div className="flex flex-wrap items-center gap-2 mb-4">
        <BarChart3 className="size-4 text-primary shrink-0" />
        <h2 className="text-base font-bold">
          {t("surveys.resultsFor")}{" "}
          <span className="text-primary">{isArabic ? survey.nameAr : survey.name}</span>
        </h2>
        <Badge variant="outline" className="text-[10px] tabular-nums">
          <span dir="ltr">{survey.responses.toLocaleString()}</span>&nbsp;{t("surveys.responses")}
        </Badge>
        <Badge variant="outline" className="text-[10px]">
          {activeQuestions.length}&nbsp;{t("surveys.questions")}
        </Badge>
      </div>

      {/* Cards grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
        {survey.questions.map((q) => {
          if (q.kpiType === "comment") {
            return <SentimentCard key={q.id} question={q} t={t} isArabic={isArabic} />
          }
          if (q.kpiType === "nps") {
            return <NpsResultCard key={q.id} question={q} t={t} isArabic={isArabic} />
          }
          return (
            <KpiResultCard key={q.id} kpiType={q.kpiType} question={q} t={t} isArabic={isArabic} />
          )
        })}
      </div>
    </div>
  )
}

// ─── Add Question Dialog ─────────────────────────────────

interface AddQuestionDialogProps {
  open: boolean
  onClose: () => void
  onAdd: (q: Omit<SurveyQuestion, "id">) => void
  t: (k: string) => string
  isArabic: boolean
}

function AddQuestionDialog({ open, onClose, onAdd, t, isArabic }: AddQuestionDialogProps) {
  const [text, setText] = useState("")
  const [kpi, setKpi] = useState<SurveyQuestion["kpiType"]>("nps")
  const [target, setTarget] = useState("500")
  const [active, setActive] = useState(true)
  const [fontSize, setFontSize] = useState([5])

  function handleSubmit() {
    if (!text.trim()) return
    onAdd({
      text: text.trim(),
      textAr: text.trim(),
      kpiType: kpi,
      targetAnswers: parseInt(target) || 500,
      active,
      fontSize: fontSize[0],
    })
    setText("")
    setKpi("nps")
    setTarget("500")
    setActive(true)
    setFontSize([5])
    onClose()
  }

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t("surveys.addQuestion")}</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-2">
          {/* Question text */}
          <div className="space-y-1.5">
            <Label htmlFor="q-text">{t("surveys.questionText")}</Label>
            <Input
              id="q-text"
              value={text}
              onChange={(e) => setText(e.target.value)}
              placeholder={isArabic ? "أدخل نص السؤال..." : "Enter question text..."}
            />
          </div>

          {/* KPI selector */}
          <div className="space-y-1.5">
            <Label>{t("surveys.kpiType")}</Label>
            <Select value={kpi} onValueChange={(v) => setKpi(v as SurveyQuestion["kpiType"])}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {Object.entries(KPI_LABELS).map(([key, lbl]) => (
                  <SelectItem key={key} value={key}>
                    {isArabic ? lbl.ar : lbl.en}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Target answers */}
          <div className="space-y-1.5">
            <Label htmlFor="q-target">{t("surveys.targetAnswers")}</Label>
            <Input
              id="q-target"
              type="number"
              value={target}
              onChange={(e) => setTarget(e.target.value)}
              min={1}
            />
          </div>

          {/* Font size slider */}
          <div className="space-y-1.5">
            <Label>
              {t("surveys.fontSize")} —{" "}
              <span className="tabular-nums font-bold text-primary">{fontSize[0]}</span>/10
            </Label>
            <Slider
              value={fontSize}
              onValueChange={(value) => setFontSize(Array.isArray(value) ? [...value] : [value])}
              min={1}
              max={10}
              step={1}
              className="py-2"
            />
            <div className="flex justify-between text-[10px] text-muted-foreground tabular-nums">
              <span>1</span>
              <span>5</span>
              <span>10</span>
            </div>
          </div>

          {/* Status switch */}
          <div className="flex items-center justify-between rounded-lg border border-border px-3 py-2.5">
            <Label htmlFor="q-active" className="cursor-pointer">
              {t("surveys.questionStatus")}
            </Label>
            <div className="flex items-center gap-2">
              <span className="text-xs text-muted-foreground">
                {active ? t("surveys.active") : t("surveys.inactive")}
              </span>
              <Switch id="q-active" checked={active} onCheckedChange={setActive} />
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>{t("common.cancel")}</Button>
          <Button
            onClick={handleSubmit}
            disabled={!text.trim()}
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
          >
            {t("surveys.addQuestion")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ─── Main Page ───────────────────────────────────────────

export default function SurveysPage() {
  const { t, i18n } = useTranslation()
  const isArabic = i18n.language === "ar"

  const [surveys, setSurveys] = useState<Survey[]>(MOCK_SURVEYS)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [addDialogOpen, setAddDialogOpen] = useState(false)
  const [addTargetSurveyId, setAddTargetSurveyId] = useState<string | null>(null)

  // Keep the last selected survey in state so the grid can animate out
  const [displayedSurvey, setDisplayedSurvey] = useState<Survey | null>(null)
  const resultsVisible = selectedId !== null

  useEffect(() => {
    if (selectedId) {
      const s = surveys.find((sv) => sv.id === selectedId) ?? null
      setDisplayedSurvey(s)
    }
  }, [selectedId, surveys])

  function toggleSurveyStatus(id: string) {
    setSurveys((prev) =>
      prev.map((s) =>
        s.id === id
          ? { ...s, status: s.status === "active" ? "inactive" : "active" }
          : s,
      ),
    )
  }

  function handleAddQuestion(q: Omit<SurveyQuestion, "id">) {
    if (!addTargetSurveyId) return
    setSurveys((prev) =>
      prev.map((s) =>
        s.id === addTargetSurveyId
          ? { ...s, questions: [...s.questions, { ...q, id: `q-${Date.now()}` }] }
          : s,
      ),
    )
  }

  function formatDate(dateStr: string) {
    const d = new Date(dateStr)
    return d.toLocaleDateString(isArabic ? "ar-SA" : "en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
    })
  }

  return (
    <TooltipProvider>
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 space-y-6 py-6">

        {/* ── Page Header ─────────────────────── */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-heading font-bold">{t("surveys.title")}</h1>
            <p className="text-sm text-muted-foreground mt-1">{t("surveys.subtitle")}</p>
          </div>
        </div>

        {/* ── Survey Table ─────────────────────── */}
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/30">
                  <TableHead className="ps-6 w-28">{t("surveys.colId")}</TableHead>
                  <TableHead>{t("surveys.colName")}</TableHead>
                  <TableHead className="text-center">{t("surveys.colResponses")}</TableHead>
                  <TableHead className="text-center">{t("surveys.colStatus")}</TableHead>
                  <TableHead className="text-center">{t("surveys.colStartDate")}</TableHead>
                  <TableHead className="text-center">{t("surveys.colExpiry")}</TableHead>
                  <TableHead className="text-end pe-6">{t("surveys.colActions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {surveys.map((survey) => {
                  const isSelected = selectedId === survey.id
                  return (
                    <TableRow
                      key={survey.id}
                      className={cn(
                        "cursor-pointer motion-safe:transition-colors",
                        isSelected
                          ? "bg-primary/5 dark:bg-primary/10 border-s-2 border-s-primary"
                          : "hover:bg-muted/50",
                      )}
                      onClick={() => setSelectedId(isSelected ? null : survey.id)}
                    >
                      <TableCell className="ps-6">
                        <span className="text-xs font-mono text-muted-foreground">{survey.id}</span>
                      </TableCell>

                      <TableCell>
                        <div>
                          <p className="font-medium text-sm">{isArabic ? survey.nameAr : survey.name}</p>
                          <p className="text-[10px] text-muted-foreground mt-0.5">
                            {survey.questions.length}&nbsp;{t("surveys.questions")}
                          </p>
                        </div>
                      </TableCell>

                      <TableCell className="text-center">
                        <span className="tabular-nums font-medium">
                          {survey.responses > 0
                            ? survey.responses.toLocaleString()
                            : <span className="text-muted-foreground">—</span>}
                        </span>
                      </TableCell>

                      <TableCell className="text-center">
                        {statusBadge(survey.status, t)}
                      </TableCell>

                      <TableCell className="text-center">
                        <span className="text-sm tabular-nums flex items-center justify-center gap-1 text-muted-foreground">
                          <Calendar className="size-3" />
                          {formatDate(survey.startDate)}
                        </span>
                      </TableCell>

                      <TableCell className="text-center">
                        <span className="text-sm tabular-nums flex items-center justify-center gap-1 text-muted-foreground">
                          <Calendar className="size-3" />
                          {formatDate(survey.expiryDate)}
                        </span>
                      </TableCell>

                      <TableCell className="text-end pe-6">
                        <div
                          className="flex items-center justify-end gap-1"
                          onClick={(e) => e.stopPropagation()}
                        >
                          {/* Edit */}
                          <Tooltip>
                            <TooltipTrigger
                              className={cn(buttonVariants({ variant: "ghost", size: "icon" }), "size-7")}
                              aria-label={t("common.edit")}
                            >
                              <Pencil className="size-3.5" />
                            </TooltipTrigger>
                            <TooltipContent>{t("common.edit")}</TooltipContent>
                          </Tooltip>

                          {/* Activate / Deactivate */}
                          <Tooltip>
                            <TooltipTrigger
                              className={cn(buttonVariants({ variant: "ghost", size: "icon" }), "size-7")}
                              onClick={() => toggleSurveyStatus(survey.id)}
                              aria-label={
                                survey.status === "active"
                                  ? t("surveys.deactivate")
                                  : t("surveys.activate")
                              }
                            >
                              {survey.status === "active" ? (
                                <PowerOff className="size-3.5 text-d5" />
                              ) : (
                                <Power className="size-3.5 text-d2" />
                              )}
                            </TooltipTrigger>
                            <TooltipContent>
                              {survey.status === "active"
                                ? t("surveys.deactivate")
                                : t("surveys.activate")}
                            </TooltipContent>
                          </Tooltip>

                          {/* Quick-add question */}
                          <Tooltip>
                            <TooltipTrigger
                              className={cn(buttonVariants({ variant: "ghost", size: "icon" }), "size-7")}
                              onClick={() => {
                                setAddTargetSurveyId(survey.id)
                                setAddDialogOpen(true)
                              }}
                              aria-label={t("surveys.addQuestion")}
                            >
                              <Plus className="size-3.5 text-primary" />
                            </TooltipTrigger>
                            <TooltipContent>{t("surveys.addQuestion")}</TooltipContent>
                          </Tooltip>
                        </div>
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </div>
        </Card>

        {/* ── Selected row indicator ───────────── */}
        {resultsVisible && displayedSurvey && (
          <button
            className="flex items-center gap-2 text-xs text-muted-foreground hover:text-foreground motion-safe:transition-colors group"
            onClick={() => setSelectedId(null)}
            aria-label={isArabic ? "إخفاء النتائج" : "Collapse results"}
          >
            <ChevronDown className="size-3 text-primary group-hover:hidden" />
            <ChevronUp className="size-3 text-primary hidden group-hover:block" />
            <span>
              {t("surveys.showingResults")}{" "}
              <span className="font-semibold text-foreground">
                {isArabic ? displayedSurvey.nameAr : displayedSurvey.name}
              </span>
              {isArabic ? " — انقر للإخفاء" : " — click to collapse"}
            </span>
          </button>
        )}

        {/* ── Survey Results — animated with CSS grid-rows trick ─── */}
        <div
          className={cn(
            "grid transition-[grid-template-rows,opacity] duration-500 ease-[cubic-bezier(0.16,1,0.3,1)]",
            resultsVisible
              ? "grid-rows-[1fr] opacity-100"
              : "grid-rows-[0fr] opacity-0",
          )}
        >
          <div className="overflow-hidden">
            {displayedSurvey && (
              <SurveyResults
                survey={displayedSurvey}
                t={t}
                isArabic={isArabic}
              />
            )}
          </div>
        </div>

      </div>

      {/* ── Add Question Dialog ──────────────── */}
      <AddQuestionDialog
        open={addDialogOpen}
        onClose={() => setAddDialogOpen(false)}
        onAdd={handleAddQuestion}
        t={t}
        isArabic={isArabic}
      />
    </TooltipProvider>
  )
}
