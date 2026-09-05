import type { StageWeightMode } from "./stage-weight-mode"

/**
 * Response of `PUT /api/v1/journeys/{id}/scoring` (200 OK). Echoes the persisted model + mode
 * (no `normalizationParams` echo, per `contracts/configuration-api.md`). `modelType` is `string`
 * for the same reason as on `ScoringConfig` — M-06 owns the valid algorithm names.
 */
export interface SaveScoringResponse {
  journeyId: string
  modelType: string
  stageWeightMode: StageWeightMode
  /** ISO-8601 UTC timestamp of the save. */
  updatedAt: string
}
