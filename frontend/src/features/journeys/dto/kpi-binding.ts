/**
 * A KPI bound to a touchpoint, as embedded in the journey detail tree. Weights across a
 * touchpoint's bindings sum to 100 (enforced server-side in US-2). Empty for unmeasured
 * touchpoints.
 */
export interface KpiBinding {
  /** KPI type key, e.g. `NPS`, `CSAT`, `CES`. */
  kpiType: string
  /** Weight percentage (1–100); the set sums to 100 for a measured touchpoint. */
  weight: number
  /** `true` for platform-standard KPI types, `false` for tenant-defined ones. */
  isPlatformStandard: boolean
}
