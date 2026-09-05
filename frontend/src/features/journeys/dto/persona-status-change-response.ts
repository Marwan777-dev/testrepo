import type { PersonaStatus } from "./persona-status"

/** Response of `PATCH /api/v1/personas/{id}/status` (200 OK). */
export interface PersonaStatusChangeResponse {
  personaId: string
  /** The new lifecycle status after the transition. */
  status: PersonaStatus
  /** ISO-8601 UTC timestamp of the transition. */
  updatedAt: string
}
