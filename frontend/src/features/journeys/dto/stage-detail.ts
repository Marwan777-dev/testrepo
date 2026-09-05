import type { TouchpointDetail } from "./touchpoint-detail"

/** A stage as embedded in the journey detail tree, with its ordered touchpoints. */
export interface StageDetail {
  stageId: string
  /** 1-based position within the journey. */
  sequenceNumber: number
  name: string
  description?: string | null
  customerGoal?: string | null
  /** Expected customer emotion at this stage, e.g. `excited`, `anxious`. */
  expectedEmotion?: string | null
  /** Free-text duration hint, e.g. `2–5 minutes`. */
  durationHint?: string | null
  touchpoints: TouchpointDetail[]
}
