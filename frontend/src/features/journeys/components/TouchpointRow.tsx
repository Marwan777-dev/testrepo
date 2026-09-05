import type { ReactNode } from "react"
import { useTranslation } from "react-i18next"
import { AlertTriangle, CircleCheck, Star, Zap } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"
import { DEFAULT_CHANNELS } from "@/lib/journey-data"
import type { TouchpointDetail, TouchpointImportance } from "@/features/journeys/api"

// Importance → filled-star count (out of 5). Critical fills the full row; Low keeps the
// bottom star empty so a glance separates the four levels on the 5-star scale.
const IMPORTANCE_STARS: Record<TouchpointImportance, number> = {
  Low: 2,
  Medium: 3,
  High: 4,
  Critical: 5,
}

const IMPORTANCE_LABEL: Record<TouchpointImportance, string> = {
  Low: "journey.impLow",
  Medium: "journey.impMedium",
  High: "journey.impHigh",
  Critical: "journey.impCritical",
}

/** Resolve a wire channel key (e.g. `mobile_app`) to its localized label. */
function resolveChannel(channel: string, isArabic: boolean): string {
  const found = DEFAULT_CHANNELS.find((c) => c.key === channel)
  if (!found) return channel
  return isArabic ? found.label.ar : found.label.en
}

/**
 * Presentational row for a touchpoint (US-1 `TouchpointDetail`) inside an expanded stage
 * accordion. Renders the in-stage sequence label (e.g. `1.2`), the name, channel chips, a
 * 5-star importance rating, the MoT marker, and a measured / unmeasured KPI badge.
 *
 * Coloring honors the Two-Palette Rule: the KPI *status* (measured → D2 "Good", unmeasured →
 * D3 "Caution") uses the semantic D-scale, while MoT — a significance marker, not a KPI state —
 * uses the cyan brand token so it never reads as a D2-green status next to the measured pill.
 * `actions` renders trailing edit / delete controls.
 */
export function TouchpointRow({
  touchpoint,
  seqLabel,
  actions,
  className,
}: {
  touchpoint: TouchpointDetail
  seqLabel: string
  actions?: ReactNode
  className?: string
}) {
  const { t, i18n } = useTranslation()
  const isArabic = i18n.language === "ar"
  const kpiCount = touchpoint.kpiBindings.length
  const measured = touchpoint.isMeasured && kpiCount > 0
  const stars = IMPORTANCE_STARS[touchpoint.importance] ?? 3
  const importanceText = t(IMPORTANCE_LABEL[touchpoint.importance] ?? "journey.impMedium")

  return (
    <article
      className={cn(
        "flex flex-wrap items-center gap-x-3 gap-y-2 rounded-md border border-border bg-card px-3 py-2.5 text-card-foreground transition-colors hover:bg-muted/30",
        className,
      )}
    >
      <span className="shrink-0 text-xs font-bold tabular-nums text-muted-foreground">
        {seqLabel}
      </span>
      <h4 className="min-w-0 flex-1 truncate text-sm font-semibold">{touchpoint.name}</h4>

      <div className="flex flex-wrap items-center gap-1.5">
        {touchpoint.channels.map((channel) => (
          <Badge key={channel} variant="outline" className="text-[10px]">
            {resolveChannel(channel, isArabic)}
          </Badge>
        ))}

        <span
          className="flex items-center gap-0.5"
          role="img"
          aria-label={`${t("journey.importanceLabel")}: ${importanceText}`}
        >
          {[1, 2, 3, 4, 5].map((n) => (
            <Star
              key={n}
              className={cn(
                "size-3.5",
                n <= stars ? "fill-nb-cyan text-nb-cyan" : "fill-muted text-muted-foreground/30",
              )}
              aria-hidden
            />
          ))}
        </span>

        {touchpoint.isMoT && (
          <Badge className="border-nb-cyan bg-nb-cyan text-[10px] text-white">
            <Zap className="size-2.5" aria-hidden />
            {t("journey.mot")}
          </Badge>
        )}

        {touchpoint.isMandatory && (
          <Badge variant="outline" className="text-[10px]">
            {t("journey.mandatory")}
          </Badge>
        )}

        {measured ? (
          <Badge className="border-transparent bg-d2-light text-[10px] text-d2-dark dark:bg-d2-dark/25 dark:text-d2-light">
            <CircleCheck className="size-2.5" aria-hidden />
            {kpiCount} {t(kpiCount === 1 ? "journey.kpiShort" : "journey.kpisShort")}
          </Badge>
        ) : (
          <Badge className="border-transparent bg-d3-light text-[10px] text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light">
            <AlertTriangle className="size-2.5" aria-hidden />
            {t("journey.noKpis")}
          </Badge>
        )}
      </div>

      {actions && <div className="flex shrink-0 items-center gap-0.5">{actions}</div>}
    </article>
  )
}
