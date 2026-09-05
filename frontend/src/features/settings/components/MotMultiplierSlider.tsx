// T106 [US4] — MOT multiplier slider (1.0–2.0, step 0.1) with a live 1-dp readout. RTL-safe numeric
// readout (forced LTR); logical spacing only.

import type { ReactNode } from "react"

import { Slider } from "@/components/ui/slider"

function first(value: number | readonly number[]): number {
  return typeof value === "number" ? value : value[0]
}

export interface MotMultiplierSliderProps {
  value: number
  onChange: (value: number) => void
  disabled?: boolean
  label: string
  /** Optional affordance (e.g. the "?" info icon) rendered beside the caption. */
  info?: ReactNode
}

export function MotMultiplierSlider({ value, onChange, disabled, label, info }: MotMultiplierSliderProps) {
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-4">
        <span className="flex items-center gap-1.5 text-sm font-medium text-foreground">
          {label}
          {info}
        </span>
        <span dir="ltr" data-testid="mot-value" className="font-mono text-lg font-bold tabular-nums">
          {value.toFixed(1)}
        </span>
      </div>
      <Slider
        value={value}
        min={1}
        max={2}
        step={0.1}
        disabled={disabled}
        aria-label={label}
        onValueChange={(v) => onChange(Math.round(first(v) * 10) / 10)}
      />
    </div>
  )
}
