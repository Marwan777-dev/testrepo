/** Query parameters for `listJourneyVersions` (maps to `GET /api/v1/journeys/{id}/versions`). */
export interface ListVersionsParams {
  /** Page size, 1–200 (the server clamps out-of-range values to its default of 20). */
  pageSize?: number
  /** Cursor returned as `nextPageToken` by the previous page. */
  pageToken?: string
}
