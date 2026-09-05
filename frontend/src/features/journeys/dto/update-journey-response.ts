/** Response of `PUT /api/v1/journeys/{id}` (200 OK). */
export interface UpdateJourneyResponse {
  journeyId: string
  name: string
  /** ISO-8601 UTC timestamp of the update. */
  updatedAt: string
}
