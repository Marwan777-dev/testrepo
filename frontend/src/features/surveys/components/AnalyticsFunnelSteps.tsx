// FR-14.1 funnel step cards (analytics) — the clickthrough's row of Sent / Opened /
// Started / Finished cards, each with an icon, count, "% of sent · ▲/▼ delta vs prev.",
// and a conversion pill + chevron bridging to the next stage. Deltas are suppressed
// when null (FR-14.5). Conversion pills use the D-scale light tokens (status), the
// chevron flips in RTL. Data comes straight from SurveyAnalytics.funnel.

import { ChevronRight, CheckCircle2, Eye, Play, Send, TrendingDown, TrendingUp } from "lucide-react"
import { useTranslation } from "react-i18next"

import { cn } from "@/lib/utils"
import { useDirection } from "@/hooks/use-direction"
import type { FunnelStageData, SurveyAnalytics } from "../api/analytics-api"

const STEP_ICON = { sent: Send, opened: Eye, started: Play, finished: CheckCircle2 }

function convPill(pct: number): string {
  if (pct >= 70) return "bg-d2-light text-d1-dark dark:bg-d2-dark/40 dark:text-d2-light"
  if (pct >= 50) return "bg-d3-light text-d3-dark dark:bg-d3-dark/40 dark:text-d3-light"
  return "bg-d5-light text-d5-dark dark:bg-d5-dark/40 dark:text-d5-light"
}

export function AnalyticsFunnelSteps({ analytics }: { analytics: SurveyAnalytics }) {
  const { t } = useTranslation()
  const { isRtl } = useDirection()
  const { funnel } = analytics
  const totalSent = Math.max(1, funnel.sent.count)

  const stages: {
    key: "sent" | "opened" | "started" | "finished"
    data: FunnelStageData
    deltaUnit: "pct" | "pp"
    delta: number | null
  }[] = [
    { key: "sent", data: funnel.sent, deltaUnit: "pct", delta: funnel.sent.deltaPct },
    { key: "opened", data: funnel.opened, deltaUnit: "pp", delta: funnel.opened.deltaPp },
    { key: "started", data: funnel.started, deltaUnit: "pp", delta: funnel.started.deltaPp },
    { key: "finished", data: funnel.finished, deltaUnit: "pp", delta: funnel.finished.deltaPp },
  ]

  return (
    <div className="flex items-start gap-0 overflow-x-auto pb-1">
      {stages.map(({ key, data, delta, deltaUnit }, i) => {
        const Icon = STEP_ICON[key]
        const pctOfSent = data.pctOfSent ?? (data.count / totalSent) * 100
        const up = (delta ?? 0) >= 0
        const TrendIcon = up ? TrendingUp : TrendingDown
        const conv = stages[i + 1]?.data.conversionFromPrevStagePct
        const isLast = i === stages.length - 1
        return (
          <div key={key} className="flex min-w-[9rem] flex-1 items-center gap-2">
            <div className="flex-1 rounded-lg border border-border bg-card p-4 shadow-sm transition-shadow hover:shadow-md dark:shadow-none">
              <div className="flex items-center gap-2">
                <Icon className="size-4 text-primary" aria-hidden />
                <p className="text-sm font-semibold text-muted-foreground">
                  {t(`surveysModule.analytics.stage_${key}`)}
                </p>
              </div>
              <p className="mt-2 font-heading text-3xl font-bold tabular-nums">
                {data.count.toLocaleString("en-US")}
              </p>
              <div className="mt-1.5 flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
                <span>
                  {pctOfSent.toFixed(1)}% {t("surveysModule.analytics.ofSent")} ·
                </span>
                {delta != null && (
                  <span
                    className={cn("inline-flex items-center gap-0.5 font-medium tabular-nums")}
                    style={{ color: up ? "#2EB85C" : "#C01B2A" }}
                    dir="ltr"
                  >
                    <TrendIcon className="size-3" />
                    {up ? "+" : ""}
                    {delta.toFixed(1)}
                    {deltaUnit === "pct" ? "%" : " pp"}
                  </span>
                )}
                <span>{t("surveysModule.analytics.vsPrev")}</span>
              </div>
            </div>
            {!isLast && conv != null && (
              <div className="flex shrink-0 flex-col items-center gap-1 px-1">
                <div
                  className={cn(
                    "rounded-full px-2.5 py-1 text-xs font-bold tabular-nums",
                    convPill(conv),
                  )}
                >
                  {conv.toFixed(1)}%
                </div>
                <ChevronRight
                  className={cn("size-5 text-muted-foreground", isRtl && "rotate-180")}
                  aria-hidden
                />
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}
