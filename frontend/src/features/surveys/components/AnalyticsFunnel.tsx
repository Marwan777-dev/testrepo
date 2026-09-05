// Visual conversion funnel (FR-14.1) — the clickthrough's stacked horizontal bars:
// one chunky bar per delivery stage with decreasing width ∝ its share of Sent, the
// count + % above it, and the count printed inside when the bar is wide enough. Brand
// colour progression (chart family, non-semantic — Two-Palette Rule). Per-stage
// deltas + conversions live in the funnel step cards; the overall completion figure
// lives in the banner above — this section is purely the volume visualisation.

import { useTranslation } from "react-i18next"

import type { SurveyAnalytics } from "../api/analytics-api"

const STAGE_BARS = ["bg-primary", "bg-nb-cyan-300", "bg-nb-mint", "bg-nb-mint-700"]

export function AnalyticsFunnel({ analytics }: { analytics: SurveyAnalytics }) {
  const { t } = useTranslation()
  const { funnel } = analytics
  const stages = [
    { key: "sent", count: funnel.sent.count },
    { key: "opened", count: funnel.opened.count },
    { key: "started", count: funnel.started.count },
    { key: "finished", count: funnel.finished.count },
  ]
  const totalSent = Math.max(1, funnel.sent.count)

  return (
    <div className="space-y-3">
      {stages.map(({ key, count }, i) => {
        const pct = (count / totalSent) * 100
        return (
          <div key={key} className="space-y-1">
            <div className="flex items-center justify-between text-xs">
              <span className="text-muted-foreground">{t(`surveysModule.analytics.stage_${key}`)}</span>
              <span className="font-semibold tabular-nums">
                {count.toLocaleString("en-US")} ({pct.toFixed(1)}%)
              </span>
            </div>
            <div className="h-8 overflow-hidden rounded-lg bg-muted">
              <div
                className={`flex h-full items-center rounded-lg ps-3 motion-safe:transition-all motion-safe:duration-700 ${STAGE_BARS[i]}`}
                style={{ width: `${Math.max(2, pct)}%`, minWidth: "2rem" }}
              >
                {pct > 12 && (
                  <span className="truncate text-xs font-medium text-white">
                    {count.toLocaleString("en-US")}
                  </span>
                )}
              </div>
            </div>
          </div>
        )
      })}
    </div>
  )
}
