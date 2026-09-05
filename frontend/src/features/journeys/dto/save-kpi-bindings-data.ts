/**
 * Request body for `PUT /api/v1/touchpoints/{id}/kpis` — the complete, authoritative set of KPI
 * bindings for the touchpoint (a full replace; existing bindings are deleted and re-inserted).
 * An empty `kpiBindings` array saves an unmeasured touchpoint. When non-empty, every `weight` is
 * an integer in `[1, 100]`, the set sums to exactly `100`, and `kpiType` values are unique.
 */
export interface SaveKpiBindingsData {
  kpiBindings: { kpiType: string; weight: number }[]
}
