import type { TouchpointImportance } from "./touchpoint-importance"

/**
 * Request body for `addTouchpoint` (`POST /api/v1/stages/{stageId}/touchpoints`). Only `name`
 * is required; `importance` defaults to `Medium` server-side when omitted.
 */
export interface AddTouchpointData {
  name: string
  description?: string | null
  channels?: string[]
  importance?: TouchpointImportance
  /** Moment of Truth flag. */
  isMoT?: boolean
  isMandatory?: boolean
}
