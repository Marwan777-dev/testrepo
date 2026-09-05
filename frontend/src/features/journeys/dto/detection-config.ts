import type { DetectionStageOverride } from "./detection-stage-override"
import type { DetectionTouchpointOverride } from "./detection-touchpoint-override"

/**
 * A journey's pain/happy detection configuration, from `GET /api/v1/journeys/{id}/detection`.
 * `GET` returns 404 `journey.no_detection_config` when none has been saved — the client maps that
 * to `null` and the UI shows defaults.
 *
 * `painThreshold`/`happyThreshold` are the journey-level defaults (a touchpoint or stage scoring
 * at or below `painThreshold` is a pain point; at or above `happyThreshold` is a happy moment).
 * Stage and touchpoint overrides refine those defaults; the most specific override wins (FR-007).
 */
export interface DetectionConfig {
  journeyId: string
  painThreshold: number
  happyThreshold: number
  stageOverrides: DetectionStageOverride[]
  touchpointOverrides: DetectionTouchpointOverride[]
  /** ISO-8601 UTC timestamp of the last save. */
  updatedAt: string
}
