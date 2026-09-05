import type { JourneyStatus } from "./journey-status"

/** A journey row as returned by `GET /api/v1/journeys`. */
export interface JourneySummary {
  journeyId: string
  name: string
  description: string | null
  journeyType: string
  status: JourneyStatus
  stageCount: number
  touchpointCount: number
  /** ISO-8601 UTC timestamp of the last update. */
  updatedAt: string
  /** UUID of the M-10 user who last updated the journey. */
  updatedBy: string
}
