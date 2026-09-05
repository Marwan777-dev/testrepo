import type { JourneyStatus } from "./journey-status"

/** Query parameters for `listJourneys` (maps to the `GET /api/v1/journeys` query string). */
export interface ListJourneysParams {
  /** Filter to a single lifecycle status; omit for all statuses. */
  status?: JourneyStatus
  /** Page size, 1–200 (the server clamps out-of-range values to its default of 50). */
  pageSize?: number
  /** Cursor returned as `nextPageToken` by the previous page. */
  pageToken?: string
}
