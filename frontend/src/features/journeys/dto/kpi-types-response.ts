import type { ScoringDirection } from "./scoring-direction"

/**
 * Raw `GET /api/v1/kpi-types` response: the six platform-standard built-ins plus the tenant's
 * custom KPI types. `getKpiTypes` flattens both arrays into a single `KpiType[]`, so consumers
 * never touch this wire shape directly.
 */
export interface KpiTypesResponse {
  platformStandardTypes: {
    typeKey: string
    labelAr: string
    labelEn: string
    scoringDirection: ScoringDirection
  }[]
  tenantDefinedTypes: {
    kpiTypeDefinitionId: string
    typeKey: string
    labelAr: string
    labelEn: string
    scoringDirection: ScoringDirection
  }[]
}
