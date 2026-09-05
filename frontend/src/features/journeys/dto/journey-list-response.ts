import type { JourneySummary } from "./journey-summary"

/** Response of `GET /api/v1/journeys` — one cursor-paginated page (API-04). */
export interface JourneyListResponse {
  items: JourneySummary[]
  nextPageToken: string | null
  totalCount: number
}
