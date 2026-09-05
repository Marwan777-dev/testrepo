/** Query parameters for `listAuditEvents` (maps to the `GET /api/v1/audit-log` query string). */
export interface ListAuditEventsParams {
  pageSize?: number
  pageToken?: string
  /** Filter by event type, e.g. `permission.modified`. */
  eventType?: string
  /** Start of the date range (inclusive), ISO 8601. */
  from?: string
  /** End of the date range (inclusive), ISO 8601. */
  to?: string
  actorId?: string
  entityId?: string
}
