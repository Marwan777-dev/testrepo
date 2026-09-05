/** Request body for `updateStage` (`PUT /api/v1/journeys/{id}/stages/{stageId}`). */
export interface UpdateStageData {
  name: string
  description?: string | null
  customerGoal?: string | null
  expectedEmotion?: string | null
  durationHint?: string | null
}
