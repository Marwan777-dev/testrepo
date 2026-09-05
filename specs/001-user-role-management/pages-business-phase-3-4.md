# M-10 — Business of Pages: Phase 3 & Phase 4

**Module**: M-10 User and Role Management
**Scope**: Frontend pages and key components delivered in **Phase 3 (User Story 1 — Tenant Login with Mandatory MFA)** and **Phase 4 (User Story 2 — Permission Modules, Persona Baselines, and Data Layer Enforcement)**.
**Sources**: `docs/SRS-M10-User_and_Role_Management_v0.1.docx`, `docs/Nabadat Platform Definition.V1.docx`, `specs/001-user-role-management/spec.md`, `specs/001-user-role-management/tasks.md`.

> This document explains **what each page is for in business terms** — who uses it, why it exists, what decisions or outcomes it drives, and which functional requirements (FR) it satisfies. It is *not* a UI spec; styling rules live in `CLAUDE.md` (Nabadat Design System) and the technical task list lives in `tasks.md`.

---

## Background Context

Nabadat is a **multi-tenant, multi-language Voice-of-Customer (VOC) SaaS platform** for enterprise and government (banking, telecom, government). M-10 is the **tenant-side authority** for three things:

1. **Authentication** — who can sign in (credentials + mandatory MFA).
2. **Authorization** — what each user can do (persona baselines, permission modules, custom rules, data scope, hierarchy cascade).
3. **Auditing** — an immutable record of what changed (Phase 6).

M-10 operates **entirely within a single tenant boundary**. It does not manage platform-level (QBS) users — that is M-11. Every screen below is part of the **tenant portal** (`frontend/tenant-app/`), is **RTL-first (Arabic primary, English secondary)**, and must honour **default-deny** authorization: absence of a grant is denial.

### Persona quick reference (resolved per spec clarifications)

| Persona | Role | User-management capability in M-10 |
|---------|------|------------------------------------|
| **P-01** | CX Program Manager | Full: create users, lifecycle ops, assign **all** permission modules (incl. the 7 CX-domain modules), define custom rules |
| **P-07** | Tenant IT Administrator | Create users + lifecycle ops (invite, MFA reset, deactivate, unlock); may assign **only** the *User Management* and *Tenant Configuration* modules — **never** CX-domain modules |
| P-02..P-06, P-08 | Survey Designer, Channel Operator, Audience Manager, Insights Analyst, Case Worker, Read-Only Viewer | **No** user-management capability |

**The 7 CX-domain modules** (P-01 exclusive): Survey Builder, Channel Management, Audience Management, Analytics and Reporting, Case Management, Alerts and Notifications, KPI Configuration. The 2 non-CX modules any admin may touch: User Management, Tenant Configuration.

---

# Phase 3 — User Story 1: Tenant Login with Mandatory MFA (P1 · MVP)

**Business goal**: Every tenant user signs in securely with username/password **plus** a TOTP authenticator app. MFA is **mandatory and non-skippable** — no session is ever created on credentials alone. First-time users must enrol an authenticator before they can use the platform. Accounts lock after repeated failures, and users can recover access through a self-service password reset that routes through M-09 notifications.

**Why it matters**: This is the **secure front door** for government and banking tenants who demand institutional-grade access control. It is the MVP — until login works, no other M-10 capability is reachable. It establishes the authenticated session that **all** downstream authorization and audit checks reference.

**Backing requirements**: FR-M10-AUTH-001..006 (credential + mandatory MFA, first-time enrolment, enrolment-status flag), FR-023 (account lockout after 5 failures), FR-022 (single-use, time-limited password reset tokens), and the per-account reset rate limit (3 per 30-min window).

---

## 3.1 Login Page — `LoginPage.tsx` (T055)

**Route**: `/login`
**Primary user**: Every tenant user (all personas).

### What it does
The entry point to the tenant portal. The user enters their **email (username)** and **password**. On submit, the page calls step-1 authentication (`loginStep1`). The backend validates the email format, looks up the user, verifies the password (bcrypt), and checks lockout status — but **does not** create a session yet.

### Business behaviour & branching
- **Credentials valid + MFA already enrolled** → navigate to the **MFA Challenge** page (`/auth/mfa`). The session is still pending.
- **Credentials valid + user has never enrolled MFA** → navigate to the **MFA Enrolment** page (`/auth/mfa/enroll`). This enforces FR-M10-AUTH-004/006: enrolment is required *before* any capability is exposed, decided at the routing layer.
- **Credentials invalid** → show an API-05 error message; no information leak about whether the email exists.
- **Account locked** (after 5 failures) → show the lockout message with the cooldown so the user knows when they can retry, and that they may ask a P-01/P-07 admin for a manual unlock.

### Why it matters
This page guarantees the platform's core security promise: **password alone never grants access**. It also routes first-time users into enrolment so an account can never exist in a "credentials work, no MFA" state.

---

## 3.2 MFA Challenge Page — `MfaChallengePage.tsx` (T056)

**Route**: `/auth/mfa`
**Primary user**: Any enrolled tenant user mid-login.

### What it does
Presents a **6-digit TOTP input**. The user reads the current code from their authenticator app (e.g., Google Authenticator) and enters it; the field **auto-submits** when six digits are present. On success the backend creates the session (returns the session token) and the user lands on the dashboard.

### Business behaviour
- **Correct code** → session established, redirect to dashboard. The successful challenge is audited (`authentication.mfa.succeeded`).
- **Wrong code** → inline error, no redirect; the failed attempt counts toward lockout and is audited (`authentication.mfa.failed`).
- **Anti-replay** → a code already used within its time step is rejected even if still numerically valid.
- **Lockout reached** → the page surfaces the cooldown message.

### Why it matters
This is the page that makes MFA *mandatory rather than optional* (FR-M10-AUTH-002/003). The session is created **only** after this challenge succeeds — the credential step and the MFA step are one authentication exchange, never two independent gates.

---

## 3.3 MFA Enrolment Page — `MfaEnrollPage.tsx` (T057)

**Route**: `/auth/mfa/enroll`
**Primary user**: First-time users, or any user whose MFA was reset by an admin.

### What it does
Shows a **QR code** that the user scans with their authenticator app, plus the **Base32 secret** revealed as a backup for manual entry. The user then enters a generated TOTP code to **confirm** enrolment. On confirmation the encrypted MFA secret is stored, a session is created, and the user proceeds to the dashboard.

### Business behaviour
- Reached automatically when a user with `IsMfaEnrolled = false` passes the credential step.
- Also the destination after an **admin MFA reset** (FR-M10-AUTH-005) — a reset user must re-enrol before any new session.
- The MFA secret is **envelope-encrypted** before storage (KMS in SaaS, local AES on-prem) — the page never persists a plaintext secret.

### Why it matters
Enrolment is the one-time onboarding step that turns the mandatory-MFA promise into reality. By gating session creation behind a confirmed code, the platform proves the user actually controls the authenticator before trusting it — there is no "enrol later" escape hatch.

---

## 3.4 Password Reset Page — `PasswordResetPage.tsx` (T058)

**Route**: `/auth/password-reset`
**Primary user**: Any tenant user who has forgotten their password (self-service).

### What it does
A **two-state** page:
1. **Request state** — the user enters their email. The backend rate-limit-checks the request, generates a single-use, time-limited token, and asks **M-09** to send the reset email. The response is **always 202 regardless of whether the email exists** (no account enumeration).
2. **Redemption state** — reached from the emailed link (token in the URL). The user sets a new password; a **complexity indicator** guides them (min 10 chars, upper, lower, digit, special). On success they are redirected to login.

### Business behaviour
- **Rate limit**: a 4th request within a 30-minute window is rejected with a `429` and a clear rate-limit message; the excess attempt is audited (`password.reset.rate_limited`). Limit defaults to 3/30-min and is configurable per tenant.
- **Token validity**: redemption verifies the token is not expired, not used, and not revoked (FR-022). Default TTL 30 minutes, configurable per tenant.
- **Transactional safety**: if M-09 fails to send, the whole request rolls back and no token is persisted — the user is never left with a dangling token.

### Why it matters
Self-service recovery keeps users productive without burdening admins, while the rate limit and no-enumeration response protect against abuse and reconnaissance — a baseline expectation for banking/government tenants.

---

## 3.5 Auth Guard — `AuthGuard.tsx` (T059) *(component, not a page)*

**Used by**: every authenticated route in the tenant portal.

### What it does
A wrapper that checks for a valid session (hydrated from `sessionStorage`) before rendering any protected page. Unauthenticated users — or users whose session returns `401` (expired, revoked, or permissions changed beyond the refresh window) — are redirected to `/login`.

### Why it matters
It enforces the **default-deny** posture at the navigation layer: no protected screen renders without a live session. It is also the mechanism by which **permission revocation propagates to live sessions** (FR-M10-AUTHZ-021/022) — a `401` on session validation forces re-authentication and a fresh permission snapshot.

---

# Phase 4 — User Story 2: Permission Modules, Persona Baselines & Data-Layer Enforcement (P1)

**Business goal**: Tenant administrators provision users and control what each user can do. **Both P-01 and P-07** can create users and run lifecycle operations (invite, deactivate, reactivate, unlock, MFA reset, admin password reset). **Only P-01** may assign or modify the 7 CX-domain permission modules; P-07 is limited to the User Management and Tenant Configuration modules. Persona baselines (the default permission set per persona) are viewable and customisable per tenant. **Every restriction is enforced at the data layer**, not just hidden in the UI.

**Why it matters**: This is the heart of role-based access control for the tenant. It lets a CX Program Manager build out their team with exactly the right access, keeps sensitive CX configuration in P-01's hands, and gives the Tenant IT Administrator enough authority to run user operations without overreaching into business configuration. The data-layer enforcement (FR-M10-AUTHZ-004) means a forged API call cannot bypass the UI guard.

**Backing requirements**: FR-M10-AUTHZ-001..004 (permission set assembly, persona matrix, P-01/P-07 scoping, data-layer enforcement), FR-006 (persona authorization matrix), FR-026 (per-tenant persona baseline management page, seeded at provisioning, hidden from non-admins, all changes audited).

---

## 4.1 User Management Page — `UserManagementPage.tsx` (T086)

**Route**: `/users`
**Primary user**: P-01 and P-07 admins. Hidden from all other personas.

### What it does
The roster of all tenant users in a **data-dense table**: username, persona, status (active/locked/deactivated), MFA-enrolled indicator, and per-row actions (deactivate, unlock, reset MFA). Includes **cursor pagination**, an **empty state** that teaches first-time admins how to invite their first user, and an **Invite User** action that opens the `InviteUserDialog`.

### Business behaviour
- **Invite a user** → opens the dialog (4.3); on success the new user appears in the list with **persona-default permissions already applied** from the authorization matrix (FR-006).
- **Action buttons** (deactivate / unlock / reset MFA) are gated by the `UserManagement.Manage` permission — visible only to admins who hold it.
- **Lifecycle operations** map to the underlying `UserManagementService` and each emits an M-17 audit event.
- Both P-01 and P-07 see and use this page identically for lifecycle operations; the CX-module distinction only appears on the detail page (4.2).

### Why it matters
This is the day-to-day workspace for running a tenant's user base — onboarding new staff, locking out departing ones, and helping users who are stuck (unlock, MFA reset). It gives admins a single, auditable surface for the entire user lifecycle.

---

## 4.2 User Detail Page — `UserDetailPage.tsx` (T087)

**Route**: `/users/:userId`
**Primary user**: P-01 (full editing) and P-07 (restricted editing).

### What it does
Shows a single user's profile and a **permission-module assignment editor** (the `UserPermissionsEditor` component). An admin can change which permission modules — and at what access level — the user holds. Saving recomputes the user's permission snapshot and increments `lastPermissionSnapshotVersion`, shown as a change indicator.

### Business behaviour — the P-01 vs P-07 split
- **P-01** can edit **all** modules, including the 7 CX-domain modules.
- **P-07** sees the **CX-domain module rows disabled** (FR-M10-AUTHZ-003). If a P-07 actor forges a request to assign a CX module, the backend rejects it with **`403 Forbidden` at the data layer** and publishes a `permission.forbidden_attempt` event — UI disabling alone is explicitly non-compliant (FR-M10-AUTHZ-004).
- **Snapshot versioning** → the incremented version is how a permission change reaches the user: their next session refresh rebuilds the snapshot, so revocations and grants take effect within the refresh window (FR-M10-AUTHZ-021/022).

### Why it matters
This is where fine-grained access is actually shaped. The visible CX-module lock makes the governance model legible to the Tenant IT Administrator, while the server-side guard guarantees the rule holds even against a malicious or buggy client.

---

## 4.3 Invite User Dialog & Permissions Editor — `InviteUserDialog.tsx` + `UserPermissionsEditor.tsx` (T088) *(components)*

### `InviteUserDialog`
A dialog with an **email input** and a **persona Select**. Choosing a persona is the key business decision: it seeds the new user with that persona's **baseline permission modules** from the authorization matrix. Only P-01/P-07 can open it.

### `UserPermissionsEditor`
Renders one row per permission module with **mode checkboxes** (the access level). For **P-07 actors it disables the CX-domain rows**, mirroring the server-side restriction so the admin sees exactly what they are allowed to change.

### Why they matter
Persona-based provisioning means an admin doesn't hand-pick permissions for every new hire — they pick a role and the platform applies the right defaults, which the admin can then refine. This keeps provisioning fast *and* consistent with the persona matrix.

---

## 4.4 Persona Baseline Management Page — `PersonaBaselinePage.tsx` (T089)

**Route**: `/settings/persona-baselines`
**Primary user**: P-01 and P-07 admins. Hidden from all other personas; direct URL access returns an access-denied state.

### What it does
Lists all **8 personas (P-01..P-08)**, each in an **accordion** with its own module-assignment editor. An admin can view and **customise the default permission modules** a persona receives at provisioning. A persona that has been changed from the seeded default shows an **`isCustomised` badge**. Saving requires confirmation.

### Business behaviour
- Baselines are **seeded from platform defaults at tenant provisioning** and stored in the control-plane DB with a `tenantId` FK — each tenant has its own customisable copy (FR-026).
- On save, the baseline is persisted, marked `isCustomised = true`, and a **`persona_baseline.updated` event** is published to M-17.
- The same CX-domain restriction applies: a **P-07 actor cannot customise CX-domain assignments** on any persona; attempts are rejected server-side.
- Non-admin personas (P-02..P-06, P-08) never see the nav entry, and direct navigation is denied.

### Why it matters
Baselines are the **policy layer above individual users**: changing a persona's baseline shapes the defaults every future user of that persona inherits. This lets a tenant tune the platform's access model to its own org structure once, rather than repeating decisions per user — fully self-service, with no schema migration and a complete audit trail.

---

## Cross-cutting notes (both phases)

- **Audit everywhere**: every state-changing action on these pages (login result, MFA enrol/verify, lockout, password reset, user create/lifecycle, permission change, baseline change) publishes an event to M-17 in the **same transaction** as the change (FR-M10-AUDIT-006/007). Phase 6 adds the page to *view* this trail.
- **Default-deny**: no page grants implicit access; absence of a permission module is denial (FR-M10-AUTHZ-017/018).
- **Data-layer enforcement**: every UI gate (disabled CX rows, hidden nav, access-denied states) is **backed by a server-side check** that returns `403`. The UI restriction is a convenience, never the control (FR-M10-AUTHZ-004).
- **RTL-first & bilingual**: all pages are authored Arabic-first (فصحى) with English secondary, using logical CSS properties per the Nabadat Design System.
- **Navigation visibility**: User Management, Persona Baselines (and later Audit Log) appear in the sidebar **only for P-01/P-07**, governed by the per-persona nav allowlist.
