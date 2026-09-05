import type { ApiErrorEnvelope } from "./dto"

/**
 * Thrown for any non-2xx response from the persona-baselines API. Carries the HTTP
 * `status` (e.g. 403 when a P-07 actor targets a CX-domain module) and the API-05 `code`.
 */
export class PersonaBaselinesApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId?: string
  readonly details?: ApiErrorEnvelope["error"]["details"]

  constructor(status: number, envelope?: ApiErrorEnvelope) {
    const err = envelope?.error
    super(err?.message ?? `Request failed with status ${status}`)
    this.name = "PersonaBaselinesApiError"
    this.status = status
    this.code = err?.code ?? "unknown_error"
    this.correlationId = err?.correlation_id
    this.details = err?.details
  }
}
