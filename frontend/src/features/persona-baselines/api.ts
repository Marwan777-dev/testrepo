// M-10 Persona Baselines API client — thin endpoint functions over `callJson`.
//
// Endpoints follow specs/001-user-role-management/contracts/permissions-api.md. Wire
// types live in ./dto, the transport helper in ./http, the error type in
// ./persona-baselines-api-error. Those are re-exported here so callers can import
// everything persona-baselines-API from "@/features/persona-baselines/api".

import { callJson } from "./http"
import type {
  PersonaBaselineListResponse,
  UpdatePersonaBaselineData,
  UpdatePersonaBaselineResponse,
} from "./dto"

export type * from "./dto"
export { PersonaBaselinesApiError } from "./persona-baselines-api-error"

/** Lists all persona authorization-matrix baselines for the tenant (P-01..P-08). */
export function listPersonaBaselines(): Promise<PersonaBaselineListResponse> {
  return callJson<PersonaBaselineListResponse>("")
}

/**
 * Replaces a persona baseline's module assignments (P-01/P-07; a P-07 actor including
 * a CX-domain module is rejected with 403). Flips the baseline to customised.
 */
export function updatePersonaBaseline(
  personaId: string,
  data: UpdatePersonaBaselineData,
): Promise<UpdatePersonaBaselineResponse> {
  return callJson<UpdatePersonaBaselineResponse>(`/${personaId}`, { method: "PUT", body: data })
}
