/**
 * Strategic scoring algorithm for a journey. Identifies which M-06 scoring model is applied.
 * M-06 is the authority on valid values; M-16 only stores and forwards the string — so the wire
 * `modelType` is typed `string` on responses, while the UI selector (which can only emit one of
 * these three known algorithms) uses this union.
 */
export type ScoringModelType = "WeightedAverage" | "HarmonicMean" | "MinScore"
