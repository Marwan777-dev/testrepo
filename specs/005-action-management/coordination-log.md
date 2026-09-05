# Cross-Module Coordination Log — Action Management (M-15)

Tracks the cross-team/cross-module dependencies M-15 needs **other** modules to ship, and the
governance changes M-15 needs ratified. Distinct from a module-local TODO list: entries here are
actions owned by **another module's team** or the **platform governance process**. Mirrors the
pattern established by Feature 004's `coordination-log.md` (M-01). Created by `/speckit-plan`.

See also: [research.md §4-6](./research.md), [plan.md → Constitution Check](./plan.md),
[contracts/published-interfaces.md](./contracts/published-interfaces.md).

Status values: `PENDING` (not started / owning module absent) → `IN PROGRESS` → `SHIPPED`
(port/impl exists and M-15 has swapped off its stub) → `RATIFIED` (governance items only).

---

## C-01 — M-06 score-computation engine (`IKpiScoreReader` real implementation)

- **Needed by**: US1 (`BaselineCaptureService`), US2/US3 (live pace colouring — Score Progress,
  Time Progress, timer state), US10 (retro-dated historical baseline). Blocks only the
  *live-value* parts of these stories; create/list/detail/archive/settings all function against
  the stub (baseline shows "no score" state, deterministic and testable).
- **Port**: `Nabadat.ActionManagement.Domain.Interfaces.IKpiScoreReader`
  ```csharp
  Task<decimal?> GetCurrentScoreAsync(Guid kpiId, CancellationToken ct);
  Task<decimal?> GetHistoricalScoreAsync(Guid kpiId, DateOnly asOfDate, CancellationToken ct);
  Task<decimal?> GetNormalisedIndexAsync(Guid kpiId, CancellationToken ct);
  ```
- **Status**: **PENDING** — `Nabadat.KpiManagement` (M-06) has no `metric_values`/score-computation
  engine today (confirmed: AMENDMENT-011 removed `metric_configs`/`metric_values` from M-06's
  owned tables "pending the M-06 score-computation engine release... out of scope of Feature
  003"; Feature 004 independently notes the same deferral). M-15 ships a stub
  (`NullKpiScoreReader`, returns `null` deterministically → surfaces as the Planned "no baseline
  yet" state / `NoBaselineScoreException` on any Save that requires a real score) so every user
  story is fully buildable and testable today.
- **Resume**: when M-06 ships live/historical/normalised score reads, implement the real adapter
  in `Nabadat.ActionManagement` (or wherever the host wires it — mirrors M-16's
  `IActiveKpiCatalogReader` adapter-swap pattern) and remove the stub registration.

## C-02 — Dedicated `kpi.deactivated` / `kpi.reactivated` events (optional hardening)

- **Needed by**: US7 (`KpiForceDeactivationCascade`) — currently designed against M-06's existing
  `settings.changed` (`entity_type: "kpi"`) event, read lazily from the shared `event_log` table
  (research.md §4.3) rather than requiring a new event type. This entry exists only if a future
  architecture review prefers a dedicated event over the diff-inspection approach.
- **Status**: **PENDING** — not currently blocking; the lazy `settings.changed` consumer is the
  shipped design. Would require a constitution Event Catalogue amendment (Section 4) to add
  `kpi.deactivated`/`kpi.reactivated` as first-class M-06-sourced events.
- **Resume**: only if the lazy-consumer approach proves insufficient in practice (e.g. audit
  latency complaints) — file the amendment, then add the dedicated event to
  `KpiEventPublisher`.

## C-03 — M-15 owned-tables registry correction (constitution Section 3)

- **Needed by**: keeping `constitution.md` accurate once the real schema ships.
- **Status**: **PENDING** — `constitution.md` Section 3 still lists the Phase-1 placeholder
  reservation (`action_plans`, `action_assignments`, `action_progress`, per AD-06/DB-06). No
  baseline migration for these ever shipped, so there is no pre-existing empty schema to
  reconcile — this feature's baseline creates the real tables directly (research.md §3):
  `actions`, `kpi_targets`, `action_settings`.
- **Resume**: file an amendment (same shape as AMENDMENT-011/012) once
  `ActionManagement_Baseline.sql` merges, correcting Section 3's M-15 row to the real table
  names.

## C-04 — M-07 overlay contract (INT-02) — **not yet assigned to a task**

- **Needed by**: SRS §12.2 — M-07's trend-analysis chart SHALL offer an Action-overlay toggle.
  M-15's obligation is limited to exposing Action metadata (name, status, dates, archived flag)
  via a published read interface.
- **Owner module**: `Nabadat.Dashboards` (M-07) — **does not exist under `src/` yet**.
- **Status**: **PENDING**. M-15 defines its side of the contract
  (`Nabadat.ActionManagement.Domain.Interfaces.IActionOverlayReader`) so M-07 has something to
  consume once it exists; no consumer to wire yet.

## C-05 — M-09 notification subscription (INT-03)

- **Needed by**: nothing in v1 — explicitly postponed in full per spec Overview/INT-03. M-15's
  only obligation is emitting its audit events (already in scope, via M-17).
- **Status**: **PENDING**, non-blocking. `Nabadat.Notifications` (M-09) does not exist under
  `src/` yet (same as Feature 004's C-04).

## C-06 — M-10 permission-model refinement (PERM-02)

- **Needed by**: nothing in v1 — PERM-01's interim matrix (P-01 Program Manager / P-02 Analyst /
  P-06 Executive-as-Viewer) is stakeholder-ratified and self-sufficient.
- **Status**: **PENDING**, non-blocking. Resume when M-10 ships a richer role/permission model
  that needs to reconcile with PERM-01's hardcoded mapping.
