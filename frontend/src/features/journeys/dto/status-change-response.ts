import type { JourneyStatus } from "./journey-status"

/** Response of `PATCH /api/v1/journeys/{id}/status` (200 OK). */
export interface StatusChangeResponse {
  journeyId: string
  /** The new lifecycle status after the transition. */
  status: JourneyStatus
  /** ISO-8601 UTC timestamp of the transition. */
  updatedAt: string
}
