// T062 [US2] — derives the D1–D5 performance band at a value given a KPI's threshold edges.
// Pure function (no React, no I/O). The thresholds split the scale into three zones —
// unsatisfactory [lower, x), average [x, y), satisfactory [y, upper] — which this maps onto the
// five-step D-scale so the gauge's inner ring and any band-coloured chrome agree on one source of
// truth (mirrors the journey-data `perfLevel` contract, but threshold-driven rather than fixed
// percentage breakpoints).

export type PerfBand = "d1" | "d2" | "d3" | "d4" | "d5"

/**
 * Returns the D-band for `value` against the band edges. The unsatisfactory zone splits into d5
 * (worst half) / d4, the average zone is d3, and the satisfactory zone splits into d2 / d1 (best
 * half). Values outside [lower, upper] clamp to the nearest extreme band.
 */
export function perfBandAt(
  value: number,
  x: number,
  y: number,
  lower: number,
  upper: number,
): PerfBand {
  if (value < x) {
    return value < (lower + x) / 2 ? "d5" : "d4"
  }
  if (value < y) {
    return "d3"
  }
  return value < (y + upper) / 2 ? "d2" : "d1"
}
