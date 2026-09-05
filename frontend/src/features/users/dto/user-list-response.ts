import type { UserSummary } from "./user-summary"

/** Response of `GET /api/v1/users` — one cursor-paginated page (API-04). */
export interface UserListResponse {
  items: UserSummary[]
  nextPageToken: string | null
  totalCount: number
}
