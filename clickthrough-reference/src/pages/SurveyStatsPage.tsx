import { useNavigate, useParams } from "react-router"
import {
  ArrowRight, ArrowLeft, Edit2, Globe, BarChart2, Users, CheckCircle,
  CalendarDays, TrendingUp, Smartphone, MessageCircle,
  Mail, Hash, MoreHorizontal, Smile, Meh, Frown, Activity,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table"
import { ChartContainer, ChartTooltip, ChartTooltipContent, ChartLegend, ChartLegendContent } from "@/components/ui/chart"
import { ComposedChart, Bar, Line, XAxis, YAxis, CartesianGrid } from "recharts"
import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"
import { MOCK_SURVEYS } from "@/data/mock-surveys"
import KpiDashboardPreview from "@/components/kpi/KpiDashboardPreview"
import { cn } from "@/lib/utils"

// ── D-scale colors (semantic status only, never decorative) ───────────────────
const D1 = "#1A7A3C"
const D2 = "#2EB85C"
const D3 = "#E8A020"
const D4 = "#E05C1A"
const D5 = "#C01B2A"

function perfColor(value: number, kpiId = ""): string {
  if (kpiId === "ces" || kpiId === "chs") {
    if (value <= 30) return D1
    if (value <= 40) return D2
    if (value <= 50) return D3
    if (value <= 60) return D4
    return D5
  }
  if (kpiId === "nps") {
    if (value >= 50) return D1
    if (value >= 40) return D2
    if (value >= 30) return D3
    if (value >= 20) return D4
    return D5
  }
  if (value >= 85) return D1
  if (value >= 75) return D2
  if (value >= 60) return D3
  if (value >= 45) return D4
  return D5
}

// ── Mock stats data per survey ────────────────────────────────────────────────

interface ChannelStat {
  id: string
  nameEn: string
  nameAr: string
  responses: number
  pct: number
  avgScore: number
}

interface TrendPoint {
  weekEn: string
  weekAr: string
  responses: number
  score: number
}

interface RecentResponse {
  id: string
  at: string
  channelEn: string
  channelAr: string
  scores: Record<string, number>
  sentiment: "positive" | "neutral" | "negative"
  comment?: string
}

interface SurveyStatsData {
  kpiIds: string[]
  completionRate: number
  daysActive: number
  primaryKpi: string
  primaryScore: number
  trend: TrendPoint[]
  channels: ChannelStat[]
  recent: RecentResponse[]
}

const STATS: Record<string, SurveyStatsData> = {
  "srv-001": {
    kpiIds: ["csat", "nps", "ces"],
    completionRate: 78.3,
    daysActive: 63,
    primaryKpi: "csat",
    primaryScore: 78,
    trend: [
      { weekEn: "May 5",  weekAr: "5 مايو",  responses: 196, score: 74 },
      { weekEn: "May 12", weekAr: "12 مايو", responses: 224, score: 76 },
      { weekEn: "May 19", weekAr: "19 مايو", responses: 241, score: 75 },
      { weekEn: "May 26", weekAr: "26 مايو", responses: 268, score: 78 },
      { weekEn: "Jun 2",  weekAr: "2 يونيو",  responses: 289, score: 79 },
      { weekEn: "Jun 9",  weekAr: "9 يونيو",  responses: 312, score: 77 },
      { weekEn: "Jun 16", weekAr: "16 يونيو", responses: 198, score: 80 },
      { weekEn: "Jun 23", weekAr: "23 يونيو", responses: 114, score: 82 },
    ],
    channels: [
      { id: "web",       nameEn: "Web App",   nameAr: "تطبيق الويب",          responses: 891, pct: 48.4, avgScore: 79 },
      { id: "whatsapp",  nameEn: "WhatsApp",  nameAr: "واتساب",               responses: 612, pct: 33.2, avgScore: 74 },
      { id: "email",     nameEn: "Email",     nameAr: "البريد الإلكتروني",    responses: 221, pct: 12.0, avgScore: 71 },
      { id: "sms",       nameEn: "SMS",       nameAr: "رسائل قصيرة",          responses: 118, pct: 6.4,  avgScore: 76 },
    ],
    recent: [
      { id: "r-001", at: "2026-06-22 14:32", channelEn: "Web App",  channelAr: "تطبيق الويب",       scores: { csat: 5, nps: 9  }, sentiment: "positive" },
      { id: "r-002", at: "2026-06-22 13:18", channelEn: "WhatsApp", channelAr: "واتساب",            scores: { csat: 4, nps: 7  }, sentiment: "positive" },
      { id: "r-003", at: "2026-06-22 11:55", channelEn: "Email",    channelAr: "البريد الإلكتروني", scores: { csat: 3, nps: 5  }, sentiment: "neutral",  comment: "Wait time was too long" },
      { id: "r-004", at: "2026-06-22 10:02", channelEn: "Web App",  channelAr: "تطبيق الويب",       scores: { csat: 2, nps: 3  }, sentiment: "negative", comment: "App kept crashing during setup" },
      { id: "r-005", at: "2026-06-22 09:44", channelEn: "SMS",      channelAr: "رسائل قصيرة",       scores: { csat: 5, nps: 10 }, sentiment: "positive" },
    ],
  },
  "srv-002": {
    kpiIds: ["csat", "ces"],
    completionRate: 71.2,
    daysActive: 8,
    primaryKpi: "csat",
    primaryScore: 74,
    trend: [
      { weekEn: "Jun 15", weekAr: "15 يونيو", responses: 98,  score: 72 },
      { weekEn: "Jun 22", weekAr: "22 يونيو", responses: 536, score: 75 },
    ],
    channels: [
      { id: "sms",      nameEn: "SMS",       nameAr: "رسائل قصيرة", responses: 321, pct: 50.6, avgScore: 75 },
      { id: "whatsapp", nameEn: "WhatsApp",  nameAr: "واتساب",      responses: 215, pct: 33.9, avgScore: 73 },
      { id: "email",    nameEn: "Email",     nameAr: "البريد الإلكتروني", responses: 98, pct: 15.5, avgScore: 69 },
    ],
    recent: [
      { id: "r-010", at: "2026-06-22 16:04", channelEn: "SMS",      channelAr: "رسائل قصيرة",       scores: { csat: 4, ces: 38 }, sentiment: "positive" },
      { id: "r-011", at: "2026-06-22 15:21", channelEn: "WhatsApp", channelAr: "واتساب",            scores: { csat: 5, ces: 28 }, sentiment: "positive" },
      { id: "r-012", at: "2026-06-22 14:09", channelEn: "Email",    channelAr: "البريد الإلكتروني", scores: { csat: 3, ces: 55 }, sentiment: "neutral" },
    ],
  },
  "srv-004": {
    kpiIds: ["csat", "fcr", "agent"],
    completionRate: 82.4,
    daysActive: 210,
    primaryKpi: "csat",
    primaryScore: 81,
    trend: [
      { weekEn: "Jan 6",  weekAr: "6 يناير",  responses: 280, score: 76 },
      { weekEn: "Jan 20", weekAr: "20 يناير", responses: 265, score: 77 },
      { weekEn: "Feb 3",  weekAr: "3 فبراير", responses: 294, score: 79 },
      { weekEn: "Feb 17", weekAr: "17 فبراير", responses: 311, score: 78 },
      { weekEn: "Mar 3",  weekAr: "3 مارس",   responses: 342, score: 80 },
      { weekEn: "Mar 17", weekAr: "17 مارس",  responses: 298, score: 81 },
      { weekEn: "Jun 9",  weekAr: "9 يونيو",  responses: 251, score: 83 },
      { weekEn: "Jun 23", weekAr: "23 يونيو", responses: 250, score: 81 },
    ],
    channels: [
      { id: "ivr",   nameEn: "IVR",       nameAr: "الرد الآلي",    responses: 1028, pct: 44.9, avgScore: 83 },
      { id: "web",   nameEn: "Web App",   nameAr: "تطبيق الويب",   responses: 718,  pct: 31.3, avgScore: 80 },
      { id: "email", nameEn: "Email",     nameAr: "البريد الإلكتروني", responses: 341, pct: 14.9, avgScore: 75 },
      { id: "sms",   nameEn: "SMS",       nameAr: "رسائل قصيرة",   responses: 204,  pct: 8.9,  avgScore: 79 },
    ],
    recent: [
      { id: "r-020", at: "2026-05-30 11:14", channelEn: "IVR",     channelAr: "الرد الآلي",          scores: { csat: 4, fcr: 1 }, sentiment: "positive" },
      { id: "r-021", at: "2026-05-30 10:59", channelEn: "Web App", channelAr: "تطبيق الويب",         scores: { csat: 5, fcr: 1 }, sentiment: "positive", comment: "Resolved on first contact, very helpful" },
      { id: "r-022", at: "2026-05-30 10:22", channelEn: "Email",   channelAr: "البريد الإلكتروني",  scores: { csat: 3, fcr: 0 }, sentiment: "neutral" },
    ],
  },
  "srv-005": {
    kpiIds: ["csat", "nps"],
    completionRate: 84.1,
    daysActive: 105,
    primaryKpi: "nps",
    primaryScore: 52,
    trend: [
      { weekEn: "Nov 10", weekAr: "10 نوف", responses: 420, score: 76 },
      { weekEn: "Nov 24", weekAr: "24 نوف", responses: 568, score: 79 },
      { weekEn: "Dec 8",  weekAr: "8 ديس",  responses: 612, score: 81 },
      { weekEn: "Dec 22", weekAr: "22 ديس", responses: 689, score: 82 },
      { weekEn: "Jan 5",  weekAr: "5 يناير", responses: 745, score: 84 },
      { weekEn: "Jan 19", weekAr: "19 يناير", responses: 698, score: 83 },
      { weekEn: "Feb 2",  weekAr: "2 فبراير", responses: 812, score: 86 },
      { weekEn: "Feb 16", weekAr: "16 فبراير", responses: 559, score: 85 },
    ],
    channels: [
      { id: "web",      nameEn: "In-App",   nameAr: "داخل التطبيق", responses: 3012, pct: 59.0, avgScore: 87 },
      { id: "email",    nameEn: "Email",    nameAr: "البريد الإلكتروني", responses: 1244, pct: 24.4, avgScore: 81 },
      { id: "whatsapp", nameEn: "WhatsApp", nameAr: "واتساب", responses: 847, pct: 16.6, avgScore: 78 },
    ],
    recent: [
      { id: "r-030", at: "2026-03-10 09:00", channelEn: "In-App",   channelAr: "داخل التطبيق",        scores: { csat: 5, nps: 10 }, sentiment: "positive", comment: "Love the new design!" },
      { id: "r-031", at: "2026-03-10 08:44", channelEn: "Email",    channelAr: "البريد الإلكتروني",   scores: { csat: 4, nps: 8  }, sentiment: "positive" },
      { id: "r-032", at: "2026-03-09 17:30", channelEn: "WhatsApp", channelAr: "واتساب",              scores: { csat: 3, nps: 6  }, sentiment: "neutral",  comment: "Some gestures are confusing" },
    ],
  },
}

// ── Status config ─────────────────────────────────────────────────────────────
const STATUS_CONFIG = {
  Draft:    { labelAr: "مسودة",  labelEn: "Draft",    className: "bg-muted text-muted-foreground" },
  Active:   { labelAr: "نشط",   labelEn: "Active",   className: "bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB]" },
  Paused:   { labelAr: "موقوف", labelEn: "Paused",   className: "bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC]" },
  Archived: { labelAr: "مؤرشف", labelEn: "Archived", className: "bg-muted/60 text-muted-foreground" },
}

const CHANNEL_ICONS: Record<string, React.ElementType> = {
  web: Smartphone, whatsapp: MessageCircle, email: Mail, sms: Hash, ivr: Activity,
}

const SENTIMENT_CONFIG = {
  positive: { icon: Smile, color: D2,   labelAr: "إيجابي",  labelEn: "Positive" },
  neutral:  { icon: Meh,   color: D3,   labelAr: "محايد",   labelEn: "Neutral"  },
  negative: { icon: Frown, color: D5,   labelAr: "سلبي",    labelEn: "Negative" },
}

const KPI_LABELS: Record<string, { shortEn: string; shortAr: string }> = {
  csat:  { shortEn: "CSAT", shortAr: "رضا العملاء" },
  nps:   { shortEn: "NPS",  shortAr: "صافي المروجين" },
  ces:   { shortEn: "CES",  shortAr: "جهد العميل" },
  fcr:   { shortEn: "FCR",  shortAr: "الحل من أول مرة" },
  agent: { shortEn: "Agent",shortAr: "أداء الموظف" },
  vfm:   { shortEn: "VFM",  shortAr: "قيمة المال" },
}

// chart config for shadcn ChartContainer
const CHART_CONFIG = {
  responses: { label: "Responses", color: "var(--color-chart-1)" },
  score:     { label: "Score %",   color: "var(--color-chart-2)" },
}

// ── Page ──────────────────────────────────────────────────────────────────────
export default function SurveyStatsPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { i18n } = useTranslation()
  const { isRtl } = useDirection()
  const isAr = i18n.language === "ar"
  const BackIcon = isRtl ? ArrowRight : ArrowLeft

  const survey = id ? MOCK_SURVEYS.find((s) => s.id === id) : null
  const stats = id ? STATS[id] : undefined

  const surveyName = survey
    ? (isAr ? survey.nameAr : survey.nameEn)
    : (isAr ? "استبيان" : "Survey")

  const statusCfg = STATUS_CONFIG[survey?.status ?? "Draft"]

  // ── No-data state ──────────────────────────────────────────────
  if (!survey || !stats || survey.responseCount === 0) {
    return (
      <div className="space-y-5 py-5 px-8">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" className="size-9 shrink-0"
            onClick={() => navigate("/surveys")}
            aria-label={isAr ? "العودة إلى المكتبة" : "Back to library"}
          >
            <BackIcon className="size-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-heading font-bold">{surveyName}</h1>
            <p className="text-sm text-muted-foreground mt-0.5">
              {isAr ? "إحصائيات الاستبيان" : "Survey Statistics"}
            </p>
          </div>
        </div>
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <BarChart2 className="size-14 text-muted-foreground mb-4" />
          <h3 className="text-lg font-bold mb-2">
            {isAr ? "لا توجد بيانات بعد" : "No data yet"}
          </h3>
          <p className="text-sm text-muted-foreground mb-6 max-w-sm leading-relaxed">
            {survey?.status === "Draft"
              ? (isAr
                  ? "فعّل الاستبيان لبدء جمع الردود وعرض الإحصائيات هنا."
                  : "Activate the survey to start collecting responses. Statistics will appear here.")
              : (isAr
                  ? "لم يتم تلقي أي ردود على هذا الاستبيان بعد."
                  : "No responses have been received for this survey yet.")}
          </p>
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => navigate(id ? `/surveys/${id}/edit` : "/surveys")}
          >
            <Edit2 className="size-4 me-1.5" />
            {isAr ? "فتح المحرر" : "Open Builder"}
          </Button>
        </div>
      </div>
    )
  }

  const maxChannelResponses = Math.max(...stats.channels.map((c) => c.responses))

  return (
    <div className="space-y-5 py-5 px-8">

      {/* ── Page header ─────────────────────────────────────── */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3 min-w-0">
          <Button
            variant="ghost"
            size="icon"
            className="size-9 shrink-0 mt-0.5"
            onClick={() => navigate("/surveys")}
            aria-label={isAr ? "العودة إلى المكتبة" : "Back to library"}
          >
            <BackIcon className="size-4" />
          </Button>
          <div className="min-w-0">
            <div className="flex items-center gap-2.5 flex-wrap">
              <h1 className="text-2xl font-heading font-bold truncate">{surveyName}</h1>
              <Badge className={cn("text-xs shrink-0", statusCfg.className)}>
                {isAr ? statusCfg.labelAr : statusCfg.labelEn}
              </Badge>
            </div>
            <p className="text-sm text-muted-foreground mt-0.5">
              {isAr ? "إحصائيات الاستبيان" : "Survey Statistics"}
              {survey.journeyId && (
                <span className="ms-2">
                  · {isAr ? (survey.journeyNameAr ?? "") : (survey.journeyNameEn ?? "")}
                </span>
              )}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <Button
            variant="secondary"
            size="sm"
            onClick={() => navigate(`/surveys/${id}/edit`)}
          >
            <Edit2 className="size-4 me-1.5" />
            {isAr ? "تعديل" : "Edit"}
          </Button>
          <DropdownMenu>
            <DropdownMenuTrigger
              className="inline-flex items-center justify-center size-9 rounded-md border border-border text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
              aria-label={isAr ? "المزيد" : "More"}
            >
              <MoreHorizontal className="size-4" />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onClick={() => navigate(`/surveys/${id}/preview`)}>
                <CheckCircle className="size-4 me-2" />
                {isAr ? "معاينة" : "Preview"}
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => navigate(`/surveys/${id}/translations`)}>
                <Globe className="size-4 me-2" />
                {isAr ? "ترجمة" : "Translations"}
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => navigate(`/surveys/${id}/funnel`)}>
                <BarChart2 className="size-4 me-2" />
                {isAr ? "مسار الاستجابة" : "Response Funnel"}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      {/* ── Summary stat cards ───────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Total Responses */}
        <Card>
          <CardContent className="p-4">
            <div className="flex items-start justify-between mb-2">
              <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
                {isAr ? "إجمالي الردود" : "Total Responses"}
              </p>
              <Users className="size-4 text-muted-foreground shrink-0" />
            </div>
            <p className="text-3xl font-heading font-bold tabular-nums text-foreground">
              {survey.responseCount.toLocaleString("en-US")}
            </p>
            <p className="text-xs text-muted-foreground mt-1">
              {isAr ? `${survey.questionCount} سؤال` : `${survey.questionCount} questions`}
            </p>
          </CardContent>
        </Card>

        {/* Completion Rate */}
        <Card>
          <CardContent className="p-4">
            <div className="flex items-start justify-between mb-2">
              <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
                {isAr ? "معدل الإكمال" : "Completion Rate"}
              </p>
              <CheckCircle className="size-4 text-muted-foreground shrink-0" />
            </div>
            <p
              className="text-3xl font-heading font-bold tabular-nums"
              style={{ color: perfColor(stats.completionRate) }}
            >
              {stats.completionRate}%
            </p>
            <p className="text-xs text-muted-foreground mt-1">
              {isAr ? "من الردود المفتوحة" : "of opened surveys"}
            </p>
          </CardContent>
        </Card>

        {/* Primary KPI score */}
        <Card>
          <CardContent className="p-4">
            <div className="flex items-start justify-between mb-2">
              <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
                {isAr
                  ? (KPI_LABELS[stats.primaryKpi]?.shortAr ?? stats.primaryKpi.toUpperCase())
                  : (KPI_LABELS[stats.primaryKpi]?.shortEn ?? stats.primaryKpi.toUpperCase())}
              </p>
              <Activity className="size-4 text-muted-foreground shrink-0" />
            </div>
            <p
              className="text-3xl font-heading font-bold tabular-nums"
              style={{ color: perfColor(stats.primaryScore, stats.primaryKpi) }}
            >
              {stats.primaryKpi === "nps" && stats.primaryScore > 0 ? "+" : ""}
              {stats.primaryScore}
              {stats.primaryKpi !== "nps" ? "%" : ""}
            </p>
            <div className="flex items-center gap-1 mt-1">
              <TrendingUp className="size-3 text-[#2EB85C]" />
              <p className="text-xs" style={{ color: D2 }}>
                {isAr ? "↑ تحسّن عن الربع الماضي" : "↑ vs last quarter"}
              </p>
            </div>
          </CardContent>
        </Card>

        {/* Days active */}
        <Card>
          <CardContent className="p-4">
            <div className="flex items-start justify-between mb-2">
              <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
                {isAr ? "أيام نشطة" : "Days Active"}
              </p>
              <CalendarDays className="size-4 text-muted-foreground shrink-0" />
            </div>
            <p className="text-3xl font-heading font-bold tabular-nums text-foreground">
              {stats.daysActive}
            </p>
            <p className="text-xs text-muted-foreground mt-1">
              {isAr
                ? `${survey.type === "Relational" ? "علائقي" : "معاملاتي"}`
                : survey.type}
            </p>
          </CardContent>
        </Card>
      </div>

      {/* ── KPI Score Gauges ─────────────────────────────────── */}
      <div>
        <h2 className="text-lg font-bold mb-3">
          {isAr ? "نتائج مؤشرات الأداء" : "KPI Scores"}
        </h2>
        <div className={cn(
          "grid gap-4",
          stats.kpiIds.length === 1 ? "grid-cols-1 max-w-xs" :
          stats.kpiIds.length === 2 ? "grid-cols-1 sm:grid-cols-2 max-w-2xl" :
          "grid-cols-1 sm:grid-cols-2 lg:grid-cols-3"
        )}>
          {stats.kpiIds.map((kpiId) => (
            <Card key={kpiId} className="hover:shadow-md transition-shadow">
              <CardContent className="p-4 pt-4">
                <KpiDashboardPreview kpiId={kpiId} />
              </CardContent>
            </Card>
          ))}
        </div>
      </div>

      {/* ── Weekly Trend Chart ───────────────────────────────── */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">
            {isAr ? "الأداء الأسبوعي" : "Weekly Performance"}
          </CardTitle>
          <CardDescription>
            {isAr
              ? "عدد الردود الأسبوعية ودرجة الرضا خلال فترة الاستبيان"
              : "Weekly response volume and satisfaction score over the survey period"}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ChartContainer config={CHART_CONFIG} className="h-64 w-full">
            <ComposedChart
              data={stats.trend}
              margin={{ top: 8, right: 24, bottom: 0, left: 0 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" strokeOpacity={0.4} />
              <XAxis
                dataKey={isAr ? "weekAr" : "weekEn"}
                tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }}
                axisLine={false}
                tickLine={false}
              />
              <YAxis
                yAxisId="left"
                orientation="left"
                tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }}
                axisLine={false}
                tickLine={false}
              />
              <YAxis
                yAxisId="right"
                orientation="right"
                domain={[50, 100]}
                tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }}
                axisLine={false}
                tickLine={false}
                tickFormatter={(v) => `${v}%`}
              />
              <ChartTooltip content={<ChartTooltipContent />} />
              <ChartLegend content={<ChartLegendContent />} />
              <Bar
                yAxisId="left"
                dataKey="responses"
                fill="var(--color-chart-1)"
                radius={[4, 4, 0, 0]}
                opacity={0.85}
              />
              <Line
                yAxisId="right"
                dataKey="score"
                stroke="var(--color-chart-2)"
                strokeWidth={2.5}
                dot={{ r: 4, fill: "var(--color-chart-2)", strokeWidth: 0 }}
                activeDot={{ r: 6 }}
              />
            </ComposedChart>
          </ChartContainer>
        </CardContent>
      </Card>

      {/* ── Channel breakdown + Recent responses ─────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">

        {/* Channel Breakdown */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">
              {isAr ? "توزيع القنوات" : "By Channel"}
            </CardTitle>
            <CardDescription>
              {isAr ? "عدد الردود ومتوسط الرضا لكل قناة" : "Response count and avg satisfaction per channel"}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {stats.channels.map((ch) => {
              const Icon = CHANNEL_ICONS[ch.id] ?? Smartphone
              const barWidth = (ch.responses / maxChannelResponses) * 100
              const color = perfColor(ch.avgScore)
              return (
                <div key={ch.id} className="space-y-1">
                  <div className="flex items-center justify-between text-xs">
                    <div className="flex items-center gap-1.5">
                      <Icon className="size-3.5 text-muted-foreground" />
                      <span className="font-medium text-foreground">
                        {isAr ? ch.nameAr : ch.nameEn}
                      </span>
                    </div>
                    <div className="flex items-center gap-3 tabular-nums">
                      <span className="text-muted-foreground">
                        {ch.responses.toLocaleString("en-US")}
                        <span className="text-muted-foreground/60 ms-1">({ch.pct}%)</span>
                      </span>
                      <span className="font-bold w-10 text-end" style={{ color }}>
                        {ch.avgScore}%
                      </span>
                    </div>
                  </div>
                  <div className="h-2 rounded-full bg-muted/40 overflow-hidden">
                    <div
                      className="h-full rounded-full transition-all duration-700 motion-safe:duration-700"
                      style={{ width: `${barWidth}%`, backgroundColor: color }}
                    />
                  </div>
                </div>
              )
            })}
          </CardContent>
        </Card>

        {/* KPI score legend card */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">
              {isAr ? "ملخص الأداء" : "Performance Summary"}
            </CardTitle>
            <CardDescription>
              {isAr ? "نتائج جميع مؤشرات هذا الاستبيان" : "All KPI results for this survey"}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {stats.kpiIds.map((kpiId) => {
              // Derive display score from PREVIEW_DATA inside KpiDashboardPreview
              const DEMO_VALUES: Record<string, number> = {
                nps: 42, csat: 78, ces: 45, fcr: 68, agent: 84, vfm: 72, chs: 45,
              }
              const DEMO_MIN: Record<string, number> = { nps: -100 }
              const DEMO_MAX: Record<string, number> = { nps: 100 }
              const value = DEMO_VALUES[kpiId] ?? 70
              const min = DEMO_MIN[kpiId] ?? 0
              const max = DEMO_MAX[kpiId] ?? 100
              const pct = Math.round(((value - min) / (max - min)) * 100)
              const color = perfColor(value, kpiId)
              const label = isAr
                ? (KPI_LABELS[kpiId]?.shortAr ?? kpiId.toUpperCase())
                : (KPI_LABELS[kpiId]?.shortEn ?? kpiId.toUpperCase())
              const displayVal = min < 0 ? (value > 0 ? `+${value}` : `${value}`) : `${value}%`

              return (
                <div key={kpiId} className="flex items-center gap-3">
                  <div className="w-20 shrink-0">
                    <p className="text-xs font-semibold text-foreground">{label}</p>
                  </div>
                  <div className="flex-1 h-2 rounded-full bg-muted/40 overflow-hidden">
                    <div
                      className="h-full rounded-full"
                      style={{ width: `${pct}%`, backgroundColor: color }}
                    />
                  </div>
                  <p className="text-sm font-bold tabular-nums w-12 text-end" style={{ color }}>
                    {displayVal}
                  </p>
                </div>
              )
            })}

            <div className="pt-3 border-t border-border">
              <div className="flex gap-4 text-xs text-muted-foreground">
                {[
                  { color: D1, labelEn: "Excellent", labelAr: "ممتاز" },
                  { color: D2, labelEn: "Good",      labelAr: "جيد" },
                  { color: D3, labelEn: "Caution",   labelAr: "تنبيه" },
                  { color: D5, labelEn: "Critical",  labelAr: "حرج" },
                ].map((d) => (
                  <div key={d.color} className="flex items-center gap-1">
                    <div className="size-2 rounded-full shrink-0" style={{ backgroundColor: d.color }} />
                    <span>{isAr ? d.labelAr : d.labelEn}</span>
                  </div>
                ))}
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* ── Recent Responses ─────────────────────────────────── */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">
            {isAr ? "آخر الردود" : "Recent Responses"}
          </CardTitle>
          <CardDescription>
            {isAr ? "آخر الردود المستلمة على هذا الاستبيان" : "Latest responses received on this survey"}
          </CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="ps-6">
                  {isAr ? "التوقيت" : "Time"}
                </TableHead>
                <TableHead>{isAr ? "القناة" : "Channel"}</TableHead>
                {stats.kpiIds.map((kpiId) => (
                  <TableHead key={kpiId} className="text-center">
                    {isAr
                      ? (KPI_LABELS[kpiId]?.shortAr ?? kpiId.toUpperCase())
                      : (KPI_LABELS[kpiId]?.shortEn ?? kpiId.toUpperCase())}
                  </TableHead>
                ))}
                <TableHead>{isAr ? "الشعور" : "Sentiment"}</TableHead>
                <TableHead>{isAr ? "تعليق" : "Comment"}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {stats.recent.map((r) => {
                const ChannelIcon = CHANNEL_ICONS[r.channelEn.toLowerCase().replace(/\s/g, "")] ?? Smartphone
                const sentCfg = SENTIMENT_CONFIG[r.sentiment]
                const SentIcon = sentCfg.icon
                return (
                  <TableRow key={r.id} className="hover:bg-muted/50 transition-colors">
                    <TableCell className="ps-6 text-xs text-muted-foreground tabular-nums whitespace-nowrap">
                      {r.at}
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-1.5">
                        <ChannelIcon className="size-3.5 text-muted-foreground shrink-0" />
                        <span className="text-xs">
                          {isAr ? r.channelAr : r.channelEn}
                        </span>
                      </div>
                    </TableCell>
                    {stats.kpiIds.map((kpiId) => {
                      const score = r.scores[kpiId]
                      return (
                        <TableCell key={kpiId} className="text-center">
                          {score !== undefined ? (
                            <span
                              className="text-sm font-bold tabular-nums"
                              style={{ color: perfColor(
                                kpiId === "nps" ? ((score / 10) * 100 - 50) : // normalize NPS 0-10 to % equiv
                                kpiId === "fcr" ? (score === 1 ? 85 : 40) :  // boolean FCR
                                score * 20,                                   // 1-5 scale → 0-100
                                kpiId
                              )}}
                            >
                              {kpiId === "fcr" ? (score === 1 ? (isAr ? "✓ نعم" : "✓ Yes") : (isAr ? "✗ لا" : "✗ No")) : score}
                            </span>
                          ) : (
                            <span className="text-muted-foreground text-xs">—</span>
                          )}
                        </TableCell>
                      )
                    })}
                    <TableCell>
                      <div className="flex items-center gap-1.5">
                        <SentIcon className="size-3.5 shrink-0" style={{ color: sentCfg.color }} />
                        <span className="text-xs" style={{ color: sentCfg.color }}>
                          {isAr ? sentCfg.labelAr : sentCfg.labelEn}
                        </span>
                      </div>
                    </TableCell>
                    <TableCell className="max-w-[180px]">
                      {r.comment ? (
                        <p className="text-xs text-muted-foreground truncate" title={r.comment}>
                          {r.comment}
                        </p>
                      ) : (
                        <span className="text-muted-foreground/50 text-xs">—</span>
                      )}
                    </TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

    </div>
  )
}
