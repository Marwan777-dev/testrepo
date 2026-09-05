# M-10 — Business of Pages: Phase 6

**Module**: M-10 User and Role Management
**Scope**: Frontend page(s) and supporting surface delivered in **Phase 6 (User Story 4 — Immutable Tenant Audit Trail for All Management Actions)**.
**Sources**: `docs/SRS-M10-User_and_Role_Management_v0.1.docx`, `docs/Nabadat Platform Definition.V1.docx`, `specs/001-user-role-management/spec.md`, `specs/001-user-role-management/tasks.md`.

> This document explains **what each page is for in business terms** — who uses it, why it exists, what decisions or outcomes it drives, and which functional requirements (FR) it satisfies. It is *not* a UI spec; styling rules live in `CLAUDE.md` (Nabadat Design System) and the technical task list lives in `tasks.md`. See `pages-business-phase-3-4.md` and `pages-business-phase-5.md` for the screens that precede this phase.

---

## Background Context

Auditing is the **third domain M-10 owns** (alongside Authentication and Authorization). Phases 3–5 *produced* audit events as a side-effect of every state change — a login, an MFA result, a user lifecycle action, a permission change, a scope assignment. **Phase 6 makes that trail complete, transactionally guaranteed, and viewable.**

The platform's audit model has a strict architecture:

- **M-10 publishes events; M-17 owns the store.** Every auditable M-10 action writes one row to M-17's `event_log` **in the same database transaction** as the change itself. M-10 never queries `event_log` directly — it reads through M-17's published interface (`IM17EventLogReader.QueryM10EventsAsync`).
- **The trail is immutable and append-only.** Audit records can never be updated or deleted; the data layer itself rejects any such operation. A correction is expressed as a *new* event that references the prior one — the original is never touched.
- **Transactional coupling is absolute.** There is no scenario where a business change commits and its audit entry does not. If the event write fails, the whole transaction — including the entity change — rolls back.

**Priority**: P1 (compliance-critical). **Why it matters**: For banking and government tenants, "who changed what, and when" is not a feature — it is a regulatory and forensic requirement. The audit trail is the **source of truth** for accountability and the foundation for safe rollback semantics.

### Backing requirements

| Requirement area | What it mandates | FRs |
|------------------|------------------|-----|
| **Record structure** | Every record captures actor, action type, entity type, record ID, **full** prior value, **full** new value, UTC timestamp — sufficient to reconstruct the change alone | FR-M10-AUDIT-001, -002 |
| **Immutability** | Append-only; corrections are new referencing events; data layer rejects update/delete | FR-M10-AUDIT-003, -004, -005 |
| **Transactional coupling** | Audit emission in the same transaction as the action; failure rolls everything back | FR-M10-AUDIT-006, -007 |
| **Tenant scope & residency** | Records are tenant-scoped and never leave the tenant's residency boundary | FR-M10-AUDIT-008, -009 |
| **Mandatory coverage** | User-management, permission assign/modify/revoke, session, and data-scope events each produce a record | FR-M10-AUDIT-010..015 |

---

# Phase 6 — User Story 4: Immutable Tenant Audit Trail (P1)

**Business goal**: Every tenant-side management action is recorded as an immutable event, and P-01/P-07 admins can browse, filter, and inspect that record through a read-only Audit Log page.

**Why it matters**: This closes the accountability loop. Phases 3–5 gave admins power (create users, grant permissions, set data scope); Phase 6 ensures every use of that power is captured and reviewable — satisfying compliance auditors and giving the tenant a forensic trail when something goes wrong.

**Note on the backend**: Most of Phase 6's *backend* work is verification, not new emission — the publish calls were built into the US1–US3 services. The new user-facing artifact of this phase is the **Audit Log page**.

---

## 6.1 Audit Log Page — `AuditLogPage.tsx` (T119)

**Route**: `/audit-log`
**Primary user**: P-01 and P-07 admins. Hidden from all other personas; direct URL returns an access-denied state.

### What it does
A **read-only, data-dense table** of the tenant's auditable events, newest first. Each row shows:

- **Event type** — as a colored badge (e.g., `permission.modified`, `session.revoked`, `user.deactivated`)
- **Actor** — the username of who performed the action
- **Entity type** — what was affected (user, permission set, session, scope assignment)
- **Timestamp** — UTC time of the change

Above the table sits a **filter bar**: an event-type select and a date-range picker. The list uses **cursor-based pagination** to handle high volume, and shows an **empty state** when no events match.

### Business behaviour
- **View** → P-01/P-07 see a chronological list of all auditable events for the tenant, including user-management actions, permission changes, and session events (FR-M10-AUDIT-010..014).
- **Filter** → narrowing by event type (e.g., only `permission.modified`) and/or date range returns only matching rows. Filters supported: `eventType`, `from`, `to`, `actorId`, `entityId`.
- **Read-only** → there are **no edit or delete controls anywhere on the page**. This is not just a UI choice — the data layer itself rejects mutation of audit records (FR-M10-AUDIT-005). The absence of edit/delete affordances communicates the immutability guarantee to the user.
- **Access control** → the page is hidden from the sidebar for non-admins, and a direct URL by a P-02..P-08 user returns an access-denied state; the backend `GET /api/v1/audit-log` enforces the same restriction server-side.
- **Data source** → the page reads through M-10's `AuditLogController`, which queries M-17's published interface — never the `event_log` table directly. This keeps the M-10/M-17 ownership boundary intact.
- **Tenant scope** → only the current tenant's events are ever returned; cross-tenant audit data is never visible (FR-M10-AUDIT-008).

### Why it matters
This is the **accountability surface** of the entire module. It turns the silent, transactionally-bound event stream produced by every other M-10 page into something a compliance officer or program manager can actually inspect. The deliberate read-only design is itself a business statement: the record cannot be doctored, by anyone, from anywhere — exactly what a regulated tenant needs to trust the platform.

---

## Cross-cutting notes

- **The trail is produced, not just displayed** — every state-changing action across Phases 3–5 (login result, MFA enrol/verify, lockout, password reset, user create/lifecycle, permission change, scope/rule change) already publishes to M-17 in the **same transaction** as the change (FR-M10-AUDIT-006/007). Phase 6 audits that coverage is complete (every mutating service method emits the correct event with full old/new values) and adds the page to *view* it.
- **Immutability is enforced at the data layer** — the read-only page is the visible half; the authoritative half is the store rejecting any update/delete on an audit row (FR-M10-AUDIT-005). Corrections are new events referencing prior ones (FR-M10-AUDIT-004).
- **Transactional all-or-nothing** — if the M-17 event write fails, the triggering change rolls back too; a committed change with no audit entry is impossible (FR-M10-AUDIT-007).
- **M-17 owns the store; M-10 only reads through the contract** — `AuditLogController` calls `IM17EventLogReader.QueryM10EventsAsync`; no direct `event_log` access from M-10.
- **Tenant scope & residency** — audit data is tenant-scoped and never crosses the tenant's data-residency boundary (FR-M10-AUDIT-008/009).
- **Default-deny & data-layer enforcement** — the hidden nav and access-denied state for non-admins are conveniences; the `GET /api/v1/audit-log` endpoint enforces the P-01/P-07 restriction server-side and returns `403` otherwise.
- **RTL-first & bilingual** — authored Arabic-first (فصحى) with English secondary, using logical CSS properties per the Nabadat Design System.
- **Navigation visibility** — the Audit Log sidebar entry appears only for P-01/P-07, governed by the per-persona nav allowlist.
