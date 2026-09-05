/** Response of `PUT /api/v1/touchpoints/{id}` (200 OK). */
export interface UpdateTouchpointResponse {
  touchpointId: string
  /** ISO-8601 UTC timestamp of the update. */
  updatedAt: string
}
