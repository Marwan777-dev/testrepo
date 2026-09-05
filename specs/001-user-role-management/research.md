# Research: User and Role Management (M-10)

**Feature**: 001-user-role-management
**Date**: 2026-06-08
**Status**: Complete — all unknowns resolved

---

## 1. TOTP MFA Implementation (.NET)

**Decision**: Use [`OTP.NET`](https://github.com/kspearrin/Otp.NET) (NuGet: `Otp.NET`) for TOTP generation and verification.

**Rationale**:
- RFC 6238–compliant, widely used in .NET ecosystem
- Supports time-window tolerance (±1 step, 30 s window) for clock skew
- Produces standard Base32 secrets compatible with Google Authenticator, Authy, and any RFC 6238 app
- QR code URI: `otpauth://totp/{issuer}:{username}?secret={base32secret}&issuer={issuer}` — rendered client-side using `qrcode.react` (already in package.json)

**Alternatives considered**: `GoogleAuthenticator` NuGet — older, less maintained; Microsoft TOTP — no official package.

**Clock-skew tolerance**: Accept TOTP codes from `now-1` to `now+1` step (±30 s). Reject any code that was valid but already presented in the same window (anti-replay via a `lastUsedTotpStep` column on `TenantUser`).

---

## 2. Password Hashing (.NET)

**Decision**: Use `BCrypt.Net-Next` (NuGet: `BCrypt.Net-Next`) with work factor 12.

**Rationale**:
- bcrypt cost 12 ≈ 250 ms on modern hardware; satisfies the security constitution (Article 2.1 — bcrypt cost ≥ 12).
- `BCrypt.Net-Next` is actively maintained, thread-safe, and has no external native dependencies.
- Argon2id is also permitted by the constitution; deferred to Phase 2 if compliance requirements mandate it.

**Alternatives considered**: `Konscious.Security.Cryptography` (Argon2id) — valid but bcrypt is simpler and sufficient for Phase 1.

---

## 3. Session Token Strategy

**Decision**: Opaque 256-bit random tokens stored as `SHA-256(token)` in the database; raw token returned to client once, never stored in plaintext.

**Rationale**:
- No JWT used for tenant-user sessions (JWT is used for SSO/federated flows per constitution §3.3; for local-auth sessions an opaque token is simpler and avoids clock-skew/revocation problems).
- Opaque token hashed at rest: even if the `auth_sessions` table is read, no valid token can be reconstructed.
- Token format: `nbd_{base64url(32 bytes)}` — human-recognisable prefix, not a valid JWT, easy to scrub in logs.
- Sliding-window TTL: `lastActivityAtUtc` updated on each authenticated request; session invalid if `now > lastActivityAtUtc + slidingTtl`; hard cap at `absoluteExpiresAtUtc`.

**Client storage**: `sessionStorage` (cleared on tab close). Tokens MUST NOT be stored in `localStorage` (XSS risk). Auth state lives in React context, hydrated from `sessionStorage` on page load.

---

## 4. Envelope Encryption for `mfaSecret`

**Decision**: Two-mode encryption service selected by `ENABLE_MULTI_TENANT` flag (AD-05).

| Mode | Key source | Implementation |
|------|-----------|----------------|
| SaaS (`ENABLE_MULTI_TENANT=true`) | Customer CMK via AWS KMS / Azure Key Vault | `IKmsEnvelopeEncryptionService` → `AwsKmsEncryptionService` or `AzureKmsEncryptionService` |
| On-prem (`ENABLE_MULTI_TENANT=false`) | Config-based symmetric AES-256 key (`MfaEncryptionKey` env var) | `IKmsEnvelopeEncryptionService` → `LocalAesEncryptionService` |

**Key reference storage**: `mfaSecretKeyRef` column stores the KMS key ID (SaaS) or config key name (on-prem). Supports key rotation: re-encrypt the `mfaSecretEncrypted` field without forcing MFA re-enrollment.

**Envelope pattern**: Generate a random 256-bit data key per user; encrypt the TOTP secret with AES-256-GCM using the data key; encrypt the data key with the CMK; store `{encryptedDataKey || iv || ciphertext}` as the `mfaSecretEncrypted` blob.

---

## 5. Permission Evaluation and Session Snapshots

**Decision**: Snapshot-at-issuance with version-based invalidation.

**Pattern**:
- At session creation, serialize the user's effective permissions (module assignments + custom rules + scope assignments) into a compact JSON snapshot stored in `AuthSession.permissionSnapshot` (jsonb).
- Track `permissionSnapshotVersion` as an incrementing integer on `TenantUser`; bump on every permission change.
- At each authenticated request, compare `session.permissionSnapshotVersion` with `user.permissionSnapshotVersion`; on mismatch, reload and re-serialize the snapshot.
- This makes permission reads O(1) for the common case (version match = use snapshot) and requires a DB round-trip only on change.

**Concurrent session handling**: All active sessions for a user get their version bumped at the next request after a permission change. No session invalidation on permission change — invalidation would log users out; the version-mismatch re-evaluation is sufficient.

---

## 6. M-13 Parameter Contract Integration

**Decision**: M-13 pushes parameter definitions to M-10 via a REST endpoint (`POST /api/v1/authorization/scope/parameters`) using a structured JSON payload. M-10 validates and stores them in `data_scope_parameter_definitions`.

**Contract payload**:
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

**Validation**: M-10 rejects payloads where `name` conflicts with a reserved parameter name, `allowedValues` is empty, or the payload exceeds 500 parameter definitions.

---

## 7. Hierarchy Scope Evaluation

**Decision**: Materialized path pattern (`path` column: `/root/region-a/branch-x/`) for O(1) descendant queries.

**Query pattern** (tenant schema):
```sql
SELECT node_id FROM organization_hierarchy_nodes
WHERE path LIKE '/root/region-a/branch-x/%'
   OR node_id = :assignedNodeId;
```

Prefix-of-path check gives downward cascade; ancestor/sibling exclusion is implicit (paths not matching the prefix are excluded). Path updated by M-11 or M-13 when nodes are created/moved.

---

## 8. Rate Limiting — Password Reset

**Decision**: Application-layer rate limiting using an in-memory sliding-window counter keyed by `SHA-256(email || tenantId)`.

**Storage**: `PasswordResetRateLimitRecord` table in tenant schema:
- `emailHash` (SHA-256 of normalized email, not the plaintext email)
- `windowStartUtc`
- `requestCount`
- TTL: records older than 30 minutes are stale and not counted

**Why in-process counter is insufficient**: The application may run as multiple instances. A database row with `FOR UPDATE SKIP LOCKED` handles concurrent requests atomically.

---

## 9. Frontend Auth Storage

**Decision**: Session token stored in `sessionStorage`; permission snapshot stored in React Context (in-memory), refreshed on each route change.

**Rationale**:
- `sessionStorage` is cleared on tab close (better than `localStorage` for session tokens).
- No sensitive data in `localStorage`.
- On hard refresh, `sessionStorage` persists within the tab; the auth context re-hydrates from `sessionStorage` on mount.
- TOTP QR secret shown only once and never stored in the browser.

---

## 10. E2E Test Infrastructure (Frontend)

**Decision**: New `tests/Nabadat.TenantApp.E2ETests/` MSTest project (Microsoft.Playwright.MSTest) for the `frontend/` workspace.

**Dev server**: `npm run dev` in `frontend/` serves at `http://localhost:5173` (proxying `/api` to the backend).
**Auth model**: Real sign-in flow (login → MFA challenge) using a seeded test user with a known TOTP secret stored in `appsettings.local.json` (gitignored). Tests call the sign-in flow end-to-end rather than injecting tokens.
**Screenshot + trace**: Captured per test, attached via `TestContext.AddResultFile`.
