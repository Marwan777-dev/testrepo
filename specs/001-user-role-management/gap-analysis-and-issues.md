# M-10 — Gap Analysis & Issues Register (Docs vs. Spec vs. Tasks vs. Implementation)

**Module**: M-10 User and Role Management
**Scope**: Discrepancies and broken/missing end-to-end cycles across Phases 3–6, found by comparing the source docs against the spec, plan, tasks, and the implemented pages.
**Sources**: `docs/SRS-M10-User_and_Role_Management_v0.1.docx`, `docs/Nabadat Platform Definition.V1.docx`, `specs/001-user-role-management/{spec,plan,tasks}.md`, and `frontend/src/features/**`.
**Companion docs**: `pages-business-phase-3-4.md`, `pages-business-phase-5.md`, `pages-business-phase-6.md`.

> **Status legend** — 🔴 Blocker (a user cycle cannot complete) · 🟠 Major (contradiction or dropped requirement) · 🟡 Minor (consistency / traceability gap) · 🔵 Recommendation (architectural).

---

## Summary table

| ID | Severity | Issue | Where it breaks |
|----|----------|-------|-----------------|
| **I-01** | ✅ Resolved | No one sets a new user's password — invite has no credential step and no invitation/first-login cycle | User Management → login |
| **I-02** | ✅ Resolved | Audit Log page reads from M-17, a module that does not exist | Audit Log page |
| **I-03** | 🔴 Blocker | Scope page assigns parameter values & hierarchy nodes that are never created (no M-13/M-11) | User Scope page |
| **I-04** | ✅ Resolved | Audit ownership moved M-10 → M-17 (SRS says M-10 owns the trail) | Auditing architecture |
| **I-05** | 🟠 Major | P-07 can create users; SRS AUTHZ-003 says **only P-01** | Permissions model |
| **I-06** | 🟠 Major | Persona catalog mismatch: more personas in docs; labels disagree; P-08 is *internal* in Platform Def | Persona model |
| **I-07** | 🟠 Major | `View`/`Manage`/`Full` access modes in spec (FR-005) — absent from tasks & UI | Permission editor |
| **I-08** | 🟠 Major | No page/task for additive **or** restrictive custom permission rules | Custom rules |
| **I-09** | 🟠 Major | F-M10-07 Bulk provisioning — absent from spec/tasks | User provisioning |
| **I-10** | 🟡 Minor | F-M10-05 Session management — no dedicated session-management surface | Session lifecycle |
| **I-11** | 🟡 Minor | Hierarchy management owned by M-10 in docs, reassigned to M-11 in spec | Hierarchy |
| **I-12** | 🔵 Reco | Persistence uses raw Npgsql/SQL; consider EF Core | Data layer |

---

## 🔴 Blockers — broken end-to-end cycles

### I-01 — New-user password cycle is broken (who assigns the password?)

**Question raised:** *who assigns a new user's password — admin or user?* **Answer today: neither, cleanly.**

- **Evidence:** `InviteUserDialog` posts only `{ username, persona }` — there is **no password field and no temporary-password generation** ([InviteUserDialog.tsx:71](frontend/src/features/users/components/InviteUserDialog.tsx#L71)). The backend `CreateUserAsync` (T076/T077) likewise takes no password.
- **The gap:** A freshly invited user has **no credential**. The only paths to a usable password are:
  1. **Self-service reset** — but that requires the user to already know they exist and trigger "forgot password."
  2. **Admin-triggered reset** — `POST /api/v1/users/{userId}/password-reset` sets `requiresPasswordChange = true` ([users-api.md:222](specs/001-user-role-management/contracts/users-api.md)). This forces the admin to run a reset for **every** new user, one by one.
- **What's missing:** A proper **invitation / activation cycle** — invite creates the user in `pending-enrollment`, emails an invitation link (via M-09), and the user sets their own initial password + enrolls MFA on first login. The `TenantUser.status` enum already has `pending-enrollment`, so the data model supports it; the flow does not exist.
- **Recommendation:** Decide the model explicitly and write it into the spec:
  - **Option A (recommended): user-set via invitation link.** Invite → M-09 sends activation link → user sets password (FR-027 complexity) → MFA enroll. Reuses the password-reset token machinery (single-use, TTL).
  - **Option B: admin-set temporary password** with forced change on first login (`requiresPasswordChange`).
  - Either way, add the missing page/flow and a task; today the cycle silently dead-ends.
- **✅ RESOLUTION (option B):** the admin now sets the new user's **initial password at invite time**. `POST /api/v1/users` requires a `password`; `UserCreationPolicy.CreateUserAsync` validates it against `IPasswordValidator` (FR-027 complexity → 422 `users.weak_password`) and stores `IPasswordHasher.Hash(password)` on the new user (status stays `pending-enrollment`). The invited user signs in with that password and enrols MFA on first login — the onboarding cycle now completes. `InviteUserDialog` gained a password field with a live complexity checklist + show/hide. Verified: unit (`UserCreationPolicyTests` hashes/stores + weak-password rejected), integration (`POST /users` 201 + weak→422), E2E (USR-1 invite). **Hardening follow-up (optional):** set `requiresPasswordChange = true` and add a first-login forced-change flow so the admin-set password is single-use — not implemented (no forced-change flow exists yet; flagging rather than setting an unenforced flag).

### I-02 — Audit Log page reads from a non-existent module (M-17)

- **Evidence:** `AuditLogController` (T117) reads via `IM17EventLogReader.QueryM10EventsAsync`; the repo ships [`UnavailableM17EventLogReader.cs`](src/Nabadat.Platform.M10/Infrastructure/Audit/UnavailableM17EventLogReader.cs) — a **fallback stub for when M-17 is absent**. There is **no M-17 project** in the solution (`M-17*/` = nothing).
- **The gap:** The Phase-6 Audit Log page (`AuditLogPage`, T119) has nothing real to read. Events are *published* to M-17's `event_log`, but the read side resolves to the "unavailable" stub, so the page cannot display history.
- **Root cause:** Audit ownership was reassigned to M-17 (see I-04), but M-17 is not defined in any doc and not built.
- **Recommendation:** Either (a) stand up M-17's `event_log` + `IM17EventLogReader` as a real dependency before Phase 6 is called done, or (b) **revert to the SRS model** and let M-10 own a read-only `audit_log` view of its own emitted events. Until one is chosen, mark Phase 6 as **blocked**, not complete.
- **✅ RESOLUTION (option b):** M-10 now owns the audit read. `IM17EventLogReader`/`UnavailableM17EventLogReader` (the 503 stub) were replaced by `IAuditLogReader` + `Infrastructure/Audit/AuditLogReader.cs`, which queries the `event_log` (`ITenantDbContext.EventLogs`) M-10 already writes — filtered, newest-first, keyset-paginated. The Audit Log page now reads real history. No M-17 module needed; T127 ("verify M-17 wired") is obsolete.

### I-03 — Scope assignment is bugged: scopes/nodes are never created

- **Evidence:** `UserScopePage` lets an admin pick **parameter values** (from `data_scope_parameter_definitions`, supplied by **M-13**) and a **hierarchy node** (from `organization_hierarchy_nodes`, owned by **M-11** per FR-028). Neither M-13 nor M-11 exists in this repo, and there is **no M-10 surface to create** parameter definitions or hierarchy nodes.
- **The gap (the cycle the user flagged):** The page assumes the *catalog of assignable things already exists*. With no M-13 ingestion and no M-11 hierarchy CRUD, the pickers are **empty** — you cannot assign a branch value or a node because none were ever created. Assigning scope is therefore a no-op / broken flow.
- **Contributing contradiction:** Docs (F-M10-04) place **hierarchy management inside M-10**; spec (FR-028) moved CRUD to M-11. By removing it from M-10 *without* M-11 existing, the create-half of the cycle vanished entirely (see I-11).
- **Recommendation:**
  - Short term (to make the page functional in Phase 1): add an M-10-owned **minimal seeding/admin surface** — a parameter-definition ingest endpoint (`POST /api/authorization/scope/parameters`, already in T106) wired to a small admin screen, and a temporary hierarchy-node entry path — so the scope page has data to bind to.
  - Long term: keep the M-11/M-13 boundary but **gate the scope page** behind a "no parameters/nodes configured yet" empty state that links to where they're created, instead of showing empty pickers that look broken.

---

## 🟠 Major — contradictions & dropped requirements

### I-04 — Audit trail ownership: M-10 (docs) vs. M-17 (spec)

- **Docs:** SRS §6 — *"M-10 maintains a complete and immutable audit trail."* §7 lists **no** M-17 dependency.
- **Spec:** Clarification ([spec.md:18](specs/001-user-role-management/spec.md)) + FR-013/017 — *all* audit records owned by **M-17**; M-10 only publishes events.
- **Impact:** Directly causes I-02. Also: M-17 is **not defined in any doc** (Platform Definition module map stops at M-16) — so M-10's entire audit story depends on an undocumented, unbuilt module.
- **Recommendation:** Resolve the ownership decision against DOC-04 (Module Inventory). If M-17 is real, document it and build the read side; if not, this is a spec defect — restore M-10 audit ownership.
- **✅ RESOLUTION:** Audit ownership restored to **M-10** (the SRS §6 model). M-10 writes *and* reads its own `event_log`; the M-17 read seam was removed (see I-02). The spec/contract (permissions-api.md "Audit ownership") and data-model.md now state M-10 ownership. A future cross-module audit aggregator, if any, can consume the same `event_log` without changing M-10.

### I-05 — User-creation authority: P-01 only (docs) vs. P-01 + P-07 (spec)

- **Docs:** SRS AUTHZ-003 — *"Only persona P-01 may create users or modify permission sets… No other persona."* §5.2.1 — P-07 = *"No user-management capability (per spec)."*
- **Spec:** FR-007 — **both P-01 and P-07** create users + lifecycle; P-07 limited to User Management + Tenant Configuration modules; P-01 exclusive over the 7 CX modules.
- **Status:** This is a deliberate clarification decision (the repo's pattern is that clarifications win — see `memory/project-m10-p01-vs-p07-conflict.md`), **but it contradicts the SRS in writing.**
- **Recommendation:** Add an explicit "supersedes SRS AUTHZ-003 / §5.2.1" note to the spec so the contradiction is documented, not silent.

### I-06 — Persona catalog mismatch (more personas in docs; labels disagree)

- **Three different persona lists:**

  | ID | SRS §5.2.1 | Platform Definition | Spec usage |
  |----|-----------|---------------------|-----------|
  | P-01 | CX Program Manager | CX Program Manager | ✓ tenant |
  | P-02 | Survey Designer | CX Analyst | tenant |
  | P-03 | Channel Operator | Survey Administrator | tenant |
  | P-04 | Audience Manager | Operational Manager | tenant |
  | P-05 | Insights Analyst | Frontline Performer | tenant |
  | P-06 | Case Worker | Executive Sponsor | tenant |
  | P-07 | Tenant Administrator | Tenant IT Administrator | tenant |
  | P-08 | Read-Only Viewer | **Tech Administrator — *internal* Nabadat CX** | tenant |
  | P-09 | — | Financial Administrator (internal) | — |
  | P-10 | — | Product Administrator (internal) | — |

- **Impact:** Every label except P-01 disagrees between the two docs. Critically, **Platform Definition marks P-08 as an *internal* Nabadat user**, yet the spec seeds a tenant `PersonaBaseline` for P-08 and the UI offers it as a tenant persona ([InviteUserDialog.tsx:29](frontend/src/features/users/components/InviteUserDialog.tsx#L29) lists `P-01..P-08`). P-09/P-10 (internal) are simply absent.
- **SRS already flagged this** as open item **O-01** ("labels are placeholders pending DOC-02").
- **Recommendation:** Lock the canonical persona catalog from DOC-02 before finalizing. Confirm whether P-08 is tenant or internal; if internal, **remove it from tenant invite options and baselines**. The UI currently shows bare IDs (`P-01`…) with no names — add the confirmed labels.

### I-07 — `View`/`Manage`/`Full` access modes: in spec, absent from tasks & UI

- **Spec:** FR-005 + `PermissionModuleAssignment.allowedModes` — modules store **coarse modes** (`View`, `Manage`, `Full`).
- **Gap:** `grep View|Manage|Full|allowedModes` over `tasks.md` returns **nothing** for the mode concept. The `UserPermissionsEditor` (T088) is described only as "mode checkboxes" with no defined mode set; no task defines the `View/Manage/Full` vocabulary, its per-module meaning, or how consuming modules resolve actions from it.
- **Impact:** The permission editor's semantics are underspecified — what a checkbox grants is ambiguous, and there's no test pinning the mode→action resolution.
- **Recommendation:** Add a task to define the access-mode enum and its DOC-02 mapping, and update `UserPermissionsEditor` + the API DTO to carry explicit modes. Add a unit-test case (`GetDefaultPermissionsForPersona(P-01)` already asserts "module access levels" — make the levels concrete).

### I-08 — No page/task for additive *or* restrictive custom permissions

- **Docs:** SRS AUTHZ-005/006 — custom rules must support fine-grained actions **and be expressible in both directions (additive *and* restrictive)** relative to the baseline.
- **Spec/impl:** `CustomRuleEditor` (T109) edits `allowedActions` (additive grants only). The spec edge case ([spec.md:265](specs/001-user-role-management/spec.md)) says custom rules *"may narrow … but may not broaden beyond default-deny"* — i.e., the **restrictive direction is dropped/contradicted**, and there's no UI affordance or task for it.
- **Impact:** The "restrict an action the persona baseline otherwise grants" use case has no home. SRS AUTHZ-006's "both directions" requirement is unmet.
- **Recommendation:** Decide the conflict-resolution order (SRS open item **O-07**) and either (a) implement restrictive rules with a clear precedence model, or (b) write an explicit "Phase 1: additive-only" scope note into the spec so the omission is intentional and documented.

### I-09 — F-M10-07 Bulk provisioning missing from spec/tasks

- **Docs:** Platform Definition lists **F-M10-07 Bulk provisioning** as an M-10 feature. (M-11 F-M11-08 "Bulk audience import" is separate.)
- **Spec/impl:** Only single-user `InviteUserDialog`. No bulk/CSV import FR, task, or page.
- **Impact:** Onboarding a tenant with dozens/hundreds of users is one-at-a-time — impractical for the enterprise/government scale the platform targets.
- **Recommendation:** Either schedule a bulk-provisioning story (CSV/template upload → validate → batch create with per-row results) or record an explicit deferral ("F-M10-07 deferred to Phase 2") in the spec.

---

## 🟡 Minor — consistency & traceability

### I-10 — F-M10-05 Session management has no dedicated surface

- **Docs:** Platform Definition lists **F-M10-05 Session management** as an M-10 feature.
- **Spec/impl:** Session *mechanics* are well-specified (sliding TTL, absolute lifetime, multiple concurrent sessions — FR-012/025) and `SessionService` exists. But there is **no user-facing session-management surface** — no "active sessions" list, no "revoke this device," no admin "terminate all sessions for user." `InvalidateAllForUserAsync` exists on the repo (T022) but is not exposed.
- **Recommendation:** If active-session visibility/revocation is in scope, add a small surface (user self-service "your sessions" and/or admin "revoke sessions" action on User Detail). Otherwise note F-M10-05 as backend-only for Phase 1.

### I-11 — Hierarchy management ownership (M-10 docs → M-11 spec)

- **Docs:** F-M10-04 "Hierarchy management" is an M-10 feature; SRS §5.4 has M-10 applying the cascade.
- **Spec:** FR-028 + clarification — **M-11 owns hierarchy CRUD**; M-10 reads only, via `hierarchySource` (`manual`=M-11 / `integration`=M-13).
- **Impact:** Combined with M-11 not existing, this is the root of I-03's "nodes are never created." The boundary decision is reasonable, but it left M-10 with a read dependency on an absent owner.
- **Recommendation:** Track M-11 hierarchy CRUD (or the temporary M-10 seeding surface from I-03) as a hard prerequisite for the scope page.

---

## 🔵 Recommendation — architecture

### I-12 — Consider EF Core instead of raw Npgsql/SQL

- **Current:** Persistence is hand-written **Npgsql/raw SQL** — `_Baseline.sql` (T010), `TenantUserRepository` is "Npgsql-based" (T043), etc.
- **Trade-off:**
  - *For EF Core:* less boilerplate, change-tracking, migrations as code, easier `oldValue/newValue` capture for audit (FR-014), compile-time-ish query safety, simpler multi-entity transactions (which M-10 needs for same-transaction event writes — FR-015).
  - *Against / cautions:* the schema-per-tenant isolation model (DB-02, no `tenantId` column) and the **control-plane vs. tenant DB split** (PersonaBaseline/IdentityProviderConfig live control-plane) need careful `DbContext` design; cross-DB transactions are already a known constraint (`memory/project-m10-cross-db-transaction-constraint.md`). EF's migration runner would need to coexist with the existing `tools/Nabadat.Migrations` reflection-based runner.
- **Recommendation:** If adopting EF Core, scope it as a deliberate data-layer story (not a silent swap): one `DbContext` per database boundary, keep the schema-per-tenant connection interception, and verify the same-transaction M-17 publish still holds. Given the cross-DB constraint already in memory, an **outbox pattern** is the cleaner long-term fix regardless of ORM choice.

---

## Cross-cutting theme

Most **spec additions** (lockout, password reset, complexity, session TTL, MFA encryption) are sound and resolve SRS open items. The **risk concentrates in three places**:

1. **Dependencies on modules that don't exist** (M-17 for audit read, M-11/M-13 for scope data) — these turn finished-looking pages into dead ends (I-02, I-03).
2. **Silent drops** never written back as explicit scope notes (bulk provisioning, restrictive custom rules, data-visibility restrictions) — they read as omissions, not decisions (I-08, I-09).
3. **An incomplete onboarding cycle** — invite creates a user who cannot actually log in (I-01).

Recommend resolving I-01, I-02, I-03 before declaring Phases 5–6 "done," and adding explicit supersedes/deferral notes to the spec for I-04, I-05, I-08, I-09 so the contradictions with the docs are documented rather than silent.
