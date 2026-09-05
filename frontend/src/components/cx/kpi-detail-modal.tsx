import { useTranslation } from "react-i18next"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Card, CardContent } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart"
import { AreaChart, Area, XAxis, YAxis, CartesianGrid } from "recharts"
import { ChevronRight, Sparkles, ThumbsUp, ThumbsDown } from "lucide-react"
import { perfColor } from "./kpi-flip-card"

// ─── KPI Detail Data ──────────────────────────────────────

interface KpiDetailEntry {
  title: string
  titleAr: string
  value: string
  color: string
  insightKey: string
  distribution: { label: string; labelAr: string; value: number; color: string }[]
  trend: number[]
  segments: { name: string; nameAr: string; value: number }[]
  topDriverPos: { en: string; ar: string }
  topDriverNeg: { en: string; ar: string }
}

const KPI_DETAIL_DATA: Record<string, KpiDetailEntry> = {
  nps: {
    title: "Net Promoter Score", titleAr: "مؤشر صافي الترويج", value: "+42", color: "var(--chart-1)", insightKey: "cx.npsInsight",
    distribution: [
      { label: "Promoters (9-10)", labelAr: "المروجون (9-10)", value: 51, color: "#1A7A3C" },
      { label: "Passives (7-8)", labelAr: "المحايدون (7-8)", value: 40, color: "#E8A020" },
      { label: "Detractors (0-6)", labelAr: "المنتقدون (0-6)", value: 9, color: "#C01B2A" },
    ],
    trend: [38, 36, 39, 37, 40, 38, 41, 39, 40, 41, 42, 42],
    segments: [
      { name: "Riyadh North", nameAr: "الرياض الشمالية", value: 52 },
      { name: "Madinah", nameAr: "المدينة المنورة", value: 47 },
      { name: "Jeddah", nameAr: "جدة", value: 44 },
      { name: "Riyadh South", nameAr: "الرياض الجنوبية", value: 38 },
      { name: "Makkah", nameAr: "مكة المكرمة", value: 35 },
      { name: "Dammam", nameAr: "الدمام", value: 29 },
    ],
    topDriverPos: { en: "Staff professionalism & knowledge", ar: "احترافية الموظفين ومعرفتهم" },
    topDriverNeg: { en: "Long wait time at Issue Resolution", ar: "طول وقت الانتظار عند حل المشكلات" },
  },
  csat: {
    title: "Customer Satisfaction", titleAr: "رضا العملاء", value: "78%", color: "var(--chart-2)", insightKey: "cx.csatInsight",
    distribution: [
      { label: "Very Satisfied (5)", labelAr: "راضٍ جدًا (5)", value: 42, color: "#1A7A3C" },
      { label: "Satisfied (4)", labelAr: "راضٍ (4)", value: 36, color: "#2EB85C" },
      { label: "Neutral (3)", labelAr: "محايد (3)", value: 14, color: "#E8A020" },
      { label: "Dissatisfied (1-2)", labelAr: "غير راضٍ (1-2)", value: 8, color: "#C01B2A" },
    ],
    trend: [75, 74, 76, 75, 77, 76, 78, 77, 76, 77, 78, 78],
    segments: [
      { name: "Riyadh North", nameAr: "الرياض الشمالية", value: 85 },
      { name: "Madinah", nameAr: "المدينة المنورة", value: 82 },
      { name: "Jeddah", nameAr: "جدة", value: 79 },
      { name: "Riyadh South", nameAr: "الرياض الجنوبية", value: 74 },
      { name: "Makkah", nameAr: "مكة المكرمة", value: 71 },
      { name: "Dammam", nameAr: "الدمام", value: 68 },
    ],
    topDriverPos: { en: "Mobile app experience", ar: "تجربة تطبيق الجوال" },
    topDriverNeg: { en: "Issue resolution turnaround", ar: "سرعة حل المشكلات" },
  },
  ces: {
    title: "Customer Effort Score", titleAr: "مؤشر جهد العميل", value: "45%", color: "var(--chart-4)", insightKey: "cx.npsInsight",
    distribution: [
      { label: "Low Effort (1-2)", labelAr: "جهد منخفض (1-2)", value: 45, color: "#1A7A3C" },
      { label: "Medium Effort (3)", labelAr: "جهد متوسط (3)", value: 33, color: "#E8A020" },
      { label: "High Effort (4-5)", labelAr: "جهد عالي (4-5)", value: 22, color: "#C01B2A" },
    ],
    trend: [48, 50, 47, 49, 46, 47, 45, 46, 47, 46, 45, 45],
    segments: [
      { name: "Riyadh North", nameAr: "الرياض الشمالية", value: 38 },
      { name: "Madinah", nameAr: "المدينة المنورة", value: 42 },
      { name: "Jeddah", nameAr: "جدة", value: 44 },
      { name: "Riyadh South", nameAr: "الرياض الجنوبية", value: 48 },
      { name: "Makkah", nameAr: "مكة المكرمة", value: 50 },
      { name: "Dammam", nameAr: "الدمام", value: 52 },
    ],
    topDriverPos: { en: "Self-service digital channels", ar: "قنوات الخدمة الذاتية الرقمية" },
    topDriverNeg: { en: "Complex documentation requirements", ar: "تعقيد متطلبات المستندات" },
  },
  agent: {
    title: "Agent Performance", titleAr: "أداء الموظفين", value: "84%", color: "var(--chart-3)", insightKey: "cx.csatInsight",
    distribution: [
      { label: "Excellent (90-100)", labelAr: "ممتاز (90-100)", value: 34, color: "#1A7A3C" },
      { label: "Good (70-89)", labelAr: "جيد (70-89)", value: 42, color: "#2EB85C" },
      { label: "Average (50-69)", labelAr: "متوسط (50-69)", value: 18, color: "#E8A020" },
      { label: "Poor (<50)", labelAr: "ضعيف (<50)", value: 6, color: "#C01B2A" },
    ],
    trend: [80, 79, 81, 80, 82, 81, 83, 82, 82, 83, 83, 84],
    segments: [
      { name: "Riyadh North", nameAr: "الرياض الشمالية", value: 90 },
      { name: "Madinah", nameAr: "المدينة المنورة", value: 86 },
      { name: "Jeddah", nameAr: "جدة", value: 84 },
      { name: "Riyadh South", nameAr: "الرياض الجنوبية", value: 70 },
      { name: "Makkah", nameAr: "مكة المكرمة", value: 75 },
      { name: "Dammam", nameAr: "الدمام", value: 78 },
    ],
    topDriverPos: { en: "Product knowledge depth", ar: "عمق المعرفة بالمنتجات" },
    topDriverNeg: { en: "Slow escalation handling", ar: "بطء معالجة التصعيدات" },
  },
  vfm: {
    title: "Value for Money", titleAr: "القيمة مقابل المال", value: "72%", color: "var(--chart-5)", insightKey: "cx.npsInsight",
    distribution: [
      { label: "High Value (5)", labelAr: "قيمة عالية (5)", value: 28, color: "#1A7A3C" },
      { label: "Fair Value (4)", labelAr: "قيمة مناسبة (4)", value: 44, color: "#2EB85C" },
      { label: "Low Value (1-3)", labelAr: "قيمة منخفضة (1-3)", value: 28, color: "#C01B2A" },
    ],
    trend: [70, 69, 71, 70, 72, 71, 73, 72, 71, 72, 72, 72],
    segments: [
      { name: "Riyadh North", nameAr: "الرياض الشمالية", value: 78 },
      { name: "Madinah", nameAr: "المدينة المنورة", value: 75 },
      { name: "Jeddah", nameAr: "جدة", value: 73 },
      { name: "Riyadh South", nameAr: "الرياض الجنوبية", value: 69 },
      { name: "Makkah", nameAr: "مكة المكرمة", value: 67 },
      { name: "Dammam", nameAr: "الدمام", value: 64 },
    ],
    topDriverPos: { en: "Bundled service offerings", ar: "عروض الخدمات المجمعة" },
    topDriverNeg: { en: "Fee transparency concerns", ar: "مخاوف بشأن شفافية الرسوم" },
  },
  fcr: {
    title: "First Contact Resolution", titleAr: "الحل من أول تواصل", value: "68%", color: "var(--chart-5)", insightKey: "cx.csatInsight",
    distribution: [
      { label: "Resolved 1st Contact", labelAr: "تم الحل من أول تواصل", value: 68, color: "#1A7A3C" },
      { label: "2nd Contact", labelAr: "تواصل ثانٍ", value: 20, color: "#E8A020" },
      { label: "3+ Contacts", labelAr: "3 تواصلات فأكثر", value: 12, color: "#C01B2A" },
    ],
    trend: [65, 64, 66, 63, 67, 65, 68, 66, 67, 67, 68, 68],
    segments: [
      { name: "Riyadh North", nameAr: "الرياض الشمالية", value: 75 },
      { name: "Madinah", nameAr: "المدينة المنورة", value: 72 },
      { name: "Jeddah", nameAr: "جدة", value: 69 },
      { name: "Riyadh South", nameAr: "الرياض الجنوبية", value: 64 },
      { name: "Makkah", nameAr: "مكة المكرمة", value: 62 },
      { name: "Dammam", nameAr: "الدمام", value: 58 },
    ],
    topDriverPos: { en: "Agent training programs", ar: "برامج تدريب الموظفين" },
    topDriverNeg: { en: "System integration gaps", ar: "فجوات تكامل الأنظمة" },
  },
}

// ─── Component ───────────────────────────────────────────

function KpiDetailModal({ kpiKey, onClose }: { kpiKey: string; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const isArabic = i18n.language === "ar"

  const data = KPI_DETAIL_DATA[kpiKey]
  if (!data) return null

  const displayTitle = isArabic ? data.titleAr : data.title
  const weekLabel = t("cx.week")
  const trendData = data.trend.map((val, i) => ({ week: `${weekLabel}${i + 1}`, value: val }))

  const trendChartConfig: ChartConfig = {
    value: { label: displayTitle, color: data.color },
  }

  const maxSegmentValue = Math.max(...data.segments.map((s) => s.value))

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-3xl max-h-[90vh] overflow-y-auto p-0" showCloseButton={false}>
        {/* ── 1. Hero Header ───────────────────── */}
        <div className="rounded-t-xl bg-accent dark:bg-muted px-6 py-5 border-b border-border">
          <div className="flex items-start justify-between gap-4">
            <div className="flex items-start gap-3">
              <Button
                variant="ghost"
                size="icon-sm"
                className="shrink-0 mt-1"
                aria-label={t("common.back")}
                onClick={onClose}
              >
                <ChevronRight className="size-5" />
              </Button>
              <div>
                <div className="flex items-center gap-2 mb-1.5">
                  <span className="size-2 rounded-full" style={{ backgroundColor: data.color }} />
                  <span className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">
                    {isArabic ? "تقرير مفصّل" : "Detailed KPI Report"}
                  </span>
                </div>
                <h2 className="text-xl font-heading font-bold">{displayTitle}</h2>
                <p className="text-xs text-muted-foreground mt-0.5">
                  {isArabic ? "استبيان ما بعد المعاملة" : "Post-Transaction Survey"}
                </p>
              </div>
            </div>
            <div className="text-end shrink-0">
              <span className="text-3xl font-heading font-bold tabular-nums" style={{ color: data.color }}>
                {data.value}
              </span>
              <p className="text-[10px] text-muted-foreground mt-1">
                {isArabic ? "الفترة الحالية" : "Current period"}
              </p>
            </div>
          </div>
        </div>

        <div className="space-y-6 px-6 pb-6 pt-4">
          <DialogHeader className="sr-only">
            <DialogTitle>{displayTitle}</DialogTitle>
          </DialogHeader>

          {/* ── 2. AI Insight + Trend Chart (side by side) ── */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            {/* AI Insight */}
            <Card className="border-s-4 border-s-d3 bg-d3-light/50 dark:bg-d3-dark/10">
              <CardContent className="flex gap-3 items-start">
                <div className="flex size-10 shrink-0 items-center justify-center rounded-full bg-d3-light dark:bg-d3-dark/20">
                  <Sparkles className="size-5 text-d3" />
                </div>
                <div>
                  <p className="text-xs font-bold text-d3-dark dark:text-d3-light mb-1.5">
                    {t("cx.detailInsightLabel")}
                  </p>
                  <p className="text-sm leading-relaxed">{t(data.insightKey)}</p>
                </div>
              </CardContent>
            </Card>

            {/* Trend Chart */}
            <Card>
              <CardContent>
                <h3 className="text-sm font-bold mb-2">{t("cx.detailTrend")}</h3>
                <ChartContainer config={trendChartConfig} className="h-40 w-full">
                  <AreaChart data={trendData} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
                    <defs>
                      <linearGradient id="detailTrendFill" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="var(--color-value)" stopOpacity={0.3} />
                        <stop offset="100%" stopColor="var(--color-value)" stopOpacity={0.05} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
                    <XAxis dataKey="week" tickLine={false} axisLine={false} className="text-xs" />
                    <YAxis tickLine={false} axisLine={false} className="text-xs" domain={["dataMin - 5", "dataMax + 5"]} />
                    <ChartTooltip content={<ChartTooltipContent />} />
                    <Area
                      type="monotone"
                      dataKey="value"
                      stroke="var(--color-value)"
                      fill="url(#detailTrendFill)"
                      strokeWidth={2.5}
                      dot={{ r: 3.5, fill: "var(--color-value)", strokeWidth: 2, stroke: "#fff" }}
                      activeDot={{ r: 5, strokeWidth: 0 }}
                    />
                  </AreaChart>
                </ChartContainer>
              </CardContent>
            </Card>
          </div>

          {/* ── 4+5. Distribution + Segments (side by side) ── */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            {/* Response Distribution */}
            <Card>
              <CardContent>
                <h3 className="text-sm font-bold mb-4">{t("cx.detailDistribution")}</h3>
                <div className="divide-y divide-border">
                  {data.distribution.map((item) => {
                    const label = isArabic ? item.labelAr : item.label
                    return (
                      <div key={item.label} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
                        <span className="text-sm w-28 shrink-0 truncate">{label}</span>
                        <span
                          className="text-xs font-bold text-white px-2.5 py-0.5 rounded-md tabular-nums shrink-0 min-w-11 text-center"
                          style={{ backgroundColor: item.color }}
                        >
                          {item.value}%
                        </span>
                        <div className="flex-1 h-2.5 bg-muted rounded-full overflow-hidden">
                          <div
                            className="h-full rounded-full motion-safe:transition-all motion-safe:duration-700"
                            style={{ width: `${item.value}%`, backgroundColor: item.color }}
                          />
                        </div>
                      </div>
                    )
                  })}
                </div>
              </CardContent>
            </Card>

            {/* Segment Breakdown */}
            <Card>
              <CardContent>
                <h3 className="text-sm font-bold mb-4">{t("cx.detailSegments")}</h3>
                <div className="divide-y divide-border">
                  {data.segments.map((seg) => {
                    const segName = isArabic ? seg.nameAr : seg.name
                    const barWidth = maxSegmentValue > 0 ? (seg.value / maxSegmentValue) * 100 : 0
                    const color = perfColor(seg.value, kpiKey)
                    return (
                      <div key={seg.name} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
                        <span className="text-sm w-28 shrink-0 truncate">{segName}</span>
                        <div className="flex-1 h-2.5 bg-muted rounded-full overflow-hidden">
                          <div
                            className="h-full rounded-full motion-safe:transition-all motion-safe:duration-500"
                            style={{ width: `${barWidth}%`, backgroundColor: color }}
                          />
                        </div>
                        <span
                          className="text-sm font-bold tabular-nums w-10 text-end"
                          style={{ color }}
                        >
                          {seg.value}
                        </span>
                      </div>
                    )
                  })}
                </div>
              </CardContent>
            </Card>
          </div>

          {/* ── 6. Top Drivers ──────────────────── */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Card className="border-s-4 border-s-d2">
              <CardContent>
                <div className="flex items-start gap-3">
                  <div className="flex items-center justify-center size-8 rounded-lg bg-d2-light dark:bg-d2-dark/20 shrink-0">
                    <ThumbsUp className="size-4 text-d2-dark dark:text-d2-light" />
                  </div>
                  <div>
                    <p className="text-xs font-bold text-d2-dark dark:text-d2-light mb-1">
                      {t("cx.detailTopDriverPos")}
                    </p>
                    <p className="text-sm leading-relaxed">
                      {isArabic ? data.topDriverPos.ar : data.topDriverPos.en}
                    </p>
                  </div>
                </div>
              </CardContent>
            </Card>
            <Card className="border-s-4 border-s-d5">
              <CardContent>
                <div className="flex items-start gap-3">
                  <div className="flex items-center justify-center size-8 rounded-lg bg-d5-light dark:bg-d5-dark/20 shrink-0">
                    <ThumbsDown className="size-4 text-d5-dark dark:text-d5-light" />
                  </div>
                  <div>
                    <p className="text-xs font-bold text-d5-dark dark:text-d5-light mb-1">
                      {t("cx.detailTopDriverNeg")}
                    </p>
                    <p className="text-sm leading-relaxed">
                      {isArabic ? data.topDriverNeg.ar : data.topDriverNeg.en}
                    </p>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  )
}

export { KpiDetailModal, KPI_DETAIL_DATA }
export type { KpiDetailEntry }
