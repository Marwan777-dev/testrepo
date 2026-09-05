# Tasks: User and Role Management (M-10)

**Input**: Design documents from `specs/001-user-role-management/`

**Feature**: M-10 User and Role Management | **Branch**: `001-user-role-management`

**Plan**: [plan.md](plan.md) | **Spec**: [spec.md](spec.md) | **Data Model**: [data-model.md](data-model.md)

**Contracts**: [auth-api.md](contracts/auth-api.md) · [users-api.md](contracts/users-api.md) · [permissions-api.md](contracts/permissions-api.md)

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no conflicting dependencies)
- **[Story]**: Maps to user story (US1–US4)
- **T_XXR**: Red Checkpoint — must verify unit tests are red before implementation

---

## Phase 1: Setup (Project Scaffolding)

**Purpose**: Create all project skeletons, solution wiring, and first-time E2E harness. No user story work until Phase 2 completes.

- [X] T001 Create `src/Nabadat.Platform.M10/Nabadat.Platform.M10.csproj` targeting .NET 10; add to `Nabadat.TenantAdmin.sln`
- [X] T002 [P] Create directory tree under `src/Nabadat.Platform.M10/`: `Api/`, `Application/Auth/`, `Application/Users/`, `Application/Permissions/`, `Application/Hierarchy/`, `Application/Events/`, `Domain/Entities/`, `Domain/ValueObjects/`, `Domain/Interfaces/`, `Infrastructure/Persistence/`, `Infrastructure/Crypto/`, `Infrastructure/ControlPlane/`
- [X] T003 [P] Create `tests/Nabadat.Platform.M10.UnitTests/Nabadat.Platform.M10.UnitTests.csproj`; add xUnit v3 (`xunit.v3 1.*`), FluentAssertions `6.12.*`, NSubstitute `5.*`, `Microsoft.Extensions.TimeProvider.Testing 9.*`; add to solution
- [X] T004 Create `tests/Nabadat.Platform.M10.IntegrationTests/Nabadat.Platform.M10.IntegrationTests.csproj`; add `Testcontainers.PostgreSql 4.*`, `Microsoft.AspNetCore.Mvc.Testing 10.*`, xUnit v3, FluentAssertions `6.12.*`; add to solution
- [X] T005 Create `tests/Nabadat.Platform.M10.IntegrationTests/Infrastructure/M10ApplicationFactory.cs` — `WebApplicationFactory<Program>` + `IAsyncLifetime` that boots Testcontainers PostgreSQL, applies the M-10 `_Baseline.sql` migration, and exposes `CreateClient()` and seeding helpers
- [X] T006 [P] Create `tests/Nabadat.TenantApp.E2ETests/Nabadat.TenantApp.E2ETests.csproj`; add `Microsoft.Playwright.MSTest`; set `E2E_BASE_URL` default to `http://localhost:5173`; add to solution
- [X] T007 Create `tests/Nabadat.TenantApp.E2ETests/E2ETestBase.cs` — `PageTest` subclass with `SignInAsync(email, password, totpSecret)` (drives login → MFA challenge → session), screenshot/trace capture via `TestContext.AddResultFile`, and `appsettings.local.json` binding for credentials
- [X] T008 [P] Create `tests/Nabadat.TenantApp.E2ETests/COVERAGE.md` — E2E coverage matrix table with columns: ID, Feature, Test Method, Status
- [X] T009 [P] Add NuGet packages to `Nabadat.Platform.M10.csproj`: `OTP.NET`, `BCrypt.Net-Next`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `FluentValidation.AspNetCore`; reference the M10 project from `Nabadat.TenantAdmin`

**Checkpoint**: All projects compile, all test projects reference M10 assembly, E2E harness builds

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared infrastructure that ALL user stories depend on. No US implementation until this phase completes.

**⚠️ CRITICAL**: Unit test projects must reference M10 production project before any story-specific tests can be written.

### Database

- [X] T010 Create `src/Nabadat.Platform.M10/Infrastructure/Migrations/_Baseline.sql` — tenant-schema tables: `tenant_users`, `auth_sessions`, `password_reset_tokens`, `password_reset_rate_limit_records`, `permission_module_assignments`, `custom_authorization_rules`, `data_scope_assignments`, `data_scope_parameter_definitions`, `organization_hierarchy_nodes` with all columns, constraints, and indexes per `data-model.md`
- [X] T011 Create `src/Nabadat.Platform.M10/Infrastructure/Migrations/_ControlPlane.sql` — control-plane tables: `persona_baselines`, `identity_provider_configs` with `tenant_id` FK; register with the control-plane migration runner

### Domain Entities

- [X] T012 [P] Create `src/Nabadat.Platform.M10/Domain/Entities/TenantUser.cs` — all fields per data-model.md; no `TenantId` property; `LastPermissionSnapshotVersion` as `long`
- [X] T013 [P] Create `src/Nabadat.Platform.M10/Domain/Entities/AuthSession.cs` — `PermissionSnapshot` as `PermissionSnapshot` value object; `TokenHash` as `byte[]`
- [X] T014 [P] Create `src/Nabadat.Platform.M10/Domain/Entities/PasswordResetToken.cs`
- [X] T015 [P] Create `src/Nabadat.Platform.M10/Domain/Entities/PermissionModuleAssignment.cs`
- [X] T016 [P] Create `src/Nabadat.Platform.M10/Domain/Entities/CustomAuthorizationRule.cs`
- [X] T017 [P] Create `src/Nabadat.Platform.M10/Domain/Entities/DataScopeAssignment.cs`
- [X] T018 [P] Create `src/Nabadat.Platform.M10/Domain/Entities/OrganizationHierarchyNode.cs`
- [X] T018a [P] Create `src/Nabadat.Platform.M10/Domain/Entities/PersonaBaseline.cs` — control-plane entity (not in tenant schema): `BaselineId` (Guid), `TenantId` (Guid), `PersonaId` (string P-01..P-08), `PermissionModuleAssignments` (jsonb), `DefaultDataScopeRules` (jsonb), `IsCustomised` (bool), `CreatedAt`, `UpdatedAt`; required by T075 (PersonaBaselineRepository) and T080 (PersonaBaselineService)
- [X] T018b [P] Create `src/Nabadat.Platform.M10/Domain/Entities/IdentityProviderConfig.cs` — control-plane entity: `ProviderId` (Guid), `TenantId` (Guid), `ProviderType` (enum: `directory | google-oidc | internal | saml2 | nafath`), `Settings` (jsonb), `IsActive` (bool), `CreatedAt`, `UpdatedAt`; satisfies FR-004/FR-018 forward-compatibility — no provider logic in Phase 1; no API endpoint needed in Phase 1
- [X] T019 [P] Create `src/Nabadat.Platform.M10/Domain/ValueObjects/PermissionSnapshot.cs` — serializable record with `Version`, `Modules` (dict), `CustomActions`, `ScopeAssignments`, `HierarchyNodeId`, `HierarchyDescendantIds`
- [X] T020 [P] Create `src/Nabadat.Platform.M10/Domain/ValueObjects/UserStatus.cs` and `PermissionMode.cs` enums/value objects

### Repository Interfaces

- [X] T021 [P] Create `src/Nabadat.Platform.M10/Domain/Interfaces/ITenantUserRepository.cs` — `GetByUsernameAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `ExistsAsync`
- [X] T022 [P] Create `src/Nabadat.Platform.M10/Domain/Interfaces/IAuthSessionRepository.cs` — `GetByTokenHashAsync`, `CreateAsync`, `InvalidateAsync`, `InvalidateAllForUserAsync`
- [X] T023 [P] Create `src/Nabadat.Platform.M10/Domain/Interfaces/IPermissionRepository.cs` — `GetAssignmentsAsync`, `ReplaceAssignmentsAsync`, `GetPersonaBaselineAsync`, `UpdatePersonaBaselineAsync`

### Published Interfaces

- [X] T024 [P] Create `src/Nabadat.Platform.M10/Domain/Interfaces/IM10AuthService.cs` — `ValidateSessionTokenAsync(token) → SessionContext?`; this is the interface other modules consume for request authentication
- [X] T025 [P] Create `src/Nabadat.Platform.M10/Domain/Interfaces/IM10PermissionService.cs` — `CheckPermissionAsync(userId, action, entityId?) → PermissionDecision`; `GetPermissionSnapshotAsync(userId) → PermissionSnapshot`

### Infrastructure: Crypto

- [X] T026 [P] Create `src/Nabadat.Platform.M10/Infrastructure/Crypto/PasswordHasher.cs` — wraps `BCrypt.Net-Next` at cost 12; `Hash(plain)` and `Verify(plain, hash)` methods
- [X] T027 [P] Create `src/Nabadat.Platform.M10/Infrastructure/Crypto/TotpService.cs` — wraps `OTP.NET`; `GenerateSecret() → Base32`, `GetOtpUri(username, secret) → string`, `VerifyCode(secret, code, lastUsedStep) → (bool valid, long step)` with ±1 step tolerance and anti-replay
- [X] T028 Create `src/Nabadat.Platform.M10/Infrastructure/Crypto/MfaSecretEncryptionService.cs` — `IMfaSecretEncryptionService` with `EncryptAsync(plainSecret) → (byte[] cipher, string keyRef)` and `DecryptAsync(cipher, keyRef) → string`; implementation selected by `ENABLE_MULTI_TENANT` flag (SaaS → `AwsKmsEncryptionService` or `AzureKmsEncryptionService` stub; on-prem → `LocalAesEncryptionService` using `MfaEncryptionKey` env var)

### M-17 Event Publisher

- [X] T029 Create `src/Nabadat.Platform.M10/Application/Events/M17EventPublisher.cs` — `IM17EventPublisher` interface + implementation; `PublishAsync(NpgsqlTransaction tx, M10Event evt)` writes one row to `event_log` within the passed transaction; throws on failure (caller's transaction will roll back); `M10Event` record carries `EventType`, `ActorId`, `ActorPersona`, `EntityType`, `EntityId`, `OldValue` (object), `NewValue` (object), `OccurredAtUtc`, `CorrelationId`

### Module Registration

- [X] T030 Create `src/Nabadat.Platform.M10/M10ServiceCollectionExtensions.cs` — `AddM10Module(IServiceCollection services, IConfiguration config)` registers all repositories, services, crypto services, `M17EventPublisher`, and published interfaces; called from `Nabadat.TenantAdmin` startup
- [X] T031 Register M-10 controllers and middleware in `Nabadat.TenantAdmin`; add `UseM10Authentication()` middleware that reads `Authorization: Bearer` header, calls `IM10AuthService.ValidateSessionTokenAsync`, and sets the request-scoped `SessionContext`
- [X] T031a [P] Create `src/Nabadat.Platform.M10/Infrastructure/ControlPlane/IdentityProviderConfigRepository.cs` — reads/writes `identity_provider_configs` control-plane table; methods: `GetByTenantIdAsync(tenantId)`, `UpsertAsync(config, tx)`; no API endpoint in Phase 1 — satisfies FR-004/FR-018 forward-compatibility (depends T018b, T011)
- [X] T031b [P] Verify `IM17EventLogReader.QueryM10EventsAsync(tenantId, filters, cursor)` is present in M-17's published interface (required by T117 — `AuditLogController`); if absent, add the method stub to M-17's interface contract with `NotImplementedException` body and document it in `contracts/permissions-api.md`; this task MUST complete before Phase 6 begins (F2 fix — ordering)
- [X] T031c [P] Add `EraseUserAsync(userId, NpgsqlTransaction tx)` to `src/Nabadat.Platform.M10/Application/Users/UserManagementService.cs` skeleton (stub only at this phase — full implementation in US2): method nulls `Username`, `PasswordHash`, `MfaSecretEncrypted`, `MfaSecretKeyRef` on `TenantUser`; hard-deletes related `AuthSession` and `PasswordResetToken` rows; publishes `user.erased` event to M-17 in the same transaction; satisfies GP-03 Right to Erasure (F4 fix)

**Checkpoint**: `dotnet build src/Nabadat.Platform.M10` compiles clean. All foundational types exist. No feature logic yet.

---

## Phase 3: User Story 1 — Tenant Login with Mandatory MFA (Priority: P1) 🎯 MVP

**Goal**: Tenant users can sign in with username/password + TOTP MFA; first-time users enroll an authenticator app; accounts lock after 5 failures; password reset via M-09 is fully functional.

**Independent Test**: `dotnet test tests/Nabadat.Platform.M10.IntegrationTests --filter "FullyQualifiedName~TenantLoginWithMandatoryMfaTests"` and `dotnet test tests/Nabadat.TenantApp.E2ETests --filter "FullyQualifiedName~AuthTests"`

### Unit Tests for User Story 1 (REQUIRED — write FIRST, must FAIL before implementation)

- [X] T032 [P] [US1] Create `tests/Nabadat.Platform.M10.UnitTests/Auth/PasswordValidatorTests.cs` — cases: `ValidatePassword("short1!")` → Invalid (min 10 chars); `ValidatePassword("alllowercase1!")` → Invalid (no uppercase); `ValidatePassword("ALLUPPERCASE1!")` → Invalid (no lowercase); `ValidatePassword("NoSpecialChar1")` → Invalid (no special); `ValidatePassword("ValidP@ss1")` → Valid
- [X] T033 [P] [US1] Create `tests/Nabadat.Platform.M10.UnitTests/Auth/PasswordHasherTests.cs` — cases: `Verify(plain, Hash(plain))` → true; `Verify("wrong", Hash("correct"))` → false; `Hash` produces distinct outputs for same input (salt randomness)
- [X] T034 [P] [US1] Create `tests/Nabadat.Platform.M10.UnitTests/Auth/TenantAuthenticationServiceTests.cs` — cases: `CreateUser(username="not-an-email")` → returns Invalid; `CreateUser("alice@example.com")` when email exists → returns Conflict; `ValidateCredentials("alice", "CorrectPassword")` with enrolled MFA → returns `ValidCredentials` with pending challenge; `ValidateCredentials("bob", "CorrectPassword")` when `IsMfaEnrolled=false` → returns `RequiresMfaEnrollment`; `ValidateCredentials(userId)` when `status=locked` and `lockedUntilUtc > now` → throws `AccountLockedException`
- [X] T035 [P] [US1] Create `tests/Nabadat.Platform.M10.UnitTests/Auth/MfaChallengeValidatorTests.cs` — cases: `VerifyTotpCode(userId, validCode)` → creates `SessionToken` and emits `authentication.mfa.succeeded` event; `VerifyTotpCode(userId, "000000")` invalid code → throws `MfaValidationException` and emits `authentication.mfa.failed` event; same code accepted twice in same step → rejected (anti-replay)
- [X] T036 [P] [US1] Create `tests/Nabadat.Platform.M10.UnitTests/Auth/SessionServiceTests.cs` — cases: `CreateSession(userId)` with no prior version mismatch → returns snapshot from session; `CreateSession(userId)` after permission change (`snapshotVersion` bumped) → rebuilds snapshot; `ValidateSession(token)` with expired `absoluteExpiresAtUtc` → returns null; sliding-window TTL reset on activity
- [X] T037 [P] [US1] Create `tests/Nabadat.Platform.M10.UnitTests/Auth/AccountLockoutTests.cs` — cases: `RecordFailedAttempt(userId)` on 5th failure → sets `status=locked`, sets `lockedUntilUtc = now+cooldown`, emits `authentication.account.locked`; after cooldown expires, next auth attempt → unlocks account, resets `failedAttemptCount`, emits `authentication.account.unlocked`; manual unlock before cooldown → emits event immediately
- [X] T038 [P] [US1] Create `tests/Nabadat.Platform.M10.UnitTests/Auth/PasswordResetServiceTests.cs` — cases: `RedeemPasswordResetToken(tokenHash)` when `expiresAtUtc < now` → throws `TokenExpiredException`; when `usedAtUtc != null` → throws `TokenAlreadyUsedException`; when `revoked = true` → throws `TokenRevokedException`; successful redemption → `usedAtUtc` set and `password.reset.completed` event published; `RequestPasswordReset` when M-09 throws → transaction rolled back, token NOT persisted
- [X] T039 [P] [US1] Create `tests/Nabadat.Platform.M10.UnitTests/Auth/PasswordResetRateLimitTests.cs` — cases: 3rd request in window → allowed; 4th request → rejected with rate limit; window expires after 30 min → counter resets; `password.reset.rate_limited` event published on rejection
- [X] T040 [P] [US1] Create `tests/Nabadat.Platform.M10.UnitTests/Events/M17EventPublisherTests.cs` — cases: `PublishAsync(tx, event)` writes row to `event_log` within transaction; `PublishAsync` when `event_log` write fails → throws and allows caller to roll back; `PublishAsync` called exactly once per auditable action with correct `EventType` and payload fields

### T041R — Red Checkpoint for User Story 1

- [X] T041R [US1] Run `dotnet test tests/Nabadat.Platform.M10.UnitTests`; confirm tests are RED (compile error or assertion failure expected since services not yet implemented); paste failing output as evidence in commit message; commit red baseline via `/speckit-git-commit` before any implementation task

### Implementation for User Story 1

- [X] T042 [P] [US1] Create `src/Nabadat.Platform.M10/Application/Auth/PasswordValidator.cs` — enforces min 10 chars, uppercase, lowercase, digit, special character; returns `ValidationResult` (Valid or Invalid with field-level error codes)
- [X] T043 [P] [US1] Create `src/Nabadat.Platform.M10/Infrastructure/Persistence/TenantUserRepository.cs` implementing `ITenantUserRepository` — Npgsql-based; all methods scoped to current tenant schema
- [X] T044 [P] [US1] Create `src/Nabadat.Platform.M10/Infrastructure/Persistence/AuthSessionRepository.cs` implementing `IAuthSessionRepository`
- [X] T045 [US1] Create `src/Nabadat.Platform.M10/Application/Auth/TenantAuthenticationService.cs` — `ValidateCredentialsAsync(username, password)`: validate email format, load user, bcrypt verify, check lockout, return `ChallengeIssuedResult` or `RequiresMfaEnrollmentResult` (depends T043, T026)
- [X] T046 [US1] Create `src/Nabadat.Platform.M10/Application/Auth/MfaEnrollmentService.cs` — `InitiateEnrollmentAsync(challengeId)` → generates TOTP secret via `TotpService`, envelope-encrypts via `MfaSecretEncryptionService`, returns OTP URI and enrollment token; `ConfirmEnrollmentAsync(enrollmentToken, totpCode)` → verifies code, stores encrypted secret, creates session (depends T027, T028)
- [X] T047 [US1] Create `src/Nabadat.Platform.M10/Application/Auth/MfaChallengeValidator.cs` — `VerifyAsync(challengeId, totpCode)`: decrypt `mfaSecretEncrypted`, call `TotpService.VerifyCode`, update `lastUsedTotpStep`, create session via `SessionService`, publish `authentication.mfa.succeeded` or `authentication.mfa.failed` event (depends T027, T028, T029)
- [X] T048 [US1] Create `src/Nabadat.Platform.M10/Application/Auth/SessionService.cs` — `CreateSessionAsync(userId)`: generate opaque token, hash it, build `PermissionSnapshot`, persist `AuthSession`; `ValidateSessionAsync(token)`: hash lookup, check TTL (sliding + absolute), rebuild snapshot on version mismatch, update `lastActivityAtUtc`; `InvalidateSessionAsync(sessionId)`: set `isActive=false`, publish `session.revoked` event (depends T044, T029)
- [X] T049 [US1] Create `src/Nabadat.Platform.M10/Application/Auth/PasswordResetService.cs` — `RequestResetAsync(email)`: rate-limit check, generate token, call `IM09NotificationService.SendPasswordResetAsync` synchronously; if M-09 throws, rollback; publish `password.reset.requested`; `RedeemResetAsync(token, newPassword)`: validate token (expiry, used, revoked), validate password, bcrypt re-hash, publish `password.reset.completed` (depends T038, T029)
- [X] T050 [US1] Create `src/Nabadat.Platform.M10/Application/Auth/AccountLockoutService.cs` — `RecordFailedAttemptAsync(userId)`: increment `failedAttemptCount`; on 5th failure, set `status=locked`, set `lockedUntilUtc`, publish `authentication.account.locked`; `AutoUnlockIfExpiredAsync(userId)`: if locked and `lockedUntilUtc <= now`, clear lockout, reset counter, publish `authentication.account.unlocked`
- [X] T051 [US1] Implement `IM10AuthService` in `src/Nabadat.Platform.M10/Application/Auth/M10AuthService.cs` — thin wrapper delegating to `SessionService.ValidateSessionAsync`; returns `SessionContext` (userId, persona, permissionSnapshot) or null
- [X] T052 [US1] Create `src/Nabadat.Platform.M10/Api/AuthController.cs` — endpoints per `contracts/auth-api.md`: `POST /api/v1/auth/login`, `POST /api/v1/auth/mfa/enroll`, `POST /api/v1/auth/mfa/enroll/confirm`, `POST /api/v1/auth/mfa/verify`, `POST /api/v1/auth/logout`, `POST /api/v1/auth/password-reset/request`, `POST /api/v1/auth/password-reset/redeem`, `GET /api/v1/auth/session`; wire API-05 error envelope on all non-2xx responses
- [X] T053 [P] [US1] Create `frontend/src/features/auth/api.ts` — `loginStep1(email, password)`, `mfaVerify(challengeId, code)`, `mfaEnroll(challengeId)`, `mfaEnrollConfirm(enrollmentToken, code)`, `logout()`, `requestPasswordReset(email)`, `redeemPasswordReset(token, newPassword)`, `getSession()`; all use `Authorization: Bearer <token>` from `sessionStorage`
- [X] T054 [P] [US1] Create `frontend/src/features/auth/hooks/useSession.ts` — React context + provider; hydrates from `sessionStorage` on mount; exposes `session`, `signOut()`, `refreshPermissions()`; redirects to `/login` on 401
- [X] T055 [US1] Create `frontend/src/features/auth/pages/LoginPage.tsx` — email + password form per Nabadat design system; submits to `loginStep1`; on success navigates to `/auth/mfa` or `/auth/mfa/enroll`; shows API-05 error messages; RTL-first, `i18next` labels
- [X] T056 [US1] Create `frontend/src/features/auth/pages/MfaChallengePage.tsx` — 6-digit OTP input using `input-otp`; auto-submits on complete; shows lockout error with cooldown message; navigates to dashboard on success
- [X] T057 [US1] Create `frontend/src/features/auth/pages/MfaEnrollPage.tsx` — shows QR code via `qrcode.react`; backup Base32 secret reveal; OTP input to confirm enrollment; navigates to dashboard on success
- [X] T058 [US1] Create `frontend/src/features/auth/pages/PasswordResetPage.tsx` — two states: request form (email) and redemption form (token from URL + new password); password complexity indicator; shows rate-limit message on 429; RTL-first
- [X] T059 [US1] Create `frontend/src/features/auth/components/AuthGuard.tsx` — HOC/wrapper that redirects unauthenticated users to `/login`; used to protect all authenticated routes
- [X] T060 [P] [US1] Register auth routes in the frontend router (`frontend/src/App.tsx` or route config): `/login`, `/auth/mfa`, `/auth/mfa/enroll`, `/auth/password-reset`; wrap all other routes with `AuthGuard`

**Unit test gate (before integration tests)**: `dotnet test tests/Nabadat.Platform.M10.UnitTests` — ALL tests must be GREEN

### Integration Tests for User Story 1

- [X] T061 [P] [US1] Create `tests/Nabadat.Platform.M10.IntegrationTests/Endpoints/AuthEndpointTests.cs` — `POST_auth_login_returns_challengeId_when_credentials_valid`; `POST_auth_login_returns_401_when_credentials_invalid`; `POST_auth_mfa_verify_returns_sessionToken_when_code_valid`; `POST_auth_mfa_verify_returns_422_when_code_invalid`; `POST_auth_login_returns_423_when_account_locked`; `POST_auth_password_reset_request_returns_202_regardless_of_email_existence`
- [X] T062 [P] [US1] Create `tests/Nabadat.Platform.M10.IntegrationTests/Endpoints/MfaEnrollEndpointTests.cs` — `POST_auth_mfa_enroll_returns_otpauth_uri_when_challenge_valid`; `POST_auth_mfa_enroll_confirm_creates_session_when_totp_valid`
- [X] T063 [US1] Create `tests/Nabadat.Platform.M10.IntegrationTests/Scenarios/TenantLoginWithMandatoryMfaTests.cs` — full scenario: enroll MFA → login → MFA verify → session valid → logout → session invalid; lockout after 5 failures → auto-unlock after cooldown; password reset (self-service) complete round-trip

**Per-story checkpoint**: `dotnet test tests/Nabadat.Platform.M10.UnitTests && dotnet test tests/Nabadat.Platform.M10.IntegrationTests --filter "FullyQualifiedName~TenantLoginWithMandatory"` → both green; `cd frontend && npm run build` → green

### E2E Tests for User Story 1 🎭

- [X] T064 [P] [US1] Create `tests/Nabadat.TenantApp.E2ETests/AuthTests.cs` with `[TestClass]` — method `Login_creates_session_when_credentials_and_totp_valid`: navigates to login, fills email + password, submitted → MFA page, enters TOTP code, lands on dashboard; screenshot + trace attached; row added to `COVERAGE.md`
- [X] T065 [P] [US1] Add `[TestMethod] Login_shows_mfa_enrollment_when_user_has_no_mfa` to `AuthTests.cs` — signs in as `pending-enrollment` user, verifies QR code page, scans/confirms code, verifies session
- [X] T066 [P] [US1] Add `[TestMethod] Login_shows_error_when_totp_code_invalid` — enters wrong TOTP, verifies error message, no redirect to dashboard
- [X] T067 [P] [US1] Add `[TestMethod] PasswordReset_delivers_and_redeems_token` — requests reset via email form, uses token from dev seed, enters new password, verifies redirect to login — authored + compiles (was missing despite the ✅ COVERAGE row); runs as part of the E2E suite (T125)
- [X] T068 [P] [US1] Add `[TestMethod] PasswordReset_rate_limit_blocks_fourth_request` — submits reset 3 times, verifies 4th shows rate-limit message

**Checkpoint**: US1 fully functional and E2E-verified. The authenticated base for all subsequent stories is ready.

---

## Phase 4: User Story 2 — Permission Modules, Persona Baselines, and Data Layer Enforcement (Priority: P1)

**Goal**: P-01/P-07 can create users and manage permission module assignments; P-01 exclusively controls CX-domain modules; persona baselines are manageable via UI; enforcement is at the data layer.

**Independent Test**: `dotnet test tests/Nabadat.Platform.M10.IntegrationTests --filter "FullyQualifiedName~PersonaBaselineAndEnforcement"` and `dotnet test tests/Nabadat.TenantApp.E2ETests --filter "FullyQualifiedName~UserManagementTests|PersonaBaselineTests"`

### Unit Tests for User Story 2 (REQUIRED — write FIRST, must FAIL before implementation)

- [X] T069 [P] [US2] Create `tests/Nabadat.Platform.M10.UnitTests/Permissions/PermissionAssignmentServiceTests.cs` — cases: `AssignPermissionModule(asP07, targetUserId, SurveyBuilder)` → throws `ForbiddenException` at service layer (before DB write); `AssignPermissionModule(asP07, targetUserId, UserManagement)` → succeeds, persists assignment; `AssignPermissionModule(asP01, targetUserId, SurveyBuilder)` → succeeds; `CheckPermission(userId, CreateSurvey)` with no SurveyBuilder assignment → returns Denied; permission revocation + session refresh → CheckPermission returns Denied
- [X] T070 [P] [US2] Create `tests/Nabadat.Platform.M10.UnitTests/Permissions/UserCreationPolicyTests.cs` — cases: `CreateUser(asP01, userData)` → persists user with persona baseline permissions; `CreateUser(asP07, userData)` → persists user with persona baseline permissions; `CreateUser(asP02, userData)` → throws `ForbiddenException` at service layer; `CreateUser(asP08, userData)` → throws `ForbiddenException`
- [X] T071 [P] [US2] Create `tests/Nabadat.Platform.M10.UnitTests/Permissions/PersonaBaselineServiceTests.cs` — cases: `GetDefaultPermissionsForPersona(P-01)` → returns expected module access levels from authorization matrix; `UpdateBaseline(asP07, P-01, modules=[SurveyBuilder])` → throws `ForbiddenException` (CX-domain); `UpdateBaseline(asP07, P-01, modules=[UserManagement])` → succeeds and sets `isCustomised=true`; `persona_baseline.updated` event published on save
- [X] T072 [P] [US2] Create `tests/Nabadat.Platform.M10.UnitTests/Permissions/DataLayerAuthorizationGuardTests.cs` — cases: calling any service method with insufficient persona at the service layer (bypassing controller) → throws `ForbiddenException`; denied attempt publishes `permission.forbidden_attempt` event to M-17

### T073R — Red Checkpoint for User Story 2

- [X] T073R [US2] Run `dotnet test tests/Nabadat.Platform.M10.UnitTests`; confirm tests are RED (US2 test classes fail while US1 tests are green); commit red baseline

### Implementation for User Story 2

- [X] T074 [P] [US2] Create `src/Nabadat.Platform.M10/Infrastructure/Persistence/PermissionRepository.cs` implementing `IPermissionRepository` — `GetAssignmentsAsync(userId)`, `ReplaceAssignmentsAsync(userId, assignments, tx)`, `GetPersonaBaselineAsync(tenantId, personaId)`, `UpdatePersonaBaselineAsync(baseline, tx)` (control-plane DB)
- [X] T075 [P] [US2] Create `src/Nabadat.Platform.M10/Infrastructure/ControlPlane/PersonaBaselineRepository.cs` — queries `persona_baselines` control-plane table by `tenantId` + `personaId`; seed 8 rows (P-01..P-08) at tenant provisioning via `SeedPersonaBaselinesAsync(tenantId)`
- [X] T076 [US2] Create `src/Nabadat.Platform.M10/Application/Users/UserCreationPolicy.cs` — validates actor persona (P-01 or P-07 only); throws `ForbiddenException` for any other actor; publishes `user.created` event; returns created `TenantUser` with baseline permissions applied from `PersonaBaselineService`
- [X] T077 [US2] Create `src/Nabadat.Platform.M10/Application/Users/UserManagementService.cs` — `CreateUserAsync`, `DeactivateUserAsync`, `ReactivateUserAsync`, `UnlockUserAsync`, `AdminMfaResetAsync`, `AdminPasswordResetAsync`; each method enforces P-01/P-07 via `UserCreationPolicy`; publishes appropriate M-17 event per action; all in single transaction with the M-17 event write
- [X] T078 [US2] Create `src/Nabadat.Platform.M10/Application/Permissions/PermissionAssignmentService.cs` — `ReplacePermissionsAsync(actorId, targetUserId, assignments)`: enforces P-07 CX-module restriction via `DataLayerAuthorizationGuard`; replaces assignments atomically; increments `lastPermissionSnapshotVersion`; publishes `permission.modified` event
- [X] T079 [US2] Create `src/Nabadat.Platform.M10/Application/Permissions/DataLayerAuthorizationGuard.cs` — `EnforceCanAssignModule(actorPersona, moduleId)`: if actor is P-07 and moduleId is a CX-domain module → throws `ForbiddenException`; list of CX-domain modules: Survey Builder, Channel Management, Audience Management, Analytics and Reporting, Case Management, Alerts and Notifications, KPI Configuration
- [X] T080 [US2] Create `src/Nabadat.Platform.M10/Application/Permissions/PersonaBaselineService.cs` — `GetAllBaselinesAsync()`, `UpdateBaselineAsync(actorPersona, personaId, assignments)`: enforces CX-domain restriction for P-07 actors; marks `isCustomised=true`; publishes `persona_baseline.updated` event
- [X] T081 [US2] Implement `IM10PermissionService` in `src/Nabadat.Platform.M10/Application/Permissions/PermissionEvaluationService.cs` — `CheckPermissionAsync(userId, action, entityId?)`: loads `PermissionSnapshot` from session (version-checked); evaluates module modes + custom actions; returns `Allowed` or `Denied`
- [X] T082 [US2] Create `src/Nabadat.Platform.M10/Api/UsersController.cs` — all endpoints per `contracts/users-api.md`; `[Authorize]` on all methods; wire `SessionContext` from middleware; return 403 (not 401) on permission failure with API-05 envelope
- [X] T083 [US2] Create `src/Nabadat.Platform.M10/Api/PersonaBaselinesController.cs` — `GET /api/v1/persona-baselines` and `PUT /api/v1/persona-baselines/{personaId}` per `contracts/permissions-api.md`
- [X] T084 [P] [US2] Create `frontend/src/features/users/api.ts` — `listUsers(params)`, `createUser(data)`, `getUser(id)`, `updateUser(id, data)`, `deactivateUser(id)`, `reactivateUser(id)`, `unlockUser(id)`, `resetMfa(id)`, `adminPasswordReset(id)`, `updatePermissions(id, assignments)`
- [X] T085 [P] [US2] Create `frontend/src/features/persona-baselines/api.ts` — `listPersonaBaselines()`, `updatePersonaBaseline(personaId, data)`
- [X] T086 [US2] Create `frontend/src/features/users/pages/UserManagementPage.tsx` — data-dense table of tenant users; `InviteUserDialog` with email + persona select; columns: username, persona, status, MFA enrolled, actions (deactivate/unlock/reset MFA); cursor pagination; empty state; `UserManagement.Manage` permission gate on action buttons; RTL-first
- [X] T087 [US2] Create `frontend/src/features/users/pages/UserDetailPage.tsx` — route `/users/:userId`; shows profile + permission module assignment editor; P-07 actors see CX-domain modules disabled; saves via `updatePermissions`; shows `lastPermissionSnapshotVersion` as change indicator
- [X] T088 [US2] Create `frontend/src/features/users/components/InviteUserDialog.tsx` and `UserPermissionsEditor.tsx` — `InviteUserDialog`: Dialog with email input + persona Select (shadcn); `UserPermissionsEditor`: renders module rows with mode checkboxes; disables CX-domain rows for P-07 actors
- [X] T089 [US2] Create `frontend/src/features/persona-baselines/pages/PersonaBaselinePage.tsx` — route `/settings/persona-baselines`; lists all 8 personas; accordion per persona with module assignment editor; `isCustomised` badge; save with confirmation; access-denied state for non-P-01/P-07; sidebar nav entry under Settings
- [X] T090 [US2] Add User Management and Persona Baselines routes + sidebar nav entries in `frontend/src/App.tsx`; protect both routes with permission gate for `UserManagement.View`

**Unit test gate**: `dotnet test tests/Nabadat.Platform.M10.UnitTests` — ALL tests green (US1 + US2)

### Integration Tests for User Story 2

- [X] T091 [P] [US2] Create `tests/Nabadat.Platform.M10.IntegrationTests/Endpoints/UsersEndpointTests.cs` — `POST_users_returns_201_when_actor_is_P01`; `POST_users_returns_201_when_actor_is_P07`; `POST_users_returns_403_when_actor_is_P02`; `PUT_users_permissions_by_P07_with_CX_module_returns_403`; `PUT_users_permissions_by_P07_with_UserManagement_returns_200`; `GET_users_id_by_actor_without_permission_returns_403`
- [X] T092 [US2] Create `tests/Nabadat.Platform.M10.IntegrationTests/Scenarios/PersonaBaselineAndEnforcementTests.cs` — full scenario: P-01 creates user → baseline permissions applied → P-07 creates user → success → P-07 tries to assign CX module → 403 → P-01 assigns CX module → success → user's snapshot version incremented → next session refresh reflects updated permissions

**Per-story checkpoint**: unit + integration tests green; `npm run build` green

### E2E Tests for User Story 2 🎭

- [X] T093 [P] [US2] Create `tests/Nabadat.TenantApp.E2ETests/UserManagementTests.cs` — `UserManagement_P01_can_invite_user_and_see_in_list`: signs in as P-01, navigates to User Management, invites a new user, verifies user appears; `COVERAGE.md` row added
- [X] T094 [P] [US2] Add `[TestMethod] UserManagement_P01_can_edit_user_permissions` to `UserManagementTests.cs` — opens user detail, modifies permission module, saves, confirms snapshot version incremented
- [X] T095 [P] [US2] Add `[TestMethod] UserManagement_P07_cannot_assign_CX_domain_modules` — signs in as P-07, opens user detail, verifies CX-domain module rows are disabled/hidden
- [X] T096 [P] [US2] Create `tests/Nabadat.TenantApp.E2ETests/PersonaBaselineTests.cs` — `PersonaBaseline_P01_can_view_and_modify_baseline`: navigates to Persona Baselines, modifies a module assignment, saves; `PersonaBaseline_P03_cannot_access_page`: direct URL returns access-denied

**Checkpoint**: US1 + US2 fully functional and E2E-verified.

---

## Phase 5: User Story 3 — Custom Data Scope Rules and Hierarchy Cascade (Priority: P2)

**Goal**: Admins can assign M-13 parameter scope values and hierarchy nodes to users; hierarchy cascade is downward-only; custom fine-grained action rules are enforceable.

**Independent Test**: `dotnet test tests/Nabadat.Platform.M10.IntegrationTests --filter "FullyQualifiedName~DataScopeAndHierarchyCascade"` and `dotnet test tests/Nabadat.TenantApp.E2ETests --filter "FullyQualifiedName~DataScopeTests"`

### Unit Tests for User Story 3 (REQUIRED — write FIRST, must FAIL before implementation)

- [X] T097 [P] [US3] Create `tests/Nabadat.Platform.M10.UnitTests/Permissions/DataScopeRuleServiceTests.cs` — cases: `EvaluateDataScope(userId, parameters)` when allowed values include `Riyadh` and `Dammam` → only those values permitted; `EvaluateDataScope` with value not in `allowedValues` → excluded; `StoreM13ParameterDefinitions(payload)` with invalid `allowedValues=[]` → throws `ValidationException`
- [X] T098 [P] [US3] Create `tests/Nabadat.Platform.M10.UnitTests/Permissions/HierarchyCascadeServiceTests.cs` — cases: `EvaluateHierarchyScope(nodeId)` → includes descendants via materialized path query; excludes nodes not in the path prefix (siblings and ancestors); root node → returns all nodes in tree; `EvaluateActionPermission(userId, UpdateSurvey)` when custom rule allows update but not delete → returns Allowed for update, Denied for delete
- [X] T099 [P] [US3] Create `tests/Nabadat.Platform.M10.UnitTests/Permissions/M13ParameterContractAdapterTests.cs` — cases: `StoreParameterDefinitions(payload)` persists parameter names and allowed values without hardcoded provider logic; payload with reserved parameter name → rejected; payload exceeding 500 definitions → rejected

### T100R — Red Checkpoint for User Story 3

- [X] T100R [US3] Run `dotnet test tests/Nabadat.Platform.M10.UnitTests`; confirm US3 tests are RED while US1+US2 remain green; commit red baseline

### Implementation for User Story 3

- [X] T101 [P] [US3] Create `src/Nabadat.Platform.M10/Infrastructure/Persistence/DataScopeRepository.cs` — `GetScopeAssignmentsAsync(userId)`, `ReplaceScopeAssignmentsAsync(userId, assignments, tx)`, `GetParameterDefinitionsAsync()`, `StoreParameterDefinitionsAsync(params, tx)`
- [X] T102 [US3] Create `src/Nabadat.Platform.M10/Application/Permissions/DataScopeRuleService.cs` — `EvaluateDataScopeAsync(userId, parameterName) → IReadOnlyList<string>` (allowed values); `AssignScopeAsync(actorId, targetUserId, assignments)`: validates values against `data_scope_parameter_definitions`, persists, publishes `scope.assigned` event
- [X] T103 [US3] Create `src/Nabadat.Platform.M10/Application/Hierarchy/HierarchyCascadeService.cs` — `GetDescendantNodeIdsAsync(nodeId) → IReadOnlyList<Guid>`: queries `organization_hierarchy_nodes` using materialized path prefix `LIKE '{path}%'`; called by `PermissionSnapshot` builder during session creation/refresh; reads hierarchy via `IM11TenantService` (manual) or `IM13IntegrationService` (integration), selected by `M10Config.hierarchySource`
- [X] T104 [US3] Create `src/Nabadat.Platform.M10/Application/Permissions/M13ParameterContractAdapter.cs` — validates M-13 payload, stores/updates `data_scope_parameter_definitions` records; rejects reserved names, empty values, >500 definitions
- [X] T105 [US3] Update `src/Nabadat.Platform.M10/Application/Permissions/PermissionEvaluationService.cs` — integrate `HierarchyCascadeService` and `DataScopeRuleService` into `CheckPermissionAsync`; entity-level scope check uses `HierarchyDescendantIds` from snapshot; custom action rules are evaluated after module-level check
- [X] T106 [US3] Create `src/Nabadat.Platform.M10/Api/DataScopeController.cs` — all endpoints per `contracts/permissions-api.md`: `GET /api/v1/users/{id}/scope`, `PUT /api/v1/users/{id}/scope`, `POST /api/v1/users/{id}/custom-rules`, `PUT /api/v1/users/{id}/custom-rules/{ruleId}`, `DELETE /api/v1/users/{id}/custom-rules/{ruleId}`, `POST /api/v1/authorization/scope/parameters`
- [X] T107 [P] [US3] Create `frontend/src/features/data-scope/api.ts` — `getUserScope(userId)`, `updateUserScope(userId, data)`, `createCustomRule(userId, data)`, `updateCustomRule(userId, ruleId, data)`, `deleteCustomRule(userId, ruleId)`
- [X] T108 [US3] Create `frontend/src/features/data-scope/pages/UserScopePage.tsx` — route `/users/:userId/scope`; shows parameter scope assignments (tag-style multi-select per parameter); hierarchy node picker (tree or select from `organization_hierarchy_nodes`); custom rules list with editor (`CustomRuleEditor`); save all; access-denied state for non-admins; RTL-first; sidebar: link from user detail page
- [X] T109 [US3] Create `frontend/src/features/data-scope/components/CustomRuleEditor.tsx` — form for `allowedActions` (multi-select from DOC-02 action codes) and `parameterScopeAssignments` (per-parameter value pickers); add/edit/delete rules

**Unit test gate**: `dotnet test tests/Nabadat.Platform.M10.UnitTests` — ALL tests green (US1+US2+US3)

### Integration Tests for User Story 3

- [X] T110 [P] [US3] Create `tests/Nabadat.Platform.M10.IntegrationTests/Endpoints/DataScopeEndpointTests.cs` — `POST_authorization_scope_parameters_stores_definitions`; `GET_users_id_scope_returns_active_assignments`; `PUT_users_id_scope_rejects_invalid_parameter_values`
- [X] T110a [P] [US3] Add `ReplaceScope` (`PUT /api/v1/users/{id}/scope`) definition×target hit/miss cases to `DataScopeEndpointTests.cs` — completes the 2×2 matrix that T110 starts (T110 already covers *definition-hit + valid value + target-hit → 200* and *definition-hit + invalid value + target-hit → 422 `value.not_allowed`*): `PUT_users_id_scope_returns_404_when_target_user_missing` — PUT for an unseeded `userId` (target **miss**) → 404; `PUT_users_id_scope_returns_422_when_parameter_definition_missing` — target **hit** + a `parameterName` never ingested (definition **miss**) → 422 envelope `scope.invalid_assignment` with detail code `parameter.not_found`. (Note: target-miss → 404 is HTTP-only — there is no browser path to PUT scope for a user the page never loaded, so it lives in this integration lane, not E2E.) — DONE: 2 cases added to `DataScopeEndpointTests.cs`; integration suite GREEN (5/5 in that class, Docker up)
- [X] T111 [US3] Create `tests/Nabadat.Platform.M10.IntegrationTests/Scenarios/DataScopeAndHierarchyCascadeTests.cs` — full scenario: ingest M-13 parameter definitions → assign `Riyadh,Dammam` scope to user → assign hierarchy node → query scope → verify only permitted branch values; assign hierarchy node → verify descendants included, sibling excluded

**Per-story checkpoint**: unit + integration tests green; `npm run build` green

### E2E Tests for User Story 3 🎭

- [X] T112 [P] [US3] Create `tests/Nabadat.TenantApp.E2ETests/DataScopeTests.cs` — `DataScope_P01_can_assign_branch_scope_and_verify_filter`: assigns branch values, verifies filtered data surface; `DataScope_P01_can_assign_hierarchy_node`: assigns org node, verifies descendants visible; `DataScope_P01_can_create_custom_rule`: grants UpdateSurvey, verifies target user can update but not delete; `DataScope_non_admin_cannot_access_scope_page`: P-03 direct URL → access denied; `COVERAGE.md` rows added
- [X] T112a [P] [US3] Add browser-observable `ReplaceScope` hit/miss cases to `DataScopeTests.cs` (the page surfaces these states; the pure status-code matrix is T110/T110a): `DataScope_shows_error_when_value_not_in_definition` — on a loaded scope page (target **hit**), assign a value the `branch` definition disallows, or a parameter with no definition (definition **miss**), then Save → the page shows the `dataScope.invalid` ("values not allowed") alert and the save does not succeed; `DataScope_shows_load_error_for_unknown_user` — an admin direct-navigating to `/users/{unknownGuid}/scope` (target **miss** on the page's `getUserScope`) shows the `dataScope.loadError` state, not the editor. (Definition-**hit** success is already covered by DS-1; target-**miss** on the PUT itself → 404 has no browser path and stays in T110a.) `COVERAGE.md` rows DS-5/DS-6 added — DONE + RAN GREEN against the live stack: all 6 DataScope E2E tests (DS-1..DS-6) pass. Also fixed a latent strict-mode selector bug the first live run exposed — DS-1 and DS-5 matched two "Parameter name" inputs (parameter-scope card + custom-rule editor share the placeholder); scoped both to `.First`. COVERAGE DS-1..DS-6 → ✅ passing

---

## Phase 6: User Story 4 — Immutable Tenant Audit Trail (Priority: P1)

**Goal**: Every M-10 action publishes an event to M-17's `event_log` in the same transaction; the audit log is viewable by P-01 and P-07 via the frontend; entries are read-only.

**Independent Test**: `dotnet test tests/Nabadat.Platform.M10.IntegrationTests --filter "FullyQualifiedName~ImmutableAuditTrail"` and `dotnet test tests/Nabadat.TenantApp.E2ETests --filter "FullyQualifiedName~AuditLogTests"`

### Unit Tests for User Story 4 (REQUIRED — write FIRST, must FAIL before implementation)

- [X] T113 [P] [US4] Create `tests/Nabadat.Platform.M10.UnitTests/Events/AuditTransactionTests.cs` — cases: `PerformUserUpdate()` in a single transaction → commits entity change and M-17 event publication atomically; `PublishAsync()` when M-17 `event_log` write fails → rolls back entire transaction including entity change; `M17EventPublisher.Publish()` called exactly once per auditable action with correct `eventType` and payload
- [X] T114 [P] [US4] Create `tests/Nabadat.Platform.M10.UnitTests/Events/EventCoverageTests.cs` — cases: verify that `UserManagementService.DeactivateUserAsync` calls `M17EventPublisher.Publish` with `user.deactivated`; `SessionService.InvalidateSessionAsync` calls with `session.revoked`; `PermissionAssignmentService.ReplacePermissionsAsync` calls with `permission.modified`; each asserted via `NSubstitute` mock verify

### T115R — Red Checkpoint for User Story 4

- [X] T115R [US4] Run `dotnet test tests/Nabadat.Platform.M10.UnitTests`; confirm US4 tests are RED; commit red baseline — NOTE: retrofit GREEN baseline (US1–US3 already implemented the publish paths); committed as `T115R` (78 unit tests green)

### Implementation for User Story 4

- [X] T116 [US4] Audit `src/Nabadat.Platform.M10/Application/` — verify every service method that mutates state already calls `M17EventPublisher.PublishAsync` with the correct event type (US1–US3 implementations); add any missing publish calls; ensure all event payloads include `actorId`, `entityId`, `oldValue`, `newValue` per `data-model.md`
- [X] T117 [US4] Create `AuditLogController` (`Api/Controllers/AuditLogController.cs`) — `GET /api/v1/audit-log` per `contracts/permissions-api.md`; reads via M-10's own `IAuditLogReader` over `event_log` (`Infrastructure/Audit/AuditLogReader.cs`); cursor-based pagination + `page_size`; filters: `eventType`, `from`, `to`, `actorId`, `entityId`. NOTE: the original M-17 read seam was dropped — M-10 owns its audit cycle (gap-analysis I-02/I-04); the `UnavailableM17EventLogReader` 503 stub and `IM17EventLogReader`/`M17EventLog*` types were replaced by `IAuditLogReader` + `AuditLog{Filter,Entry,Page}`
- [X] T118 [P] [US4] Create `frontend/src/features/audit-log/api.ts` — `listAuditEvents(params)` with cursor pagination, event_type filter, date range filter
- [X] T119 [US4] Create `frontend/src/features/audit-log/pages/AuditLogPage.tsx` — route `/audit-log`; data-dense table: event type (colored badge), actor username, entity type, timestamp; filter bar: event type select + date range picker; cursor pagination; read-only (no edit/delete controls); empty state; RTL-first; sidebar nav entry for P-01/P-07 only

**Unit test gate**: `dotnet test tests/Nabadat.Platform.M10.UnitTests` — ALL tests green (US1+US2+US3+US4)

### Integration Tests for User Story 4

- [X] T120 [P] [US4] Create `tests/Nabadat.Platform.M10.IntegrationTests/Endpoints/AuditLogEndpointTests.cs` — `PUT_users_id_updates_user_and_writes_locked_audit_entry`; `POST_auth_logout_writes_session_revoked_event`; `PUT_users_id_permissions_revoke_persists_audit_history` — authored + compiles; green run pending Docker (daemon down in authoring session — run at the per-story checkpoint)
- [X] T121 [P] [US4] Create `tests/Nabadat.Platform.M10.IntegrationTests/Services/AuditTransactionIntegrationTests.cs` — verifies that aborting the outer transaction (simulated by rollback after entity write but before event write) rolls back both entity and event; verifies append-only semantics (no UPDATE on event rows) — GREEN (Docker up)
- [X] T122 [US4] Create `tests/Nabadat.Platform.M10.IntegrationTests/Scenarios/ImmutableAuditTrailTests.cs` — full scenario: perform permission change → query audit log → verify event present with correct old/new values → attempt to query audit log as P-03 → 403; verify no audit entry can be modified or deleted via any API — GREEN (Docker up). Event content verified via `event_log` (canonical store); the P-01 API read is asserted as authorised-not-403. (The read path is no longer M-17-gated — M-10 now owns the reader, so the assertion could later be tightened to verify the event in the response body.)

**Per-story checkpoint**: unit + integration tests green; `npm run build` green

### E2E Tests for User Story 4 🎭

- [X] T123 [P] [US4] Create `tests/Nabadat.TenantApp.E2ETests/AuditLogTests.cs` — `AuditLog_P01_can_view_recent_events`: navigates to Audit Log, verifies list shows events including permission changes; `AuditLog_P01_can_filter_by_event_type`: filters to `permission.modified`, verifies only matching rows; `AuditLog_P01_cannot_edit_records`: verifies no edit/delete buttons visible; `AuditLog_P03_cannot_access_page`: direct URL → access denied; `COVERAGE.md` rows added (AL-1..AL-4, 🟡 authored) — authored + compiles; green run pending the running stack at the checkpoint. (M-10 now owns the audit read, so AL-1/AL-2 are no longer M-17-blocked.)

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, documentation, and full regression validation.

- [X] T124 [P] Run full solution test: `dotnet test Nabadat.TenantAdmin.sln` — all unit + integration tests green — unit 78 + integration 25 green (Docker up); E2E project excluded here (T125's separate run). Fixed: M-17 reader DI registration (controller activation 500) + T120 update test FK (used persona change, not a synthetic org-node id)
- [ ] T125 [P] Run full E2E suite: `dotnet test tests/Nabadat.TenantApp.E2ETests` — all browser tests green — needs the full stack running (Postgres + TenantAdmin host + `npm run dev` + Playwright browsers). (No longer M-17-blocked — M-10 owns the audit read, so AL-1/AL-2 can pass with the stack up.)
- [X] T126 [P] Validate `quickstart.md` — walk through each step; update any command or path that changed during implementation — fixed: E2E `appsettings.local.json` shape (flat `e2e` section, not nested `E2ETestSettings`; point to the canonical `.example`), `playwright.ps1` path (`bin/Debug/net10.0/`), prerequisites install note, Step 2/3 test tables (audit unit + integration classes), and M-17/T127 caveats on the audit-log manual + E2E steps
- [X] T127 [P] ~~Verify `IM17EventLogReader.QueryM10EventsAsync` is fully implemented end-to-end in M-17~~ — **OBSOLETE / superseded.** The M-17 dependency was dropped: M-10 now owns the audit read via `IAuditLogReader` + `AuditLogReader` over its own `event_log` (gap-analysis I-02/I-04; `contracts/permissions-api.md` "Audit ownership"). There is no M-17 module to verify. The read path is exercised by the US4 integration tests + AL-1/AL-2 E2E directly.
- [ ] T128 [P] Review `appsettings.Development.json` — add M-10 config section: `lockoutDurationMinutes`, `sessionSlidingTtlMinutes`, `absoluteSessionLifetimeHours`, `passwordResetTokenTtlMinutes`, `passwordResetRateLimitCount`, `passwordResetRateLimitWindowMinutes`
- [X] T129 [P] Add i18n translation keys to `frontend/src/i18n/` for all new M-10 pages — Arabic (`ar.json`) and English (`en.json`); ensure all labels use `t()` calls; no hardcoded strings in pages or components — audit-log keys present in both locales (added T118/T119); swept all M-10 pages/components for hardcoded literals — fixed `AuthLayout` brand wordmark (was hardcoded "Nabadat", now `t("common.appName")` so Arabic shows "نبضات"); no other hardcoded attribute/JSX-text literals found; `npm run build` green
- [X] T130 [P] Validate RTL layout for all 8 M-10 frontend pages in browser — check: logical padding/margin (`ps-*`, `pe-*`), icon placement (`ms-2` / `me-2`), text direction, OTP input RTL behavior; fix any physical direction properties found — swept all M-10 pages + components: no physical-direction violations. Only physical usages are intentional/documented exceptions: `OtpField` (forced `dir="ltr"` western-digit boxes) and the shadcn `sidebar.tsx` primitive (physical CSS positioning). Page chrome uses logical props throughout. BROWSER PASS (Playwright, app forced to ar/rtl): captured all pages — login (wordmark "نبضات", labels/links/eye-icon mirrored), MFA OTP (correct LTR digit island in RTL page), user-management (sidebar right, primary CTA + actions at end), audit-log (graceful M-17-unavailable state in RTL), persona-baselines, user-scope (forms/buttons/checkboxes mirrored) — all RTL-correct. Fixed one shared-primitive physical prop found: `ui/select.tsx` `SelectValue` `text-left` → `text-start`. Flagged (pre-existing, app-wide, not RTL): base-ui `Select.Value` shows the raw value (e.g. "all") instead of the localized label until the item renders once; and pre-auth pages don't set `<html dir>` (only `AppLayout` runs `useDirection`), so true RTL-by-default would need `dir`/`lang` bootstrapped globally / in `index.html`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (US1)**: Depends on Phase 2
- **Phase 4 (US2)**: Depends on Phase 2 + Phase 3 (reuses Session + Auth middleware)
- **Phase 5 (US3)**: Depends on Phase 2 + Phase 4 (needs `PermissionEvaluationService` base)
- **Phase 6 (US4)**: Depends on Phase 2 — `M17EventPublisher` already exists; audit coverage depends on US1–US3 services being present; can begin in parallel with US3 if staffed
- **Phase 7 (Polish)**: Depends on all phases complete

### Within Each User Story

1. Unit tests written FIRST → T_XXR red checkpoint commit → implementation → integration tests → E2E tests
2. Entities/repositories before services, services before controllers, backend before frontend

### Parallel Opportunities

- All `[P]` tasks within a phase have no intra-phase file conflicts
- All unit test files per story can be written in parallel (different class files)
- All frontend page files can be written in parallel after backend contracts are stable
- E2E test methods within a `[TestClass]` can be developed in parallel (different methods)
- US3 and US4 can be worked in parallel by two developers once US2 is complete

---

## Parallel Example: User Story 1

```
Parallel — write simultaneously:
  T032 PasswordValidatorTests.cs
  T033 PasswordHasherTests.cs
  T034 TenantAuthenticationServiceTests.cs
  T035 MfaChallengeValidatorTests.cs
  T036 SessionServiceTests.cs
  T037 AccountLockoutTests.cs
  T038 PasswordResetServiceTests.cs
  T039 PasswordResetRateLimitTests.cs
  T040 M17EventPublisherTests.cs

→ T041R Red Checkpoint (sequential)

Parallel — implement simultaneously:
  T042 PasswordValidator.cs
  T043 TenantUserRepository.cs
  T044 AuthSessionRepository.cs

→ T045 TenantAuthenticationService.cs (depends on T043)
→ T046 MfaEnrollmentService.cs
→ T047 MfaChallengeValidator.cs (depends on T045)
→ T048 SessionService.cs
→ T049 PasswordResetService.cs
→ T050 AccountLockoutService.cs
→ T051 M10AuthService.cs
→ T052 AuthController.cs (depends on all services)

Parallel — frontend + integration tests:
  T053 auth/api.ts
  T054 useSession.ts
  T055 LoginPage.tsx
  T056 MfaChallengePage.tsx
  T057 MfaEnrollPage.tsx
  T058 PasswordResetPage.tsx
  T061 AuthEndpointTests.cs
  T062 MfaEnrollEndpointTests.cs

→ T063 TenantLoginWithMandatoryMfaTests.cs (scenario — sequential)

Parallel — E2E:
  T064 AuthTests — Login_creates_session
  T065 AuthTests — Login_shows_mfa_enrollment
  T066 AuthTests — Login_shows_error_invalid_totp
  T067 AuthTests — PasswordReset_delivers
  T068 AuthTests — PasswordReset_rate_limit
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1 (Auth core)
4. **STOP and VALIDATE**: `dotnet test tests/Nabadat.Platform.M10.UnitTests && dotnet test tests/Nabadat.Platform.M10.IntegrationTests --filter US1 && dotnet test tests/Nabadat.TenantApp.E2ETests --filter AuthTests && npm run build`
5. Users can now log in and access the platform with a secure, MFA-gated session

### Incremental Delivery

- MVP: Phase 1+2+3 → Secure login ✓
- Add US2 → User provisioning + permission management ✓
- Add US3 → Data scope + hierarchy ✓
- Add US4 → Audit trail completeness ✓
- Phase 7 → Polish and full regression ✓

### Build Gate (per task)

```powershell
# After each backend implementation task:
dotnet test tests/Nabadat.Platform.M10.UnitTests

# At each user-story checkpoint:
dotnet test tests/Nabadat.Platform.M10.UnitTests
dotnet test tests/Nabadat.Platform.M10.IntegrationTests --filter "FullyQualifiedName~{StoryTests}"
cd frontend && npm run build

# E2E (requires running stack):
dotnet test tests/Nabadat.TenantApp.E2ETests --filter "FullyQualifiedName~{StoryTests}"
```

---

## Notes

- `[P]` = different files, truly parallelizable by separate developers or Claude agents
- Red Checkpoint tasks (`T_XXR`) are non-negotiable; commit the failing test output before writing any implementation
- Tests in `Nabadat.Platform.M10.UnitTests` MUST stay pure (no I/O, no containers, runs in < 1 s per test)
- Integration tests spin up Testcontainers — require Docker; do not run between every implementation task
- E2E tests require frontend dev server + backend running; run at story checkpoint only
- Frontend pages follow Nabadat design system (CLAUDE.md) — use existing `ui/` primitives; no recreating what exists
- All frontend labels use `i18next`; no hardcoded Arabic or English strings in TSX
