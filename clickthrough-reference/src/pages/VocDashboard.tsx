import { useState, useEffect, useRef } from "react"
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip"
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  ChartLegend,
  ChartLegendContent,
  type ChartConfig,
} from "@/components/ui/chart"
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  PieChart,
  Pie,
  Cell,
  Label,
} from "recharts"
import {
  TrendingUp,
  TrendingDown,
  MessageSquare,
  Star,
  Target,
  BarChart3,
  Plus,
  Download,
  Eye,
  Calendar,
  Mail,
  Globe,
  ClipboardList,
} from "lucide-react"
import { cn } from "@/lib/utils"

// ─── CSS Animations ────────────────────────────────────────

const animationStyles = `
  @keyframes voc-fade-in-up {
    from { opacity: 0; transform: translateY(24px); }
    to   { opacity: 1; transform: translateY(0); }
  }
  @keyframes voc-scale-in {
    from { opacity: 0; transform: scale(0.92); }
    to   { opacity: 1; transform: scale(1); }
  }
  @keyframes voc-shimmer {
    0%   { background-position: -200% 0; }
    100% { background-position: 200% 0; }
  }
  @keyframes voc-float {
    0%, 100% { transform: translateY(0); }
    50%      { transform: translateY(-10px); }
  }

  .voc-fade-in-up {
    opacity: 0;
    animation: voc-fade-in-up 0.6s cubic-bezier(0.16, 1, 0.3, 1) forwards;
  }
  .voc-scale-in {
    opacity: 0;
    animation: voc-scale-in 0.5s cubic-bezier(0.16, 1, 0.3, 1) forwards;
  }
  .voc-float {
    animation: voc-float 6s ease-in-out infinite;
  }
  .voc-shimmer {
    background: linear-gradient(90deg, transparent 0%, rgba(255,255,255,0.07) 50%, transparent 100%);
    background-size: 200% 100%;
    animation: voc-shimmer 3s ease-in-out infinite;
  }

  @media (prefers-reduced-motion: reduce) {
    .voc-fade-in-up,
    .voc-scale-in {
      opacity: 1;
      animation: none;
    }
    .voc-float,
    .voc-shimmer {
      animation: none;
    }
  }
`

// ─── Helpers ───────────────────────────────────────────────

const ARABIC_MONTHS = [
  "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
  "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر",
]
const ARABIC_DAYS = [
  "الأحد", "الإثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت",
]

function getArabicDate() {
  const d = new Date()
  return `${ARABIC_DAYS[d.getDay()]}، ${d.getDate()} ${ARABIC_MONTHS[d.getMonth()]} ${d.getFullYear()}`
}

// ─── Animated Counter Hook ─────────────────────────────────

function useCountUp(target: number, duration = 2000, decimals = 0): number {
  const [count, setCount] = useState(0)
  const startTimeRef = useRef<number | null>(null)
  const rafRef = useRef(0)

  useEffect(() => {
    startTimeRef.current = null

    const animate = (timestamp: number) => {
      if (startTimeRef.current === null) startTimeRef.current = timestamp
      const progress = Math.min((timestamp - startTimeRef.current) / duration, 1)
      // Ease-out-quart for smooth deceleration
      const eased = 1 - Math.pow(1 - progress, 4)
      setCount(Number((eased * target).toFixed(decimals)))
      if (progress < 1) rafRef.current = requestAnimationFrame(animate)
    }

    rafRef.current = requestAnimationFrame(animate)
    return () => cancelAnimationFrame(rafRef.current)
  }, [target, duration, decimals])

  return count
}

// ─── Types ─────────────────────────────────────────────────

interface KpiItem {
  id: string
  title: string
  value: number
  decimals: number
  suffix: string
  trend: number
  trendUp: boolean
  icon: React.ComponentType<{ className?: string }>
  gradient: string
  iconBg: string
  tooltip: string
}

interface FeedbackRow {
  id: number
  customer: string
  feedback: string
  sentiment: "positive" | "neutral" | "negative"
  source: "survey" | "email" | "social"
  date: string
}

// ─── Mock Data ─────────────────────────────────────────────

const kpiData: KpiItem[] = [
  {
    id: "responses",
    title: "إجمالي الاستجابات",
    value: 12847,
    decimals: 0,
    suffix: "",
    trend: 12.5,
    trendUp: true,
    icon: MessageSquare,
    gradient: "from-nb-cyan to-nb-cyan-300",
    iconBg: "bg-nb-cyan-100 text-nb-cyan-700 dark:bg-nb-cyan-700/30 dark:text-nb-cyan-300",
    tooltip: "استبيانات: 8,234 | بريد: 3,102 | اجتماعي: 1,511",
  },
  {
    id: "satisfaction",
    title: "متوسط الرضا",
    value: 4.2,
    decimals: 1,
    suffix: "/5",
    trend: 3.1,
    trendUp: true,
    icon: Star,
    gradient: "from-d3 to-d3-light",
    iconBg: "bg-d3-light text-d3-dark dark:bg-d3-dark/30 dark:text-d3-light",
    tooltip: "ممتاز: 45% | جيد: 30% | متوسط: 15% | ضعيف: 10%",
  },
  {
    id: "nps",
    title: "مؤشر NPS",
    value: 72,
    decimals: 0,
    suffix: "",
    trend: 5.3,
    trendUp: true,
    icon: Target,
    gradient: "from-nb-mint to-nb-mint-300",
    iconBg: "bg-nb-mint-100 text-nb-mint-700 dark:bg-nb-mint-700/30 dark:text-nb-mint-300",
    tooltip: "مروجون: 58% | محايدون: 28% | منتقدون: 14%",
  },
  {
    id: "rate",
    title: "معدل الاستجابة",
    value: 68,
    decimals: 0,
    suffix: "%",
    trend: 2.1,
    trendUp: false,
    icon: BarChart3,
    gradient: "from-nb-cyan to-nb-cyan-300",
    iconBg: "bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-800/30 dark:text-nb-cyan-300",
    tooltip: "هذا الشهر: 68% | الشهر الماضي: 70.1%",
  },
]

const sentimentData = [
  { month: "يناير", positive: 65, neutral: 25, negative: 10 },
  { month: "فبراير", positive: 68, neutral: 22, negative: 10 },
  { month: "مارس", positive: 72, neutral: 20, negative: 8 },
  { month: "أبريل", positive: 70, neutral: 21, negative: 9 },
  { month: "مايو", positive: 75, neutral: 18, negative: 7 },
  { month: "يونيو", positive: 78, neutral: 16, negative: 6 },
]

const categoryData = [
  { category: "product", value: 35 },
  { category: "service", value: 25 },
  { category: "pricing", value: 20 },
  { category: "usability", value: 12 },
  { category: "other", value: 8 },
]

const feedbackData: FeedbackRow[] = [
  { id: 1, customer: "أحمد محمد", feedback: "الخدمة ممتازة والفريق متعاون جداً في حل المشاكل", sentiment: "positive", source: "survey", date: "10 مايو 2026" },
  { id: 2, customer: "سارة أحمد", feedback: "أسعار المنتجات مرتفعة مقارنة بالمنافسين", sentiment: "negative", source: "email", date: "10 مايو 2026" },
  { id: 3, customer: "محمد علي", feedback: "تجربة الشراء كانت سلسة بشكل عام", sentiment: "neutral", source: "social", date: "9 مايو 2026" },
  { id: 4, customer: "فاطمة حسن", feedback: "التطبيق سريع وسهل الاستخدام، أنصح به بشدة", sentiment: "positive", source: "survey", date: "9 مايو 2026" },
  { id: 5, customer: "عمر خالد", feedback: "خدمة التوصيل متأخرة دائماً ولا يوجد تتبع دقيق", sentiment: "negative", source: "social", date: "8 مايو 2026" },
  { id: 6, customer: "نور عبدالله", feedback: "المنتج مطابق للوصف تماماً وبجودة ممتازة", sentiment: "positive", source: "email", date: "8 مايو 2026" },
  { id: 7, customer: "ريم سعيد", feedback: "الموقع يحتاج تحسين في سرعة التحميل", sentiment: "neutral", source: "survey", date: "7 مايو 2026" },
  { id: 8, customer: "يوسف إبراهيم", feedback: "فريق الدعم الفني محترف ومتجاوب مع الاستفسارات", sentiment: "positive", source: "email", date: "7 مايو 2026" },
  { id: 9, customer: "لينا وليد", feedback: "لا توجد خيارات دفع كافية وهذا أمر مزعج", sentiment: "negative", source: "survey", date: "6 مايو 2026" },
  { id: 10, customer: "كريم ياسر", feedback: "تجربة مستخدم ممتازة في التطبيق الجديد", sentiment: "positive", source: "social", date: "6 مايو 2026" },
]

const quickActions = [
  {
    id: "create",
    title: "إنشاء استبيان",
    description: "أنشئ استبياناً جديداً لجمع الآراء",
    icon: Plus,
    gradient: "from-nb-cyan to-nb-mint",
  },
  {
    id: "export",
    title: "تصدير التقارير",
    description: "حمّل تقارير بتنسيق PDF أو Excel",
    icon: Download,
    gradient: "from-nb-mint to-nb-cyan",
  },
  {
    id: "view",
    title: "عرض الملاحظات",
    description: "تصفّح جميع ملاحظات العملاء",
    icon: Eye,
    gradient: "from-nb-cyan to-d3",
  },
]

// ─── Chart Configs ─────────────────────────────────────────

const sentimentChartConfig = {
  positive: { label: "إيجابي", color: "var(--chart-1)" },
  neutral: { label: "محايد", color: "var(--chart-4)" },
  negative: { label: "سلبي", color: "var(--chart-5)" },
} satisfies ChartConfig

const categoryChartConfig = {
  product: { label: "جودة المنتج", color: "var(--chart-1)" },
  service: { label: "خدمة العملاء", color: "var(--chart-2)" },
  pricing: { label: "الأسعار", color: "var(--chart-3)" },
  usability: { label: "سهولة الاستخدام", color: "var(--chart-4)" },
  other: { label: "أخرى", color: "var(--chart-5)" },
} satisfies ChartConfig

// ─── Sentiment & Source Display Configs ────────────────────

const sentimentDisplay: Record<
  FeedbackRow["sentiment"],
  { label: string; className: string }
> = {
  positive: {
    label: "إيجابي",
    className:
      "bg-nb-cyan-100 text-nb-cyan-700 border-nb-cyan/20 dark:bg-nb-cyan-700/20 dark:text-nb-cyan-300 dark:border-nb-cyan-300/20",
  },
  neutral: {
    label: "محايد",
    className:
      "bg-d3-light text-d3-dark border-d3/20 dark:bg-d3-dark/20 dark:text-d3-light dark:border-d3-light/20",
  },
  negative: {
    label: "سلبي",
    className:
      "bg-d5-light text-d5-dark border-d5/20 dark:bg-d5-dark/20 dark:text-d5-light dark:border-d5-light/20",
  },
}

const sourceDisplay: Record<
  FeedbackRow["source"],
  { label: string; icon: React.ComponentType<{ className?: string }>; className: string }
> = {
  survey: {
    label: "استبيان",
    icon: ClipboardList,
    className:
      "bg-nb-mint-100 text-nb-mint-700 border-nb-mint/20 dark:bg-nb-mint-700/20 dark:text-nb-mint-300 dark:border-nb-mint-300/20",
  },
  email: {
    label: "بريد إلكتروني",
    icon: Mail,
    className:
      "bg-nb-cyan-100 text-nb-cyan-800 border-nb-cyan/20 dark:bg-nb-cyan-800/20 dark:text-nb-cyan-300 dark:border-nb-cyan-300/20",
  },
  social: {
    label: "وسائل التواصل",
    icon: Globe,
    className:
      "bg-d3-light text-d3-dark border-d3/20 dark:bg-d3-dark/20 dark:text-d3-light dark:border-d3-light/20",
  },
}

// ─── KPI Card Component ────────────────────────────────────

function KpiCard({ kpi, delay }: { kpi: KpiItem; delay: number }) {
  const count = useCountUp(kpi.value, 2000, kpi.decimals)
  const formatted =
    kpi.decimals > 0
      ? count.toFixed(kpi.decimals)
      : count.toLocaleString("en-US")
  const Icon = kpi.icon

  return (
    <Tooltip>
      <TooltipTrigger render={<div />} className="rounded-xl">
        <Card
          className={cn(
            "relative overflow-hidden voc-fade-in-up",
            "motion-safe:transition-all motion-safe:duration-300",
            "hover:-translate-y-1 hover:shadow-lg",
          )}
          style={{ animationDelay: `${delay}s` }}
        >
          {/* Gradient top accent bar */}
          <div
            className={cn(
              "absolute inset-x-0 top-0 h-1 bg-gradient-to-r",
              kpi.gradient,
            )}
            aria-hidden="true"
          />

          <CardContent className="pt-5">
            <div className="flex items-start justify-between">
              <div className="space-y-1.5">
                <p className="text-sm text-muted-foreground">{kpi.title}</p>
                <p className="text-3xl font-heading font-bold tabular-nums">
                  {formatted}
                  {kpi.suffix && (
                    <span className="text-lg text-muted-foreground ms-0.5">
                      {kpi.suffix}
                    </span>
                  )}
                </p>
                <div
                  className={cn(
                    "flex items-center gap-1 text-xs font-medium",
                    kpi.trendUp
                      ? "text-d2 dark:text-d2"
                      : "text-d5 dark:text-d5",
                  )}
                >
                  {kpi.trendUp ? (
                    <TrendingUp className="size-3.5" />
                  ) : (
                    <TrendingDown className="size-3.5" />
                  )}
                  <span dir="ltr" className="inline-block tabular-nums">
                    {kpi.trendUp ? "+" : "-"}
                    {kpi.trend}%
                  </span>
                  <span className="text-muted-foreground">من الشهر الماضي</span>
                </div>
              </div>

              <div
                className={cn(
                  "flex size-11 shrink-0 items-center justify-center rounded-xl",
                  kpi.iconBg,
                )}
              >
                <Icon className="size-5" />
              </div>
            </div>
          </CardContent>
        </Card>
      </TooltipTrigger>
      <TooltipContent side="bottom">
        <p>{kpi.tooltip}</p>
      </TooltipContent>
    </Tooltip>
  )
}

// ─── Main Dashboard ────────────────────────────────────────

export default function VocDashboard() {
  const totalCategories = categoryData.reduce((s, i) => s + i.value, 0)

  return (
    <TooltipProvider>
      <style>{animationStyles}</style>

      <div className="space-y-8 py-6 max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* ── Welcome Banner ──────────────────────── */}
        <section
          className="voc-fade-in-up relative overflow-hidden rounded-2xl bg-gradient-to-br from-nb-cyan via-nb-mint to-nb-cyan p-8 sm:p-10 text-white"
          aria-label="ملخص اليوم"
        >
          {/* Decorative floating orbs */}
          <div className="absolute inset-0 voc-shimmer pointer-events-none" />
          <div
            className="absolute -top-8 -end-8 size-44 rounded-full bg-white/10 voc-float"
            aria-hidden="true"
          />
          <div
            className="absolute bottom-4 start-1/4 size-24 rounded-full bg-white/5 voc-float"
            style={{ animationDelay: "2s" }}
            aria-hidden="true"
          />
          <div
            className="absolute top-1/3 end-1/3 size-14 rounded-full bg-white/10 voc-float"
            style={{ animationDelay: "4s" }}
            aria-hidden="true"
          />

          <div className="relative z-10 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h1 className="text-2xl font-heading font-bold">
                مرحباً، أحمد
              </h1>
              <p className="text-white/75 text-sm mt-1.5 leading-relaxed">
                {getArabicDate()} — إليك ملخّص أداء صوت العميل اليوم
              </p>
            </div>

            <div className="flex items-center gap-3">
              <div className="rounded-xl bg-white/15 backdrop-blur-sm px-5 py-3 text-center">
                <p className="text-2xl font-heading font-bold tabular-nums">
                  847
                </p>
                <p className="text-white/70 text-xs mt-0.5">استجابة اليوم</p>
              </div>
              <div className="rounded-xl bg-white/15 backdrop-blur-sm px-5 py-3 text-center">
                <p className="text-2xl font-heading font-bold tabular-nums">
                  92%
                </p>
                <p className="text-white/70 text-xs mt-0.5">إيجابية</p>
              </div>
            </div>
          </div>

          {/* Wave edge */}
          <svg
            className="absolute -bottom-px inset-x-0 w-full"
            height="32"
            preserveAspectRatio="none"
            viewBox="0 0 1440 32"
            aria-hidden="true"
          >
            <path
              d="M0,16 C360,32 720,0 1080,16 C1260,24 1380,20 1440,16 L1440,32 L0,32 Z"
              className="fill-background"
            />
          </svg>
        </section>

        {/* ── Filter Bar ──────────────────────────── */}
        <div
          className="voc-fade-in-up flex items-center justify-between"
          style={{ animationDelay: "0.12s" }}
        >
          <h2 className="text-lg font-bold">نظرة عامة</h2>
          <Select defaultValue="30">
            <SelectTrigger className="w-44">
              <Calendar className="size-4 text-muted-foreground" />
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="7">آخر 7 أيام</SelectItem>
              <SelectItem value="30">آخر 30 يوم</SelectItem>
              <SelectItem value="90">آخر 90 يوم</SelectItem>
              <SelectItem value="365">آخر سنة</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {/* ── KPI Cards ───────────────────────────── */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {kpiData.map((kpi, i) => (
            <KpiCard key={kpi.id} kpi={kpi} delay={i * 0.1 + 0.2} />
          ))}
        </div>

        {/* ── Charts Row ──────────────────────────── */}
        <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
          {/* Sentiment Trend — Area */}
          <Card
            className="lg:col-span-3 voc-fade-in-up overflow-hidden"
            style={{ animationDelay: "0.6s" }}
          >
            <CardHeader>
              <CardTitle>اتجاه المشاعر</CardTitle>
              <CardDescription>
                تحليل مشاعر العملاء خلال الأشهر الستة الماضية
              </CardDescription>
            </CardHeader>
            <CardContent>
              <ChartContainer
                config={sentimentChartConfig}
                className="h-72 w-full"
              >
                <AreaChart
                  data={sentimentData}
                  margin={{ top: 8, right: 8, left: 0, bottom: 0 }}
                >
                  <defs>
                    <linearGradient
                      id="fillPositive"
                      x1="0"
                      y1="0"
                      x2="0"
                      y2="1"
                    >
                      <stop
                        offset="0%"
                        stopColor="var(--color-positive)"
                        stopOpacity={0.35}
                      />
                      <stop
                        offset="100%"
                        stopColor="var(--color-positive)"
                        stopOpacity={0.05}
                      />
                    </linearGradient>
                    <linearGradient
                      id="fillNeutral"
                      x1="0"
                      y1="0"
                      x2="0"
                      y2="1"
                    >
                      <stop
                        offset="0%"
                        stopColor="var(--color-neutral)"
                        stopOpacity={0.35}
                      />
                      <stop
                        offset="100%"
                        stopColor="var(--color-neutral)"
                        stopOpacity={0.05}
                      />
                    </linearGradient>
                    <linearGradient
                      id="fillNegative"
                      x1="0"
                      y1="0"
                      x2="0"
                      y2="1"
                    >
                      <stop
                        offset="0%"
                        stopColor="var(--color-negative)"
                        stopOpacity={0.35}
                      />
                      <stop
                        offset="100%"
                        stopColor="var(--color-negative)"
                        stopOpacity={0.05}
                      />
                    </linearGradient>
                  </defs>
                  <CartesianGrid
                    strokeDasharray="3 3"
                    className="stroke-border"
                  />
                  <XAxis
                    dataKey="month"
                    tickLine={false}
                    axisLine={false}
                    className="text-xs"
                  />
                  <YAxis
                    tickLine={false}
                    axisLine={false}
                    className="text-xs"
                  />
                  <ChartTooltip
                    content={<ChartTooltipContent indicator="dot" />}
                  />
                  <ChartLegend content={<ChartLegendContent />} />
                  <Area
                    type="monotone"
                    dataKey="positive"
                    stroke="var(--color-positive)"
                    fill="url(#fillPositive)"
                    strokeWidth={2.5}
                  />
                  <Area
                    type="monotone"
                    dataKey="neutral"
                    stroke="var(--color-neutral)"
                    fill="url(#fillNeutral)"
                    strokeWidth={2.5}
                  />
                  <Area
                    type="monotone"
                    dataKey="negative"
                    stroke="var(--color-negative)"
                    fill="url(#fillNegative)"
                    strokeWidth={2.5}
                  />
                </AreaChart>
              </ChartContainer>
            </CardContent>
          </Card>

          {/* Category Breakdown — Donut */}
          <Card
            className="lg:col-span-2 voc-fade-in-up overflow-hidden"
            style={{ animationDelay: "0.7s" }}
          >
            <CardHeader>
              <CardTitle>التصنيفات</CardTitle>
              <CardDescription>
                توزيع الملاحظات حسب الفئة
              </CardDescription>
            </CardHeader>
            <CardContent>
              <ChartContainer
                config={categoryChartConfig}
                className="h-72 w-full"
              >
                <PieChart>
                  <ChartTooltip
                    content={
                      <ChartTooltipContent nameKey="category" hideLabel />
                    }
                  />
                  <Pie
                    data={categoryData}
                    dataKey="value"
                    nameKey="category"
                    cx="50%"
                    cy="50%"
                    innerRadius={55}
                    outerRadius={90}
                    paddingAngle={3}
                    strokeWidth={2}
                    stroke="var(--background)"
                  >
                    {categoryData.map((entry) => (
                      <Cell
                        key={entry.category}
                        fill={`var(--color-${entry.category})`}
                      />
                    ))}
                    <Label
                      content={({ viewBox }) => {
                        if (viewBox && "cx" in viewBox && "cy" in viewBox) {
                          return (
                            <text
                              x={viewBox.cx}
                              y={viewBox.cy}
                              textAnchor="middle"
                              dominantBaseline="middle"
                            >
                              <tspan
                                x={viewBox.cx}
                                y={viewBox.cy}
                                className="fill-foreground text-3xl font-heading font-bold"
                              >
                                {totalCategories.toLocaleString("en-US")}
                              </tspan>
                              <tspan
                                x={viewBox.cx}
                                y={(viewBox.cy || 0) + 22}
                                className="fill-muted-foreground text-xs"
                              >
                                ملاحظة
                              </tspan>
                            </text>
                          )
                        }
                      }}
                    />
                  </Pie>
                  <ChartLegend
                    content={<ChartLegendContent nameKey="category" />}
                  />
                </PieChart>
              </ChartContainer>
            </CardContent>
          </Card>
        </div>

        {/* ── Table + Quick Actions ───────────────── */}
        <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
          {/* Recent Feedback Table */}
          <Card
            className="lg:col-span-3 voc-fade-in-up overflow-hidden"
            style={{ animationDelay: "0.8s" }}
          >
            <CardHeader>
              <CardTitle>آخر الملاحظات</CardTitle>
              <CardDescription>
                أحدث 10 ملاحظات مستلمة من العملاء
              </CardDescription>
            </CardHeader>
            <CardContent className="px-0">
              <Table>
                <TableHeader>
                  <TableRow className="hover:bg-transparent">
                    <TableHead className="text-start ps-6">العميل</TableHead>
                    <TableHead className="text-start">الملاحظة</TableHead>
                    <TableHead className="text-center">المشاعر</TableHead>
                    <TableHead className="text-center">المصدر</TableHead>
                    <TableHead className="text-end pe-6">التاريخ</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {feedbackData.map((row) => {
                    const sent = sentimentDisplay[row.sentiment]
                    const src = sourceDisplay[row.source]
                    const SrcIcon = src.icon
                    return (
                      <TableRow
                        key={row.id}
                        className="cursor-pointer hover:bg-muted/50 motion-safe:transition-colors"
                      >
                        <TableCell className="ps-6 font-medium">
                          {row.customer}
                        </TableCell>
                        <TableCell className="max-w-xs truncate text-muted-foreground">
                          {row.feedback}
                        </TableCell>
                        <TableCell className="text-center">
                          <Badge
                            variant="outline"
                            className={cn("text-xs", sent.className)}
                          >
                            {sent.label}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-center">
                          <Badge
                            variant="outline"
                            className={cn("text-xs gap-1", src.className)}
                          >
                            <SrcIcon className="size-3" />
                            {src.label}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-end pe-6 text-muted-foreground tabular-nums whitespace-nowrap">
                          {row.date}
                        </TableCell>
                      </TableRow>
                    )
                  })}
                </TableBody>
              </Table>
            </CardContent>
          </Card>

          {/* Quick Actions Sidebar */}
          <div className="space-y-4">
            <h3
              className="text-lg font-bold voc-fade-in-up"
              style={{ animationDelay: "0.8s" }}
            >
              إجراءات سريعة
            </h3>

            {quickActions.map((action, i) => {
              const ActionIcon = action.icon
              return (
                <button
                  key={action.id}
                  className={cn(
                    "group w-full rounded-xl p-4 text-start",
                    "bg-gradient-to-br text-white",
                    action.gradient,
                    "motion-safe:transition-all motion-safe:duration-300",
                    "hover:-translate-y-0.5 hover:shadow-lg hover:brightness-110",
                    "focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none",
                    "voc-scale-in",
                  )}
                  style={{ animationDelay: `${0.9 + i * 0.1}s` }}
                >
                  <div className="flex items-center gap-3">
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-white/20 backdrop-blur-sm motion-safe:transition-transform motion-safe:duration-300 group-hover:scale-110">
                      <ActionIcon className="size-5" />
                    </div>
                    <div>
                      <p className="font-bold text-sm">{action.title}</p>
                      <p className="text-xs text-white/70 mt-0.5">
                        {action.description}
                      </p>
                    </div>
                  </div>
                </button>
              )
            })}
          </div>
        </div>
      </div>
    </TooltipProvider>
  )
}
