import type { TouchpointImportance } from "./touchpoint-importance"

/** Request body for `updateTouchpoint` (`PUT /api/v1/touchpoints/{id}`); same shape as add. */
export interface UpdateTouchpointData {
  name: string
  description?: string | null
  channels?: string[]
  importance?: TouchpointImportance
  /** Moment of Truth flag. */
  isMoT?: boolean
  isMandatory?: boolean
}
