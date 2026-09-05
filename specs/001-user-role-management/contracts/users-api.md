# API Contract: User Management (M-10)

**Module**: M-10 User and Role Management
**Base path**: `/api/v1/users`
**Date**: 2026-06-08

All list endpoints use cursor-based pagination (API-04). All write endpoints publish an event to M-17 in the same transaction.

---

## GET /api/v1/users

Lists tenant users. Accessible by P-01 and P-07.

**Required permission**: `UserManagement.View`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `page_size` | int | Default 50, max 200 |
| `page_token` | string | Cursor for next page |
| `status` | string | Filter: `active` \| `inactive` \| `locked` \| `pending-enrollment` |
| `persona` | string | Filter: `P-01`..`P-08` |
| `q` | string | Free-text search on username |

### Response — 200 OK

```json
{
  "items": [
    {
      "userId": "uuid",
      "username": "alice@example.com",
      "persona": "P-01",
      "status": "active",
      "isMfaEnrolled": true,
      "organizationNodeId": "uuid",
      "createdAt": "2026-06-08T10:00:00Z",
      "updatedAt": "2026-06-08T10:00:00Z"
    }
  ],
  "nextPageToken": "cursor_string_or_null",
  "totalCount": 42
}
```

---

## POST /api/v1/users

Invites a new tenant user. Accessible by P-01 and P-07.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Request

```json
{
  "username": "bob@example.com",
  "persona": "P-03",
  "password": "Initial-P@ss1",
  "organizationNodeId": "uuid-optional"
}
```

`password` is **required** — the admin sets the new user's initial credential (FR-027
complexity). The user signs in with it and enrols MFA on first login (resolves gap-analysis
I-01). It is validated server-side and stored as a bcrypt hash; never echoed back.

### Responses

**201 Created** — user created with status `pending-enrollment`; `user.created` event published (M-10 `event_log`)

```json
{
  "userId": "uuid",
  "username": "bob@example.com",
  "persona": "P-03",
  "status": "pending-enrollment",
  "isMfaEnrolled": false,
  "createdAt": "2026-06-08T10:00:00Z"
}
```

**403 Forbidden** — actor does not have `UserManagement.Manage` (e.g. P-02..P-06, P-08)

**409 Conflict** — username already exists within the tenant
```json
{
  "error": { "code": "users.username_conflict", "message": "...", "correlation_id": "uuid", "tenant_id": "uuid" }
}
```

**422 Unprocessable Entity** — invalid email format, unknown persona, or a weak initial password
```json
{
  "error": { "code": "users.weak_password", "message": "...", "details": [{ "field": "password", "code": "min_length" }] }
}
```

---

## GET /api/v1/users/{userId}

Returns a single user's profile and current permission summary.

**Required permission**: `UserManagement.View`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Response — 200 OK

```json
{
  "userId": "uuid",
  "username": "alice@example.com",
  "persona": "P-01",
  "status": "active",
  "isMfaEnrolled": true,
  "organizationNodeId": "uuid",
  "permissionModuleAssignments": [
    { "moduleId": "SurveyBuilder", "allowedModes": ["View", "Manage"] }
  ],
  "customAuthorizationRules": [
    { "ruleId": "uuid", "allowedActions": ["UpdateSurvey"], "parameterScopeAssignments": {} }
  ],
  "dataScopeAssignments": [
    { "parameterName": "branch", "allowedValues": ["Riyadh", "Dammam"] }
  ],
  "createdAt": "2026-06-08T10:00:00Z",
  "updatedAt": "2026-06-08T10:00:00Z"
}
```

**403 Forbidden** — insufficient permission
**404 Not Found** — user does not exist or is out of scope (indistinguishable)

---

## PUT /api/v1/users/{userId}

Updates user profile (persona, organizationNodeId). Only P-01 may change persona.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01 (full), P-07 (lifecycle only, not persona change)

### Request

```json
{
  "persona": "P-04",
  "organizationNodeId": "uuid-or-null"
}
```

### Responses

**200 OK** — user updated; `user.updated` event published to M-17

**403 Forbidden** — P-07 attempting to change persona

---

## POST /api/v1/users/{userId}/deactivate

Soft-deletes a user (sets status to `inactive`).

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Request

Empty body.

### Responses

**204 No Content** — `user.deactivated` event published to M-17; all active sessions for the user are revoked

---

## POST /api/v1/users/{userId}/reactivate

Re-activates an inactive user.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Responses

**204 No Content** — `user.reactivated` event published to M-17

---

## POST /api/v1/users/{userId}/unlock

Manually unlocks a locked user account before the cooldown expires.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Responses

**204 No Content** — `authentication.account.unlocked` event published to M-17

**409 Conflict** — user is not locked

---

## POST /api/v1/users/{userId}/mfa-reset

Admin-triggered MFA reset. Sets `isMfaEnrolled = false`; user must re-enroll on next login. Calls M-09 to notify user.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Responses

**204 No Content** — `mfa.reset` event published to M-17

---

## POST /api/v1/users/{userId}/password-reset

Admin-triggered password reset. Sets `requiresPasswordChange = true`; calls M-09 synchronously to notify user.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01, P-07

### Responses

**204 No Content** — `password.reset.requested` event published to M-17

**503 Service Unavailable** — M-09 unavailable; no state change persisted

---

## PUT /api/v1/users/{userId}/permissions

Replaces a user's permission module assignments. Only P-01 may assign CX-domain modules; P-07 may assign UserManagement and TenantConfiguration only.

**Required permission**: `UserManagement.Manage`
**Required scope**: `organisation`
**Default personas**: P-01 (all modules), P-07 (UserManagement, TenantConfiguration only)

### Request

```json
{
  "assignments": [
    { "moduleId": "SurveyBuilder", "allowedModes": ["View", "Manage"] },
    { "moduleId": "UserManagement", "allowedModes": ["Full"] }
  ]
}
```

### Responses

**200 OK** — assignments replaced; `permission.modified` event published to M-17; `lastPermissionSnapshotVersion` incremented

```json
{
  "assignments": [
    { "assignmentId": "uuid", "moduleId": "SurveyBuilder", "allowedModes": ["View", "Manage"] }
  ]
}
```

**403 Forbidden** — P-07 attempting to assign a CX-domain module
```json
{
  "error": { "code": "permissions.forbidden_module", "message": "...", "correlation_id": "uuid", "tenant_id": "uuid" }
}
```
