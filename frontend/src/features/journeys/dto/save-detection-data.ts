import type { DetectionStageOverride } from "./detection-stage-override"
import type { DetectionTouchpointOverride } from "./detection-touchpoint-override"

/**
 * Request body for `PUT /api/v1/journeys/{id}/detection`. This is a full replace — the override
 * lists are the complete, authoritative set for the journey. `painThreshold`/`happyThreshold` are
 * required journey-level defaults (both in `[0, 100]`, `painThreshold` strictly less than
 * `happyThreshold`). In overrides, `null` for a threshold means "inherit from the parent level."
 */
export interface SaveDetectionData {
  painThreshold: number
  happyThreshold: number
  stageOverrides: DetectionStageOverride[]
  touchpointOverrides: DetectionTouchpointOverride[]
}
