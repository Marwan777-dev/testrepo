# API Contract: Authentication (M-10)

**Module**: M-10 User and Role Management
**Base path**: `/api/v1/auth`
**Date**: 2026-06-08

All endpoints return the API-05 error envelope on non-2xx responses. All endpoints are versioned under `/api/v1/`.

---

## POST /api/v1/auth/login

Validates username and password. Does NOT create a session. Returns a challenge ID for the MFA step.

**Required permission**: none (public endpoint)
**Required scope**: n/a
**Default personas**: all

### Request

```json
{
  "username": "alice@example.com",
  "password": "ValidP@ss1"
}
```

### Responses

**200 OK — MFA challenge pending**
```json
{
  "challengeId": "uuid",
  "requiresMfaEnrollment": false
}
```

**200 OK — MFA enrollment required (first-time user or reset MFA)**
```json
{
  "challengeId": "uuid",
  "requiresMfaEnrollment": true
}
```

**401 Unauthorized** — invalid credentials (never reveals which field failed)
```json
{
  "error": { "code": "auth.invalid_credentials", "message": "...", "correlation_id": "uuid", "tenant_id": "uuid" }
}
```

**423 Locked** — account locked
```json
{
  "error": { "code": "auth.account_locked", "message": "...", "correlation_id": "uuid", "tenant_id": "uuid" }
}
```

**Notes**:
- `challengeId` is a short-lived (5-minute TTL) opaque token stored server-side; it gates the MFA step.
- On the 5th consecutive failure the response becomes **423** and the audit event `authentication.account.locked` is published to M-17 in the same transaction.
- Auto-unlock: on subsequent login after `lockedUntilUtc` has elapsed, the account is unlocked before credential validation proceeds; the `authentication.account.unlocked` event is published.

---

## POST /api/v1/auth/mfa/enroll

Initiates TOTP enrollment for a first-time user or a user with reset MFA.

**Required permission**: valid `challengeId` from login step (no session required)
**Required scope**: n/a

### Request

```json
{
  "challengeId": "uuid"
}
```

### Responses

**200 OK**
```json
{
  "otpauthUri": "otpauth://totp/Nabadat:alice%40example.com?secret=BASE32SECRET&issuer=Nabadat",
  "base32Secret": "JBSWY3DPEHPK3PXP",
  "enrollmentToken": "uuid"
}
```

`enrollmentToken` is required by `POST /api/v1/auth/mfa/enroll/confirm`.

**400 Bad Request** — challengeId expired or invalid

---

## POST /api/v1/auth/mfa/enroll/confirm

Confirms enrollment by verifying the first TOTP code entered from the authenticator app.

**Required permission**: valid `enrollmentToken` from enroll step

### Request

```json
{
  "enrollmentToken": "uuid",
  "totpCode": "123456"
}
```

### Responses

**200 OK — enrollment confirmed; session created**
```json
{
  "sessionToken": "nbd_XXXXXXXXXXXXXXXX",
  "userId": "uuid",
  "expiresAtUtc": "2026-06-09T12:00:00Z",
  "permissionSnapshot": { ... }
}
```

**422 Unprocessable Entity** — invalid TOTP code
```json
{
  "error": { "code": "auth.mfa.invalid_code", "message": "...", "correlation_id": "uuid", "tenant_id": "uuid" }
}
```

---

## POST /api/v1/auth/mfa/verify

Completes the MFA challenge after a successful password step.

**Required permission**: valid `challengeId` from login step

### Request

```json
{
  "challengeId": "uuid",
  "totpCode": "123456"
}
```

### Responses

**200 OK — session created**
```json
{
  "sessionToken": "nbd_XXXXXXXXXXXXXXXX",
  "userId": "uuid",
  "expiresAtUtc": "2026-06-09T12:00:00Z",
  "permissionSnapshot": { ... }
}
```

**422 Unprocessable Entity** — invalid TOTP code (increments `failedAttemptCount`)
```json
{
  "error": { "code": "auth.mfa.invalid_code", "message": "...", "correlation_id": "uuid", "tenant_id": "uuid" }
}
```

**423 Locked** — account locked after this failure

---

## POST /api/v1/auth/logout

Invalidates the current session.

**Required permission**: authenticated session
**Required scope**: own
**Default personas**: all

### Request

`Authorization: Bearer <sessionToken>` header only; no body.

### Responses

**204 No Content** — session revoked; `session.revoked` event published to M-17

---

## POST /api/v1/auth/password-reset/request

Initiates a self-service password reset. Calls M-09 synchronously to deliver the token. If M-09 fails, the token is NOT persisted and the request returns 503.

**Required permission**: none (public endpoint)
**Required scope**: n/a
**Rate limit**: 3 requests per email per 30-minute window (configurable via M-11). Excess → **429**.

### Request

```json
{
  "email": "alice@example.com"
}
```

### Responses

**202 Accepted** — reset token issued and delivery attempted (even if email does not exist, to avoid user enumeration)

**429 Too Many Requests** — rate limit exceeded; `password.reset.rate_limited` event published to M-17
```json
{
  "error": { "code": "auth.password_reset.rate_limited", "message": "...", "correlation_id": "uuid", "tenant_id": "uuid" },
  "retryAfter": 1200
}
```

**503 Service Unavailable** — M-09 unavailable; token NOT persisted; retry safe

---

## POST /api/v1/auth/password-reset/redeem

Validates a reset token and sets a new password.

**Required permission**: valid reset token

### Request

```json
{
  "token": "RAW_RESET_TOKEN",
  "newPassword": "NewValidP@ss2"
}
```

### Responses

**200 OK** — password changed; `password.reset.completed` event published to M-17
```json
{
  "requiresMfaReenrollment": false
}
```

**400 Bad Request** — token expired, already used, or revoked
```json
{
  "error": { "code": "auth.password_reset.invalid_token", "message": "...", "correlation_id": "uuid", "tenant_id": "uuid" }
}
```

**422 Unprocessable Entity** — password fails complexity requirements
```json
{
  "error": {
    "code": "auth.password_reset.weak_password",
    "message": "Password does not meet complexity requirements",
    "correlation_id": "uuid",
    "tenant_id": "uuid",
    "details": [
      { "field": "newPassword", "code": "min_length", "message": "Minimum 10 characters" }
    ]
  }
}
```

---

## GET /api/v1/auth/session

Returns the current session's permission snapshot (used to refresh the in-memory permission state on the frontend).

**Required permission**: authenticated session
**Required scope**: own

### Responses

**200 OK**
```json
{
  "userId": "uuid",
  "persona": "P-01",
  "expiresAtUtc": "2026-06-09T12:00:00Z",
  "permissionSnapshot": {
    "version": 7,
    "modules": { "SurveyBuilder": ["View", "Manage"] },
    "customActions": ["UpdateSurvey"],
    "scopeAssignments": { "branch": ["Riyadh"] },
    "hierarchyNodeId": "uuid",
    "hierarchyDescendantIds": ["uuid1", "uuid2"]
  }
}
```

**401 Unauthorized** — session expired or invalid
