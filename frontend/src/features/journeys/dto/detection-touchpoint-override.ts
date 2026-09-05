/**
 * A touchpoint-level pain/happy threshold override inside a journey's detection configuration
 * (`contracts/configuration-api.md` → `PUT`/`GET /api/v1/journeys/{id}/detection`). A `null`
 * threshold means "inherit from the stage or journey level" — the most specific override wins.
 */
export interface DetectionTouchpointOverride {
  touchpointId: string
  /** Score at or below which the touchpoint is a pain point; `null` inherits the parent level. */
  painThreshold: number | null
  /** Score at or above which the touchpoint is a happy moment; `null` inherits the parent level. */
  happyThreshold: number | null
}
