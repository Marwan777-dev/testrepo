/** Response of `POST /api/v1/stages/{stageId}/touchpoints` (201 Created). */
export interface AddTouchpointResponse {
  touchpointId: string
  /** ISO-8601 UTC creation timestamp. */
  createdAt: string
}
