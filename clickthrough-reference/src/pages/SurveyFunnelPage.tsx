import { useState } from "react"
import { useNavigate, useParams } from "react-router"
import {
  ArrowRight, ArrowLeft, Send, Eye, Play, CheckCircle2,
  ChevronRight, TrendingUp, TrendingDown, Info, Calendar,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"
import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"
import { MOCK_SURVEYS } from "@/data/mock-surveys"
import { cn } from "@/lib/utils"

// ── Mock funnel data ──────────────────────────────────────────────────────────
const FUNNEL_DATA = {
  sent:     { value: 4820, changeVsPrev: +4.2, label: { ar: "أُرسل", en: "Sent" } },
  opened:   { value: 3241, changeVsPrev: +2.1, label: { ar: "فُتح", en: "Opened" } },
  started:  { value: 2156, changeVsPrev: -1.3, label: { ar: "بدأ", en: "Started" } },
  finished: { value: 1894, changeVsPrev: +5.8, label: { ar: "اكتمل", en: "Finished" } },
}

const CHANNEL_BREAKDOWN = [
  { id: "web",       labelAr: "ويب",             labelEn: "Web",       sent: 2140, finished: 910,  color: "#0D8BBC" },
  { id: "whatsapp",  labelAr: "واتساب",            labelEn: "WhatsApp",  sent: 1580, finished: 652,  color: "#25D366" },
  { id: "email",     labelAr: "البريد الإلكتروني", labelEn: "Email",     sent: 820,  finished: 243,  color: "#E8A020" },
  { id: "sms",       labelAr: "رسائل SMS",         labelEn: "SMS",       sent: 280,  finished: 89,   color: "#8B90A5" },
]

const DATE_RANGE_OPTIONS = [
  { value: "7d",  labelAr: "آخر 7 أيام",  labelEn: "Last 7 days" },
  { value: "30d", labelAr: "آخر 30 يوم",  labelEn: "Last 30 days" },
  { value: "90d", labelAr: "آخر 90 يوم",  labelEn: "Last 90 days" },
  { value: "all", labelAr: "كل الوقت",     labelEn: "All time" },
]

// ── Step icons ────────────────────────────────────────────────────────────────
const STEP_ICONS: Record<string, React.ReactNode> = {
  sent:     <Send className="size-5" />,
  opened:   <Eye className="size-5" />,
  started:  <Play className="size-5" />,
  finished: <CheckCircle2 className="size-5" />,
}

// ── Funnel step card ──────────────────────────────────────────────────────────
function FunnelStep({
  id,
  data,
  totalSent,
  conversionRate,
  isLast,
  isAr,
}: {
  id: string
  data: { value: number; changeVsPrev: number; label: { ar: string; en: string } }
  totalSent: number
  conversionRate?: number
  isLast: boolean
  isAr: boolean
}) {
  const pctOfTotal = totalSent > 0 ? (data.value / totalSent) * 100 : 0
  const isUp = data.changeVsPrev >= 0
  const TrendIcon = isUp ? TrendingUp : TrendingDown

  return (
    <div className="flex items-center gap-2">
      <div className="flex-1 rounded-lg border border-border bg-card p-5 space-y-3 hover:shadow-md transition-shadow">
        {/* Icon + label */}
        <div className="flex items-center gap-3">
          <div className="size-9 rounded-lg bg-primary/10 flex items-center justify-center text-primary">
            {STEP_ICONS[id]}
          </div>
          <p className="text-sm font-semibold text-muted-foreground">
            {isAr ? data.label.ar : data.label.en}
          </p>
        </div>

        {/* Value */}
        <p className="text-3xl font-heading font-bold tabular-nums">
          {data.value.toLocaleString()}
        </p>

        {/* Stats row */}
        <div className="flex items-center gap-3 flex-wrap">
          <span className="text-xs text-muted-foreground">
            {pctOfTotal.toFixed(1)}% {isAr ? "من الإجمالي" : "of total"}
          </span>
          <div className={cn("flex items-center gap-1 text-xs font-medium", isUp ? "text-[#2EB85C]" : "text-[#C01B2A]")}>
            <TrendIcon className="size-3" />
            <span>
              {isUp ? "+" : ""}{data.changeVsPrev.toFixed(1)}%
            </span>
            <span className="text-muted-foreground font-normal">
              {isAr ? "vs. الشهر السابق" : "vs. last month"}
            </span>
          </div>
        </div>
      </div>

      {/* Conversion arrow */}
      {!isLast && (
        <div className="flex flex-col items-center gap-1 shrink-0 px-1">
          <div
            className={cn(
              "px-2.5 py-1 rounded-full text-xs font-bold tabular-nums",
              (conversionRate ?? 0) >= 70 ? "bg-[#C8F5DB] text-[#1A7A3C]"
              : (conversionRate ?? 0) >= 50 ? "bg-[#FFF0CC] text-[#7A5000]"
              : "bg-[#FFD6DA] text-[#6B0010]"
            )}
          >
            {conversionRate?.toFixed(1)}%
          </div>
          <ChevronRight className={cn("size-5 text-muted-foreground", isRtl && "rotate-180")} />
        </div>
      )}
    </div>
  )
}

let isRtl = false // Hoisted for use in FunnelStep — overridden inside the page

// ── Channel bar ───────────────────────────────────────────────────────────────
function ChannelBar({
  ch,
  maxSent,
  isAr,
}: {
  ch: (typeof CHANNEL_BREAKDOWN)[0]
  maxSent: number
  isAr: boolean
}) {
  const rate = ch.sent > 0 ? (ch.finished / ch.sent) * 100 : 0
  const barWidth = maxSent > 0 ? (ch.sent / maxSent) * 100 : 0

  return (
    <div className="grid grid-cols-[140px_1fr_120px] items-center gap-4">
      <p className="text-sm font-medium text-end">{isAr ? ch.labelAr : ch.labelEn}</p>
      <div className="h-2.5 rounded-full bg-muted/40 overflow-hidden">
        <div
          className="h-full rounded-full motion-safe:transition-all motion-safe:duration-700"
          style={{ width: `${barWidth}%`, backgroundColor: ch.color }}
        />
      </div>
      <div className="flex items-center justify-between text-xs tabular-nums">
        <span className="text-muted-foreground">{ch.sent.toLocaleString()} →</span>
        <span className="font-semibold" style={{ color: ch.color }}>
          {rate.toFixed(1)}%
        </span>
      </div>
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────
export default function SurveyFunnelPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { i18n } = useTranslation()
  const { isRtl: rtl } = useDirection()
  isRtl = rtl // Update module-level variable for FunnelStep component
  const isAr = i18n.language === "ar"
  const BackIcon = rtl ? ArrowRight : ArrowLeft

  const survey = id ? MOCK_SURVEYS.find((s) => s.id === id) : null
  const surveyName = survey ? (isAr ? survey.nameAr : survey.nameEn) : (isAr ? "مسار الاستجابة" : "Response Funnel")

  const [dateRange, setDateRange] = useState("30d")
  const [channelFilter, setChannelFilter] = useState("all")

  const steps = Object.entries(FUNNEL_DATA) as [string, (typeof FUNNEL_DATA)[keyof typeof FUNNEL_DATA]][]
  const totalSent = FUNNEL_DATA.sent.value
  const maxChannelSent = Math.max(...CHANNEL_BREAKDOWN.map((c) => c.sent))

  function convRate(aKey: string, bKey: string): number {
    const a = FUNNEL_DATA[aKey as keyof typeof FUNNEL_DATA]?.value ?? 0
    const b = FUNNEL_DATA[bKey as keyof typeof FUNNEL_DATA]?.value ?? 0
    return a > 0 ? (b / a) * 100 : 0
  }
  const convRates = [
    convRate("sent", "opened"),
    convRate("opened", "started"),
    convRate("started", "finished"),
  ]

  const overallRate = totalSent > 0 ? (FUNNEL_DATA.finished.value / totalSent) * 100 : 0
  const filteredChannels =
    channelFilter === "all" ? CHANNEL_BREAKDOWN : CHANNEL_BREAKDOWN.filter((c) => c.id === channelFilter)

  return (
    <div className="space-y-6 py-5 px-8 pb-20">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-4">
          <Button
            variant="ghost"
            size="icon"
            className="size-9 mt-0.5 shrink-0"
            onClick={() => navigate(id ? `/surveys/${id}/edit` : "/surveys")}
            aria-label={isAr ? "العودة إلى المحرر" : "Back to builder"}
          >
            <BackIcon className="size-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-heading font-bold">
              {isAr ? "مسار الاستجابة" : "Response Funnel"}
            </h1>
            <p className="text-sm text-muted-foreground mt-0.5 truncate max-w-lg">{surveyName}</p>
          </div>
        </div>

        {/* Controls */}
        <div className="flex items-center gap-2 shrink-0">
          <Badge className="text-xs bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200 border-transparent gap-1">
            <Info className="size-3" />
            {isAr ? "بيانات تجريبية" : "Demo Data"}
          </Badge>
          <Select value={channelFilter} onValueChange={(v) => setChannelFilter(v ?? "all")}>
            <SelectTrigger className="w-36">
              <SelectValue placeholder={isAr ? "القناة" : "Channel"} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{isAr ? "جميع القنوات" : "All Channels"}</SelectItem>
              {CHANNEL_BREAKDOWN.map((c) => (
                <SelectItem key={c.id} value={c.id}>
                  {isAr ? c.labelAr : c.labelEn}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={dateRange} onValueChange={(v) => setDateRange(v ?? "30d")}>
            <SelectTrigger className="w-36">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {DATE_RANGE_OPTIONS.map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {isAr ? o.labelAr : o.labelEn}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button variant="secondary" size="sm">
            <Calendar className="size-4 me-1.5" />
            {isAr ? "تصدير" : "Export"}
          </Button>
        </div>
      </div>

      {/* Overall completion badge */}
      <div className="flex items-center gap-3 rounded-lg border border-border bg-card px-5 py-4">
        <div className="size-10 rounded-lg bg-primary/10 flex items-center justify-center">
          <CheckCircle2 className="size-5 text-primary" />
        </div>
        <div>
          <p className="text-sm text-muted-foreground">
            {isAr ? "معدل الإكمال الإجمالي" : "Overall Completion Rate"}
          </p>
          <p className="text-2xl font-heading font-bold tabular-nums">
            {overallRate.toFixed(1)}%
          </p>
        </div>
        <div className="ms-auto text-end">
          <p className="text-xs text-muted-foreground">
            {isAr ? "الفترة المختارة" : "Selected period"}
          </p>
          <p className="text-sm font-medium">
            {DATE_RANGE_OPTIONS.find((o) => o.value === dateRange)?.[isAr ? "labelAr" : "labelEn"]}
          </p>
        </div>
      </div>

      {/* Funnel steps */}
      <div className="flex items-start gap-0 overflow-x-auto pb-2">
        {steps.map(([key, data], idx) => (
          <FunnelStep
            key={key}
            id={key}
            data={data}
            totalSent={totalSent}
            conversionRate={idx < convRates.length ? convRates[idx] : undefined}
            isLast={idx === steps.length - 1}
            isAr={isAr}
          />
        ))}
      </div>

      {/* Funnel bar chart (visual) */}
      <div className="rounded-lg border border-border bg-card p-6 space-y-4">
        <h2 className="text-base font-bold">{isAr ? "مسار التحويل المرئي" : "Visual Conversion Funnel"}</h2>
        <div className="space-y-3">
          {steps.map(([key, data], idx) => {
            const pct = totalSent > 0 ? (data.value / totalSent) * 100 : 0
            const colors = ["bg-primary", "bg-nb-cyan-300", "bg-nb-mint", "bg-nb-mint-700"]
            return (
              <div key={key} className="space-y-1">
                <div className="flex items-center justify-between text-xs">
                  <span className="text-muted-foreground">{isAr ? data.label.ar : data.label.en}</span>
                  <span className="font-semibold tabular-nums">
                    {data.value.toLocaleString()} ({pct.toFixed(1)}%)
                  </span>
                </div>
                <div className="h-8 bg-muted/30 rounded-lg overflow-hidden">
                  <div
                    className={cn("h-full rounded-lg motion-safe:transition-all motion-safe:duration-700 flex items-center ps-3", colors[idx])}
                    style={{ width: `${pct}%`, minWidth: "2rem" }}
                  >
                    {pct > 12 && (
                      <span className="text-white text-xs font-medium truncate">{data.value.toLocaleString()}</span>
                    )}
                  </div>
                </div>
              </div>
            )
          })}
        </div>
      </div>

      {/* Channel breakdown */}
      <div className="rounded-lg border border-border bg-card p-6 space-y-5">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-bold">
            {isAr ? "التوزيع حسب القناة" : "Breakdown by Channel"}
          </h2>
          <p className="text-xs text-muted-foreground">
            {isAr ? "الإرسال → معدل الإكمال" : "Sent → Completion Rate"}
          </p>
        </div>
        <div className="space-y-4">
          {filteredChannels.map((ch) => (
            <ChannelBar key={ch.id} ch={ch} maxSent={maxChannelSent} isAr={isAr} />
          ))}
        </div>
      </div>

      {/* Upstream data note */}
      <div className="rounded-lg border border-border bg-nb-cyan-100/40 dark:bg-nb-cyan-900/15 p-5 space-y-2">
        <div className="flex items-center gap-2">
          <Info className="size-4 text-nb-cyan shrink-0" />
          <p className="text-sm font-semibold text-nb-cyan-800 dark:text-nb-cyan-200">
            {isAr ? "ملاحظة حول مصادر البيانات" : "Data Source Note"}
          </p>
        </div>
        <p className="text-xs text-nb-cyan-700 dark:text-nb-cyan-300 leading-relaxed">
          {isAr
            ? "البيانات المعروضة هي بيانات تجريبية. في الإنتاج، تأتي أحداث 'أُرسل' و'فُتح' من وحدة توزيع الاستبيانات (M-02)، وأحداث 'بدأ' و'اكتمل' من وحدة استيعاب الردود (M-04). ستُعرض البيانات الحقيقية تلقائياً بعد ربط هذه الوحدات."
            : "Data shown is illustrative. In production, 'Sent' and 'Opened' events come from the Survey Distribution module (M-02), while 'Started' and 'Finished' events come from the Response Ingestion module (M-04). Real data will appear automatically once these modules are connected."}
        </p>
      </div>
    </div>
  )
}
