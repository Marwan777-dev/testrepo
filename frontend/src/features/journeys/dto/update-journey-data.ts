/**
 * Request body for `updateJourney` (`PUT /api/v1/journeys/{id}`). Same validation as create.
 * Allowed on Draft/Active/Inactive journeys; an Archived journey returns 403
 * `journey.archived_immutable`.
 */
export interface UpdateJourneyData {
  /** 1–255 characters; unique per tenant, case-insensitive, excluding Archived journeys. */
  name: string
  description?: string | null
  journeyType: string
  /**
   * Full replacement set of bound persona IDs (US-3, FR-005). Each referenced persona must be
   * `Active` — the server returns 422 `journey.invalid_persona` otherwise. Omit to leave bindings
   * unchanged. (Per the journeys-API contract; the binding selector sends the complete set on
   * every change.)
   */
  personaIds?: string[]
}
