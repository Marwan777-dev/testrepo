import type { ScoringDirection } from "./scoring-direction"

/** A persisted KPI binding as returned by `PUT /api/v1/touchpoints/{id}/kpis` (200 OK). */
export interface SavedKpiBinding {
  kpiBindingId: string
  kpiType: string
  weight: number
  isPlatformStandard: boolean
  scoringDirection: ScoringDirection
}
