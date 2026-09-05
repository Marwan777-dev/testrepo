import type { ApiErrorEnvelope } from "./dto"

/**
 * Thrown for any non-2xx response from the User Management API. Carries the HTTP
 * `status` (so callers can branch on 403/404/409/503) and the API-05 `code`.
 */
export class UsersApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId?: string
  readonly details?: ApiErrorEnvelope["error"]["details"]

  constructor(status: number, envelope?: ApiErrorEnvelope) {
    const err = envelope?.error
    super(err?.message ?? `Request failed with status ${status}`)
    this.name = "UsersApiError"
    this.status = status
    this.code = err?.code ?? "unknown_error"
    this.correlationId = err?.correlation_id
    this.details = err?.details
  }
}
