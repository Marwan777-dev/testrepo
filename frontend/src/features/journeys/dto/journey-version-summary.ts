/** A published-version row as returned by `GET /api/v1/journeys/{id}/versions` (newest first). */
export interface JourneyVersionSummary {
  versionId: string
  /** Sequential, 1-based within the journey; increments on each publish. */
  versionNumber: number
  /** ISO-8601 UTC publish timestamp. */
  publishedAt: string
  /** Display name of the publisher; empty until the M-10 user-name lookup lands (backend defers it). */
  publishedByName: string
}
