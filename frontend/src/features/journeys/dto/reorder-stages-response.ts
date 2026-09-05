/** Response of `PUT /api/v1/journeys/{id}/stages/reorder` (200 OK). */
export interface ReorderStagesResponse {
  journeyId: string
  /** ISO-8601 UTC timestamp of the reorder. */
  reorderedAt: string
}
