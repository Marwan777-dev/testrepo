import type { JourneyVersionSummary } from "./journey-version-summary"

/** Response of `GET /api/v1/journeys/{id}/versions` — one cursor-paginated page (API-04), newest first. */
export interface JourneyVersionListResponse {
  items: JourneyVersionSummary[]
  nextPageToken: string | null
  totalCount: number
}
