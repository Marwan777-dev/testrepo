import type { ScoringDirection } from "./scoring-direction"

/**
 * A KPI type available to the tenant, normalized from `GET /api/v1/kpi-types`. The wire splits
 * platform-standard vs tenant-defined types into two arrays; `getKpiTypes` flattens both into
 * this single shape with `isPlatformStandard` set. Used by the KPI weight editor's type picker.
 */
export interface KpiType {
  /** Stable key used in KPI bindings, e.g. `NPS`, `CSAT`, `CES`, or a tenant key like `LOYALTY`. */
  typeKey: string
  labelAr: string
  labelEn: string
  scoringDirection: ScoringDirection
  /** `true` for the six built-in types, `false` for tenant-defined ones. */
  isPlatformStandard: boolean
  /** Present only for tenant-defined types (the `kpi_type_definitions` row id). */
  kpiTypeDefinitionId?: string
}
