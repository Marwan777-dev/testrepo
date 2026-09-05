import type { ApiErrorEnvelope } from "./dto"

/**
 * Thrown for any non-2xx response from the journeys/stages/touchpoints API. Carries the HTTP
 * `status` and the API-05 `code` so callers can branch on the documented failures, e.g.:
 *
 * - `409` + `journey.name_conflict` — a non-Archived journey already uses the name (POST/PUT).
 * - `403` + `journey.archived_immutable` — the journey is Archived and cannot be mutated.
 * - `422` + `journey.invalid_transition` / `journey.archived_terminal` — illegal status change.
 * - `409` + `journey.stage_has_touchpoints` — stage delete blocked while it owns touchpoints.
 * - `422` + `journey.stage_limit_reached` / `journey.touchpoint_limit_reached` — tenant limit hit.
 */
export class JourneysApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId?: string
  readonly details?: ApiErrorEnvelope["error"]["details"]

  constructor(status: number, envelope?: ApiErrorEnvelope) {
    const err = envelope?.error
    super(err?.message ?? `Request failed with status ${status}`)
    this.name = "JourneysApiError"
    this.status = status
    this.code = err?.code ?? "unknown_error"
    this.correlationId = err?.correlation_id
    this.details = err?.details
  }

  /** True when this is the 409 name-conflict raised by journey create/update. */
  get isNameConflict(): boolean {
    return this.status === 409 && this.code === "journey.name_conflict"
  }

  /** True when the journey is Archived and the attempted mutation was rejected (403). */
  get isArchivedImmutable(): boolean {
    return this.status === 403 && this.code === "journey.archived_immutable"
  }
}
