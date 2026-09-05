import type { ScoringModelType } from "./scoring-model-type"
import type { StageWeightMode } from "./stage-weight-mode"

/**
 * Request body for `PUT /api/v1/journeys/{id}/scoring`. The UI only emits one of the three known
 * `modelType` algorithms (hence the union here, unlike the response's free `string`).
 * `normalizationParams` is opaque M-06-owned `jsonb` forwarded as-is; omit/`null` to clear it.
 */
export interface SaveScoringData {
  modelType: ScoringModelType
  stageWeightMode: StageWeightMode
  normalizationParams?: Record<string, unknown> | null
}
