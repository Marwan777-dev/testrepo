// F14 Survey Analytics (T265): period + granularity selectors, the FR-14.1 funnel
// (stacked horizontal bars), FR-14.2 channel breakdown, FR-14.4 trend chart, with
// FR-14.3 ▲/▼ deltas (% for counts, pp for rates) suppressed when null (FR-14.5).

import { useCallback, useEffect, useState } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { ArrowLeft, ArrowRight, BarChart2, CircleCheck } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { useDirection } from "@/hooks/use-direction"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import { Switch } from "@/components/ui/switch"
import {
  getSurveyAnalytics,
  type AnalyticsGranularity,
  type SurveyAnalytics,
} from "../api/analytics-api"
import { SAMPLE_ANALYTICS, SAMPLE_TREND_EVENTS } from "../api/sample-data"
import type { ReportPeriod } from "../api/report-api"
import { AnalyticsChannelBars } from "../components/AnalyticsChannelBars"
import { AnalyticsFunnel } from "../components/AnalyticsFunnel"
import { AnalyticsFunnelSteps } from "../components/AnalyticsFunnelSteps"
import { AnalyticsTrendChart } from "../components/AnalyticsTrendChart"
import { DeltaIndicator } from "../components/DeltaIndicator"

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

const GRANULARITIES: AnalyticsGranularity[] = ["daily", "weekly", "monthly"]

export default function AnalyticsPage({ surveyId }: { surveyId: string }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isRtl } = useDirection()
  const BackIcon = isRtl ? ArrowRight : ArrowLeft
  const [period, setPeriod] = useState<ReportPeriod>("last_7_days")
  const [granularity, setGranularity] = useState<AnalyticsGranularity>("daily")
  const [customFrom, setCustomFrom] = useState("")
  const [customTo, setCustomTo] = useState("")
  const [analytics, setAnalytics] = useState<SurveyAnalytics | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  // UI-testing mode: renders SAMPLE_ANALYTICS (funnel/channels/trend + event markers).
  const [sampleMode, setSampleMode] = useState(false)

  const load = useCallback(async () => {
    if (sampleMode) {
      setAnalytics(SAMPLE_ANALYTICS)
      setError(false)
      setLoading(false)
      return
    }
    if (period === "custom" && (!customFrom || !customTo)) return
    setLoading(true)
    setError(false)
    try {
      const result = await getSurveyAnalytics(
        surveyId,
        period,
        granularity,
        period === "custom"
          ? { from: new Date(customFrom).toISOString(), to: new Date(customTo).toISOString() }
          : undefined
      )
      setAnalytics(result)
    } catch {
      setError(true)
    } finally {
      setLoading(false)
    }
  }, [surveyId, period, granularity, customFrom, customTo, sampleMode])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <div className="space-y-5 py-5">
      {/* Breadcrumb */}
      <p className="text-xs text-muted-foreground">{t("surveysModule.analytics.breadcrumb")}</p>

      {/* Header: back + title/subtitle, cross-link to the Survey Report */}
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
            <h1 className="text-2xl font-heading font-bold">
              {t("surveysModule.analytics.title")}
            </h1>
            <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
              {t("surveysModule.analytics.subtitle")}
            </p>
          </div>
        </div>

        <Button
          variant="secondary"
          className="shrink-0"
          onClick={() => navigate(`/surveys/${surveyId}/report`)}
        >
          <BarChart2 className="size-4" aria-hidden />
          {t("surveysModule.analytics.surveyReport")}
        </Button>
      </div>

      {/* Period + granularity filters */}
      <div className="flex flex-wrap items-end gap-3">
          {/* UI-testing toggle — sample payload, no fetch */}
          <div className="flex h-10 items-center gap-2">
            <Switch id="analytics-sample" checked={sampleMode} onCheckedChange={setSampleMode} />
            <Label htmlFor="analytics-sample" className="cursor-pointer text-sm font-normal">
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
          <div className="flex flex-col gap-1.5 sm:w-40">
            <span className="block text-xs font-medium uppercase tracking-widest text-muted-foreground">
              {t("surveysModule.analytics.granularityLabel")}
            </span>
            <Select
              value={granularity}
              onValueChange={(v) => setGranularity((v ?? "daily") as AnalyticsGranularity)}
            >
              <SelectTrigger className="w-full">
                <SelectValue>
                  {(v) => t(`surveysModule.analytics.granularity_${String(v ?? granularity)}`)}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {GRANULARITIES.map((g) => (
                  <SelectItem key={g} value={g}>
                    {t(`surveysModule.analytics.granularity_${g}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {period === "custom" && (
            <>
              <div className="flex flex-col gap-1.5">
                <Label
                  htmlFor="analytics-from"
                  className="text-xs font-medium uppercase tracking-widest text-muted-foreground"
                >
                  {t("surveysModule.report.from")}
                </Label>
                <Input
                  id="analytics-from"
                  type="date"
                  value={customFrom}
                  onChange={(e) => setCustomFrom(e.target.value)}
                  className="w-40"
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label
                  htmlFor="analytics-to"
                  className="text-xs font-medium uppercase tracking-widest text-muted-foreground"
                >
                  {t("surveysModule.report.to")}
                </Label>
                <Input
                  id="analytics-to"
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
          {t("surveysModule.analytics.loadError")}
        </div>
      )}

      {loading || !analytics ? (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          <Skeleton className="h-72 w-full" />
          <Skeleton className="h-72 w-full" />
        </div>
      ) : (
        <>
          {/* Overall completion banner */}
          <div className="flex flex-wrap items-center gap-4 rounded-lg border border-border bg-card px-5 py-4 shadow-sm dark:shadow-none">
            <span className="flex size-10 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
              <CircleCheck className="size-5" aria-hidden />
            </span>
            <div className="min-w-0">
              <p className="text-sm text-muted-foreground">
                {t("surveysModule.analytics.overallTitle")}
              </p>
              <p className="font-heading text-2xl font-bold tabular-nums">
                {analytics.overallCompletionRate.valuePct.toFixed(1)}%
              </p>
              {analytics.overallCompletionRate.deltaPp != null && (
                <p className="mt-0.5 flex items-center gap-1 text-xs">
                  <DeltaIndicator value={analytics.overallCompletionRate.deltaPp} unit="pp" />
                  <span className="text-muted-foreground">
                    {t("surveysModule.analytics.vsPrevPeriod")}
                  </span>
                </p>
              )}
            </div>
            <div className="ms-auto text-end">
              <p className="text-xs text-muted-foreground">
                {t("surveysModule.analytics.selectedPeriod")}
              </p>
              <p className="text-sm font-medium">{t(`surveysModule.periods.${period}`)}</p>
            </div>
          </div>

          {/* Funnel step cards */}
          <AnalyticsFunnelSteps analytics={analytics} />

          {/* Visual conversion funnel */}
          <Card>
            <CardHeader>
              <CardTitle>{t("surveysModule.analytics.visualFunnelTitle")}</CardTitle>
              <CardDescription>{t("surveysModule.analytics.visualFunnelDesc")}</CardDescription>
            </CardHeader>
            <CardContent>
              <AnalyticsFunnel analytics={analytics} />
            </CardContent>
          </Card>

          {/* Breakdown by channel */}
          <Card>
            <CardHeader>
              <CardTitle>{t("surveysModule.analytics.channelsTitle")}</CardTitle>
              <CardDescription>{t("surveysModule.analytics.channelsDesc")}</CardDescription>
            </CardHeader>
            <CardContent>
              {analytics.channels.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  {t("surveysModule.analytics.noChannels")}
                </p>
              ) : (
                <AnalyticsChannelBars channels={analytics.channels} />
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t("surveysModule.analytics.trendTitle")}</CardTitle>
              <CardDescription>{t("surveysModule.analytics.trendDesc")}</CardDescription>
            </CardHeader>
            <CardContent>
              {analytics.trend.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  {t("surveysModule.analytics.noTrend")}
                </p>
              ) : (
                <AnalyticsTrendChart
                  trend={analytics.trend}
                  granularity={analytics.period.granularity}
                  // Sample mode exercises the ReferenceDot/Line event markers too
                  // (no real event source yet — TODO-M01-029).
                  events={sampleMode ? SAMPLE_TREND_EVENTS : undefined}
                />
              )}
            </CardContent>
          </Card>
        </>
      )}
    </div>
  )
}
