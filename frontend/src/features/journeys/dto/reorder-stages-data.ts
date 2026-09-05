/**
 * Request body for `reorderStages` (`PUT /api/v1/journeys/{id}/stages/reorder`). The array must
 * contain exactly all of the journey's stage IDs in the desired order — no omissions, no dupes.
 */
export interface ReorderStagesData {
  stageIds: string[]
}
