// Transport layer for the auth API: a single `callJson` helper that attaches
// the Bearer token, normalizes empty 2xx bodies to `undefined`, and turns
// API-05 error envelopes into `AuthApiError`.

import type { ApiErrorEnvelope } from "./dto"
import { AuthApiError } from "./auth-api-error"
import { getSessionToken } from "./session-token"

const API_BASE = "/api/v1/auth"

export interface CallOptions {
  method?: string
  body?: unknown
}

/**
 * Performs a JSON request against the auth API.
 *
 * - Sends `Authorization: Bearer <token>` when a session token is present.
 * - Treats 204, and any 2xx with an empty body, as `undefined`.
 * - On non-2xx, parses the API-05 envelope (if any) and throws `AuthApiError`.
 */
export async function callJson<T>(path: string, opts: CallOptions = {}): Promise<T> {
  const headers: Record<string, string> = {}
  const token = getSessionToken()
  if (token) headers["Authorization"] = `Bearer ${token}`

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
      // Non-JSON error body — fall through with status only.
    }
    throw new AuthApiError(response.status, envelope)
  }

  // 204 No Content, or a 2xx with an empty body (Ok()/NoContent() send no
  // content-type header) → resolve as undefined.
  if (response.status === 204) return undefined as T
  if (response.headers.get("content-length") === "0") return undefined as T
  const text = await response.text()
  if (text.length === 0) return undefined as T
  return JSON.parse(text) as T
}
