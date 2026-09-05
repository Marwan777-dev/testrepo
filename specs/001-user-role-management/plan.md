# Implementation Plan: User and Role Management (M-10)

**Branch**: `001-user-role-management` | **Date**: 2026-06-08 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-user-role-management/spec.md`

---

## Summary

M-10 delivers the authentication, authorization, and audit infrastructure for all tenant-side users of the Nabadat platform. It introduces mandatory TOTP MFA login, a modular permission catalogue with persona baselines, custom data-scope and hierarchy-cascade rules sourced from M-13, and a complete immutable audit trail co-written with M-17 inside the same transaction. The frontend ships eight pages alongside the backend in Phase 1: Login, MFA Challenge, MFA Enrollment, Password Reset, User Management, PersonaBaseline Management, Data Scope & Custom Rules, and Audit Log.

---

## Technical Context

**Language/Version**: C# 13 / .NET 10, ASP.NET Core 10 (backend); TypeScript 5, React 19, Vite 6, Tailwind CSS v4, `@base-ui/react` (frontend)

**Primary Dependencies**:
- Backend: `OTP.NET` (TOTP), `BCrypt.Net-Next` (password hashing), `Npgsql.EntityFrameworkCore.PostgreSQL` (EF Core driver), `FluentValidation`, `NSubstitute` + `FluentAssertions` (tests), `Testcontainers.PostgreSql` (integration tests), `Microsoft.Playwright.MSTest` (E2E)
- Frontend: `qrcode.react` (MFA QR code), `react-router`, `i18next`, `@base-ui/react`, `input-otp` (OTP input), `sonner` (toasts), `lucide-react` (icons)

**Storage**:
- Tenant schema (per-tenant PostgreSQL): `tenant_users`, `auth_sessions`, `password_reset_tokens`, `permission_module_assignments`, `custom_authorization_rules`, `data_scope_assignments`, `data_scope_parameter_definitions`, `organization_hierarchy_nodes`, `password_reset_rate_limit_records`
- Control-plane database: `persona_baselines`, `identity_provider_configs`

**Testing**:
- Unit: `dotnet test tests/Nabadat.Platform.M10.UnitTests` (xUnit v3, NSubstitute, FluentAssertions 6.12.*)
- Integration: `dotnet test tests/Nabadat.Platform.M10.IntegrationTests` (Testcontainers PostgreSQL, WebApplicationFactory)
- E2E: `dotnet test tests/Nabadat.TenantApp.E2ETests` (MSTest + Playwright against `http://localhost:5173`)

**Target Platform**: Linux container (SaaS, Kubernetes) + Docker Compose (on-premises)

**Project Type**: Modular monolith module (ASP.NET Core) + SPA frontend feature set

**Performance Goals**:
- Login + MFA verify round-trip: < 300 ms p95 (password hashing ≈ 250 ms is the dominant cost; total budgeted at 300 ms)
- Permission check (snapshot hit): < 5 ms (in-process snapshot read, no DB round-trip)
- Session refresh (snapshot miss): < 50 ms (single DB read to rebuild snapshot)

**Constraints**:
- `mfaSecretEncrypted` MUST be envelope-encrypted before any write (GP-02)
- No `tenant_id` column in any tenant-schema table (DB-02, AD-02)
- All M-17 event writes MUST be in the same DB transaction as the triggering action (FR-015)
- M-09 call for password reset is synchronous; failure rolls back the token write (FR-021)
- bcrypt cost factor ≥ 12 (security constitution Article 2.1)

**Scale/Scope**: ~500 tenant users per tenant at launch; permission evaluation must not degrade as tenant grows to 10,000 users

---

## Constitution Check

*GATE: Must pass before implementation begins.*

| Principle | Requirement | M-10 Design |
|-----------|-------------|-------------|
| **GP-01** — PostgreSQL is authoritative | All tenant user, session, permission, and audit data committed to PostgreSQL first | ✅ Every entity write targets PostgreSQL; M-17 `event_log` is written in the same transaction |
| **GP-02** — Customer-Controlled Encryption | High-sensitivity fields envelope-encrypted under CMK | ✅ `mfaSecretEncrypted` is envelope-encrypted; `passwordHash` is one-way bcrypt (not encrypted — correct per spec clarification) |
| **GP-03** — Right to Erasure | Erasure clears personal data across all stores within SLA | ✅ `UserManagementService.EraseUserAsync` (T031c) nulls PII fields on `TenantUser` (`Username`, `PasswordHash`, `MfaSecretEncrypted`, `MfaSecretKeyRef`) and hard-deletes related `AuthSession` and `PasswordResetToken` rows; publishes `user.erased` event to M-17 in the same transaction. Note: soft-deactivation (T077) is a separate lifecycle action and does NOT null PII fields. An orchestrating data-governance module (M-11 or future) may call `EraseUserAsync` to fulfil GDPR SLA obligations. |
| **GP-04** — Tenant/Scope Isolation | No cross-tenant data access, denied attempts audited | ✅ Schema-per-tenant (AD-02); no `tenant_id` columns; all permission checks at module boundary |
| **GP-05** — Constitution Compliance Gate | Plan passes constitution check before implementation | ✅ This check |

**Architecture Decisions verified:**

| Decision | Requirement | Status |
|----------|-------------|--------|
| AD-01 — Modular Monolith | M-10 exposes a published interface; no concrete-type cross-module references | ✅ `IM10AuthService`, `IM10PermissionService` published interfaces; consumers depend only on those |
| AD-02 — Schema-Per-Tenant | Tenant tables have no `tenant_id` column | ✅ All tenant-schema tables use schema-level isolation only; `IdentityProviderConfig` and `PersonaBaseline` are control-plane tables and retain `tenant_id` FK (DB-02 exemption) |
| AD-03 — No Caching | No Redis; no in-memory analytics cache | ✅ Session permission snapshot stored in PostgreSQL, not a cache layer |
| AD-05 — Two Deployment Modes | All code works in SaaS and on-prem modes | ✅ `ENABLE_MULTI_TENANT` flag selects KMS vs. config-based key source for `mfaSecret` encryption |
| DB-02 — No `tenant_id` in tenant tables | Tenant schema tables must not carry `tenant_id` | ✅ Confirmed; `PersonaBaseline` and `IdentityProviderConfig` are control-plane tables (exempt) |
| API-01 — Versioned endpoints | All endpoints at `/api/v1/` | ✅ All M-10 endpoints under `/api/v1/` |
| API-03 — Permission declaration | Every endpoint declares `required_permission`, `required_scope`, `default_personas` | ✅ Documented in contracts/ |
| API-04 — Cursor-based pagination | All list endpoints use cursor pagination | ✅ User list and audit log use cursor pagination |
| T-01 — Multi-Language | Frontend supports Arabic (RTL) and English | ✅ All frontend pages use `i18next`; RTL-first layout with logical CSS properties |
| T-05 — Tenant Isolation Without Exception | No cross-tenant data path | ✅ Tenant context resolved once at request entry (AD-07); all queries are schema-scoped |

**No constitution violations found.** Complexity Tracking table left empty.

---

## Project Structure

### Documentation (this feature)

```text
specs/001-user-role-management/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── auth-api.md      # Login, MFA, password reset endpoints
│   ├── users-api.md     # User CRUD, lifecycle endpoints
│   └── permissions-api.md  # Permission assignment, persona baselines, data scope
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
└── Nabadat.Platform.M10/
    ├── Nabadat.Platform.M10.csproj
    ├── Api/
    │   ├── AuthController.cs            # POST /auth/login, /auth/mfa/*, /auth/logout
    │   ├── UsersController.cs           # GET/POST/PUT /users, /users/{id}
    │   ├── PermissionsController.cs     # PUT /users/{id}/permissions
    │   ├── PersonaBaselinesController.cs # GET/PUT /persona-baselines
    │   ├── DataScopeController.cs       # GET/PUT /users/{id}/scope, POST /authorization/scope/parameters
    │   └── AuditLogController.cs        # GET /audit-log
    ├── Application/
    │   ├── Auth/
    │   │   ├── TenantAuthenticationService.cs
    │   │   ├── MfaEnrollmentService.cs
    │   │   ├── MfaChallengeValidator.cs
    │   │   ├── SessionService.cs
    │   │   ├── PasswordValidator.cs
    │   │   └── PasswordResetService.cs
    │   ├── Users/
    │   │   ├── UserManagementService.cs
    │   │   └── UserCreationPolicy.cs
    │   ├── Permissions/
    │   │   ├── PermissionAssignmentService.cs
    │   │   ├── PermissionEvaluationService.cs
    │   │   ├── PersonaBaselineService.cs
    │   │   └── DataScopeRuleService.cs
    │   ├── Hierarchy/
    │   │   └── HierarchyCascadeService.cs
    │   └── Events/
    │       └── M17EventPublisher.cs
    ├── Domain/
    │   ├── Entities/
    │   │   ├── TenantUser.cs
    │   │   ├── AuthSession.cs
    │   │   ├── PasswordResetToken.cs
    │   │   ├── PermissionModuleAssignment.cs
    │   │   ├── CustomAuthorizationRule.cs
    │   │   ├── DataScopeAssignment.cs
    │   │   └── OrganizationHierarchyNode.cs
    │   ├── ValueObjects/
    │   │   ├── PermissionMode.cs
    │   │   ├── UserStatus.cs
    │   │   └── PermissionSnapshot.cs
    │   └── Interfaces/
    │       ├── ITenantUserRepository.cs
    │       ├── IAuthSessionRepository.cs
    │       ├── IPermissionRepository.cs
    │       ├── IM10AuthService.cs        # Published interface
    │       └── IM10PermissionService.cs  # Published interface
    └── Infrastructure/
        ├── Persistence/
        │   ├── TenantUserRepository.cs
        │   ├── AuthSessionRepository.cs
        │   └── PermissionRepository.cs
        ├── Crypto/
        │   ├── PasswordHasher.cs
        │   ├── TotpService.cs
        │   └── MfaSecretEncryptionService.cs
        └── ControlPlane/
            ├── PersonaBaselineRepository.cs
            └── IdentityProviderConfigRepository.cs

tests/
├── Nabadat.Platform.M10.UnitTests/
│   ├── Nabadat.Platform.M10.UnitTests.csproj
│   ├── Auth/
│   │   ├── TenantAuthenticationServiceTests.cs
│   │   ├── MfaEnrollmentServiceTests.cs
│   │   ├── MfaChallengeValidatorTests.cs
│   │   ├── PasswordHasherTests.cs
│   │   ├── PasswordValidatorTests.cs
│   │   └── SessionServiceTests.cs
│   ├── Permissions/
│   │   ├── PermissionAssignmentServiceTests.cs
│   │   ├── PersonaBaselineServiceTests.cs
│   │   └── DataScopeRuleServiceTests.cs
│   ├── Hierarchy/
│   │   └── HierarchyCascadeServiceTests.cs
│   └── Events/
│       └── M17EventPublisherTests.cs
├── Nabadat.Platform.M10.IntegrationTests/
│   ├── Nabadat.Platform.M10.IntegrationTests.csproj
│   ├── Infrastructure/
│   │   └── M10ApplicationFactory.cs
│   ├── Endpoints/
│   │   ├── AuthEndpointTests.cs
│   │   ├── UsersEndpointTests.cs
│   │   └── PermissionsEndpointTests.cs
│   ├── Services/
│   │   └── SessionTransactionTests.cs
│   └── Scenarios/
│       ├── TenantLoginWithMandatoryMfaTests.cs
│       ├── PersonaBaselineAndEnforcementTests.cs
│       ├── DataScopeAndHierarchyCascadeTests.cs
│       └── ImmutableAuditTrailTests.cs
└── Nabadat.TenantApp.E2ETests/          # First feature; scaffold on this story
    ├── Nabadat.TenantApp.E2ETests.csproj
    ├── E2ETestBase.cs                   # SignInAsync, screenshot/trace helpers
    ├── COVERAGE.md                      # E2E coverage matrix
    ├── AuthTests.cs                     # US-1 flows
    ├── UserManagementTests.cs           # US-2 flows
    ├── PersonaBaselineTests.cs          # US-2 persona baseline flows
    ├── DataScopeTests.cs                # US-3 flows
    └── AuditLogTests.cs                 # US-4 flows

frontend/src/
├── features/
│   ├── auth/
│   │   ├── pages/
│   │   │   ├── LoginPage.tsx
│   │   │   ├── MfaChallengePage.tsx
│   │   │   ├── MfaEnrollPage.tsx
│   │   │   └── PasswordResetPage.tsx
│   │   ├── components/
│   │   │   └── AuthGuard.tsx
│   │   ├── hooks/
│   │   │   └── useSession.ts
│   │   └── api.ts
│   ├── users/
│   │   ├── pages/
│   │   │   ├── UserManagementPage.tsx
│   │   │   └── UserDetailPage.tsx
│   │   ├── components/
│   │   │   ├── InviteUserDialog.tsx
│   │   │   └── UserPermissionsEditor.tsx
│   │   └── api.ts
│   ├── persona-baselines/
│   │   ├── pages/
│   │   │   └── PersonaBaselinePage.tsx
│   │   └── api.ts
│   ├── data-scope/
│   │   ├── pages/
│   │   │   └── UserScopePage.tsx
│   │   ├── components/
│   │   │   └── CustomRuleEditor.tsx
│   │   └── api.ts
│   └── audit-log/
│       ├── pages/
│       │   └── AuditLogPage.tsx
│       └── api.ts
```

**Structure Decision**: Web application pattern with a new `src/Nabadat.Platform.M10/` C# module alongside existing `M11` and `M18` modules. Frontend features added under `frontend/src/features/`. First-feature E2E scaffold creates `tests/Nabadat.TenantApp.E2ETests/` for the `frontend/` workspace.

---

## Complexity Tracking

> No constitution violations requiring justification.

---

## Phases

### Phase 0: Research (Complete)

All unknowns resolved in `research.md`:
- TOTP library: `OTP.NET` ✅
- Password hashing: `BCrypt.Net-Next` cost 12 ✅
- Session token: Opaque SHA-256-hashed, `nbd_` prefix ✅
- `mfaSecret` envelope encryption: deployment-mode-aware two-path service ✅
- Permission snapshot: version-based invalidation, snapshot stored in `auth_sessions.permission_snapshot` ✅
- M-13 parameter contract: REST push to `POST /api/v1/authorization/scope/parameters` ✅
- Hierarchy evaluation: materialized path pattern ✅
- Rate limiting: database-backed sliding-window counter ✅
- Frontend storage: `sessionStorage` for token, in-memory React Context for permission state ✅
- E2E infrastructure: `tests/Nabadat.TenantApp.E2ETests/` scaffold ✅

---

### Phase 1: Design & Contracts (This document + artifacts)

Outputs:
- `data-model.md` — entity schema for all nine tenant-schema entities + two control-plane entities
- `contracts/auth-api.md` — Login, MFA enroll/verify, password reset, logout
- `contracts/users-api.md` — User CRUD, lifecycle, persona assignment
- `contracts/permissions-api.md` — Permission module assignment, persona baselines, data scope, custom rules, audit log
- `quickstart.md` — Runnable validation guide

---

### Phase 2: Tasks (Output of `/speckit-tasks`)

Four user-story phases:

**Phase A — Auth Core (US-1)**
Covers: login, MFA enrollment + challenge, session management, password reset, account lockout, rate limiting. Backend first (unit tests → implementation → integration tests → scenario test), then frontend pages (Login, MFA Challenge, MFA Enroll, Password Reset), then E2E tests.

**Phase B — Users & Permissions (US-2)**
Covers: user CRUD (P-01/P-07 create, P-02..P-06 deny), persona baseline storage + management screen, permission module assignment, permission enforcement. Backend first, then User Management and PersonaBaseline frontend pages, then E2E tests.

**Phase C — Data Scope & Hierarchy (US-3)**
Covers: M-13 parameter contract ingestion, data scope assignment, hierarchy cascade (materialized path), custom authorization rules, scope management frontend. Backend first, then Data Scope frontend page, then E2E tests.

**Phase D — Audit Trail (US-4)**
Covers: M17EventPublisher transactional coupling for all M-10 events, audit log read endpoint, Audit Log frontend page. Backend first, then frontend, then E2E tests.

**Cross-cutting foundational tasks** (before Phase A):
- Scaffold `Nabadat.Platform.M10.csproj` + test projects
- Scaffold `Nabadat.TenantApp.E2ETests.csproj` + `E2ETestBase.cs`
- Database migration: all M-10 tenant-schema tables + control-plane tables
- Module registration in DI container + ASP.NET Core pipeline
- `IM10AuthService` + `IM10PermissionService` published interfaces

---

## Key Design Decisions

### 1. Published Interface Boundary

M-10 exposes two published interfaces consumed by other modules:
- `IM10AuthService.ValidateSessionToken(token) → SessionContext?` — used by `nabadat-api` middleware to authenticate every request
- `IM10PermissionService.CheckPermission(userId, action, entityId?) → PermissionDecision` — used by every module's action boundary

No other module references M-10 concrete types.

### 2. P-07 Permission Scope Enforcement at Data Layer

The spec requires that P-07 cannot assign CX-domain modules. This is enforced by `UserCreationPolicy` and `DataLayerAuthorizationGuard` — classes that run inside the service layer before any persistence, not just in the UI. The guard is tested in unit tests with a direct service call (bypassing controller/middleware) to prove data-layer enforcement.

### 3. Transactional Audit via M-17

`M17EventPublisher.Publish(...)` writes to `event_log` using the same `NpgsqlTransaction` passed in from the calling service. The calling service opens the transaction, calls the business logic, calls `M17EventPublisher.Publish`, then commits. If `Publish` throws, the transaction rolls back and neither the entity change nor the event is persisted.

### 4. SSO Forward Compatibility

`IdentityProviderConfig` stores provider type and settings as `jsonb`, with `providerType` enum (`directory | google-oidc | internal | saml2 | nafath`). No provider logic is implemented in Phase 1. The record is created/updated via a management API; the enum is extensible without a migration (additive).

### 5. Frontend Token Lifecycle

Login flow:
1. `POST /api/v1/auth/login` → `{challengeId}` (MFA challenge pending)
2. `POST /api/v1/auth/mfa/verify` → `{sessionToken, userId, permissionSnapshot}` or `{enrollmentRequired: true}`
3. Token stored in `sessionStorage` as `nbd_session`; `AuthContext` hydrates on page load.

On each API call, `Authorization: Bearer <token>` header is sent. On 401, clear session and redirect to Login.

### 6. Hierarchy Source Toggle

`M10Config` (read from M-11 tenant settings) carries `hierarchySource: "manual" | "integration"`. `HierarchyCascadeService` calls `IM11TenantService.GetHierarchyNodes(tenantId)` (manual) or `IM13IntegrationService.GetHierarchyNodes(tenantId)` (integration). Neither implementation is hardcoded in M-10's cascade logic.
