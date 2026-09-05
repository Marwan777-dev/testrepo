import type { ApiErrorEnvelope } from "./dto"

/**
 * Thrown for any non-2xx response from the data-scope API. Carries the HTTP `status`
 * (e.g. 422 when a parameter value is not in its definition, 403 for a non-admin) and
 * the API-05 `code` and field-level `details`.
 */
export class DataScopeApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId?: string
  readonly details?: ApiErrorEnvelope["error"]["details"]

  constructor(status: number, envelope?: ApiErrorEnvelope) {
    const err = envelope?.error
    super(err?.message ?? `Request failed with status ${status}`)
    this.name = "DataScopeApiError"
    this.status = status
    this.code = err?.code ?? "unknown_error"
    this.correlationId = err?.correlation_id
    this.details = err?.details
  }
}
