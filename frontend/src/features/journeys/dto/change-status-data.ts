import type { JourneyStatus } from "./journey-status"

/**
 * Request body for `changeJourneyStatus` (`PATCH /api/v1/journeys/{id}/status`). Valid
 * transitions: Draft→Active, Active→Inactive, Inactive→Active, any non-Archived→Archived.
 * Out-of-Archived transitions are rejected with `journey.archived_terminal`.
 */
export interface ChangeStatusData {
  status: JourneyStatus
}
