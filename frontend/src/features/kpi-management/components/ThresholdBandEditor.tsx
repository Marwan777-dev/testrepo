// T063 [US2] — performance-threshold editor. Three D-coloured band rows rendered as inline
// inequalities: Unsatisfactory (lower ≤ score ≤ x), Average (x < score ≤ y), Satisfactory
// (y < score ≤ upper). The shared x / y boundaries are integer inputs — each appears in the two
// bands it divides, bound to the same value — while the scale endpoints (lower / upper) are
// read-only text. Surfaces an inline error when the strictly-ascending invariant
// (lower < x < y < upper) is violated. The inequality always reads LTR (math convention); only the
// band label follows document direction, so the layout stays RTL-safe.

import { useTranslation } from "react-i18next"

import { Input } from "@/components/ui/input"
import { cn } from "@/lib/utils"

export interface ThresholdBandEditorProps {
  lower: number
  upper: number
  x: number
  y: number
  onChange: (next: { x: number; y: number }) => void
  /** When true, fields are display-only (P-02 read-only mode). */
  readOnly?: boolean
}

export function ThresholdBandEditor({ lower, upper, x, y, onChange, readOnly }: ThresholdBandEditorProps) {
  const { t } = useTranslation()
  const ascending = lower < x && x < y && y < upper

  const boundaryInput = (value: number, onCommit: (next: number) => void, ariaLabel: string, testId: string) => (
    <Input
      type="number"
      inputMode="numeric"
      aria-label={ariaLabel}
      aria-invalid={!ascending}
      disabled={readOnly}
      data-testid={testId}
      value={Number.isFinite(value) ? value : ""}
      onChange={(e) => onCommit(Math.round(Number(e.target.value)))}
      className="h-9 w-16 px-2 text-center tabular-nums"
    />
  )

  const commitX = (next: number) => onChange({ x: next, y })
  const commitY = (next: number) => onChange({ x, y: next })
  const endpoint = (v: number) => <span className="tabular-nums">{Math.round(v)}</span>
  const score = <span className="px-0.5">{t("kpi.thresholdScore")}</span>

  const bands = [
    {
      key: "unsatisfactory",
      label: t("kpi.bandUnsatisfactory"),
      labelColor: "text-d5 dark:text-d5-light",
      expr: (
        <>
          {endpoint(lower)}
          <span aria-hidden>≤</span>
          {score}
          <span aria-hidden>≤</span>
          {boundaryInput(x, commitX, t("kpi.thresholdX"), "kpi-threshold-x")}
        </>
      ),
    },
    {
      key: "average",
      label: t("kpi.bandAverage"),
      labelColor: "text-d3 dark:text-d3-light",
      expr: (
        <>
          {boundaryInput(x, commitX, t("kpi.thresholdX"), "kpi-threshold-x")}
          <span aria-hidden>&lt;</span>
          {score}
          <span aria-hidden>≤</span>
          {boundaryInput(y, commitY, t("kpi.thresholdY"), "kpi-threshold-y")}
        </>
      ),
    },
    {
      key: "satisfactory",
      label: t("kpi.bandSatisfactory"),
      labelColor: "text-d1 dark:text-d1-light",
      expr: (
        <>
          {boundaryInput(y, commitY, t("kpi.thresholdY"), "kpi-threshold-y")}
          <span aria-hidden>&lt;</span>
          {score}
          <span aria-hidden>≤</span>
          {endpoint(upper)}
        </>
      ),
    },
  ]

  return (
    <div className="space-y-3">
      <div className="space-y-2">
        {bands.map((band) => (
          <div
            key={band.key}
            className="flex flex-wrap items-center gap-x-3 gap-y-2 rounded-md border border-border bg-card px-3 py-2.5"
          >
            <span className={cn("w-28 shrink-0 text-sm font-semibold", band.labelColor)}>
              {band.label}
            </span>
            <div
              dir="ltr"
              className="flex flex-1 flex-wrap items-center gap-1.5 text-sm font-medium tabular-nums text-muted-foreground"
            >
              {band.expr}
            </div>
          </div>
        ))}
      </div>

      {!ascending && (
        <p className="text-sm text-destructive" role="alert">
          {t("kpi.thresholdAscending", { lower: Math.round(lower), upper: Math.round(upper) })}
        </p>
      )}
    </div>
  )
}
