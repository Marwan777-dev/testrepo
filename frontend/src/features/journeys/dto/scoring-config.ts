import type { StageWeightMode } from "./stage-weight-mode"

/**
 * Strategic scoring configuration for a journey, from `GET /api/v1/journeys/{id}/scoring`.
 * `GET` returns 404 `journey.no_scoring_config` when none has been saved — the client maps that
 * to `null` and the UI shows defaults.
 *
 * `modelType` is typed `string` (not the `ScoringModelType` union) because M-06 owns the set of
 * valid algorithm names and may extend it; the UI normalizes it to a known value for its picker.
 * `normalizationParams` is arbitrary `jsonb` defined and interpreted by M-06 — M-16 stores and
 * returns it verbatim, so it is opaque to this client.
 */
export interface ScoringConfig {
  journeyId: string
  modelType: string
  stageWeightMode: StageWeightMode
  /** Opaque M-06-owned params object; `null`/absent when none was saved. */
  normalizationParams?: Record<string, unknown> | null
  /** ISO-8601 UTC timestamp of the last save. */
  updatedAt: string
}
