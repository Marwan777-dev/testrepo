import type { AuditLogEntry } from "./audit-log-entry"

/**
 * Response of `GET /api/v1/audit-log` — one cursor-paginated page (API-04).
 * `totalCount` is `null` under cursor pagination (the M-17 reader exposes no count).
 */
export interface AuditLogResponse {
  items: AuditLogEntry[]
  nextPageToken: string | null
  totalCount: number | null
}
