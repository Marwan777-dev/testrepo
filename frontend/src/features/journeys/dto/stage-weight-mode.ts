/**
 * How a journey's stage scores are combined into the journey score. `Equal` weights every stage
 * the same; `Custom` reads per-stage weights from `normalizationParams.stageWeights` (summing to
 * 100). M-06 owns the custom-weight semantics; M-16 only stores the mode + the opaque params.
 */
export type StageWeightMode = "Equal" | "Custom"
