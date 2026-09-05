/**
 * Request body for `addStage` (`POST /api/v1/journeys/{id}/stages`). The stage is appended at
 * the end of the journey's sequence. Only `name` is required.
 */
export interface AddStageData {
  name: string
  description?: string | null
  customerGoal?: string | null
  expectedEmotion?: string | null
  durationHint?: string | null
}
