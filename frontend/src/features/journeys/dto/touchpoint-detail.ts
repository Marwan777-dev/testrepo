import type { KpiBinding } from "./kpi-binding"
import type { TouchpointImportance } from "./touchpoint-importance"

/** A touchpoint as embedded in the journey detail tree (`GET /api/v1/journeys/{id}`). */
export interface TouchpointDetail {
  touchpointId: string
  name: string
  channels: string[]
  importance: TouchpointImportance
  /** Moment of Truth flag. */
  isMoT: boolean
  isMandatory: boolean
  /** `true` once the touchpoint has at least one KPI binding (computed in US-2). */
  isMeasured: boolean
  /** KPI bindings; empty for an unmeasured touchpoint. */
  kpiBindings: KpiBinding[]
}
