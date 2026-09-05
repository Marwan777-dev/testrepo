// Transport layer for the Platform Settings API: `callJson` for JSON request/response and
// `callMultipart` for the logo upload (multipart/form-data). Both attach the Bearer token,
// normalize empty 2xx bodies to `undefined`, and turn API-05 error envelopes into `SettingsApiError`.
// Mirrors the M-06 KPI transport (features/kpi-management/http.ts).

import { getSessionToken } from "@/features/auth/session-token"
import type { ApiErrorEnvelope } from "./dto"
import { SettingsApiError } from "./settings-api-error"

const API_BASE = "/api/v1"

export interface CallOptions {
  method?: string
  body?: unknown
}

async function parseEnvelope(response: Response): Promise<ApiErrorEnvelope | undefined> {
  try {
    return (await response.json()) as ApiErrorEnvelope
  } catch {
    return undefined
  }
}

async function readBody<T>(response: Response): Promise<T> {
  if (response.status === 204) return undefined as T
  if (response.headers.get("content-length") === "0") return undefined as T
  const text = await response.text()
  if (text.length === 0) return undefined as T
  return JSON.parse(text) as T
}

/** Performs a JSON request against the Settings API. */
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
    throw new SettingsApiError(response.status, await parseEnvelope(response))
  }
  return readBody<T>(response)
}

/**
 * POSTs a `multipart/form-data` body (the logo upload). The boundary is set by the browser from the
 * `FormData`, so we attach only the Bearer header — never a manual `Content-Type`.
 */
export async function callMultipart<T>(path: string, form: FormData): Promise<T> {
  const headers: Record<string, string> = {}
  const token = getSessionToken()
  if (token) headers["Authorization"] = `Bearer ${token}`

  const response = await fetch(`${API_BASE}${path}`, { method: "POST", headers, body: form })

  if (!response.ok) {
    throw new SettingsApiError(response.status, await parseEnvelope(response))
  }
  return readBody<T>(response)
}
