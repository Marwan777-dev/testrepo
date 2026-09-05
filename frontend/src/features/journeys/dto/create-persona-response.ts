import type { PersonaStatus } from "./persona-status"

/** Response of `POST /api/v1/personas` (201 Created). */
export interface CreatePersonaResponse {
  personaId: string
  /** Always `Draft` for a freshly created persona. */
  status: PersonaStatus
  /** ISO-8601 UTC creation timestamp. */
  createdAt: string
}
