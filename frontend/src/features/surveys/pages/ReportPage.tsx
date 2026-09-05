// F13 Survey Report (T246): metric cards, headline KPI gauges (custom SVG per the KPI
// Gauge Design Spec), FR-13.1 period filter (+ custom range), and per-question views
// per FR-13.3 — KPI: bars + gauge; single-select/YesNo: donut; multi-select: bars vs
// respondents; Scale: gauge + distribution; Text/Paragraph: verbatims with "show
// more"; Number/Date/Time: value-distribution line. Matrix/Ranking have NO report
// visual defined in the spec (tracked as TODO-M01-024) — they render an explicit
// "no visual" note instead of guessing. Headline values are perfColor-coded.

import { useCallback, useEffect, useState } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import {
  ArrowLeft,
  ArrowRight,
  CircleCheck,
  Clock,
  Eye,
  EyeOff,
  FileChartColumn,
  Layers,
  LineChart,
  Users,
} from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Switch } from "@/components/ui/switch"
import { useDirection } from "@/hooks/use-direction"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import { perfColor } from "@/lib/journey-data"
import {
  getSurveyReport,
  type PerQuestionCard,
  type ReportPeriod,
  type SurveyReport,
} from "../api/report-api"
import { SAMPLE_REPORT } from "../api/sample-data"
import { DistributionDonut } from "../components/DistributionDonut"
import { KpiGaugeSvg } from "../components/KpiGaugeSvg"
import { MultiSelectBarChart } from "../components/MultiSelectBarChart"
import { NumericDistributionLine } from "../components/NumericDistributionLine"
import { PerQuestionSideGauge } from "../components/PerQuestionSideGauge"
import { ReportMetricCard } from "../components/ReportMetricCard"
import { VerbatimTable } from "../components/VerbatimTable"

function isNumericInput(subtype: string | null): boolean {
  return subtype === "Number" || subtype === "Date" || subtype === "Time"
}

const PERIODS: ReportPeriod[] = [
  "last_1_day",
  "last_7_days",
  "last_month",
  "last_3_months",
  "last_6_months",
  "last_9_months",
  "last_year",
  "custom",
]

function formatDuration(seconds: number | null, dash: string): string {
  if (seconds == null) return dash
  const m = Math.floor(seconds / 60)
  const s = Math.round(seconds % 60)
  return m > 0 ? `${m}m ${s}s` : `${s}s`
}

function QuestionCardView({
  card,
  surveyId,
}: {
  card: PerQuestionCard
  surveyId: string
}) {
  const { t } = useTranslation()
  const dist = card.distribution
  const isDonut = card.type === "SingleSelect" || card.type === "YesNo"

  // Type badge (question-type + count) — matches the clickthrough's wording, with
  // "answers" / "responses" / "respondents" chosen per type.
  const badge = (() => {
    const n = card.responsesCount.toLocaleString("en-US")
    switch (card.type) {
      case "Kpi":
        return t("surveysModule.report.qBadgeKpi", { n })
      case "SingleSelect":
        return t("surveysModule.report.qBadgeSingle", { n })
      case "YesNo":
        return t("surveysModule.report.qBadgeBool", { n })
      case "MultiSelect":
        return t("surveysModule.report.qBadgeMulti", { n })
      case "Scale": {
        const view = (card.subtype ?? "labels").toLowerCase()
        return t("surveysModule.report.qBadgeScale", {
          view: t(`surveysModule.report.scaleView_${view}`, { defaultValue: view }),
          n,
        })
      }
      case "InputField":
        return isNumericInput(card.subtype)
          ? t("surveysModule.report.qBadgeNumber", { n })
          : t("surveysModule.report.qBadgeText", { n })
      case "Matrix":
        return t("surveysModule.report.qBadgeMatrix", { n })
      case "Ranking":
        return t("surveysModule.report.qBadgeRanking", { n })
      default:
        return t("surveysModule.report.responsesCount", { count: card.responsesCount })
    }
  })()

  // Title: the real question text (populated in sample mode); falls back to the
  // question-type name when the wire has no text.
  const title =
    card.questionText ??
    t(`surveysModule.report.qType${card.type}`, { defaultValue: card.type })

  // FR-13.3 view routing — keyed on the wire QuestionType name (+ subtype).
  let body: React.ReactNode
  if (card.type === "Kpi") {
    body = (
      <div className="grid grid-cols-1 items-center gap-5 sm:grid-cols-[1fr_200px]">
        {dist && <MultiSelectBarChart buckets={dist} respondentsBase={card.responsesCount} graded />}
        <PerQuestionSideGauge card={card} />
      </div>
    )
  } else if (card.type === "Scale") {
    const isLabels = (card.subtype ?? "").toLowerCase() === "labels"
    body =
      isLabels || !card.gauge ? (
        dist ? (
          <MultiSelectBarChart buckets={dist} respondentsBase={card.responsesCount} graded />
        ) : null
      ) : (
        <div className="grid grid-cols-1 items-center gap-5 sm:grid-cols-[1fr_200px]">
          {dist && <MultiSelectBarChart buckets={dist} respondentsBase={card.responsesCount} graded />}
          <PerQuestionSideGauge card={card} />
        </div>
      )
  } else if (isDonut) {
    body = dist ? (
      <DistributionDonut buckets={dist} centerLabel={t("surveysModule.report.answers")} />
    ) : null
  } else if (card.type === "MultiSelect") {
    body = dist ? (
      <div className="space-y-2">
        <MultiSelectBarChart buckets={dist} respondentsBase={card.respondentsBase} graded showCount />
        <p className="pt-1 text-xs text-muted-foreground">
          {t("surveysModule.report.multiNote", {
            n: (card.respondentsBase ?? card.responsesCount).toLocaleString("en-US"),
          })}
        </p>
      </div>
    ) : null
  } else if (card.type === "InputField") {
    if (isNumericInput(card.subtype)) {
      body = dist ? (
        <NumericDistributionLine
          buckets={dist}
          average={card.average}
          axisLabel={card.numberAxis}
          unit={card.avgUnit}
        />
      ) : null
    } else {
      body = (
        <VerbatimTable
          surveyId={surveyId}
          questionId={card.questionId}
          sample={card.sample ?? []}
          totalAvailable={card.totalAvailable}
        />
      )
    }
  } else {
    // Matrix / Ranking — no spec-defined visual (TODO-M01-024); say so explicitly.
    body = <p className="text-sm text-muted-foreground">{t("surveysModule.report.noVisual")}</p>
  }

  // Donut cards pair 2-up; every other card spans the full grid width, matching the
  // clickthrough. Donut cards stack the badge under the title; wide cards keep it inline.
  return (
    <Card className={isDonut ? undefined : "lg:col-span-2"}>
      <CardContent className="space-y-3 px-5">
        {isDonut ? (
          <div className="space-y-2">
            <h3 className="text-sm font-semibold leading-snug">{title}</h3>
            <Badge variant="outline" className="text-xs">
              {badge}
            </Badge>
          </div>
        ) : (
          <div className="flex flex-wrap items-start justify-between gap-3">
            <h3 className="min-w-0 text-sm font-semibold leading-snug">{title}</h3>
            <Badge variant="outline" className="shrink-0 text-xs">
              {badge}
            </Badge>
          </div>
        )}
        {body}
      </CardContent>
    </Card>
  )
}

export default function ReportPage({ surveyId }: { surveyId: string }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isRtl } = useDirection()
  const BackIcon = isRtl ? ArrowRight : ArrowLeft
  const [period, setPeriod] = useState<ReportPeriod>("last_7_days")
  const [customFrom, setCustomFrom] = useState("")
  const [customTo, setCustomTo] = useState("")
  const [report, setReport] = useState<SurveyReport | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  // UI-testing mode: renders SAMPLE_REPORT (every component populated) without fetching.
  const [sampleMode, setSampleMode] = useState(false)
  const [showPerQ, setShowPerQ] = useState(true)

  const load = useCallback(async () => {
    if (sampleMode) {
      setReport(SAMPLE_REPORT)
      setError(false)
      setLoading(false)
      return
    }
    if (period === "custom" && (!customFrom || !customTo)) return
    setLoading(true)
    setError(false)
    try {
      const result = await getSurveyReport(
        surveyId,
        period,
        period === "custom"
          ? {
              from: new Date(customFrom).toISOString(),
              to: new Date(customTo).toISOString(),
            }
          : undefined
      )
      setReport(result)
    } catch {
      setError(true)
    } finally {
      setLoading(false)
    }
  }, [surveyId, period, customFrom, customTo, sampleMode])

  useEffect(() => {
    void load()
  }, [load])

  const kpis = report?.headlineKpis

  return (
    <div className="space-y-5 py-5">
      {/* Breadcrumb */}
      <p className="text-xs text-muted-foreground">{t("surveysModule.report.breadcrumb")}</p>

      {/* Header: back + title/subtitle, cross-link to Analytics */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between sm:gap-4">
        <div className="flex min-w-0 items-start gap-3">
          <Button
            variant="outline"
            size="icon"
            className="mt-0.5 size-9 shrink-0"
            onClick={() => navigate("/surveys")}
            aria-label={t("common.back")}
          >
            <BackIcon className="size-4" aria-hidden />
          </Button>
          <div className="min-w-0">
            <h1 className="text-2xl font-heading font-bold">{t("surveysModule.report.title")}</h1>
            <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
              {t("surveysModule.report.subtitle")}
            </p>
          </div>
        </div>

        <Button
          variant="secondary"
          className="shrink-0"
          onClick={() => navigate(`/surveys/${surveyId}/analytics`)}
        >
          <LineChart className="size-4" aria-hidden />
          {t("surveysModule.report.openAnalytics")}
        </Button>
      </div>

      {/* FR-13.1 period filter */}
      <div className="flex flex-wrap items-end gap-3">
          {/* UI-testing toggle — sample payload, no fetch */}
          <div className="flex h-10 items-center gap-2">
            <Switch id="report-sample" checked={sampleMode} onCheckedChange={setSampleMode} />
            <Label htmlFor="report-sample" className="cursor-pointer text-sm font-normal">
              {t("surveysModule.sample.toggle")}
            </Label>
          </div>
          <div className="flex flex-col gap-1.5 sm:w-48">
            <span className="block text-xs font-medium uppercase tracking-widest text-muted-foreground">
              {t("surveysModule.report.periodLabel")}
            </span>
            <Select value={period} onValueChange={(v) => setPeriod((v ?? "last_7_days") as ReportPeriod)}>
              <SelectTrigger className="w-full">
                <SelectValue>
                  {(v) => t(`surveysModule.periods.${String(v ?? period)}`)}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {PERIODS.map((p) => (
                  <SelectItem key={p} value={p}>
                    {t(`surveysModule.periods.${p}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {period === "custom" && (
            <>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="report-from" className="text-xs font-medium uppercase tracking-widest text-muted-foreground">
                  {t("surveysModule.report.from")}
                </Label>
                <Input
                  id="report-from"
                  type="date"
                  value={customFrom}
                  onChange={(e) => setCustomFrom(e.target.value)}
                  className="w-40"
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="report-to" className="text-xs font-medium uppercase tracking-widest text-muted-foreground">
                  {t("surveysModule.report.to")}
                </Label>
                <Input
                  id="report-to"
                  type="date"
                  value={customTo}
                  onChange={(e) => setCustomTo(e.target.value)}
                  className="w-40"
                />
              </div>
            </>
          )}
        </div>

      {sampleMode && (
        <div className="rounded-md border border-nb-cyan-200 bg-nb-cyan-100/50 px-3 py-2 text-sm text-nb-cyan-800 dark:border-nb-cyan-800 dark:bg-nb-cyan-900/25 dark:text-nb-cyan-200">
          {t("surveysModule.sample.note")}
        </div>
      )}

      {error && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("surveysModule.report.loadError")}
        </div>
      )}

      {loading || !report ? (
        <div className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-28 w-full" />
            ))}
          </div>
          <Skeleton className="h-64 w-full" />
        </div>
      ) : (
        <>
          {/* Metric cards */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <ReportMetricCard
              icon={Users}
              label={t("surveysModule.report.responses")}
              value={String(report.metricCards.responses)}
            />
            <ReportMetricCard
              icon={CircleCheck}
              label={t("surveysModule.report.completionRate")}
              value={`${Math.round(report.metricCards.completionRate)}%`}
            />
            <ReportMetricCard
              icon={Clock}
              label={t("surveysModule.report.medianTime")}
              value={formatDuration(report.metricCards.medianTimeSeconds, "—")}
            />
            <ReportMetricCard
              icon={Layers}
              label={t("surveysModule.report.touchpoints")}
              value={String(report.metricCards.touchpoints)}
            />
          </div>

          {/* Headline KPI gauges — value colour is KPI-aware (NPS 52 ≠ CES 52) */}
          {(kpis?.csat || kpis?.nps || kpis?.ces) && (
            <div className="space-y-3">
            <h2 className="text-lg font-bold">{t("surveysModule.report.kpiScores")}</h2>
            <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
              {kpis.csat && (
                <Card>
                  <CardContent className="flex flex-col items-center gap-2 text-center">
                    <div>
                      <p className="text-base font-bold" style={{ color: perfColor(kpis.csat.value) }}>
                        CSAT
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {t("surveysModule.report.kpiName_csat")}
                      </p>
                    </div>
                    <div style={{ color: perfColor(kpis.csat.value) }}>
                      <KpiGaugeSvg
                        value={kpis.csat.value}
                        target={kpis.csat.target}
                        label="CSAT"
                        deltaPp={kpis.csat.deltaPp}
                      />
                    </div>
                    {kpis.csat.target != null && (
                      <p className="text-sm font-medium text-muted-foreground">
                        {t("surveysModule.report.target", { value: `${kpis.csat.target}%` })}
                      </p>
                    )}
                  </CardContent>
                </Card>
              )}
              {kpis.nps && (
                <Card>
                  <CardContent className="flex flex-col items-center gap-2 text-center">
                    <div>
                      <p
                        className="text-base font-bold"
                        style={{ color: perfColor(kpis.nps.value, "nps") }}
                      >
                        NPS
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {t("surveysModule.report.kpiName_nps")}
                      </p>
                    </div>
                    <KpiGaugeSvg
                      value={kpis.nps.value}
                      target={kpis.nps.target}
                      min={-100}
                      max={100}
                      label="NPS"
                      kpiId="nps"
                      deltaPp={kpis.nps.deltaPp}
                    />
                    {kpis.nps.target != null && (
                      <p className="text-sm font-medium text-muted-foreground">
                        {t("surveysModule.report.target", {
                          value: `${kpis.nps.target >= 0 ? "+" : ""}${kpis.nps.target}`,
                        })}
                      </p>
                    )}
                  </CardContent>
                </Card>
              )}
              {kpis.ces && (
                <Card>
                  <CardContent className="flex flex-col items-center gap-2 text-center">
                    <div>
                      <p
                        className="text-base font-bold"
                        style={{ color: perfColor(kpis.ces.value, "ces") }}
                      >
                        CES
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {t("surveysModule.report.kpiName_ces")}
                      </p>
                    </div>
                    <KpiGaugeSvg
                      value={kpis.ces.value}
                      target={kpis.ces.target}
                      label="CES"
                      kpiId="ces"
                      deltaPp={kpis.ces.deltaPp}
                    />
                    {kpis.ces.target != null && (
                      <p className="text-sm font-medium text-muted-foreground">
                        {t("surveysModule.report.target", { value: `≤${kpis.ces.target}%` })}
                      </p>
                    )}
                  </CardContent>
                </Card>
              )}
            </div>
            </div>
          )}

          {/* Per-question views (FR-13.3) — section header + hide toggle */}
          <div className="space-y-3">
            <div className="flex items-center justify-between gap-2">
              <h2 className="text-lg font-bold">{t("surveysModule.report.perQuestionTitle")}</h2>
              <Button variant="ghost" size="sm" onClick={() => setShowPerQ((s) => !s)}>
                {showPerQ ? (
                  <EyeOff className="size-4" aria-hidden />
                ) : (
                  <Eye className="size-4" aria-hidden />
                )}
                {showPerQ
                  ? t("surveysModule.report.hideSection")
                  : t("surveysModule.report.showSection")}
              </Button>
            </div>

            {showPerQ &&
              (report.perQuestion.length === 0 ? (
                <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
                  <FileChartColumn className="mb-4 size-12 text-muted-foreground" aria-hidden />
                  <h3 className="mb-2 text-lg font-bold">{t("surveysModule.report.empty")}</h3>
                  <p className="max-w-sm text-muted-foreground">
                    {t("surveysModule.report.emptyHelp")}
                  </p>
                </div>
              ) : (
                <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
                  {report.perQuestion.map((card) => (
                    <QuestionCardView key={card.questionId} card={card} surveyId={surveyId} />
                  ))}
                </div>
              ))}
          </div>
        </>
      )}
    </div>
  )
}
