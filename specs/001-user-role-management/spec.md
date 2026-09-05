# Feature Specification: User and Role Management Module (M-10)

**Feature Branch**: `[M-10-user-role-management]`

**Created**: 2026-06-08

**Status**: Draft

**Input**: User description: "Build the User and Role Management module (M-10) for the Nabadat multi-tenant VOC platform. This module covers three domains: authentication for tenant-side users, authorization via a modular permission system, and a full audit trail for all system changes. M-10 operates entirely within a single tenant boundary — it has no cross-tenant visibility and is not M-17, which is a separate module with its own user management for platform-level users. Authentication: tenant users must be able to log in using a username and password. MFA using a TOTP-based authenticator app (such as Google Authenticator) is mandatory — no session is established until both credential verification and MFA challenge succeed; first-time users and users who have had their MFA reset must complete MFA enrolment before any session is created. The system must be architecturally designed to support SSO in a future phase without structural rework; SSO scaffolding must accommodate directory-based login, Google OAuth/OIDC, built-in internal authentication, SAML 2.0 federation, and Nafath (the Saudi national digital identity service); SSO provider configuration is stored per tenant as a structured extensible identity provider record and no provider logic is hardcoded. SSO is not executed in Phase 1 but the data model and configuration surface must be fully forward-compatible. Authorization: the system is built on a modular permission catalogue defined in DOC-02 comprising nine permission modules — Survey Builder, Channel Management, Audience Management, Analytics and Reporting, Case Management, Alerts and Notifications, KPI Configuration, User Management, and Tenant Configuration — and M-10 owns the assembly mechanism that composes per-user permission sets from those building blocks without modifying the catalogue definitions. The system must expose a predefined authorization matrix mapping each platform persona (P-01 through P-08) to their default permission module access levels as the baseline for all user provisioning. P-01 (CX Program Manager) is the only persona permitted to create users or modify permission sets within a tenant and this restriction is enforced at the data layer not only in the UI. Administrators must be able to define custom authorization rules beyond predefined persona profiles; custom rules must support three capabilities: first, defining which actions a specific user may perform — including creating surveys, updating surveys, adding data, and deleting data, covering any action exposed by any permission module; second, restricting which data is visible to a user; third, applying data scope filters based on structured parameter sets received from M-13 (Integration Hub, Phase 1) — these parameters have predefined names and value sets, for example a branch parameter with allowed values Riyadh, Jeddah, and Dammam, and a user may be granted access to one or more values of a given parameter so that all data surfaces including NPS charts and dashboards are automatically filtered to only the permitted values; M-10 stores and enforces the filter assignments and M-13 supplies the parameter definitions and value sets through a defined integration contract. Data scope also cascades through the tenant organisational hierarchy: a user assigned to a hierarchy node sees that node and all descendant nodes and never sees siblings or ancestor nodes; this downward-cascade rule is absolute and cannot be overridden by custom authorization rules. The enforcement model is default-deny: a user with no permission module assigned for a capability has zero access to that capability with no implicit or inherited fallback. Permission checks are enforced at the boundary of every user-facing action by the module exposing that action; no module may skip enforcement on the assumption that an upstream caller already verified. Permission changes take effect at the next session refresh or sooner and a revoked permission must not remain exercisable by an in-progress session. Auditing: the module must maintain a complete and immutable audit trail for all changes made to any system entity within the tenant. Every audit record must capture the identity of the actor who performed the action, the action type performed, the entity type affected, the specific record identifier affected, the full old value before the change, the full new value after the change, and the UTC timestamp of the change. Audit records are append-only and immutable — corrections are recorded as new events referencing the prior record and the original record is never modified or deleted. Audit emission must occur within the same database transaction as the triggering action so that no committed change can exist without a corresponding audit entry. Audit data is tenant-scoped and must not leave the tenant's data residency boundary. Every user management action, permission assignment, permission modification, permission revocation, session event, and data scope change must produce an audit record. Out of scope for this specification: M-17 platform-level user management and its authentication infrastructure, the DOC-02 permission catalogue definitions themselves, cross-tenant user management, and any integration orchestration logic inside M-13 — M-10 consumes M-13's parameter output but does not own the integration pipeline that produces it."

## Clarifications

### Session 2026-06-08
- Q: Password reset approach → A & C
- Q: P-07 user management scope vs P-01 restriction → Both P-01 and P-07 may create tenant users and perform lifecycle operations (invite, MFA reset, deactivate, unlock); only P-01 may assign or modify CX permission modules.
- Q: Session TTL and concurrent session policy → Sliding-window session: TTL resets on every authenticated request; no concurrent session limit across devices; permission revocation takes effect within one refresh cycle.
- Q: Account lockout / brute-force protection → Lockout after 5 consecutive failed attempts (TOTP or password); auto-unlock after a configurable cooldown (default 15 minutes); all lockout and unlock events are audited.
- Q: Audit record ownership (M-10 vs M-17) → All audit/event records are written exclusively by M-17; M-10 publishes events to M-17's `event_log` in the same database transaction as the triggering action. M-10 does not own an audit table. The event catalogue must be extended to cover all M-10 audit events. `AuditRecord` is not a M-10 entity.
- Q: Password reset token delivery mechanism → M-10 calls M-09 synchronously via its published interface to trigger email/SMS delivery of the reset token in the same request/response cycle.
- Q: M-10 entity schema placement (DB-02 compliance) → `IdentityProviderConfig` lives in the control-plane database and retains `tenantId`; all other M-10 entities (`TenantUser`, `OrganizationHierarchyNode`, `AuthSession`, `PasswordResetToken`, and all join/assignment tables) live in the per-tenant schema and MUST NOT carry a `tenantId` column.
- Q: GP-02 encryption of credential fields → `mfaSecret` MUST be envelope-encrypted; `passwordHash` is NOT encrypted (one-way hash, no benefit). The encryption key source is deployment-mode-aware: CMK (cloud KMS) in SaaS mode, config-based key in on-premises mode (AD-05 compliance). A configuration flag determines which key source to use at runtime.
- Q: P-07 permission module assignment scope → P-07 may assign only the User Management and Tenant Configuration permission modules; P-01 retains exclusive authority over the 7 CX-domain modules (Survey Builder, Channel Management, Audience Management, Analytics and Reporting, Case Management, Alerts and Notifications, KPI Configuration).
- Q: M-09 failure mode during password reset → If the M-09 synchronous call fails or times out, the entire reset request fails; the token is NOT persisted; the user receives a retryable error response.
- Q: Absolute session lifetime cap → Default 24 hours, configurable per tenant (stored in M-11 tenant settings). Sliding-window TTL extends the session on activity but never past `absoluteExpiresAtUtc`.
- Q: `PersonaBaseline` storage strategy → Stored in the control-plane database with a `tenantId` FK (DB-02 exemption, same as M-18/M-19); each tenant has its own baseline records manageable through a per-tenant management screen. Global platform defaults are seeded at tenant provisioning and can be customised per-tenant thereafter.
- Q: Password complexity policy → Minimum 10 characters; must contain at least one uppercase letter, one lowercase letter, one digit, and one special character. No breach/dictionary check in Phase 1.
- Q: `OrganizationHierarchyNode` CRUD ownership → M-11 owns hierarchy CRUD (tenant configuration); P-01 and P-07 can manage nodes via M-11. M-13 may supply hierarchy data as an integration source. M-10 is configured per tenant with a `hierarchySource` setting (values: `manual` = M-11-managed, `integration` = M-13-supplied) and reads hierarchy nodes from the configured source for scope evaluation only.
- Q: Username format and uniqueness → Username IS the user's email address; unique within the tenant schema; email format is validated at user creation and update time.
- Q: Password reset token TTL default → 30 minutes default; configurable per tenant by P-01 or P-07 admins via M-11 tenant settings.
- Q: Time-bounded permission assignment (effectiveFrom/effectiveTo) in Phase 1 scope → Out of Phase 1 scope. `PermissionModuleAssignment` does not carry `effectiveFrom` or `effectiveTo`; permissions are indefinite until explicitly revoked. Time-bounded grants are deferred to Phase 2.
- Q: `allowedActions` representation in `PermissionModuleAssignment` vs `CustomAuthorizationRule` → Two different types. `PermissionModuleAssignment.allowedActions` stores coarse module-level access modes (e.g., `View`, `Manage`, `Full`) as defined by DOC-02 per module; consuming modules map these modes to individual actions. `CustomAuthorizationRule.allowedActions` stores fine-grained DOC-02 action codes (e.g., `UpdateSurvey`, `DeleteSurvey`) for explicit per-action overrides.
- Q: `CustomAuthorizationRule.restrictedEntities` meaning in Phase 1 → Removed from Phase 1. Data visibility restriction is fully expressed through M-13 parameter scope (`parameterScopeAssignments`) and hierarchy cascade. `restrictedEntities` is excluded from `CustomAuthorizationRule`; it may be added in Phase 2 if a concrete row-level masking use case emerges.
- Q: Application-level rate limiting on password reset endpoint → Per-account rate limiting in Phase 1: maximum 3 reset requests per email address per 30-minute window (default, configurable per tenant by P-01/P-07 via M-11). Requests exceeding the limit are rejected with a `429 Too Many Requests` response and the excess attempts are published as events to M-17. IP-level rate limiting is deferred to the infrastructure/gateway layer.
- Q: PersonaBaseline management screen Phase 1 scope → Full frontend + backend in Phase 1. The tenant portal includes a PersonaBaseline management page accessible by P-01 and P-07 users to view and customise per-persona permission module assignments. E2E coverage is added to User Story 2; the backend API (`GET/PUT /api/tenant/persona-baselines`) and the management page both ship in Phase 1.


## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tenant Login with Mandatory MFA (Priority: P1)

A tenant user signs in to the tenant portal using username and password, then completes a required TOTP MFA challenge before any session is issued. First-time users and users whose MFA has been reset must enroll an authenticator app before a session can be created.

**Why this priority**: Secure tenant access is the foundation of M-10; without mandatory MFA and the enrollment flow, the tenant boundary cannot be trusted.

**Independent Test**: This can be tested by attempting login with valid credentials and verifying that the session is not created until MFA is validated, and by verifying the enrollment requirement for first-time/reset MFA users.

**Acceptance Scenarios**:

1. **Given** a tenant user with valid username/password and an active MFA secret, **When** they submit credentials, **Then** the system responds with a pending MFA challenge and does not create an authenticated session.
2. **Given** a tenant user with a pending MFA challenge, **When** they submit the correct TOTP code, **Then** the system creates a session token and records a session event audit record.
3. **Given** a first-time tenant user or a user whose MFA has been reset, **When** they authenticate with valid credentials, **Then** the system requires MFA enrollment before any session is created.
4. **Given** a tenant user who submits an invalid TOTP code, **When** the MFA challenge is verified, **Then** the system denies access and records the failed authentication attempt.
5. **Given** a tenant user who has reached 5 consecutive failed authentication attempts, **When** they attempt to authenticate again, **Then** the system rejects the attempt with a lockout response, sets `status = locked` and `lockedUntilUtc` to the configured cooldown, and emits an audit record. After the cooldown expires the account auto-unlocks.
6. **Given** a tenant user who lost access to their password, **When** they request a self-service password reset, **Then** M-10 issues a single-use time-limited reset token, calls M-09 synchronously to deliver it via the tenant-configured email or SMS channel, and publishes a `password.reset.requested` event to M-17. **When** an administrator triggers a reset, **Then** M-10 sets the admin-reset flag, calls M-09 to notify the user, and publishes the event to M-17. In both cases the user must set a new password and re-enroll TOTP if MFA was reset.
7. **Given** a tenant user who has already submitted 3 self-service password reset requests within the current 30-minute window, **When** they submit a fourth reset request, **Then** M-10 rejects the request with `429 Too Many Requests`, does not issue a token, does not call M-09, and publishes a `password.reset.rate_limited` event to M-17.

**Unit Test Coverage**:

- **Units under test**: `TenantAuthenticationService`, `MfaEnrollmentService`, `SessionService`, `PasswordHasher`, `PasswordValidator`, `MfaChallengeValidator`, `TenantUserRepository`, `M17EventPublisher`.
- **Required cases**:
  - `CreateUser(username="not-an-email")` → returns `Invalid` (email format validation failure).
  - `CreateUser(username="alice@example.com")` when another user with the same email exists → returns `Conflict` (uniqueness violation within tenant schema).
  - `ValidatePassword("short1!")` → returns `Invalid` (fails minimum 10-character requirement).
  - `ValidatePassword("alllowercase1!")` → returns `Invalid` (missing uppercase).
  - `ValidatePassword("ValidP@ss1")` → returns `Valid`.
  - `ValidateCredentials("alice", "CorrectPassword")` → returns `ValidCredentials` with pending MFA challenge when MFA is enrolled.
  - `ValidateCredentials("bob", "CorrectPassword")` when `user.IsMfaEnrolled == false` → returns `RequiresMfaEnrollment` and no session is created.
  - `VerifyTotpCode(userId, "123456")` with valid TOTP → creates `SessionToken` and emits audit record `authentication.mfa.succeeded`.
  - `VerifyTotpCode(userId, "000000")` with invalid TOTP → throws `MfaValidationException` and emits audit record `authentication.mfa.failed`.
  - `RedeemPasswordResetToken(tokenHash)` when `expiresAtUtc < now` → throws `TokenExpiredException`; token is not consumed.
  - `RedeemPasswordResetToken(tokenHash)` when `usedAtUtc != null` → throws `TokenAlreadyUsedException`.
  - `RecordFailedAttempt(userId)` on the 5th consecutive failure → sets `status = locked`, sets `lockedUntilUtc = now + cooldown`, emits audit record `authentication.account.locked`.
  - `ValidateCredentials(userId)` when `status = locked` and `lockedUntilUtc > now` → throws `AccountLockedException` without incrementing `failedAttemptCount`.
  - `CreateSession(userId)` when permission set changed in the same tenancy → returns a token reflecting the latest permission snapshot.
- **Skip declaration**: `unit-tests: skipped — not applicable` is intentionally not used because backend units are required.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/auth/login` with valid and invalid credentials.
  - `POST /api/auth/mfa/verify` with valid and invalid TOTP.
  - `POST /api/auth/mfa/enroll` for first-time/reset users.
- **What's intentionally NOT covered end-to-end**: internal password hashing algorithms and TOTP time drift arithmetic, which are covered by unit tests.

**Scenario Test**:

- `scenario-test: TenantLoginWithMandatoryMfa`.

**E2E Test Coverage**:

- **User flows under test**: Tenant login flow (credential entry → MFA challenge → session), MFA enrollment flow for first-time users, password reset flow (request → new password entry).
- **Required scenarios**:
  - A user navigates to the tenant login page, enters valid credentials, is redirected to the MFA challenge page, enters the correct TOTP code, and lands on the authenticated dashboard.
  - A first-time user completes the login credential step and is redirected to the MFA enrollment page; after scanning the QR code and entering the first TOTP code, the session is created.
  - A user enters an incorrect TOTP code on the MFA challenge page and sees a validation error without a session being created.
  - A user navigates to the password reset page, submits their email, and receives confirmation that a reset link has been sent; after clicking the link they can enter a new password and are redirected to login.
  - A user who has exceeded the reset rate limit sees a `429`-equivalent message on the reset page and cannot request another token.

---

### User Story 2 - Permission Modules, Persona Baselines, and Data Layer Enforcement (Priority: P1)

Tenant administrators create users and assign persona-based default permission module access levels. Both P-01 (CX Program Manager) and P-07 (Tenant IT Administrator) may create users and perform user lifecycle operations; only P-01 may assign or modify CX permission modules. Both restrictions are enforced at the data layer.

**Why this priority**: Tenant user provisioning and baseline authorization are essential to let tenant teams work safely within the tenant boundary.

**Independent Test**: This can be tested by provisioning a user under both a P-01 actor and a P-07 actor, verifying the assigned default permission modules, and confirming that a non-P-01 actor cannot assign or update CX permission modules.

**Acceptance Scenarios**:

1. **Given** a P-01 or P-07 user authenticated in the tenant, **When** they create a new tenant user, **Then** the new user receives the persona default permission module assignments from the predefined authorization matrix.
2. **Given** a P-02..P-06 or P-08 user authenticated in the tenant, **When** they attempt to create a user, **Then** the operation is rejected with `403 Forbidden` and an audit record of the denied attempt is emitted.
3. **Given** a P-07 user authenticated in the tenant, **When** they attempt to assign or modify a CX-domain permission module (Survey Builder, Channel Management, Audience Management, Analytics and Reporting, Case Management, Alerts and Notifications, or KPI Configuration), **Then** the operation is rejected with `403 Forbidden` and an event is published to M-17. **When** they assign the User Management or Tenant Configuration module, **Then** the operation succeeds.
4. **Given** a tenant user with no assigned permission module for `Survey Builder`, **When** they request a `Survey Builder` action, **Then** the action is denied by the enforcing module and no implicit access is granted.
5. **Given** a permission revocation occurs while a user has an active session, **When** the user performs a guarded action after session refresh, **Then** the revoked permission is no longer exercisable.
6. **Given** a P-01 or P-07 user authenticated in the tenant portal, **When** they navigate to the PersonaBaseline management page, **Then** they can view the current permission module assignments for each persona (P-01..P-08) and modify them. **When** a change is saved, **Then** the updated baseline is persisted, marked `isCustomised = true`, and a `persona_baseline.updated` event is published to M-17.
7. **Given** a P-02..P-06 or P-08 user authenticated in the tenant portal, **When** they attempt to access the PersonaBaseline management page, **Then** the page is not accessible (hidden from navigation) and direct API calls return `403 Forbidden`.
8. **Given** a P-01 or P-07 user authenticated in the tenant portal, **When** they navigate to the User Management page, **Then** they can view the list of tenant users, invite a new user by entering email and persona, and see the new user appear in the list with default permissions applied.
9. **Given** a P-01 user authenticated in the tenant portal, **When** they open a user's detail page and modify their permission module assignments, **Then** the changes are saved and reflected on the user's next session refresh.

**Unit Test Coverage**:

- **Units under test**: `PermissionAssignmentService`, `PersonaBaselineService`, `PermissionCheckMiddleware`, `UserCreationPolicy`, `DataLayerAuthorizationGuard`, `PermissionMatrixRepository`, `M17EventPublisher`.
- **Required cases**:
  - `GetDefaultPermissionsForPersona(P-01)` → returns expected module access levels from the authorization matrix.
  - `CreateUser(asUserWithPersona=P-01, targetUserData)` → persists new user with baseline permissions.
  - `CreateUser(asUserWithPersona=P-07, targetUserData)` → persists new user with baseline permissions.
  - `CreateUser(asUserWithPersona=P-02, targetUserData)` → throws `ForbiddenException` at the data layer.
  - `AssignPermissionModule(asUserWithPersona=P-07, targetUserId, moduleId=SurveyBuilder)` → throws `ForbiddenException` at the data layer.
  - `AssignPermissionModule(asUserWithPersona=P-07, targetUserId, moduleId=UserManagement)` → succeeds and persists the assignment.
  - `CheckPermission(userId, Action.CreateSurvey)` when no `SurveyBuilder` assignment exists → returns `Denied`.
  - `CheckPermission(userId, Action.CreateSurvey)` after permission assignment removal and session refresh → returns `Denied`.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/users` by P-01 succeeds.
  - `POST /api/users` by P-07 succeeds.
  - `POST /api/users` by P-02 returns `403`.
  - `PUT /api/users/{id}/permissions` by P-07 with a CX-domain module returns `403`.
  - `PUT /api/users/{id}/permissions` by P-07 with User Management or Tenant Configuration module succeeds.
  - `GET /api/users/{id}` by a user without the required module returns `403` or `404` as appropriate.
- **What's intentionally NOT covered end-to-end**: the complete cross-module action enforcement inside every consuming module; those modules use their own integration tests to verify their permission boundary once M-10 permission decisions are available.

**Scenario Test**:

- `scenario-test: PersonaBaselineAndEnforcement`.

**E2E Test Coverage**:

- **User flows under test**: User Management page (invite user, view list, edit permissions); PersonaBaseline management page (view and customise per-persona module assignments); access denial for non-admin personas.
- **Required scenarios**:
  - P-01 authenticated user can navigate to the User Management page, invite a new user, and verify the user appears with the correct default permissions.
  - P-01 authenticated user can open a user's detail page, modify their permission module assignments, and confirm the change is saved.
  - P-07 authenticated user can invite new users and manage User Management/Tenant Configuration module assignments but cannot assign CX-domain modules (option is disabled or hidden).
  - P-01 authenticated user can navigate to the PersonaBaseline management page, view all persona configurations, modify a module assignment, and save successfully.
  - P-07 authenticated user can view and modify persona baselines in the same management page.
  - P-03 authenticated user cannot access the User Management or PersonaBaseline management pages (hidden from navigation; direct URLs return access-denied state).

---

### User Story 3 - Custom Data Scope Rules and Hierarchy Cascade (Priority: P2)

Tenant administrators define custom authorization rules that restrict actions, data visibility, and parameter-based scope values sourced from M-13. A user's tenancy hierarchy assignment cascades downward to grant access to descendant nodes only.

**Why this priority**: Data scope and hierarchy-aware visibility are required for secure enterprise segmentation and for the platform to respect branch-level restrictions.

**Independent Test**: This can be tested by assigning a user a branch parameter scope, then verifying the user can only access permitted values and that hierarchy descendants are included while siblings and ancestors are excluded.

**Acceptance Scenarios**:

1. **Given** a tenant user assigned to branch `Riyadh` with allowed values `[Riyadh, Dammam]`, **When** they query a scoped dataset, **Then** the returned data is filtered to only those allowed branch values.
2. **Given** a tenant user assigned to an organisational hierarchy node `Region A`, **When** they access data, **Then** they see records for `Region A` and all descendant nodes, and they do not see records for sibling or ancestor nodes.
3. **Given** custom rules grant `UpdateSurvey` for a specific user but not `DeleteSurvey`, **When** that user attempts each action, **Then** `UpdateSurvey` succeeds and `DeleteSurvey` is denied.
4. **Given** M-13 supplies parameter definitions and allowed values, **When** M-10 receives the contract payload, **Then** it stores the scope definitions and enforces them consistently across all guarded tenant data surfaces.
5. **Given** a P-01 or P-07 user authenticated in the tenant portal, **When** they navigate to a user's scope management page, **Then** they can view the user's current parameter scope assignments (e.g., branch values) and hierarchy node assignment, and modify them. **When** a change is saved, **Then** the new scope takes effect and a scope-change event is published to M-17.
6. **Given** a P-01 or P-07 user authenticated in the tenant portal, **When** they create a custom authorization rule for a user, **Then** the rule specifying allowed fine-grained actions and parameter scope assignments is saved and immediately applied to that user's permission evaluation.

**Unit Test Coverage**:

- **Units under test**: `DataScopeRuleService`, `HierarchyCascadeService`, `CustomAuthorizationRuleRepository`, `PermissionEvaluationService`, `M13ParameterContractAdapter`, `M17EventPublisher`.
- **Required cases**:
  - `EvaluateDataScope(userId, parameters)` when allowed values include `Riyadh` and `Dammam` → only those values are permitted.
  - `EvaluateHierarchyScope(nodeId)` for a parent assignment → includes descendants and excludes siblings/ancestors.
  - `EvaluateActionPermission(userId, UpdateSurvey)` when custom rule allows update but not delete → returns `Allowed` for update and `Denied` for delete.
  - `StoreM13ParameterDefinitions(payload)` → persists parameter names and allowed values without hardcoded provider logic.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/authorization/scope` to ingest M-13 parameter definitions.
  - `GET /api/users/{id}/scope` returns active scope assignments.
  - A protected data query using the enforcement boundary applies the stored parameter filters.
- **What's intentionally NOT covered end-to-end**: the M-13 integration pipeline that publishes parameters; M-10 only consumes the contract payload.

**Scenario Test**:

- `scenario-test: DataScopeAndHierarchyCascade`.

**E2E Test Coverage**:

- **User flows under test**: Admin assigning parameter scope and hierarchy nodes to a user; admin creating custom authorization rules; scope enforcement visible through filtered data access.
- **Required scenarios**:
  - P-01 authenticated user can navigate to a user's scope management page, assign branch parameter values (e.g., Riyadh, Dammam), and save; querying the data surface as that user returns only the permitted values.
  - P-01 authenticated user can assign a user to an organisational hierarchy node; the user subsequently sees data for that node and its descendants only.
  - P-01 authenticated user can create a custom authorization rule granting `UpdateSurvey` but not `DeleteSurvey`; the target user can update but not delete surveys in the portal.
  - P-02..P-06 or P-08 user cannot access the scope management or custom rule pages (hidden from navigation).

---

### User Story 4 - Immutable Tenant Audit Trail for All Management Actions (Priority: P1)

Every tenant-side user management, permission assignment, permission revocation, session event, and data scope change causes M-10 to publish an event to M-17's `event_log` in the same database transaction. M-17 owns and derives all audit log entries from these events.

**Why this priority**: Audit completeness and transactional coupling are required for compliance and for safe rollback semantics.

**Independent Test**: This can be tested by performing user-management and authorization changes and verifying audit entries are written and never updated or deleted.

**Acceptance Scenarios**:

1. **Given** a P-01 user changes another user's permission set, **When** the change commits, **Then** M-10 publishes a `permission.modified` event to M-17's `event_log` in the same transaction, carrying actor identity, entity ID, old value, new value, and UTC timestamp.
2. **Given** a user session is created or revoked, **When** the event occurs, **Then** M-10 publishes a `session.created` or `session.revoked` event to M-17's `event_log` in the same database transaction.
3. **Given** a correction to a prior action is needed, **When** the correction action is performed, **Then** M-10 publishes a new corrective event referencing the prior event ID; M-17 ensures the original event entry remains unchanged.
4. **Given** M-10 attempts to publish an event but the M-17 `event_log` write fails, **When** the transaction is evaluated, **Then** the entire transaction rolls back and the triggering entity change is not persisted.
5. **Given** a P-01 or P-07 user authenticated in the tenant portal, **When** they navigate to the Audit Log page, **Then** they can view a chronological list of all auditable events for the tenant, filter by event type and date range, and see actor identity, affected entity, and timestamp for each entry. Audit records are read-only and cannot be edited or deleted from the UI.

**Unit Test Coverage**:

- **Units under test**: `M17EventPublisher`, `UserManagementService`, `PermissionAssignmentService`, `SessionService`.
- **Required cases**:
  - `PublishEvent(actor, eventType, entityType, entityId, oldValue, newValue)` → writes event to M-17 `event_log` within the same transaction.
  - `PerformUserUpdate()` in a single transaction → commits entity change and M-17 event publication atomically.
  - `PublishEvent()` when M-17 `event_log` write fails → rolls back the entire transaction including the entity change.
  - `M17EventPublisher.Publish()` is called exactly once per auditable action with the correct event type and payload.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `PUT /api/users/{id}` updates a user and writes a locked audit entry.
  - `POST /api/auth/logout` or session invalidation writes a session event audit entry.
  - `PUT /api/v1/users/{userId}/permissions` (with module removal in the assignment set) revokes permissions and persists audit history; the resulting `permission.revoked` event is verified in the audit log.
- **What's intentionally NOT covered end-to-end**: audit retention and archival policies, which are out of scope for this module.

**Scenario Test**:

- `scenario-test: ImmutableAuditTrail`.

**E2E Test Coverage**:

- **User flows under test**: P-01 and P-07 users viewing and filtering the tenant audit log; non-admin personas denied access to the audit log page.
- **Required scenarios**:
  - P-01 authenticated user can navigate to the Audit Log page and see a list of recent audit events including user management actions, permission changes, and session events.
  - P-01 authenticated user can filter the audit log by event type (e.g., `permission.modified`) and date range and see matching records only.
  - P-01 authenticated user cannot edit or delete any audit record; all entries are read-only.
  - P-03 authenticated user cannot access the Audit Log page (hidden from navigation; direct URL returns access-denied state).

---

### Edge Cases

- What happens when a tenant has no P-01 user and a management action requires P-01 authorization? The system should prevent user creation/modification and flag tenant configuration for remediation.
- How does the system handle `M-13` parameter payloads with overlapping or invalid allowed values? It must validate against the registered parameter contract and reject invalid definitions with a clear error.
- What happens if MFA provisioning data is lost before enrollment completes? The user must be forced to restart enrollment; partial enrollment state cannot create a session.
- How does the system behave when a permission change occurs while a user has an active session? The change takes effect at the next sliding-window refresh cycle; the permission snapshot version on the session token is compared against the current version on each refresh, and a mismatch forces a re-evaluation. All concurrent sessions for that user are affected.
- What if a tenant user has both persona baseline permissions and custom authorization rules that conflict? Custom rules may narrow access but may not broaden beyond the default-deny baseline.
- What happens if M-09 is unavailable when a password reset is requested? The reset request fails entirely; the `PasswordResetToken` is NOT written to the database; M-10 returns a retryable error (e.g., `503 Service Unavailable`) so the user can retry. No partial state is left.
- What happens when a locked account's cooldown expires? The system auto-unlocks the account (sets `status = active`, clears `lockedUntilUtc`, resets `failedAttemptCount`) on the next authentication attempt after `lockedUntilUtc` has passed; no admin action is required. The auto-unlock event is audited.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST authenticate tenant users with username (email address) and password. The `username` field MUST be a valid email address, unique within the tenant schema, and validated at creation and update time. Duplicate email addresses within the same tenant MUST be rejected with a descriptive error.
- **FR-002**: System MUST enforce mandatory TOTP MFA for every tenant user login before creating a session.
- **FR-003**: System MUST require MFA enrollment for first-time users and users with reset MFA before any session is established.
- **FR-004**: System MUST store per-tenant identity provider configuration records that are extensible enough to support directory login, Google OAuth/OIDC, internal auth, SAML 2.0, and Nafath in future phases.
- **FR-005**: System MUST compose per-user permission sets from DOC-02 permission modules without modifying the catalogue definitions. Module assignments store coarse access modes (e.g., `View`, `Manage`, `Full`) as defined in DOC-02; consuming modules resolve individual actions from these modes. Custom authorization rules store fine-grained DOC-02 action codes (e.g., `UpdateSurvey`) for per-user overrides beyond the baseline mode grant.
- **FR-006**: System MUST expose and apply a predefined authorization matrix mapping personas P-01..P-08 to default permission module access levels.
- **FR-007**: Users with persona P-01 or P-07 may create tenant users and perform lifecycle operations (invite, MFA reset, deactivate, unlock). P-07 may assign or modify only the **User Management** and **Tenant Configuration** permission modules. P-01 retains exclusive authority over the 7 CX-domain modules: Survey Builder, Channel Management, Audience Management, Analytics and Reporting, Case Management, Alerts and Notifications, and KPI Configuration. All restrictions MUST be enforced at the data layer.
- **FR-008**: System MUST support custom authorization rules that define allowed actions, data visibility, and parameter-based scope restrictions from M-13.
- **FR-009**: System MUST enforce downward-cascading hierarchical scope: a user sees assigned nodes and all descendants only.
- **FR-010**: System MUST use default-deny authorization: no capability is granted unless explicitly assigned.
- **FR-011**: System MUST enforce permission checks at every user-facing boundary and not rely on upstream validation.
- **FR-012**: System MUST apply permission changes within one refresh cycle of the session's sliding-window TTL. Revoked permissions MUST NOT remain exercisable after the next token refresh. Multiple concurrent sessions per user are permitted; permission revocation applies to all active sessions at their next refresh.
- **FR-013**: M-10 MUST publish an event to M-17's `event_log` for every user management action, permission assignment/modification/revocation, session event, and data scope change. M-17 owns audit record creation; M-10 never writes `audit_log` directly.
- **FR-014**: Each event published to M-17 MUST carry: actor identity, event type, entity type, entity ID, full old value, full new value, and UTC timestamp.
- **FR-015**: The M-17 event publication MUST occur within the same database transaction as the triggering action so that no committed change can exist without a corresponding event record.
- **FR-016**: Events published by M-10 are tenant-scoped and M-17 MUST NOT expose them outside the originating tenant's data boundary.
- **FR-017**: Immutability and append-only semantics of the audit log are enforced by M-17; M-10 MUST NOT attempt to update or delete any previously published event.
- **FR-018**: System MUST store SSO provider configuration per tenant in a structured, extensible format; no provider logic is hardcoded in Phase 1.
- **FR-019**: System MUST validate M-13 parameter definitions and allowed values before persisting them.
- **FR-020**: System MUST produce clear `403 Forbidden` responses for unauthorized access attempts while avoiding information leakage.
- **FR-023**: System MUST lock a tenant user account after 5 consecutive failed authentication attempts (failed password or failed TOTP code). The lockout duration is configurable per tenant (default 15 minutes) and auto-expires without admin intervention. All lockout and auto-unlock events MUST be audited. P-01 and P-07 users MAY manually unlock an account before the cooldown expires; manual unlock events MUST also be audited.

- **FR-021**: System MUST support password recovery flows in Phase 1: both self-service email/code-based password reset and administrator-triggered password reset. Self-service resets MUST issue single-use expiring tokens and deliver them by calling M-09 synchronously via its published interface (email or SMS, per tenant configuration); if M-09 is unavailable or times out, the reset request MUST fail and the token MUST NOT be persisted — the caller receives a retryable error. Admin-triggered resets MUST create an admin-reset flag that requires the user to set a new password on next login and notifies the user via M-09. All reset actions MUST publish an event to M-17. Self-service reset requests MUST be rate-limited per email address: a maximum of 3 requests per 30-minute window (default; configurable per tenant by P-01/P-07 via M-11); requests exceeding the limit MUST be rejected with `429 Too Many Requests` and the excess attempt MUST be published as an event to M-17.
- **FR-022**: System MUST ensure password reset tokens are single-use, time-limited, scope-limited (only valid for password reset), and revocable. Token TTL defaults to 30 minutes and is configurable per tenant by P-01 or P-07 admins via M-11 settings. The reset flow MUST verify token validity (not expired, not used, not revoked) before allowing password change and must require TOTP re-enrollment if MFA secret was rotated or reset.
- **FR-028**: M-10 MUST be configurable per tenant with a `hierarchySource` flag (values: `manual` | `integration`). When `manual`, M-10 reads `OrganizationHierarchyNode` records managed by M-11. When `integration`, M-10 reads nodes supplied by M-13. In both cases M-10 accesses hierarchy data via the owning module's published interface and never writes hierarchy nodes directly.
- **FR-027**: Passwords MUST meet the following complexity requirements: minimum 10 characters; at least one uppercase letter, one lowercase letter, one digit, and one special character. The system MUST reject passwords that fail these requirements at creation and reset time with a descriptive validation error. Password breach or dictionary checking is out of scope for Phase 1.
- **FR-026**: System MUST seed a default `PersonaBaseline` record for each persona (P-01..P-08) into the control-plane database at tenant provisioning time. P-01 and P-07 users MUST be able to view and customise their tenant's persona baseline through a dedicated tenant portal management page (frontend + backend API both ship in Phase 1). The management page MUST be hidden from all other personas. All baseline changes MUST be published as events to M-17.
- **FR-025**: Sessions MUST expire no later than `absoluteExpiresAtUtc`, regardless of sliding-window activity. The absolute session lifetime defaults to 24 hours from `issuedAtUtc` and is configurable per tenant via M-11 settings. After expiry the session MUST be invalidated and the user MUST re-authenticate fully (including MFA).
- **FR-024**: The `mfaSecret` field MUST be envelope-encrypted before persistence and decrypted only at the point of TOTP verification. The encryption key source is determined by a deployment-mode configuration flag: cloud KMS CMK in SaaS mode (`ENABLE_MULTI_TENANT=true`), config-based symmetric key in on-premises mode (`ENABLE_MULTI_TENANT=false`). `passwordHash` is NOT envelope-encrypted. The key reference (`mfaSecretKeyRef`) MUST be stored alongside the ciphertext to support key rotation without re-enrolling users.

### Key Entities

- **TenantUser** *(tenant schema)*: represents a user inside a tenant boundary. Key attributes: `userId`, `username` (email address; unique within the tenant schema; serves as the login identifier), `passwordHash`, `isMfaEnrolled`, `mfaSecretEncrypted` (envelope-encrypted ciphertext), `mfaSecretKeyRef` (reference to the encryption key used — CMK key ID in SaaS, config key name in on-prem), `persona`, `status` (active | inactive | locked | pending-enrollment), `assignedPermissions`, `scopeAssignments`, `organizationNodeId`, `lastPermissionSnapshotVersion`, `failedAttemptCount`, `lockedUntilUtc`. No `tenantId` column — isolation is at the schema level (DB-02). `mfaSecret` is never stored in plaintext.
- **PersonaBaseline** *(control-plane database)*: represents the persona-to-permission-module mapping for P-01..P-08, scoped per tenant. Resides in the control-plane DB with a `tenantId` FK (DB-02 exemption applies, same pattern as M-18/M-19). Platform defaults are seeded at tenant provisioning; P-01 or P-07 admins may customise the baseline for their tenant via a management screen. Key attributes: `baselineId`, `tenantId`, `personaId`, `permissionModuleAssignments`, `defaultDataScopeRules`, `isCustomised`, `createdAt`, `updatedAt`.
- **PermissionModuleAssignment** *(tenant schema)*: represents a user's access to a permission module. Key attributes: `assignmentId`, `userId`, `moduleId`, `allowedModes` (coarse access modes, e.g., `View`, `Manage`, `Full`, as defined by DOC-02 per module). Note: `effectiveFrom`/`effectiveTo` are excluded from Phase 1; permissions are indefinite until explicitly revoked.
- **CustomAuthorizationRule** *(tenant schema)*: represents a tenant-specific rule beyond persona baselines. Key attributes: `ruleId`, `userId`, `allowedActions` (fine-grained DOC-02 action codes, e.g., `UpdateSurvey`), `parameterScopeAssignments`, `createdBy`, `createdAt`. Note: `restrictedEntities` excluded from Phase 1; data visibility restriction is covered by `parameterScopeAssignments` and hierarchy cascade.
- **IdentityProviderConfig** *(control-plane database)*: represents per-tenant SSO provider configuration metadata. Resides in the control-plane DB (not the tenant schema) so it is accessible during subdomain-based tenant resolution before a tenant connection is established. Key attributes: `providerId`, `tenantId`, `providerType`, `settings`, `isActive`, `createdAt`, `updatedAt`. Retains `tenantId` FK as a control-plane table (DB-02 exemption applies).
- **DataScopeAssignment** *(tenant schema)*: represents allowed parameter-based values sourced from M-13. Key attributes: `assignmentId`, `userId`, `parameterName`, `allowedValues`, `createdAt`.
- **OrganizationHierarchyNode** *(tenant schema)*: represents tenant organisational scope nodes. Owned and managed by M-11 (tenant configuration); M-10 reads nodes read-only for scope evaluation via M-11's published interface. May also be populated by M-13 when `hierarchySource = integration`. Key attributes: `nodeId`, `parentNodeId`, `name`, `path`, `source` (enum: manual | integration), `externalRef` (optional external ID for M-13-supplied nodes). No `tenantId` column (DB-02).
- **M-10 Audit Events (owned by M-17)**: M-10 does not own an audit table. Every auditable action publishes an event to M-17's `event_log` in the same database transaction. M-17 derives the `audit_log` entries from these events. Required M-10 event types (constitution amendment needed to register all): `user.created`, `user.updated`, `user.deactivated`, `user.reactivated`, `user.unlocked`, `role.assigned`, `role.revoked`, `permission.assigned`, `permission.modified`, `permission.revoked`, `session.created`, `session.revoked`, `mfa.enrolled`, `mfa.reset`, `authentication.succeeded`, `authentication.mfa.succeeded`, `authentication.mfa.failed`, `authentication.account.locked`, `authentication.account.unlocked`, `password.reset.requested`, `password.reset.completed`, `scope.assigned`, `scope.modified`, `scope.revoked`.
- **AuthSession** *(tenant schema)*: represents an authenticated user session. Key attributes: `sessionId`, `userId`, `issuedAtUtc`, `absoluteExpiresAtUtc`, `lastActivityAtUtc`, `permissionSnapshotVersion`, `isActive`. No `tenantId` column (DB-02). Session uses a sliding-window TTL that resets on every authenticated request; multiple concurrent sessions across devices are permitted; `absoluteExpiresAtUtc` caps the maximum session lifetime regardless of activity — default 24 hours from `issuedAtUtc`, configurable per tenant via M-11 settings.
- **PasswordResetToken** *(tenant schema)*: represents a single-use, time-limited password reset token. TTL defaults to 30 minutes, configurable per tenant via M-11. Key attributes: `tokenId`, `userId`, `tokenHash`, `expiresAtUtc`, `usedAtUtc`, `issuedBy` (enum: self-service|admin), `issuedVia` (email|sms|admin-api), `revoked`. No `tenantId` column (DB-02).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tenant users can authenticate with username/password and complete TOTP MFA enrollment/login with zero session created before successful MFA completion.
- **SC-002**: P-01 and P-07 users can create tenant users; only P-01 can assign or modify CX permission modules; unauthorized attempts return `403 Forbidden` at the data layer.
- **SC-003**: Custom data scope assignments from M-13 are enforced so users only see allowed parameter values and downward-hierarchy descendants.
- **SC-004**: Every tenant management action causes M-10 to publish an event to M-17's `event_log` in the same database transaction; no committed change exists without a corresponding M-17 event.
- **SC-005**: Permission revocation takes effect within one refresh cycle of the sliding-window session TTL; revoked permissions cannot be exercised after the next refresh across all concurrent sessions.
- **SC-006**: Tenant-level identity provider configuration is stored in an extensible structure that can support SSO providers without hardcoded logic in Phase 1.

## Assumptions

- Tenant users are authenticated and authorized entirely within the tenant schema; there is no cross-tenant user lookup.
- Phase 1 does not require executing SSO flows; only the configuration and contract model must be forward-compatible.
- M-13 is responsible for producing and delivering parameter definitions; M-10 only consumes the contract payload and persists the values. When `hierarchySource = integration`, M-13 also supplies the `OrganizationHierarchyNode` data.
- M-11 owns `OrganizationHierarchyNode` CRUD when `hierarchySource = manual`; M-10 reads hierarchy nodes via M-11's published interface and never writes them directly.
- The DOC-02 permission catalogue exists as a canonical external definition and is not modified by M-10.
- `PersonaBaseline` records are seeded from platform defaults at provisioning time; tenant admins may customise them thereafter via the management screen without requiring a schema migration.
- Session invalidation and refresh semantics are managed by M-10 and do not rely on an external global session service.
- M-09 is available as a synchronous dependency for password reset token delivery; M-10 calls M-09 via its published interface and does not dispatch email or SMS directly.
- M-11 tenant settings are readable by M-10 to resolve per-tenant configuration values (e.g., absolute session lifetime, lockout cooldown duration, password reset token TTL).
- The encryption key service (KMS in SaaS, config-based key store in on-prem) is available as a dependency for `mfaSecret` encryption and decryption; the key source is selected by the `ENABLE_MULTI_TENANT` deployment flag.
- M-17 is available as a synchronous dependency for event publication; M-10 writes to M-17's `event_log` within the same transaction as every auditable action.
- The tenant portal frontend ships in Phase 1 alongside the backend API. Pages included in Phase 1: Login, MFA Enrollment, MFA Challenge, Password Reset, User Management (list + invite + detail), PersonaBaseline Management, Data Scope & Custom Rules Management, and Audit Log.
