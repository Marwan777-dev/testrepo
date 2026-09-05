// Typed wrapper for SurveyAnalyticsController (contracts/report-and-analytics.md, US9):
// GET /api/v1/surveys/{id}/analytics?period=…&granularity=…[&from&to]. Snake_case wire
// (explicit JsonPropertyName) mapped to camel domain here. Scale notes from the DTO:
// funnel pct_of_sent + overall completion are 0–100 percentages; per-channel and
// per-bucket completion_rate are [0,1] ratios.

import { callJsonWithEtag } from "./etag"
import type { ReportPeriod } from "./report-api"

export type AnalyticsGranularity = "daily" | "weekly" | "monthly"

export interface FunnelStageData {
  count: number
  /** 0–100; null on the Sent stage. */
  pctOfSent: number | null
  /** % delta vs previous period (counts — FR-14.3); Sent stage only. */
  deltaPct: number | null
  /** pp delta vs previous period (rates — FR-14.3). */
  deltaPp: number | null
  /** 0–100 conversion from the previous funnel stage. */
  conversionFromPrevStagePct: number | null
}

export interface ChannelBreakdownData {
  channel: string
  sent: number
  /** [0,1] ratio. */
  completionRate: number
  deltaPp: number | null
}

export interface TrendBucketData {
  bucketStart: string
  sent: number
  finished: number
  /** [0,1] ratio. */
  completionRate: number
}

export interface SurveyAnalytics {
  period: { resolvedFrom: string; resolvedTo: string; granularity: AnalyticsGranularity }
  funnel: {
    sent: FunnelStageData
    opened: FunnelStageData
    started: FunnelStageData
    finished: FunnelStageData
  }
  overallCompletionRate: { valuePct: number; deltaPp: number | null }
  channels: ChannelBreakdownData[]
  trend: TrendBucketData[]
}

/* eslint-disable @typescript-eslint/no-explicit-any -- snake_case wire boundary */
function toStage(w: any): FunnelStageData {
  return {
    count: w?.count ?? 0,
    pctOfSent: w?.pct_of_sent ?? null,
    deltaPct: w?.delta_pct ?? null,
    deltaPp: w?.delta_pp ?? null,
    conversionFromPrevStagePct: w?.conversion_from_prev_stage_pct ?? null,
  }
}

export async function getSurveyAnalytics(
  surveyId: string,
  period: ReportPeriod,
  granularity: AnalyticsGranularity,
  custom?: { from: string; to: string }
): Promise<SurveyAnalytics> {
  const qs = new URLSearchParams({ period, granularity })
  if (period === "custom" && custom) {
    qs.set("from", custom.from)
    qs.set("to", custom.to)
  }
  const { data } = await callJsonWithEtag<any>(`/surveys/${surveyId}/analytics?${qs.toString()}`)
  return {
    period: {
      resolvedFrom: data.period?.resolved_from,
      resolvedTo: data.period?.resolved_to,
      granularity: (data.period?.granularity ?? granularity) as AnalyticsGranularity,
    },
    funnel: {
      sent: toStage(data.funnel?.sent),
      opened: toStage(data.funnel?.opened),
      started: toStage(data.funnel?.started),
      finished: toStage(data.funnel?.finished),
    },
    overallCompletionRate: {
      valuePct: Number(data.overall_completion_rate?.value_pct ?? 0),
      deltaPp: data.overall_completion_rate?.delta_pp ?? null,
    },
    channels: (data.channels ?? []).map((c: any) => ({
      channel: c.channel ?? "",
      sent: c.sent ?? 0,
      completionRate: Number(c.completion_rate ?? 0),
      deltaPp: c.delta_pp ?? null,
    })),
    trend: (data.trend ?? []).map((t: any) => ({
      bucketStart: t.bucket_start,
      sent: t.sent ?? 0,
      finished: t.finished ?? 0,
      completionRate: Number(t.completion_rate ?? 0),
    })),
  }
}
/* eslint-enable @typescript-eslint/no-explicit-any */
