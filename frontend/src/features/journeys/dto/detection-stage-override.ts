/**
 * A stage-level pain/happy threshold override inside a journey's detection configuration
 * (`contracts/configuration-api.md` → `PUT`/`GET /api/v1/journeys/{id}/detection`). A `null`
 * threshold means "inherit the journey-level value" — the most specific override wins (FR-007).
 */
export interface DetectionStageOverride {
  stageId: string
  /** Score at or below which the stage is a pain point; `null` inherits the journey level. */
  painThreshold: number | null
  /** Score at or above which the stage is a happy moment; `null` inherits the journey level. */
  happyThreshold: number | null
}
