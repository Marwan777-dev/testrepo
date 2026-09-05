/** Response of `POST /api/v1/journeys/{id}/stages` (201 Created). */
export interface AddStageResponse {
  stageId: string
  /** 1-based position the new stage was appended at. */
  sequenceNumber: number
  /** ISO-8601 UTC creation timestamp. */
  createdAt: string
}
