// M-10 Audit Log API client — thin endpoint functions over `callJson`.
//
// Endpoint follows specs/001-user-role-management/contracts/permissions-api.md. Wire
// types live in ./dto, the transport helper in ./http, the error type in
// ./audit-log-api-error. Those are re-exported here so callers can import everything
// audit-log-API from "@/features/audit-log/api".
//
// Read-only: the audit trail is append-only and exposes no write verbs.

import { callJson } from "./http"
import type { AuditLogResponse, ListAuditEventsParams } from "./dto"

export type * from "./dto"
export { AuditLogApiError } from "./audit-log-api-error"

/** Lists the tenant's audit events (cursor-paginated) with optional type/date/actor/entity filters. */
export function listAuditEvents(params: ListAuditEventsParams = {}): Promise<AuditLogResponse> {
  const query = new URLSearchParams()
  if (params.pageSize != null) query.set("page_size", String(params.pageSize))
  if (params.pageToken) query.set("page_token", params.pageToken)
  if (params.eventType) query.set("event_type", params.eventType)
  if (params.from) query.set("from", params.from)
  if (params.to) query.set("to", params.to)
  if (params.actorId) query.set("actor_id", params.actorId)
  if (params.entityId) query.set("entity_id", params.entityId)
  const qs = query.toString()
  return callJson<AuditLogResponse>(qs ? `?${qs}` : "")
}
