// FR-14.2 channel breakdown (T266) — the clickthrough layout: channel label at the
// start, a bar ∝ the dataset's max sent volume, the sent count, the completion %, and
// the FR-14.3 pp delta at the end. Channels are CATEGORICAL, so bars + the % use the
// brand chart-1…5 palette (never perfColor — a low completion rate must not paint the
// whole row red; the Two-Palette Rule keeps semantic D-colours for status only).

import { useMemo } from "react"
import { TrendingDown, TrendingUp } from "lucide-react"

import { D2, D5 } from "@/lib/journey-data"
import type { ChannelBreakdownData } from "../api/analytics-api"

// Brand chart series (non-semantic). One colour per channel, by sent-desc rank.
const SERIES = [
  "var(--chart-1)",
  "var(--chart-2)",
  "var(--chart-3)",
  "var(--chart-4)",
  "var(--chart-5)",
]

export function AnalyticsChannelBars({ channels }: { channels: ChannelBreakdownData[] }) {
  const sorted = useMemo(() => [...channels].sort((a, b) => b.sent - a.sent), [channels])
  const maxSent = Math.max(1, ...sorted.map((c) => c.sent))

  return (
    <ul className="space-y-3.5">
      {sorted.map((c, i) => {
        const ratePct = c.completionRate * 100
        const colour = SERIES[i % SERIES.length]
        return (
          <li key={c.channel} className="flex items-center gap-3">
            <span className="w-24 shrink-0 truncate text-end text-sm font-medium">{c.channel}</span>
            <div className="h-2.5 min-w-0 flex-1 rounded-full bg-muted/40">
              <div
                className="h-2.5 rounded-full motion-safe:transition-all motion-safe:duration-700"
                style={{ width: `${(c.sent / maxSent) * 100}%`, backgroundColor: colour }}
              />
            </div>
            <span
              className="w-20 shrink-0 text-end text-xs tabular-nums text-muted-foreground"
              dir="ltr"
            >
              {c.sent.toLocaleString("en-US")} →
            </span>
            <span
              className="w-12 shrink-0 text-end text-sm font-bold tabular-nums"
              style={{ color: colour }}
            >
              {Math.round(ratePct)}%
            </span>
            <span className="flex w-16 shrink-0 items-center justify-end">
              {c.deltaPp != null &&
                (() => {
                  const up = c.deltaPp >= 0
                  const Icon = up ? TrendingUp : TrendingDown
                  return (
                    <span
                      className="flex items-center gap-0.5 text-sm font-medium tabular-nums"
                      style={{ color: up ? D2 : D5 }}
                      dir="ltr"
                    >
                      <Icon className="size-3" aria-hidden />
                      {up ? "+" : ""}
                      {c.deltaPp.toFixed(1)} pp
                    </span>
                  )
                })()}
            </span>
          </li>
        )
      })}
    </ul>
  )
}
