import type { JourneyStatus } from "./journey-status"
import type { PersonaBinding } from "./persona-binding"
import type { StageDetail } from "./stage-detail"

/** Full journey tree returned by `GET /api/v1/journeys/{id}` (journey → stages → touchpoints). */
export interface JourneyDetail {
  journeyId: string
  name: string
  description?: string | null
  journeyType: string
  status: JourneyStatus
  /** Bound personas; populated in US-3. */
  personaBindings: PersonaBinding[]
  /** Stages ordered by `sequenceNumber`. */
  stages: StageDetail[]
  /** ISO-8601 UTC timestamp of the last update. */
  updatedAt: string
  /** UUID of the M-10 user who last updated the journey. */
  updatedBy: string
}
