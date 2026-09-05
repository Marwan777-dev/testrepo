// FR-14.3 delta indicator (T266): ▲ green / ▼ red with the correct unit — % for count
// deltas, pp for rate deltas. Suppressed entirely when the delta is null (FR-14.5 —
// no prior-period data ⇒ no misleading zero). Colour is paired with the arrow glyph,
// never colour-alone. D2/D5 status hex per the perfColor pattern (JS values).

import { D2, D5 } from "@/lib/journey-data"

export function DeltaIndicator({
  value,
  unit,
}: {
  value: number | null
  unit: "pct" | "pp"
}) {
  if (value == null) return null
  const up = value >= 0
  return (
    <span
      className="whitespace-nowrap text-sm font-medium tabular-nums"
      style={{ color: up ? D2 : D5 }}
      dir="ltr"
    >
      {up ? "▲" : "▼"} {Math.abs(value).toFixed(1)}
      {unit === "pct" ? "%" : " pp"}
    </span>
  )
}
