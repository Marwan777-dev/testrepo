import type { Persona } from "./persona"

/** Response of a successful `PUT /api/v1/persona-baselines/{personaId}`. */
export interface UpdatePersonaBaselineResponse {
  baselineId: string
  personaId: Persona
  isCustomised: boolean
  updatedAt: string
}
