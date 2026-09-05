// T105 [US4] — Alpha slider (0.000–1.000) with a live read-only β display beside it. β is derived
// (1 − α) and rounded to 3 dp to avoid IEEE-754 drift (e.g. 1 − 0.7 = 0.30000000000000004 → "0.300").
// RTL-safe: numeric readouts are forced LTR; logical spacing only.

import type { ReactNode } from "react"

import { Slider } from "@/components/ui/slider"

function first(value: number | readonly number[]): number {
  return typeof value === "number" ? value : value[0]
}

export interface AlphaBetaSliderProps {
  alpha: number
  onAlphaChange: (alpha: number) => void
  disabled?: boolean
  /** Localised "Alpha (α)" / "Beta (β)" captions. */
  alphaLabel: string
  betaLabel: string
  /** Optional affordance (e.g. the "?" info icon) rendered beside the alpha caption. */
  info?: ReactNode
}

export function AlphaBetaSlider({
  alpha,
  onAlphaChange,
  disabled,
  alphaLabel,
  betaLabel,
  info,
}: AlphaBetaSliderProps) {
  const beta = Math.round((1 - alpha) * 1000) / 1000

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-4">
        <span className="flex items-center gap-1.5 text-sm font-medium text-foreground">
          {alphaLabel}
          {info}
        </span>
        <span dir="ltr" data-testid="alpha-value" className="font-mono text-lg font-bold tabular-nums">
          {alpha.toFixed(3)}
        </span>
      </div>
      <Slider
        value={alpha}
        min={0}
        max={1}
        step={0.001}
        disabled={disabled}
        aria-label={alphaLabel}
        onValueChange={(value) => onAlphaChange(first(value))}
      />
      <div className="flex items-center justify-between gap-4 rounded-md bg-muted/40 px-3 py-2">
        <span className="text-sm text-muted-foreground">{betaLabel}</span>
        <span dir="ltr" data-testid="beta-value" className="font-mono text-lg font-bold tabular-nums text-foreground">
          {beta.toFixed(3)}
        </span>
      </div>
    </div>
  )
}
