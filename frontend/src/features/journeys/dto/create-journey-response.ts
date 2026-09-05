import type { JourneyStatus } from "./journey-status"

/** Response of `POST /api/v1/journeys` (201 Created). */
export interface CreateJourneyResponse {
  journeyId: string
  name: string
  /** Always `Draft` for a freshly created journey. */
  status: JourneyStatus
  /** ISO-8601 UTC creation timestamp. */
  createdAt: string
}
