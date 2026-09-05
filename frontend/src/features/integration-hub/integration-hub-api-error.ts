import type { ApiErrorEnvelope } from "./dto"

/**
 * Thrown for any non-2xx response from the M-13 Integration Hub **console** API
 * (`/api/v1/integration-hub/...`). Carries the HTTP `status` and the API-05 `code` so callers can
 * branch on the documented failures in contracts/api-endpoints.md — e.g. 409
 * `validation.duplicate_channel_id` (VR-F04), 409 `channel.id_locked` (BR-05), 409
 * `parameter.type_locked` (`[PO-G27]`), 400 `validation.capacity_exceeded` (VR-F13).
 *
 * The inbound scenario API (`/v1/survey-requests/{channelId}`, …) uses a **different** envelope
 * (`{ result_code, message, request_id }`, F0.3) and is never called from the console SPA — this
 * error type is console-only.
 */
export class IntegrationHubApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId?: string
  readonly details?: ApiErrorEnvelope["error"]["details"]

  constructor(status: number, envelope?: ApiErrorEnvelope) {
    const err = envelope?.error
    super(err?.message ?? `Request failed with status ${status}`)
    this.name = "IntegrationHubApiError"
    this.status = status
    this.code = err?.code ?? "unknown_error"
    this.correlationId = err?.correlation_id
    this.details = err?.details
  }
}
