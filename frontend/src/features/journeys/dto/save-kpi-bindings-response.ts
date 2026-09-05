import type { SavedKpiBinding } from "./saved-kpi-binding"

/** Response of `PUT /api/v1/touchpoints/{id}/kpis` (200 OK). */
export interface SaveKpiBindingsResponse {
  touchpointId: string
  /** The persisted, authoritative binding set (empty when the touchpoint is unmeasured). */
  kpiBindings: SavedKpiBinding[]
  /** `true` once the touchpoint has at least one binding. */
  isMeasured: boolean
  /**
   * Non-blocking indicator: `true` when `NPS` is in the binding set. The UI shows an
   * informational banner (the save still succeeds with 200).
   */
  npsWarning: boolean
  /** ISO-8601 UTC timestamp of the save. */
  updatedAt: string
}
