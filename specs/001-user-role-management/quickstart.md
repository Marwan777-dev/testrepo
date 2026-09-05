# Quickstart & Validation Guide: User and Role Management (M-10)

**Feature**: 001-user-role-management
**Date**: 2026-06-08

This guide describes how to run the feature end-to-end once the implementation is complete. It covers backend unit/integration tests, frontend dev server, and E2E browser tests.

---

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| .NET SDK | 10.0+ | `dotnet --version` |
| Docker Desktop | 24+ | Required for Testcontainers (integration tests) |
| Node.js | 22+ | `node --version` |
| Playwright browsers | — | Install once after building the E2E project: `pwsh tests/Nabadat.TenantApp.E2ETests/bin/Debug/net10.0/playwright.ps1 install` (see Step 6) |

---

## Step 1 — Build the Backend Module

```powershell
# From repo root
dotnet build src/Nabadat.Platform.M10/Nabadat.Platform.M10.csproj
```

Expected: Zero errors, zero warnings (treat warnings as errors in CI).

---

## Step 2 — Run Unit Tests (no Docker required)

```powershell
dotnet test tests/Nabadat.Platform.M10.UnitTests
```

Expected: All tests green. Key test classes to verify:

| Class | What it proves |
|-------|---------------|
| `PasswordValidatorTests` | Complexity rules enforced (min 10, uppercase, digit, special) |
| `MfaChallengeValidatorTests` | Valid TOTP code accepted; invalid rejected; anti-replay on same step |
| `SessionServiceTests` | Snapshot version mismatch triggers rebuild |
| `DataLayerAuthorizationGuardTests` | P-07 cannot assign CX-domain modules at service layer |
| `HierarchyCascadeServiceTests` | Descendant nodes included; ancestors/siblings excluded |
| `M17EventPublisherTests` | Event payload serialized to jsonb; null-transaction guard |
| `AuditTransactionTests` / `EventCoverageTests` | Every state change writes its audit event atomically with the correct type/payload (FR-015). Real transaction rollback is verified in the integration lane (`AuditTransactionIntegrationTests`). |

---

## Step 3 — Run Integration Tests (Docker required)

```powershell
dotnet test tests/Nabadat.Platform.M10.IntegrationTests
```

Docker must be running. Testcontainers provisions a fresh PostgreSQL instance per test fixture. Expected: All tests green.

Key scenarios:

| Scenario test | What it proves end-to-end |
|---------------|--------------------------|
| `TenantLoginWithMandatoryMfaTests` | Full login → MFA challenge → session creation; lockout after 5 failures; auto-unlock |
| `PersonaBaselineAndEnforcementTests` | P-01 creates user → permissions applied; P-07 blocked from CX-domain |
| `DataScopeAndHierarchyCascadeTests` | Scope assignment stored; hierarchy descendants cascade |
| `AuditTransactionIntegrationTests` | Aborted unit of work rolls back entity + event together; events are append-only (no overwrite) |
| `ImmutableAuditTrailTests` | A permission change is recorded with old/new values; only admins may read the log; no API can modify/delete an entry |

---

## Step 4 — Start the Frontend Dev Server

```powershell
cd frontend
npm install          # First time only
npm run dev
```

Frontend available at `http://localhost:5173`. The Vite dev server proxies `/api` to the backend (default `https://localhost:7286`). The backend must be running for API calls to succeed.

Start the backend:
```powershell
# From repo root
dotnet run --project src/Nabadat.TenantAdmin/Nabadat.TenantAdmin.csproj
```

---

## Step 5 — Manual Frontend Validation

Open `http://localhost:5173` in a browser. Use the seeded test credentials (see `appsettings.Development.json`) to walk through:

### 5a. Login + MFA Flow (US-1)

1. Navigate to the Login page — expect the login form with email + password fields
2. Enter valid credentials → expect redirect to MFA Challenge page
3. Enter correct TOTP code → expect redirect to the authenticated dashboard
4. Repeat step 2–3 with an incorrect TOTP code → expect validation error, no session created

### 5b. MFA Enrollment (US-1, first-time user)

1. Log in as a user with `isMfaEnrolled = false`
2. After credentials, expect redirect to MFA Enrollment page with QR code
3. Scan the QR code in an authenticator app, enter the first code → expect session created and redirect to dashboard

### 5c. Password Reset (US-1)

1. Navigate to the Login page → click "Forgot password"
2. Enter email → expect success message (202 returned regardless of whether email exists)
3. Use the reset token from the backend logs (dev mode) → enter new password → expect redirect to login
4. Submit the same token again → expect "token already used" error

### 5d. User Management (US-2)

1. Log in as P-01 → navigate to "User Management" in the sidebar
2. Expect user list with invite button
3. Invite a new user (email, persona) → expect user appears in list with `pending-enrollment` status
4. Open user detail → modify permission modules → save → confirm snapshot version incremented
5. Log in as P-07 → navigate to "User Management" → verify CX-domain permission modules are disabled/hidden when editing
6. Log in as P-03 → navigate to "User Management" URL directly → expect access denied

### 5e. Persona Baselines (US-2)

1. Log in as P-01 → navigate to "Persona Baselines"
2. Modify a permission module assignment for P-03 → save
3. Confirm `isCustomised = true` on the baseline
4. Log in as P-03 → confirm access denied to the Persona Baselines page

### 5f. Data Scope & Custom Rules (US-3)

1. Log in as P-01 → open a user's scope management page
2. Assign branch parameter values `[Riyadh, Dammam]` → save
3. Add a custom rule granting `UpdateSurvey` but not `DeleteSurvey`
4. Verify the assigned user can update but not delete surveys in the portal

### 5g. Audit Log (US-4)

1. Log in as P-01 → navigate to "Audit Log" (sidebar entry visible to P-01/P-07 only)
2. Expect a chronological list of recent events (permission changes, session events, …)
3. Filter by `event_type = permission.modified` and/or a date range → only matching rows
4. Confirm no edit/delete controls are visible on any audit record (the log is read-only)
5. Log in as P-03 → open the `/audit-log` URL directly → expect access denied

> **Ownership:** M-10 owns this audit log end-to-end — it writes events to `event_log`
> and reads them back via `IAuditLogReader` (`AuditLogReader`). There is no external M-17
> dependency (the earlier M-17 plan was dropped — see gap-analysis I-02/I-04).

---

## Step 6 — Run E2E Tests

### One-time Playwright browser install

```powershell
# From repo root — build first; the install script is emitted into the build output.
dotnet build tests/Nabadat.TenantApp.E2ETests
# Install Chromium, Firefox, WebKit
pwsh tests/Nabadat.TenantApp.E2ETests/bin/Debug/net10.0/playwright.ps1 install
```

### Configure test credentials

The run reads `tests/Nabadat.TenantApp.E2ETests/appsettings.local.json` (gitignored). A
ready-to-use template whose values match the Development data seeder
(`src/Nabadat.TenantAdmin/Development/DevDataSeeder.cs`) ships beside it — copy it as-is:

```powershell
Copy-Item tests/Nabadat.TenantApp.E2ETests/appsettings.local.json.example `
          tests/Nabadat.TenantApp.E2ETests/appsettings.local.json
```

`E2ESettings` reads a **flat `e2e` section** (each key also overridable via an `E2E_*`
env var — e.g. `E2E_EMAIL`, `E2E_P03_TOTP_SECRET`):

```json
{
  "e2e": {
    "baseUrl": "http://localhost:5173",
    "email": "e2e-active@dev.local",     "password": "Admin123!",  "totpSecret": "…",
    "enrolEmail": "e2e-enroll@dev.local", "enrolPassword": "Admin123!",
    "resetEmail": "e2e-reset@dev.local",
    "p07Email": "e2e-p07@dev.local",      "p07Password": "Admin123!", "p07TotpSecret": "…",
    "p03Email": "e2e-p03@dev.local",      "p03Password": "Admin123!", "p03TotpSecret": "…"
  }
}
```

Ensure the backend and frontend dev server are both running, then:

```powershell
# From repo root
$env:E2E_BASE_URL = "http://localhost:5173"
dotnet test tests/Nabadat.TenantApp.E2ETests
```

Expected: All tests green. Screenshots and Playwright trace files are attached to each
test result and visible in the VS Test Explorer **Attachments** section.

> **Audit log:** all four audit-log E2E rows (AL-1..AL-4) run against M-10's own reader —
> there is no M-17 dependency. AL-1/AL-2 (view + filter) require seeded audit history, which
> the seeded admin actions naturally produce.

### Run only one feature's tests

```powershell
dotnet test tests/Nabadat.TenantApp.E2ETests --filter "FullyQualifiedName~AuthTests"
dotnet test tests/Nabadat.TenantApp.E2ETests --filter "FullyQualifiedName~UserManagementTests"
```

---

## Step 7 — Per-Story Build Gate

The story checkpoint is green when BOTH of the following pass:

```powershell
# 1. Build + unit tests
dotnet build src/Nabadat.Platform.M10
dotnet test tests/Nabadat.Platform.M10.UnitTests

# 2. Integration tests (requires Docker)
dotnet test tests/Nabadat.Platform.M10.IntegrationTests

# 3. Frontend build (typecheck + bundle)
cd frontend && npm run build
```

E2E tests run at the per-story checkpoint too (requires running stack):
```powershell
dotnet test tests/Nabadat.TenantApp.E2ETests --filter "FullyQualifiedName~{StoryFeatureTests}"
```

---

## Key API Calls for Quick Smoke Testing

### Login flow (curl)

```bash
# Step 1: credentials
curl -X POST https://localhost:7286/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin@test.com","password":"TestP@ssword1"}'
# → { "challengeId": "...", "requiresMfaEnrollment": false }

# Step 2: MFA verify (compute TOTP code from your authenticator or test TOTP secret)
curl -X POST https://localhost:7286/api/v1/auth/mfa/verify \
  -H "Content-Type: application/json" \
  -d '{"challengeId":"...","totpCode":"123456"}'
# → { "sessionToken": "nbd_...", "userId": "...", ... }

# Step 3: use the session token
curl https://localhost:7286/api/v1/users \
  -H "Authorization: Bearer nbd_..."
# → { "items": [...], ... }
```

---

## References

- Data model: [data-model.md](data-model.md)
- Auth API contract: [contracts/auth-api.md](contracts/auth-api.md)
- Users API contract: [contracts/users-api.md](contracts/users-api.md)
- Permissions, scope, audit API: [contracts/permissions-api.md](contracts/permissions-api.md)
- Feature spec: [spec.md](spec.md)
- Implementation plan: [plan.md](plan.md)
