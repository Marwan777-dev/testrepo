/**
 * Response of `GET /api/v1/journeys/{id}/updated-at` — the concurrent-edit polling endpoint
 * consumed by `useJourneyUpdated`. `updatedByName` is resolved via M-10 (empty until wired).
 */
export interface UpdatedAtResponse {
  /** ISO-8601 UTC timestamp of the journey's last update. */
  updatedAt: string
  /** UUID of the user who last updated the journey. */
  updatedByUserId: string
  /** Display name of that user (empty string until the M-10 lookup is wired). */
  updatedByName: string
}
