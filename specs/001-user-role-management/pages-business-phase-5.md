# M-10 — Business of Pages: Phase 5

**Module**: M-10 User and Role Management
**Scope**: Frontend pages and key components delivered in **Phase 5 (User Story 3 — Custom Data Scope Rules and Hierarchy Cascade)**.
**Sources**: `docs/SRS-M10-User_and_Role_Management_v0.1.docx`, `docs/Nabadat Platform Definition.V1.docx`, `specs/001-user-role-management/spec.md`, `specs/001-user-role-management/tasks.md`.

> This document explains **what each page is for in business terms** — who uses it, why it exists, what decisions or outcomes it drives, and which functional requirements (FR) it satisfies. It is *not* a UI spec; styling rules live in `CLAUDE.md` (Nabadat Design System) and the technical task list lives in `tasks.md`. See `pages-business-phase-3-4.md` for the login/MFA and user/permission pages that precede this phase.

---

## Background Context

By Phase 5, tenant users can already **sign in** (Phase 3) and admins can already **provision users and assign permission modules** (Phase 4). Permission modules answer *"which capabilities can this user use?"* — Phase 5 answers the next two questions:

1. **"Which data records may this user see?"** — narrowed by **parameter-based data scope** (e.g., branch values) sourced from **M-13**, and by the tenant's **organisational hierarchy**.
2. **"Which fine-grained actions may this user perform?"** — beyond the persona baseline, via **custom authorization rules** (e.g., grant *UpdateSurvey* but not *DeleteSurvey*).

This is **enterprise segmentation**: a Riyadh branch manager should see Riyadh data and nothing else; a regional lead should see their region and every branch beneath it — but never sibling regions or the head office above them. For banking and government tenants, this branch-level and hierarchy-level containment is a hard requirement, not a nice-to-have.

**Priority**: P2 (after the P1 auth and permission-management stories). **Personas with access**: P-01 and P-07 only; the pages are hidden from P-02..P-06 and P-08, and direct URLs return an access-denied state.

### The three orthogonal capabilities Phase 5 introduces (FR-M10-AUTHZ-005..016)

| Capability | What it controls | Backing FRs |
|------------|------------------|-------------|
| **Action-level custom rules** | Which fine-grained actions (DOC-02 action codes) a specific user may perform — additive or restrictive vs. the persona baseline | FR-M10-AUTHZ-005, -006 |
| **Parameter data-scope filters (from M-13)** | Which values of a parameter (e.g., `branch ∈ {Riyadh, Dammam}`) a user may see; **every** data surface is filtered, no exemptions | FR-M10-AUTHZ-008..013 |
| **Hierarchy cascade** | A user assigned to a node sees that node **and all descendants** — never siblings or ancestors; the downward rule is **absolute** | FR-M10-AUTHZ-014..016 |

### Key boundaries (who owns what)
- **M-13** supplies parameter **definitions and allowed value sets** through a defined integration contract. **M-10 stores and enforces** the assignments — it does not own the M-13 pipeline (FR-M10-AUTHZ-012).
- **Fail-closed**: if M-13 cannot supply a definition that a stored assignment references, M-10 shows the user **no data** for that surface rather than falling back to unfiltered data (FR-M10-AUTHZ-013).
- **Hierarchy CRUD** is owned by **M-11** (tenant configuration); M-10 only **reads** hierarchy nodes for scope evaluation, from the source set by the tenant's `hierarchySource` config (`manual` = M-11-managed, `integration` = M-13-supplied).

---

# Phase 5 — User Story 3: Custom Data Scope Rules & Hierarchy Cascade (P2)

**Business goal**: Admins can give each user a precise data-visibility envelope — by parameter values, by hierarchy position, and by fine-grained action — and the platform enforces that envelope consistently across **every** dashboard, chart, list, and export.

**Why it matters**: Without this, permission modules alone would grant a user access to *all* of a capability's data. Phase 5 is what lets a tenant safely give a branch operator the Analytics module while ensuring they only ever see their own branch's numbers. The cascade rule guarantees a manager's reach follows the org chart downward and stops there.

**Backing requirements**: FR-M10-AUTHZ-005..016 (custom rules, data scope filters, hierarchy cascade), FR-M10-AUDIT-015 (every scope change is audited).

---

## 5.1 User Scope Management Page — `UserScopePage.tsx` (T108)

**Route**: `/users/:userId/scope` (reached from the User Detail page)
**Primary user**: P-01 and P-07 admins. Hidden from all other personas; direct URL returns access-denied.

### What it does
The single screen for shaping one user's **data-visibility envelope**. It has three working areas:

1. **Parameter scope assignments** — for each M-13-supplied parameter (e.g., `branch`), a **tag-style multi-select** of its allowed values. The admin grants the user one or more values (multi-value grants are native — FR-M10-AUTHZ-010). Only values present in the M-13 `data_scope_parameter_definitions` are selectable.
2. **Hierarchy node assignment** — a **node picker** (tree or select) over the tenant's `organization_hierarchy_nodes`. Assigning a node grants the user that node **and all descendants** automatically.
3. **Custom rules list** — the user's existing custom authorization rules, each editable/removable via the `CustomRuleEditor` (5.2).

A single **Save all** persists every change in one operation.

### Business behaviour
- **Parameter grant** → the user's data is filtered to only the granted values **on every guarded surface** — NPS/CSAT charts, dashboards, list views, exports, no exceptions (FR-M10-AUTHZ-011). Acceptance: a user granted `[Riyadh, Dammam]` querying a scoped dataset gets only those branches' rows.
- **Hierarchy grant** → the user sees the assigned node and its descendants only; **siblings and ancestors are never visible**, regardless of any other rule (FR-M10-AUTHZ-014/015). The cascade is computed downward via a materialized-path query.
- **Invalid parameter value** → rejected at save with a validation error; the page cannot assign a value M-13 does not define.
- **Fail-closed** → if a referenced parameter definition is missing from M-13, the user sees no data for that surface — never unfiltered data.
- **Audit** → every scope change (assignment, modification, removal of a parameter filter or hierarchy assignment) publishes a `scope.assigned` / scope-change event to M-17 in the same transaction (FR-M10-AUDIT-015).
- **Immediate effect** → the new scope takes effect on the user's next session refresh, the same propagation model as permission changes.

### Why it matters
This is where enterprise segmentation becomes concrete. It turns the abstract "this user is a Riyadh branch operator" into an enforced, audited boundary that follows the user across the entire product — satisfying the branch-level and hierarchy-level containment that banking and government tenants demand.

---

## 5.2 Custom Rule Editor — `CustomRuleEditor.tsx` (T109) *(component)*

**Used within**: the User Scope Management page (5.1).

### What it does
A form for authoring a single **custom authorization rule** for the user. It captures two things:

1. **Allowed actions** — a multi-select of fine-grained **DOC-02 action codes** (e.g., `CreateSurvey`, `UpdateSurvey`, `AddData`, `DeleteData`). This is where an admin grants *part* of a capability — e.g., the ability to **update** surveys without the ability to **delete** them.
2. **Parameter scope assignments** — per-parameter value pickers attached to the rule, letting a rule carry its own data-scope narrowing.

Rules can be **added, edited, and deleted**.

### Business behaviour
- **Additive or restrictive** — rules can broaden *or* narrow a user's actions relative to their persona baseline; both directions are expressible (FR-M10-AUTHZ-006). However, a custom rule may **never broaden beyond the default-deny baseline's intent** — custom rules narrow within granted modules; they do not grant capabilities a user's modules don't include.
- **Action-level enforcement** — once saved, the rule is applied immediately to the user's permission evaluation. Acceptance: a rule granting `UpdateSurvey` but not `DeleteSurvey` means the target user can update but cannot delete surveys in the portal.
- **Hierarchy is absolute over rules** — no custom rule can grant visibility outside the user's assigned hierarchy subtree (FR-M10-AUTHZ-016). The cascade always wins.
- **Audit** — saving or removing a rule is published to M-17.

### Why it matters
Persona baselines and permission modules cover the common cases; custom rules handle the exceptions an enterprise inevitably needs — "this one analyst may edit but not delete," "this contractor sees only Dammam." Expressing those exceptions declaratively (instead of inventing new personas) keeps the access model auditable and maintainable.

---

## Cross-cutting notes

- **Enforcement at the data layer, not the UI** — disabled controls, hidden nav, and access-denied states are conveniences. The authoritative check is server-side: a forged request from a non-admin, or one that exceeds a user's scope, is rejected and (where applicable) audited. UI-only enforcement is explicitly non-compliant (FR-M10-AUTHZ-004).
- **Default-deny remains the floor** — Phase 5 only ever *narrows* or grants within what a user's permission modules already permit. Absence of a grant is denial (FR-M10-AUTHZ-017/018); custom rules cannot create access from nothing.
- **No surface is exempt from scope** — when a data-scope filter applies, it filters charts, dashboards, lists, and exports uniformly (FR-M10-AUTHZ-011). New data surfaces inherit enforcement at their own boundary (FR-M10-AUTHZ-019/020).
- **M-13 is consumed, not owned** — the scope page only offers parameters and values M-13 has supplied; M-10 never invents parameter definitions.
- **Audit everywhere** — every scope and rule change publishes to M-17 in the same transaction as the change (FR-M10-AUDIT-006/007/015). Phase 6 adds the page to *view* this trail.
- **RTL-first & bilingual** — authored Arabic-first (فصحى) with English secondary, using logical CSS properties per the Nabadat Design System.
- **Navigation visibility** — the scope and custom-rule surfaces are reachable only by P-01/P-07, governed by the per-persona nav allowlist and a link from the User Detail page.
