/**
 * One audit event from `GET /api/v1/audit-log` (a read-only projection of an M-17
 * `event_log` row). `oldValue` / `newValue` are the stored JSON payloads (arbitrary
 * shape); `null` when the row carried none. `actorUsername` is resolved server-side and
 * is `"[erased]"` when the actor has been erased (GP-03).
 */
export interface AuditLogEntry {
  eventId: string
  eventType: string
  actorId: string | null
  actorUsername: string | null
  entityType: string | null
  entityId: string | null
  oldValue: unknown
  newValue: unknown
  occurredAtUtc: string
  correlationId: string | null
}
