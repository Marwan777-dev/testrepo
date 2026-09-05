/** Response of `PUT /api/v1/journeys/{id}/stages/{stageId}` (200 OK). */
export interface UpdateStageResponse {
  stageId: string
  /** ISO-8601 UTC timestamp of the update. */
  updatedAt: string
}
