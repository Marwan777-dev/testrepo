// Distribution bars (T247) per the Distribution / Progress Bars spec: horizontal
// rounded-full bars over a subtle track, label at the start, % + count at the end,
// mount animation. Two colour modes:
//  • default — brand cyan fill, width ∝ % of RESPONDENTS (multi-select, FR-13.5).
//  • graded  — D-scale ramp green→red by rank (KPI / Scale rating distributions),
//    width ∝ % of the response base. Graded is the only place a D-colour is used
//    here, and it signals rating status (Two-Palette Rule honoured).

import { D1, D2, D3, D4, D5 } from "@/lib/journey-data"
import type { DistributionBucket } from "../api/report-api"

// Best→worst ramp; a bucket's rank maps evenly onto it.
const RAMP = [D1, D2, D3, D4, D5]

function gradedColor(index: number, count: number): string {
  if (count <= 1) return D2
  return RAMP[Math.round((index / (count - 1)) * (RAMP.length - 1))]
}

export function MultiSelectBarChart({
  buckets,
  respondentsBase,
  graded = false,
  showCount = false,
}: {
  buckets: DistributionBucket[]
  respondentsBase: number | null
  /** Colour bars by rank on the D-scale (green→red) instead of brand cyan. */
  graded?: boolean
  /** Show "count · %" (multi-select) instead of just "%" (rating scales). */
  showCount?: boolean
}) {
  return (
    <ul className="space-y-2.5">
      {buckets.map((b, i) => {
        const pct =
          b.pctOfRespondents ??
          (respondentsBase && respondentsBase > 0 ? (b.count / respondentsBase) * 100 : 0)
        const colour = graded ? gradedColor(i, buckets.length) : undefined
        return (
          <li key={b.label + i} className="space-y-1">
            <div className="flex items-baseline justify-between gap-2 text-sm">
              <span className="min-w-0 flex-1 truncate">{b.label}</span>
              <span className="shrink-0 tabular-nums text-muted-foreground">
                {showCount
                  ? `${b.count.toLocaleString("en-US")} · ${Math.round(pct)}%`
                  : `${Math.round(pct)}%`}
              </span>
            </div>
            <div className="h-2 w-full rounded-full bg-muted/20">
              <div
                className={`h-2 rounded-full motion-safe:transition-all motion-safe:duration-700 ${
                  graded ? "" : "bg-nb-cyan"
                }`}
                style={{
                  width: `${Math.min(100, Math.max(0, pct))}%`,
                  ...(colour ? { backgroundColor: colour } : {}),
                }}
              />
            </div>
          </li>
        )
      })}
    </ul>
  )
}
