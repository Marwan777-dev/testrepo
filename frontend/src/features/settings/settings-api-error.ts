// Error type for the Platform Settings API. Mirrors the M-06 KPI feature's KpiApiError: wraps the
// HTTP status and the API-05 envelope so callers can branch on `code` (e.g. ORGANIZATION_NAME_REQUIRED,
// LOGO_CONTENT_TYPE_UNSUPPORTED, LOGO_SVG_UNSAFE_CONTENT).

import type { ApiErrorEnvelope } from "./dto"

export class SettingsApiError extends Error {
  readonly status: number
  readonly envelope?: ApiErrorEnvelope

  constructor(status: number, envelope?: ApiErrorEnvelope) {
    super(envelope?.error?.message ?? `Settings API request failed (${status}).`)
    this.name = "SettingsApiError"
    this.status = status
    this.envelope = envelope
  }

  /** The API-05 error code, e.g. `ORGANIZATION_NAME_REQUIRED`; `undefined` for a non-envelope failure. */
  get code(): string | undefined {
    return this.envelope?.error?.code
  }
}
