import type { ApiErrorEnvelope } from "./dto"

/**
 * Thrown for any non-2xx response from the audit-log API. Carries the HTTP `status`
 * (e.g. 403 for a non-admin persona, 400 for an invalid page size) and the API-05
 * `code` and `correlationId`.
 */
export class AuditLogApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId?: string

  constructor(status: number, envelope?: ApiErrorEnvelope) {
    const err = envelope?.error
    super(err?.message ?? `Request failed with status ${status}`)
    this.name = "AuditLogApiError"
    this.status = status
    this.code = err?.code ?? "unknown_error"
    this.correlationId = err?.correlation_id
  }
}
