// Transport layer for the M-01 Survey Builder API with first-class ETag support (Q1:
// explicit Save + optimistic concurrency — no autosave). Mirrors the M-06/M-16 `callJson`
// pattern (Bearer token, empty-2xx → undefined, API-05 envelope → typed error) and adds:
//
// - `ETag` response-header capture on every call (`EtagResult<T>` return shape),
// - `If-Match` request header on writes (`ifMatch` option),
// - `Idempotency-Key` header on creates (`idempotencyKey` option),
// - a stale-write conflict (412, or 409 with an API-05 `<aggregate>.conflict` code)
//   thrown as `ETagConflictError` so the UI can open `EtagConflictDialog` (T099).
//
// T085 (`surveys-api.ts`) builds the typed per-route wrappers on top of this file.

import { getSessionToken } from "@/features/auth/session-token"

const API_BASE = "/api/v1"

/** API-05 error envelope shape produced by `ApiErrorEnvelopeMiddleware` (T025). */
export interface ApiErrorEnvelope {
  error: {
    code: string
    message: string
    correlation_id?: string
    tenant_id?: string
    details?: Record<string, unknown>
  }
}

/**
 * Thrown for any non-2xx response from the M-01 Survey API. Carries the HTTP `status` and
 * the API-05 `code` so callers can branch on documented failures (e.g. 409
 * `publish.requires_content`, 409 `survey.pause.requires_rules_confirmation`).
 */
export class SurveysApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId?: string
  readonly details?: ApiErrorEnvelope["error"]["details"]

  constructor(status: number, envelope?: ApiErrorEnvelope) {
    const err = envelope?.error
    super(err?.message ?? `Request failed with status ${status}`)
    this.name = "SurveysApiError"
    this.status = status
    this.code = err?.code ?? "unknown_error"
    this.correlationId = err?.correlation_id
    this.details = err?.details
  }
}

/**
 * A write hit a stale ETag (another editor saved first — Q8 team-owned Drafts make this
 * a normal flow, not an edge case). The UI catches this to open `EtagConflictDialog`
 * offering "Reload latest" / "Copy my changes".
 */
export class ETagConflictError extends SurveysApiError {
  constructor(status: number, envelope?: ApiErrorEnvelope) {
    super(status, envelope)
    this.name = "ETagConflictError"
  }
}

/** A response body paired with the `ETag` header captured from the same response. */
export interface EtagResult<T> {
  data: T
  /** Weak ETag exactly as sent by the server (e.g. `W/"7"`), or null when absent. */
  etag: string | null
}

export interface EtagCallOptions {
  method?: string
  body?: unknown
  /** Sent as `If-Match` — required on every mutating call per Q1. */
  ifMatch?: string
  /** Sent as `Idempotency-Key` — required on creates per APIs-constitution Art. 7.1. */
  idempotencyKey?: string
}

/** Formats a `row_version` into the weak-ETag wire shape the backend emits (T023). */
export function formatETag(rowVersion: number): string {
  return `W/"${rowVersion}"`
}

/**
 * Performs a JSON request against the M-01 Survey API, returning the parsed body together
 * with the response's `ETag` header.
 *
 * - Sends `Authorization: Bearer <token>` when a session token is present.
 * - Sends `If-Match` / `Idempotency-Key` when provided in options.
 * - Treats 204, and any 2xx with an empty body, as `undefined` data.
 * - On 412 — or 409 whose API-05 code ends in `.conflict` (the EtagMiddleware wire
 *   shape) — throws `ETagConflictError`.
 * - On any other non-2xx, parses the API-05 envelope and throws `SurveysApiError`.
 *
 * @param path Resource sub-path beginning with `/`, e.g. `/surveys`.
 */
export async function callJsonWithEtag<T>(
  path: string,
  opts: EtagCallOptions = {}
): Promise<EtagResult<T>> {
  const headers: Record<string, string> = {}
  const token = getSessionToken()
  if (token) headers["Authorization"] = `Bearer ${token}`
  if (opts.ifMatch) headers["If-Match"] = opts.ifMatch
  if (opts.idempotencyKey) headers["Idempotency-Key"] = opts.idempotencyKey

  let body: string | undefined
  if (opts.body !== undefined) {
    headers["Content-Type"] = "application/json"
    body = JSON.stringify(opts.body)
  }

  const response = await fetch(`${API_BASE}${path}`, {
    method: opts.method ?? (opts.body !== undefined ? "POST" : "GET"),
    headers,
    body,
  })

  if (!response.ok) {
    let envelope: ApiErrorEnvelope | undefined
    try {
      envelope = (await response.json()) as ApiErrorEnvelope
    } catch {
      // Non-JSON error body — fall through with the status only.
    }
    const isConflict =
      response.status === 412 ||
      (response.status === 409 && (envelope?.error.code.endsWith(".conflict") ?? false))
    if (isConflict) throw new ETagConflictError(response.status, envelope)
    throw new SurveysApiError(response.status, envelope)
  }

  const etag = response.headers.get("ETag")
  if (response.status === 204) return { data: undefined as T, etag }
  if (response.headers.get("content-length") === "0") return { data: undefined as T, etag }
  const text = await response.text()
  if (text.length === 0) return { data: undefined as T, etag }
  return { data: JSON.parse(text) as T, etag }
}

/**
 * Convenience wrapper matching the plain `callJson` shape (data only) for reads that
 * don't care about the ETag. Writes should always go through `callJsonWithEtag` so the
 * refreshed ETag is captured.
 */
export async function callJson<T>(path: string, opts: EtagCallOptions = {}): Promise<T> {
  const { data } = await callJsonWithEtag<T>(path, opts)
  return data
}
