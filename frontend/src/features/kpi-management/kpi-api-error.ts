import type { ApiErrorEnvelope } from "./dto"

/**
 * Thrown for any non-2xx response from the M-06 KPI API. Carries the HTTP `status` and the API-05
 * `code` so callers can branch on documented failures (e.g. 403 `auth.required`, and in later
 * stories 400 `KPI_VALIDATION_FAILED`, 409 `KPI_SCALE_CHANGE_AFFECTS_BINDINGS`).
 */
export class KpiApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId?: string
  readonly details?: ApiErrorEnvelope["error"]["details"]

  constructor(status: number, envelope?: ApiErrorEnvelope) {
    const err = envelope?.error
    super(err?.message ?? `Request failed with status ${status}`)
    this.name = "KpiApiError"
    this.status = status
    this.code = err?.code ?? "unknown_error"
    this.correlationId = err?.correlation_id
    this.details = err?.details
  }
}
