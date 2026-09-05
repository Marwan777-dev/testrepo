import type { PersonaStatus } from "./persona-status"

/**
 * Request body for `changePersonaStatus` (`PATCH /api/v1/personas/{id}/status`). Valid
 * transitions: Draft→Active, Active→Inactive, Inactive→Active, any non-Archived→Archived.
 * Out-of-Archived transitions are rejected with `persona.archived_terminal`; archiving a persona
 * with active journey bindings is rejected with `persona.archive_blocked_active_bindings` (409).
 */
export interface ChangePersonaStatusData {
  status: PersonaStatus
}
