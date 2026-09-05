// Transport layer for the M-13 Integration Hub console API. Mirrors the M-06 KPI transport
// (`features/kpi-management/http.ts`): one `callJson` helper that attaches the Bearer token,
// normalizes empty 2xx bodies to `undefined`, and turns API-05 error envelopes into
// `IntegrationHubApiError`.
//
// Two additions M-13 needs that the KPI transport doesn't have:
//   - `callBlob`  — the mapping export (FR-S7-05) and log export (FR-S8-04) return files, not JSON.
//   - `callUpload` — the mapping import (FR-S7-06) posts multipart form data, so the
//                    `Content-Type` header must be left OFF for the browser to set the boundary.
//
// CLAUDE.md "Backend Integration" §4 is why the empty-2xx handling is explicit: .NET endpoints
// returning `Ok()`/`NoContent()` send a 2xx with no body and no content-type.

import { getSessionToken } from "@/features/auth/session-token"
import type { ApiErrorEnvelope } from "./dto"
import { IntegrationHubApiError } from "./integration-hub-api-error"

const API_BASE = "/api/v1/integration-hub"

export interface CallOptions {
  method?: string
  body?: unknown
}

/** Attaches `Authorization: Bearer <token>` when a session token is present. */
function authHeaders(): Record<string, string> {
  const headers: Record<string, string> = {}
  const token = getSessionToken()
  if (token) headers["Authorization"] = `Bearer ${token}`
  return headers
}

/** Parses an API-05 envelope off a failed response and throws the typed error. */
async function throwApiError(response: Response): Promise<never> {
  let envelope: ApiErrorEnvelope | undefined
  try {
    envelope = (await response.json()) as ApiErrorEnvelope
  } catch {
    // Non-JSON error body — fall through with the status only.
  }
  throw new IntegrationHubApiError(response.status, envelope)
}

/**
 * Performs a JSON request against the Integration Hub console API.
 *
 * - Treats 204, and any 2xx with an empty body, as `undefined`.
 * - On non-2xx, parses the API-05 envelope (if any) and throws `IntegrationHubApiError`.
 *
 * @param path Resource sub-path beginning with `/`, e.g. `/service-channels`.
 */
export async function callJson<T>(path: string, opts: CallOptions = {}): Promise<T> {
  const headers = authHeaders()

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

  if (!response.ok) return throwApiError(response)

  if (response.status === 204) return undefined as T
  if (response.headers.get("content-length") === "0") return undefined as T
  const text = await response.text()
  if (text.length === 0) return undefined as T
  return JSON.parse(text) as T
}

/**
 * Downloads a binary/file response (Excel export FR-S7-05, log export FR-S8-04). Returns the blob
 * plus the server-supplied filename parsed off `Content-Disposition`, so the caller can trigger a
 * download without hard-coding a name.
 */
export async function callBlob(
  path: string,
  fallbackFilename: string,
): Promise<{ blob: Blob; filename: string }> {
  const response = await fetch(`${API_BASE}${path}`, { headers: authHeaders() })
  if (!response.ok) return throwApiError(response)

  const disposition = response.headers.get("content-disposition") ?? ""
  // Prefer RFC 5987 `filename*=UTF-8''…` (carries Arabic names correctly), then plain `filename=`.
  const extended = /filename\*=UTF-8''([^;]+)/i.exec(disposition)
  const plain = /filename="?([^";]+)"?/i.exec(disposition)
  const filename = extended
    ? decodeURIComponent(extended[1])
    : (plain?.[1] ?? fallbackFilename)

  return { blob: await response.blob(), filename }
}

/**
 * Posts multipart form data (the Excel mapping import, FR-S7-06). The `Content-Type` header is
 * deliberately NOT set — the browser must generate it with the multipart boundary.
 *
 * On a validation failure the server answers 400/422 carrying the row-level report (VR-F09); that
 * report rides on `IntegrationHubApiError.details`, so an all-or-nothing rejection is a thrown
 * error the caller catches, not a success shape.
 */
export async function callUpload<T>(path: string, form: FormData): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    headers: authHeaders(),
    body: form,
  })

  if (!response.ok) return throwApiError(response)

  if (response.status === 204) return undefined as T
  const text = await response.text()
  if (text.length === 0) return undefined as T
  return JSON.parse(text) as T
}

/**
 * Builds a `?a=1&b=2` suffix, skipping `undefined`/empty values so an unset filter never reaches
 * the server as a blank query param.
 */
export function toQuery(params: Record<string, string | number | boolean | undefined>): string {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined) continue
    if (typeof value === "string" && value.trim() === "") continue
    query.set(key, String(value))
  }
  const serialized = query.toString()
  return serialized ? `?${serialized}` : ""
}
