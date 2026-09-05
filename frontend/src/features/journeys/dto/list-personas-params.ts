import type { PersonaStatus } from "./persona-status"

/** Query parameters for `listPersonas` (maps to the `GET /api/v1/personas` query string). */
export interface ListPersonasParams {
  /** Filter to a single lifecycle status; omit for all statuses. */
  status?: PersonaStatus
  /** Page size, 1–200 (the server clamps out-of-range values to its default of 50). */
  pageSize?: number
  /** Cursor returned as `nextPageToken` by the previous page. */
  pageToken?: string
}
