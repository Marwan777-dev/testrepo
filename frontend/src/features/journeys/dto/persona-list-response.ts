import type { PersonaSummary } from "./persona-summary"

/** Response of `GET /api/v1/personas` — one cursor-paginated page (API-04). */
export interface PersonaListResponse {
  items: PersonaSummary[]
  nextPageToken: string | null
  totalCount: number
}
