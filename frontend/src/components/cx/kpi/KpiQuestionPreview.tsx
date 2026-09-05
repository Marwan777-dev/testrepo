// T061 [US2] — live preview of how a KPI question renders to a respondent: the question prompt
// ("Rate your experience with <KPI name>.") sits above the scale, which renders as Number (numbered
// chips), Stars (lucide stars), Emoji (one glyph per scale point per research.md R2's per-K slot
// rule), or Slider (only valid on the 1–3 scale). Wide numeric scales (1–100, NPS) are capped to the
// first MAX_VISIBLE_POINTS values so a respondent never sees 100 chips. The optional Min / Max Scale
// Description anchor labels sit beneath the scale's ends. Logical CSS properties throughout (RTL-safe).

import { useTranslation } from "react-i18next"
import { Star } from "lucide-react"

import { cn } from "@/lib/utils"
import {
  type EmojiSetKey,
  type ScaleKey,
  emojiGlyphsForScale,
  scalePoints,
} from "./kpi-scale-meta"

export type RepresentationStyleKey = "Number" | "Stars" | "Emoji" | "Slider"

export interface KpiQuestionPreviewProps {
  scale: ScaleKey | null
  representationStyle: RepresentationStyleKey | null
  emojiSet: EmojiSetKey | null
  /** The KPI's name, interpolated into the question prompt. */
  kpiName?: string
  minDescription?: string
  maxDescription?: string
  className?: string
}

// A respondent should never face 100 chips on a 1–100 scale (or 201 on NPS). Show at most this many
// chips, taken as the first MAX_VISIBLE_POINTS values from the start of the scale (e.g. 1–100 → 1…10).
const MAX_VISIBLE_POINTS = 10

function samplePoints(points: number[]): number[] {
  return points.slice(0, MAX_VISIBLE_POINTS)
}

export function KpiQuestionPreview({
  scale,
  representationStyle,
  emojiSet,
  kpiName,
  minDescription,
  maxDescription,
  className,
}: KpiQuestionPreviewProps) {
  const { t } = useTranslation()

  if (!scale) {
    return (
      <p className={cn("text-sm text-muted-foreground", className)}>
        {t("kpi.questionPreviewComposite", {
          defaultValue: "No question preview — composite KPIs are computed from their members.",
        })}
      </p>
    )
  }

  const points = samplePoints(scalePoints(scale))
  const name = kpiName?.trim() || t("kpi.questionPreviewNameFallback")
  const prompt = t("kpi.questionPreviewPrompt", { name })

  return (
    <div
      className={cn(
        "space-y-4 rounded-md border border-border bg-background p-4",
        className,
      )}
    >
      <p className="text-sm font-semibold leading-relaxed text-foreground">{prompt}</p>

      <div className="flex justify-center">
        <ScaleVisual
          scale={scale}
          representationStyle={representationStyle ?? "Number"}
          emojiSet={emojiSet}
          points={points}
        />
      </div>

      {(minDescription || maxDescription) && (
        <div className="flex items-center justify-between gap-4 text-xs text-muted-foreground">
          <span className="text-start" data-testid="kpi-preview-min-anchor">{minDescription}</span>
          <span className="text-end" data-testid="kpi-preview-max-anchor">{maxDescription}</span>
        </div>
      )}
    </div>
  )
}

function ScaleVisual({
  scale,
  representationStyle,
  emojiSet,
  points,
}: {
  scale: ScaleKey
  representationStyle: RepresentationStyleKey
  emojiSet: EmojiSetKey | null
  points: number[]
}) {
  // Slider is only valid on the 1–3 scale; otherwise fall back to numbered chips.
  const style = representationStyle === "Slider" && scale !== "Scale1_3" ? "Number" : representationStyle

  if (style === "Slider") {
    return (
      <div className="w-full">
        <div className="relative h-2 rounded-full bg-muted">
          <div className="absolute inset-y-0 start-0 w-1/2 rounded-full bg-primary/30" />
          <div className="absolute top-1/2 start-1/2 size-4 -translate-y-1/2 -translate-x-1/2 rounded-full border-2 border-card bg-primary shadow-sm" />
        </div>
        <div className="mt-1 flex justify-between text-xs tabular-nums text-muted-foreground">
          {points.map((p) => (
            <span key={p}>{p}</span>
          ))}
        </div>
      </div>
    )
  }

  if (style === "Stars") {
    return (
      <div className="flex flex-wrap gap-1.5">
        {points.map((p) => (
          <Star key={p} className="size-6 text-nb-cyan" aria-hidden />
        ))}
      </div>
    )
  }

  if (style === "Emoji" && emojiSet) {
    const glyphs = emojiGlyphsForScale(emojiSet, scale)
    return (
      <div className="flex flex-wrap gap-2 text-2xl leading-none">
        {glyphs.map((g, i) => (
          <span key={i} aria-hidden>
            {g}
          </span>
        ))}
      </div>
    )
  }

  // Number (default): numbered chips.
  return (
    <div className="flex flex-wrap gap-2">
      {points.map((p) => (
        <span
          key={p}
          className="inline-flex h-9 min-w-9 items-center justify-center rounded-md border border-border bg-card px-2 text-sm font-medium tabular-nums text-foreground"
        >
          {p}
        </span>
      ))}
    </div>
  )
}
