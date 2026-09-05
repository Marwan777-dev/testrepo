import type { ApiErrorEnvelope } from "./dto"

/**
 * Thrown for any non-2xx response. Carries the API-05 `code` so callers can
 * branch (e.g. `auth.account_locked`, `auth.mfa.invalid_code`) and the HTTP
 * `status` so the session layer can react to 401s.
 */
export class AuthApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId?: string
  readonly details?: ApiErrorEnvelope["error"]["details"]
  readonly retryAfter?: number

  constructor(status: number, envelope?: ApiErrorEnvelope) {
    const err = envelope?.error
    super(err?.message ?? `Request failed with status ${status}`)
    this.name = "AuthApiError"
    this.status = status
    this.code = err?.code ?? "unknown_error"
    this.correlationId = err?.correlation_id
    this.details = err?.details
    this.retryAfter = envelope?.retryAfter
  }
}
