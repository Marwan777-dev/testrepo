// Type-accurate mock preview rendered inside each canvas question card (clickthrough
// parity — the card shows what respondents will see, driven by the live settings).
// Purely presentational: token colors only, one choice pre-selected for realism.

import { useTranslation } from "react-i18next"
import { CheckSquare, ChevronDown, Frown, Meh, Smile, Star } from "lucide-react"

import { cn } from "@/lib/utils"
import type { BuilderQuestion } from "./builder-types"

function Pill({ selected, children }: { selected?: boolean; children: React.ReactNode }) {
  return (
    <span
      className={cn(
        "inline-flex min-w-8 items-center justify-center rounded-md border px-2 py-1.5 text-sm font-medium",
        selected
          ? "border-primary bg-primary text-primary-foreground"
          : "border-input bg-card text-foreground"
      )}
    >
      {children}
    </span>
  )
}

export function QuestionPreview({ question: q }: { question: BuilderQuestion }) {
  const { t } = useTranslation()

  if (q.type === "Paragraph") {
    return <p className="whitespace-pre-wrap text-sm text-muted-foreground">{q.text}</p>
  }

  if (q.type === "Kpi") {
    // The KPI question renders on the KPI's own configured scale (NPS 0..10, …).
    const values = q.kpiScaleValues ?? [1, 2, 3, 4, 5]
    const points = values.length
    const sel = Math.ceil(points * 0.8)
    // Very wide scales (1..100) read as a slider bar, not a pill row.
    if (points > 11)
      return (
        <div>
          <div className="relative h-1.5 rounded-full bg-muted">
            <div className="absolute inset-y-0 start-0 w-4/5 rounded-full bg-primary/60" />
          </div>
          <div className="mt-1.5 flex justify-between text-[11px] text-muted-foreground">
            <span dir="ltr">{values[0]}</span>
            <span dir="ltr">{values[points - 1]}</span>
          </div>
        </div>
      )
    return (
      <div className="flex flex-wrap gap-1.5">
        {values.map((v, i) => (
          <Pill key={v} selected={i + 1 === sel}>
            {v}
          </Pill>
        ))}
        {q.allowNA && <Pill>N/A</Pill>}
      </div>
    )
  }

  if (q.type === "Scale") {
    const n = Math.max(2, q.scalePoints)
    const sel = Math.ceil(n * 0.8)
    if (q.subType === "Stars")
      return (
        <div className="flex gap-0.5">
          {Array.from({ length: n }).map((_, i) => (
            <Star
              key={i}
              className={cn("size-5 text-primary", i < sel && "fill-primary")}
              aria-hidden
            />
          ))}
        </div>
      )
    if (q.subType === "Smileys")
      return (
        <div className="flex gap-1.5">
          {Array.from({ length: n }).map((_, i) => {
            const ratio = i / Math.max(1, n - 1)
            const Icon = ratio < 0.34 ? Frown : ratio < 0.67 ? Meh : Smile
            return (
              <Icon
                key={i}
                className={cn("size-6", i + 1 === sel ? "text-primary" : "text-border")}
                aria-hidden
              />
            )
          })}
        </div>
      )
    if (q.subType === "Slider") {
      const steps = Math.max(1, Math.min(20, q.sliderSteps))
      const labels = Array.from({ length: steps + 1 }, (_, i) =>
        Math.round(q.sliderMin + (i * (q.sliderMax - q.sliderMin)) / steps)
      )
      return (
        <div>
          {/* Mock slider: a plain track with a single thumb (no fill / tick dots). */}
          <div className="relative h-1.5 rounded-full bg-muted">
            <span
              className="absolute top-1/2 size-4 -translate-y-1/2 rounded-full border-2 border-card bg-primary shadow-sm"
              style={{ insetInlineStart: "calc(40% - 0.5rem)" }}
              aria-hidden
            />
          </div>
          <div className="mt-2 flex justify-between text-[11px] text-muted-foreground">
            {labels.map((v, i) => (
              <span key={i} dir="ltr">
                {v}
              </span>
            ))}
          </div>
        </div>
      )
    }
    return (
      <div className="flex flex-wrap gap-1.5">
        {Array.from({ length: n }).map((_, i) => (
          <Pill key={i} selected={i + 1 === sel}>
            {q.pointLabels[i]?.trim() || i + 1}
          </Pill>
        ))}
      </div>
    )
  }

  if (q.type === "YesNo") {
    return (
      <div className="flex gap-2">
        <Pill selected>{q.yesLabel || t("common.yes")}</Pill>
        <Pill>{q.noLabel || t("common.no")}</Pill>
      </div>
    )
  }

  if (q.type === "Ranking") {
    const items = q.rankingItems.length > 0 ? q.rankingItems : ["Item 1", "Item 2"]
    return (
      <div className="flex flex-col gap-1.5">
        {items.map((item, i) => (
          <span
            key={i}
            className="flex items-center gap-2 rounded-md border border-border px-2.5 py-1.5 text-sm"
          >
            <b className="text-primary tabular-nums">{i + 1}</b> {item}
          </span>
        ))}
      </div>
    )
  }

  if (q.type === "InputField") {
    const isParagraph = q.subType === "Paragraph"
    return (
      <div
        className={cn(
          "flex rounded-md border border-input px-3 text-sm text-muted-foreground",
          isParagraph ? "h-16 py-2" : "h-9 items-center"
        )}
      >
        {t("surveysModule.preview.inputMock", { type: t(`surveysModule.subType.${q.subType}`) })}
      </div>
    )
  }

  if (q.type === "Matrix") {
    const rows = q.matrixRows.length > 0 ? q.matrixRows : ["Row 1", "Row 2"]
    const kpiMode = q.subType === "KpiScale"
    const colCount = kpiMode
      ? Math.min(q.matrixKpiPoints ?? 5, 7)
      : Math.max(1, q.matrixColumns.length)
    const faceFor = (i: number) => {
      const ratio = i / Math.max(1, colCount - 1)
      return ratio < 0.34 ? Frown : ratio < 0.67 ? Meh : Smile
    }
    return (
      <div className="space-y-1 text-sm">
        {rows.map((row, ri) => (
          <div key={ri} className="flex items-center gap-2">
            <span className="w-24 truncate text-muted-foreground">{row}</span>
            <div className="flex gap-1.5">
              {Array.from({ length: colCount }).map((_, ci) =>
                kpiMode ? (
                  (() => {
                    const Icon = faceFor(ci)
                    return (
                      <Icon
                        key={ci}
                        className={cn(
                          "size-4",
                          ci === colCount - 2 ? "text-primary" : "text-border"
                        )}
                        aria-hidden
                      />
                    )
                  })()
                ) : (
                  <span key={ci} className="size-4 rounded-full border-[1.5px] border-border" />
                )
              )}
            </div>
          </div>
        ))}
      </div>
    )
  }

  // Single select / Multi-select
  const opts = q.options.length > 0 ? q.options : ["Option 1", "Option 2"]
  const isMulti = q.type === "MultiSelect"
  const asDropdown =
    (q.type === "SingleSelect" && q.subType === "Dropdown") ||
    (isMulti && q.multiDisplay === "dropdown")
  if (asDropdown) {
    return (
      <div className="flex items-center justify-between rounded-md border border-input px-3 py-1.5 text-sm text-muted-foreground">
        <span>{isMulti ? t("surveysModule.preview.nOptions", { count: opts.length }) : opts[0]}</span>
        <ChevronDown className="size-3.5" aria-hidden />
      </div>
    )
  }
  return (
    <div className="flex flex-col gap-1.5">
      {opts.map((opt, i) => (
        <span
          key={i}
          className="flex items-center gap-2 rounded-md border border-border px-3 py-1.5 text-sm"
        >
          {isMulti ? (
            <CheckSquare
              className={cn("size-3.5 shrink-0", i === 0 ? "text-primary" : "text-border")}
              aria-hidden
            />
          ) : (
            <span
              className={cn(
                "size-3.5 shrink-0 rounded-full border-[1.5px]",
                i === 0 ? "border-primary bg-primary" : "border-border"
              )}
            />
          )}
          {opt}
        </span>
      ))}
    </div>
  )
}
