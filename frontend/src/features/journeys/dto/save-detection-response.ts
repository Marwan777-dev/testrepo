/**
 * Response of `PUT /api/v1/journeys/{id}/detection` (200 OK). Echoes the persisted journey-level
 * thresholds plus the count of stored overrides (the override rows themselves are not echoed —
 * re-fetch via `GET /detection` if the full set is needed).
 */
export interface SaveDetectionResponse {
  journeyId: string
  painThreshold: number
  happyThreshold: number
  stageOverrideCount: number
  touchpointOverrideCount: number
  /** ISO-8601 UTC timestamp of the save. */
  updatedAt: string
}
