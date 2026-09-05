# API Contract: Permissions, Baselines, Scope, and Audit (M-10)

**Module**: M-10 User and Role Management
**Date**: 2026-06-08

---

## Persona Baselines

### GET /api/v1/persona-baselines

Returns all persona baseline records for the current tenant.

**Required permission**: `UserManagement.View`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Response — 200 OK

```json
{
  "items": [
    {
      "baselineId": "uuid",
      "personaId": "P-01",
      "permissionModuleAssignments": [
        { "moduleId": "SurveyBuilder", "allowedModes": ["View", "Manage", "Full"] }
      ],
      "defaultDataScopeRules": {},
      "isCustomised": false,
      "updatedAt": "2026-06-08T10:00:00Z"
    }
  ]
}
```

---

### PUT /api/v1/persona-baselines/{personaId}

Updates the permission module assignments for a persona baseline. Only P-01 may update CX-domain modules; P-07 may update UserManagement and TenantConfiguration only.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Request

```json
{
  "permissionModuleAssignments": [
    { "moduleId": "SurveyBuilder", "allowedModes": ["View"] },
    { "moduleId": "UserManagement", "allowedModes": ["Full"] }
  ]
}
```

### Responses

**200 OK** — baseline updated, `isCustomised` set to true; `persona_baseline.updated` event published to M-17

```json
{
  "baselineId": "uuid",
  "personaId": "P-03",
  "isCustomised": true,
  "updatedAt": "2026-06-08T12:00:00Z"
}
```

**403 Forbidden** — P-07 attempting to update a CX-domain module in the baseline

---

## Data Scope

### GET /api/v1/users/{userId}/scope

Returns a user's current data scope: parameter assignments and hierarchy node.

**Required permission**: `UserManagement.View`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Response — 200 OK

```json
{
  "organizationNodeId": "uuid",
  "dataScopeAssignments": [
    { "parameterName": "branch", "allowedValues": ["Riyadh", "Dammam"] }
  ],
  "customRules": [
    {
      "ruleId": "uuid",
      "allowedActions": ["UpdateSurvey"],
      "parameterScopeAssignments": { "branch": ["Riyadh"] }
    }
  ]
}
```

---

### PUT /api/v1/users/{userId}/scope

Replaces a user's data scope assignments and hierarchy node.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Request

```json
{
  "organizationNodeId": "uuid-or-null",
  "dataScopeAssignments": [
    { "parameterName": "branch", "allowedValues": ["Riyadh"] }
  ]
}
```

### Responses

**200 OK** — scope saved; `scope.assigned` or `scope.modified` event published to M-17

**422 Unprocessable Entity** — `parameterName` not found in `data_scope_parameter_definitions` or value not in `allowedValues`

---

### POST /api/v1/users/{userId}/custom-rules

Creates a custom authorization rule for a user.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Request

```json
{
  "allowedActions": ["UpdateSurvey"],
  "parameterScopeAssignments": { "branch": ["Riyadh", "Dammam"] }
}
```

### Responses

**201 Created** — `custom_rule.created` event published to M-17

```json
{
  "ruleId": "uuid",
  "allowedActions": ["UpdateSurvey"],
  "parameterScopeAssignments": { "branch": ["Riyadh", "Dammam"] },
  "createdAt": "2026-06-08T12:00:00Z"
}
```

---

### PUT /api/v1/users/{userId}/custom-rules/{ruleId}

Updates a custom authorization rule.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Responses

**200 OK** — `custom_rule.updated` event published to M-17

---

### DELETE /api/v1/users/{userId}/custom-rules/{ruleId}

Deletes a custom authorization rule.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Responses

**204 No Content** — `custom_rule.deleted` event published to M-17

---

## M-13 Parameter Definitions Ingestion

### POST /api/v1/authorization/scope/parameters

Accepts M-13 parameter definitions. M-10 validates and persists them.

**Required permission**: internal service call (no user session; validated by service-identity header)
**Rate limit**: max 500 parameter definitions per payload

### Request

```json
{
  "sourceModule": "M-13",
  "parameters": [
    {
      "name": "branch",
      "label": "Branch",
      "allowedValues": ["Riyadh", "Jeddah", "Dammam"]
    }
  ]
}
```

### Responses

**200 OK** — definitions stored or updated

**400 Bad Request** — validation failure (reserved name, empty `allowedValues`, etc.)
```json
{
  "error": {
    "code": "scope.invalid_parameter_definition",
    "message": "...",
    "correlation_id": "uuid",
    "tenant_id": "uuid",
    "details": [{ "field": "parameters[0].allowedValues", "code": "empty" }]
  }
}
```

---

## Audit Log

### GET /api/v1/audit-log

Returns a chronological, append-only list of auditable events for the tenant. Read-only.

**Required permission**: `UserManagement.View`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `page_size` | int | Default 50, max 200 |
| `page_token` | string | Cursor for next page (opaque, time-ordered) |
| `event_type` | string | Filter by event type (e.g. `permission.modified`) |
| `from` | ISO 8601 | Start of date range (inclusive) |
| `to` | ISO 8601 | End of date range (inclusive) |
| `actor_id` | uuid | Filter by actor user ID |
| `entity_id` | uuid | Filter by affected entity ID |

### Response — 200 OK

```json
{
  "items": [
    {
      "eventId": "uuid",
      "eventType": "permission.modified",
      "actorId": "uuid",
      "actorUsername": "admin@example.com",
      "entityType": "PermissionModuleAssignment",
      "entityId": "uuid",
      "oldValue": { "moduleId": "SurveyBuilder", "allowedModes": ["View"] },
      "newValue": { "moduleId": "SurveyBuilder", "allowedModes": ["View", "Manage"] },
      "occurredAtUtc": "2026-06-08T12:00:00Z",
      "correlationId": "uuid"
    }
  ],
  "nextPageToken": "cursor_or_null",
  "totalCount": 150
}
```

**Notes**:
- Audit records are read-only; no POST/PUT/DELETE on this endpoint.
- The endpoint reads M-10's own audit events from the tenant-schema `event_log` — the same table M-10 writes them to. **M-10 owns the full audit cycle** (write + read); there is no external M-17 dependency (see "Audit ownership" below).
- `actorUsername` is resolved at read time; if the actor has been erased, it displays as `[erased]`.

#### Audit ownership — M-10-owned read port `IAuditLogReader`

`GET /api/v1/audit-log` (T117) reads through M-10's own reader over its `event_log`:

```csharp
Task<AuditLogPage> QueryEventsAsync(
    AuditLogFilter filter,   // EventType?, FromUtc?, ToUtc?, ActorId?, EntityId?
    int pageSize,            // forwarded; 1..200
    string? cursor,          // API-04 keyset cursor (newest-first)
    CancellationToken ct = default);
```

Defined at `Domain/Interfaces/IAuditLogReader.cs` (value objects `AuditLogFilter`,
`AuditLogEntry`, `AuditLogPage` under `Domain/ValueObjects/`) and implemented by
`Infrastructure/Audit/AuditLogReader.cs`, which queries `ITenantDbContext.EventLogs`
directly.

**Decision (supersedes the earlier M-17 plan):** audit ownership for M-10's events sits
with **M-10**, per SRS §6 ("M-10 maintains a complete and immutable audit trail"). The
previously-planned external **M-17 Audit module** was never defined or built, which left
the read path dangling (gap-analysis **I-02/I-04**). Resolution: M-10 reads its own
`event_log` it already writes to — the table is append-only and M-10-owned end-to-end.
If a platform-wide audit aggregator (across modules) is ever introduced, it can consume
the same `event_log`; that is out of scope for M-10/US4.
