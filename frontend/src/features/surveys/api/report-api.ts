// Typed wrappers for SurveyReportController (contracts/report-and-analytics.md, US8):
// GET /api/v1/surveys/{id}/report?period=…[&from&to] and GET …/report/verbatims.
// NOTE: unlike the survey/section/question DTOs (camelCase), the report DTOs carry
// explicit snake_case [JsonPropertyName] attributes — this module maps snake wire →
// camel domain in one place so components never see snake_case.

import { callJsonWithEtag } from "./etag"

export type ReportPeriod =
  | "last_1_day"
  | "last_7_days"
  | "last_month"
  | "last_3_months"
  | "last_6_months"
  | "last_9_months"
  | "last_year"
  | "custom"

export interface ReportMetricCards {
  responses: number
  /** 0–100 percentage. */
  completionRate: number
  medianTimeSeconds: number | null
  touchpoints: number
}

export interface ReportHeadlineKpi {
  value: number
  target: number | null
  deltaPp: number | null
}

export interface ReportGauge {
  value: number
  target: number | null
}

export interface DistributionBucket {
  label: string
  count: number
  /** Multi-select respondent-base percentage (FR-13.5); null elsewhere. */
  pctOfRespondents: number | null
}

export interface VerbatimEntry {
  responseId: string
  channel: string
  submittedAt: string
  text: string
}

export interface PerQuestionCard {
  questionId: string
  /** PascalCase QuestionType name from the wire (e.g. "Kpi", "SingleSelect"). */
  type: string
  subtype: string | null
  kind: string
  responsesCount: number
  distribution: DistributionBucket[] | null
  respondentsBase: number | null
  gauge: ReportGauge | null
  average: number | null
  sample: VerbatimEntry[] | null
  totalAvailable: number | null
  /** Optional display metadata (populated in sample mode; may be absent on the wire). */
  questionText?: string | null
  /** KPI scale id for a Kpi card ("csat" | "nps" | "ces") — colours + centre label. */
  kpiId?: string | null
  /** Unit shown under the numeric-average side panel (e.g. "days"). */
  avgUnit?: string | null
  /** X-axis caption for the numeric distribution line (e.g. "Number of days"). */
  numberAxis?: string | null
}

export interface SurveyReport {
  period: { resolvedFrom: string; resolvedTo: string }
  metricCards: ReportMetricCards
  headlineKpis: {
    csat: ReportHeadlineKpi | null
    nps: ReportHeadlineKpi | null
    ces: ReportHeadlineKpi | null
  }
  perQuestion: PerQuestionCard[]
}

/* eslint-disable @typescript-eslint/no-explicit-any -- snake_case wire boundary */
function toKpi(w: any): ReportHeadlineKpi | null {
  if (!w) return null
  return { value: Number(w.value), target: w.target ?? null, deltaPp: w.delta_pp ?? null }
}

function toVerbatim(w: any): VerbatimEntry {
  return {
    responseId: w.response_id,
    channel: w.channel ?? "",
    submittedAt: w.submitted_at,
    text: w.text ?? "",
  }
}

function toCard(w: any): PerQuestionCard {
  const view = w.view ?? {}
  return {
    questionId: w.question_id,
    type: w.type ?? "",
    subtype: w.subtype ?? null,
    kind: view.kind ?? "",
    responsesCount: w.responses_count ?? 0,
    distribution:
      view.distribution?.map((d: any) => ({
        label: d.label ?? "",
        count: d.count ?? 0,
        pctOfRespondents: d.pct_of_respondents ?? null,
      })) ?? null,
    respondentsBase: view.respondents_base ?? null,
    gauge: view.gauge
      ? { value: Number(view.gauge.value), target: view.gauge.target ?? null }
      : null,
    average: view.average ?? null,
    sample: view.sample?.map(toVerbatim) ?? null,
    totalAvailable: view.total_available ?? null,
    questionText: w.question_text ?? w.text ?? null,
    kpiId: w.kpi_id ?? null,
    avgUnit: view.avg_unit ?? null,
    numberAxis: view.number_axis ?? null,
  }
}

/** GET /surveys/{id}/report — FR-13.x; custom period sends from/to ISO stamps. */
export async function getSurveyReport(
  surveyId: string,
  period: ReportPeriod,
  custom?: { from: string; to: string }
): Promise<SurveyReport> {
  const qs = new URLSearchParams({ period })
  if (period === "custom" && custom) {
    qs.set("from", custom.from)
    qs.set("to", custom.to)
  }
  const { data } = await callJsonWithEtag<any>(`/surveys/${surveyId}/report?${qs.toString()}`)
  return {
    period: {
      resolvedFrom: data.period?.resolved_from,
      resolvedTo: data.period?.resolved_to,
    },
    metricCards: {
      responses: data.metric_cards?.responses ?? 0,
      completionRate: Number(data.metric_cards?.completion_rate ?? 0),
      medianTimeSeconds: data.metric_cards?.median_time_seconds ?? null,
      touchpoints: data.metric_cards?.touchpoints ?? 0,
    },
    headlineKpis: {
      csat: toKpi(data.headline_kpis?.csat),
      nps: toKpi(data.headline_kpis?.nps),
      ces: toKpi(data.headline_kpis?.ces),
    },
    perQuestion: (data.per_question ?? []).map(toCard),
  }
}

/** GET /surveys/{id}/report/verbatims — the FR-13.7 "show more" fetch (≤ 100). */
export async function getReportVerbatims(
  surveyId: string,
  questionId: string,
  limit = 100
): Promise<VerbatimEntry[]> {
  const { data } = await callJsonWithEtag<any>(
    `/surveys/${surveyId}/report/verbatims?question_id=${questionId}&limit=${limit}`
  )
  const rows = Array.isArray(data) ? data : (data.items ?? data.sample ?? [])
  return rows.map(toVerbatim)
}
/* eslint-enable @typescript-eslint/no-explicit-any */
