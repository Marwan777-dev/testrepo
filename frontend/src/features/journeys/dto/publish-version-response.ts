/** Response of `POST /api/v1/journeys/{id}/publish` (201 Created). */
export interface PublishVersionResponse {
  versionId: string
  /** The newly assigned version number (previous max + 1). */
  versionNumber: number
  /** ISO-8601 UTC publish timestamp. */
  publishedAt: string
}
