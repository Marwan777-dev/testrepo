# Implementation Plan: M-15 Action Management

**Branch**: `005` (tracks `005-action-management`) | **Date**: 2026-07-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/005-action-management/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

**Team**: 3 resources — 2 backend (AbuKr, Atia), 1 frontend (Marawan). See
[research.md §1](./research.md#1-team--sequencing-context) for the sequencing/split this plan
assumes: AbuKr owns the write path (US1/US4/US7/US9), Atia owns the read/status/lifecycle path
(US2/US3/US5/US6/US10), Marawan builds SCR-01 → SCR-02 → SCR-03 sequentially once each
endpoint's contract is stable.

## Summary

M-15 Action Management closes the *act* stage of the VOC loop at the initiative level: CX teams
define improvement Actions, attach one or more KPI Targets (Lower/Upper Threshold deltas over an
auto-captured Baseline), and the system computes pace (Score Progress vs. Time Progress) and,
once each Target Date passes, an outcome (Successful / Partially Successful / Unsuccessful) —
all from a two-anchor date model (Baseline anchored at Action Start, monitoring clock anchored
at Action End + 1 day). Three screens (All Actions, Add/Edit Action, Action Details), a Settings
→ Actions subsection (two tenant parameters X/PAD), and forward contracts with M-06 (KPI data)
and M-07 (trend-chart overlay).

**Technical approach**: a new backend module `Nabadat.ActionManagement` (DB-08/AMENDMENT-009
reference layout) owning two new tables (`actions`, `kpi_targets`) plus a single-row
`action_settings` table, all server-side date/outcome logic computed via injected
`TimeProvider` at day granularity in the tenant timezone (BR-022). The module's single hard
external dependency — M-06's live/historical KPI scores — does not exist in
`Nabadat.KpiManagement` yet (research.md §4); this plan follows the dependency-inversion pattern
M-16 already established for the identical problem: M-15 owns its own `IKpiScoreReader` port
with a deterministic stub (`NullKpiScoreReader`) today, swapped for a real M-06-backed adapter
the moment M-06 ships its score-computation engine, with zero M-15 code change. A companion
`frontend/src/features/actions/` SPA feature ships the three screens plus three new custom-SVG
design-system primitives (Stepped Zone Slider, Threshold Slider, Timer Ring per CLAUDE.md's
custom-SVG rule for gauges/zones/needles).

## Technical Context

**Language/Version**: C# / .NET 10 (ASP.NET Core) — backend; TypeScript + React 19 — frontend
(constitution Section 1 stack table)

**Primary Dependencies**: EF Core (Npgsql provider, DB-08) · direct reference to
`Nabadat.KpiManagement.Application.Kpis.Interfaces.IKpiConfigReader` (existing, stable) ·
M-15-owned `IKpiScoreReader` port + `NullKpiScoreReader` stub adapter (research.md §4) ·
Vite + React 19 + Tailwind 4 + `@base-ui/react` + shadcn-style components (repo CLAUDE.md,
binding for all `frontend/` work)

**Storage**: PostgreSQL 16+, tenant schema `tenant_{slug}` (AD-02) — new tables `actions`,
`kpi_targets`, `action_settings` (data-model.md); shared `event_log` table for audit writes
(M-17-owned, mapped locally per the established KpiManagement/UserManagement/
CustomerJourneyManagement convention). No Elasticsearch — this feature is operational CRUD +
client-computed pace values, not analytics/dashboard-class data (AD-04 scope).

**Testing**: xUnit v3 + FluentAssertions 6.12.\* + NSubstitute 5.\* —
`tests/Nabadat.ActionManagement.UnitTests` · Testcontainers Postgres + `WebApplicationFactory` —
`tests/Nabadat.ActionManagement.IntegrationTests` · MSTest + `Microsoft.Playwright.MSTest`,
appended to the existing shared `tests/Nabadat.E2ETests/ActionManagement/` (no new E2E project,
CLAUDE.md E2E Test Policy rule 1).

**Target Platform**: Kubernetes (SaaS) / Docker Compose (on-prem), single codebase (AD-05);
browsers per NFR-9 (last 2 evergreen Chrome/Edge/Firefox/Safari)

**Project Type**: Web application — backend module (`Nabadat.ActionManagement`, hosted inside
`Nabadat.TenantAdmin`) + frontend SPA feature (`frontend/src/features/actions/`)

**Performance Goals**: NFR-5 — SCR-01 interactive < 2s @ 200 Actions; search/filter feedback
< 100ms; slider drag 60fps; server authoritative for evaluation-time facts only (baseline
snapshots, `final_score`, `outcome`), live pace values (Score/Time Progress, timer state,
lowest-performing selection) computed client-side from delivered raw inputs

**Constraints**: day-granularity, tenant-timezone date comparisons everywhere (BR-022/NFR-8, via
injected `TimeProvider`, DB-08 rule 7); cursor-only pagination (API-04); last-write-wins
concurrency (spec-ratified R-4, a documented exception to Article 7.2's default optimistic
concurrency — see Complexity Tracking); no new background-worker infrastructure — the KPI
force-deactivation cascade runs as a lazy, watermarked read of the shared `event_log` table on
existing request paths (research.md §4.3), not a new `IHostedService`

**Scale/Scope**: 3 screens (SCR-01/02/03) + 1 Settings subsection; ~10 REST endpoints (2 new
tenant tables); 10 user stories, 23 cross-screen business rules (BR-001..023), 11 validation
rules (VAL-201..211), 17 measurement-model formulas (FR-M01..M17)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Core Governing Principles (GP-01–GP-05)

- **GP-01 (Single Source of Truth)** — PASS. PostgreSQL (`actions`, `kpi_targets`,
  `action_settings`) is authoritative; this feature has no Elasticsearch projection at all
  (no analytics/dashboard use case in scope — AD-04), so there is nothing to rebuild.
- **GP-02 (Customer-Controlled Encryption)** — N/A. No high-sensitivity fields or attachments;
  Action/Target data is operational configuration, not PII beyond `created_by` attribution
  (NFR-7).
- **GP-03 (Right to Erasure)** — N/A for this feature's own data (no personal data stored);
  `created_by` is a user-id reference into M-10, erased there per M-10's own GP-03 handling —
  M-15 stores no name/contact data to erase.
- **GP-04 (Tenant/Scope Isolation)** — PASS. Schema-per-tenant (AD-02) is the only boundary;
  `GET /api/v1/actions/{id}` for a foreign-tenant id returns 404 (ERR-6), same as
  non-existent (API-04.6 indistinguishable-absence rule); denied write attempts are
  audit-logged (PERM-01 + INT-04).
- **GP-05 (Constitution Compliance Gate)** — this section is the pass condition; re-checked
  post-design below.

### Frontend Design Gate *(feature ships UI under `frontend/` — gate applies)*

- [x] Repo-root `CLAUDE.md` read in full (design system, RTL rules, brand palette and D1–D5
      scale, Component Sourcing Rule, DO/DO NOT lists, brand voice) — see research.md §7.
- [x] Component Sourcing Rule confirmed: `frontend/src/features/{kpi-management,journeys,
      settings}/` inspected as the reuse baseline (route/folder conventions, existing
      `Select`/`Dialog`/`Sheet`/`Table` primitives in `frontend/src/components/ui/`). No
      existing KPI-gauge/zone-slider/timer-ring component exists to reuse — the three new
      custom-SVG primitives (Stepped Zone Slider, Threshold Slider, Timer Ring) are genuinely
      new per the Component Sourcing Rule's step 4 ("only then build custom"), and match the
      rule's explicit custom-SVG carve-out (gradients/zones/needle markers).
- [x] Two-Palette Rule applies directly: D1–D5 for pace/outcome/zone colouring only (never
      decoratively — FR-M10/FR-M14); `nb-*`/`bg-primary` for chrome, CTAs, the "Lowest
      performing" cyan ring highlight. Logical properties only (`ps-*`, `ms-*`, `text-start`)
      per NFR-1/RTL-first architecture.
- [x] Both themes + both directions are an explicit NFR (NFR-1, NFR-2, SC-012) — verified per
      story at implementation time, not deferred.

A spec that violates the repo-root `CLAUDE.md` is invalid and must be revised before tasks are
generated. **No violation found** — the spec's FR-501/502/503 component specs already comply
with the custom-SVG + neutral-chrome-theme-aware rules verbatim.

### Backend Data-Access Gate *(feature reads/writes PostgreSQL — gate applies)*

- [x] **EF Core only** — no raw ADO.NET/Dapper/`FromSql*`/`ExecuteSql*` planned; all reads/
      writes go through `ITenantDbContext` (data-model.md entities map 1:1 to
      `IEntityTypeConfiguration<T>` classes).
- [x] Tables in `Infrastructure/Migrations/ActionManagement_Baseline.sql` — **no EF
      migrations**. One config class per entity (`Action`, `KpiTarget`, `ActionSettings`,
      `EventLog`) with explicit `HasColumnName` + the intra-module `KpiTarget → Action` FK
      (Article 4.1 permits FKs within a module's own tables; `kpi_id` is an identifier
      reference to M-06, never a FK, per the same article).
- [x] `ITenantDbContext` in `Application/Interfaces/`, concrete `TenantDbContext` in
      `Infrastructure/Persistence/`. Multi-write atomicity (e.g. Save Action + N Targets +
      baseline captures in one transaction) via `ExecuteAsync` — no unit-of-work type; no
      control-plane database involved (this module has no control-plane tables), so no
      cross-database transaction risk exists.
- [x] Per-aggregate services: `ActionService`, `KpiTargetService`, `ActionSettingsService` in
      `Application/<SubDomain>/`, each behind a port in the same folder's `Interfaces/` (the
      unit-test seam — mirrors `Nabadat.KpiManagement`'s `KpiDefinitionService`/
      `IKpiDefinitionService` shape). All date logic via injected `TimeProvider` (DB-08 rule 7,
      Unit Test Policy rule 8) — `ActionStatusCalculator`, `TimezoneDayBoundary`,
      `PerTargetEvaluationCalculator` all take `TimeProvider`, never `DateTime.UtcNow`.

### Backend Module Structure Gate *(feature adds a new backend module — gate applies)*

- [x] Single `Nabadat.ActionManagement` library (AMENDMENT-008 — research.md §2), four layer
      folders, no new top-level folder kind.
- [x] Inward-only dependencies: `Api → Application → Domain`,
      `Infrastructure → Application → Domain`; `Domain` (entities + the `IKpiScoreReader` /
      `IActionOverlayReader` published ports, contracts/published-interfaces.md) references
      nothing; `Api` and `Infrastructure` never reference each other; wiring in
      `ActionManagementServiceCollectionExtensions`.
- [x] Interface placement: `ITenantDbContext` → `Application/Interfaces/`; per-sub-domain
      service ports (`IActionService`, `IKpiTargetService`, `IActionSettingsService`) →
      `Application/<SubDomain>/Interfaces/`; published cross-module ports (`IKpiScoreReader`,
      `IActionOverlayReader`) → `Domain/Interfaces/`; `ICurrentTenant`-style accessors →
      `Api/Interfaces/`.
- [x] Sub-domain folders: `Application/Actions/` (create/edit/list), `Application/Targets/`
      (lifecycle: activate/deactivate/delete/force-deactivation cascade),
      `Application/Measurement/` (Score/Time Progress, timer colour, outcome evaluation,
      lowest-performing selection), `Application/Settings/` (SET-1/SET-2),
      `Application/Events/` (M-17 event writes) — each with its own unit-test mirror folder.

## Cross-Module Coordination Gate *(project-specific addition — see coordination-log.md)*

This feature has a hard external dependency (M-06 live/historical KPI scores) that does not
exist in the codebase today. Per constitution §12.2 ("a question not answered here is flagged
for a constitution amendment, not silently resolved in the spec") and the Feature 004 precedent
(`coordination-log.md`), this is tracked explicitly rather than silently stubbed without
record:

- [x] `coordination-log.md` filed, listing 6 items (C-01..C-06), all `PENDING`, none blocking
      this feature's own Phase 1/2 work (research.md §4/§6).
- [x] `IKpiScoreReader` (Domain/Interfaces, M-15-owned) + `NullKpiScoreReader` default adapter
      make every user story buildable and testable standalone today, with a documented,
      zero-code-change swap path once M-06 ships (C-01).
- [x] The KPI force-deactivation cascade (BR-011) is redesigned as a lazy `event_log` read
      (research.md §4.3) rather than requiring new background-worker infrastructure or a new
      M-06 event type — avoids a second coordination blocker (C-02 recorded as optional
      hardening only).

## Project Structure

### Documentation (this feature)

```text
specs/005-action-management/
├── plan.md                    # This file (/speckit-plan command output)
├── research.md                # Phase 0 output
├── data-model.md              # Phase 1 output
├── quickstart.md              # Phase 1 output
├── coordination-log.md        # Cross-module dependency tracker (mirrors Feature 004's pattern)
├── contracts/
│   ├── api-endpoints.md       # REST contracts (proposed technical design, per spec's own note)
│   └── published-interfaces.md # M-06-consumed / M-07-published cross-module interfaces
└── tasks.md                   # Phase 2 output (/speckit-tasks command — NOT created here)
```

### Source Code (repository root)

```text
# Backend module — constitution AMENDMENT-009 / architecture Article 1A (M-10 reference layout)
src/Nabadat.ActionManagement/
├── Nabadat.ActionManagement.csproj
├── ActionManagementServiceCollectionExtensions.cs   # composition root: AddActionManagementModule(...)
├── Api/
│   ├── Controllers/            # ActionsController, ActionTargetsController, ActionsSettingsController
│   ├── Contracts/               # request/response DTOs — one type per file (contracts/api-endpoints.md)
│   ├── Middleware/              # (none new expected — reuses platform auth/tenant middleware)
│   └── Interfaces/               # ICurrentTenant-style accessors (if module-local ones are needed)
├── Application/
│   ├── Interfaces/               # ITenantDbContext
│   ├── Actions/                  # create/edit/list/archive/unarchive
│   │   ├── ActionService.cs, Interfaces/, Dtos/, Exceptions/
│   │   └── Validators/           # ThresholdValidator, action-level VAL-201..211
│   ├── Targets/                  # KPI Target lifecycle
│   │   ├── KpiTargetService.cs, Interfaces/, Dtos/
│   │   └── KpiForceDeactivationCascade.cs   # lazy event_log consumer (research.md §4.3)
│   ├── Measurement/               # pure calculators — the FR-M01..M17 formula set
│   │   ├── ActionStatusCalculator.cs, ScoreProgressCalculator.cs, TimeProgressCalculator.cs
│   │   ├── TimerColourResolver.cs, OutcomeEvaluator.cs, LowestPerformingTargetSelector.cs
│   │   ├── TargetStartDeriver.cs, DisplayClamper.cs, TimezoneDayBoundary.cs
│   │   └── Interfaces/
│   ├── Settings/                  # SET-1/SET-2
│   │   ├── ActionSettingsService.cs, SettingsUpdateValidator.cs, Interfaces/
│   └── Events/                    # M-17 writes
│       ├── ActionManagementEventPublisher.cs, EventLogFactory.cs, Dtos/
├── Domain/
│   ├── Entities/                  # Action, KpiTarget, ActionSettings, EventLog
│   ├── ValueObjects/              # ActionStatus, TargetLifecycleState, Outcome, TimerState enums
│   └── Interfaces/                # PUBLISHED: IKpiScoreReader, IActionOverlayReader (contracts/published-interfaces.md)
└── Infrastructure/
    ├── Persistence/
    │   ├── TenantDbContext.cs
    │   └── Configurations/        # one IEntityTypeConfiguration<T> per entity
    ├── Migrations/
    │   └── ActionManagement_Baseline.sql
    └── KpiIntegration/            # module-specific adapter folder (Article 1A rule 4)
        └── NullKpiScoreReader.cs  # default stub adapter (research.md §4); real M-06-backed
                                   # adapter registered by the host once C-01 ships

tests/
├── Nabadat.ActionManagement.UnitTests/
│   ├── Actions/ Targets/ Measurement/ Settings/ Events/   # mirrors Application/<SubDomain>/
│   └── TestSupport/
└── Nabadat.ActionManagement.IntegrationTests/
    ├── Infrastructure/            # ActionManagementApplicationFactory.cs (Testcontainers Postgres)
    ├── Endpoints/                 # one test class per controller
    ├── Services/                  # concurrency (ERR-8), transaction-atomicity checks
    └── Scenarios/                 # ActionCreationScenarioTests, ActionArchivalScenarioTests,
                                    # ActionEditGuardScenarioTests, ActionLifecycleScenarioTests,
                                    # KpiForceDeactivationScenarioTests (per spec's Scenario Test blocks)

tests/Nabadat.E2ETests/ActionManagement/    # appended to the existing shared E2E project
├── ActionAddEditTests.cs
├── AllActionsTests.cs
├── ActionDetailsTests.cs
└── ActionsSettingsTests.cs

# Frontend SPA feature — mirrors frontend/src/features/{kpi-management,journeys,settings}/
frontend/src/features/actions/
├── components/       # ActionCard, KpiTargetRow, ActionForm, KpiTargetFieldset
├── hooks/             # useActions, useAction, useActionSettings (query/mutation hooks)
└── pages/             # AllActionsPage.tsx, ActionFormPage.tsx, ActionDetailsPage.tsx

frontend/src/features/settings/pages/
└── ActionsSettingsPage.tsx    # new Settings → Actions subsection page

frontend/src/components/cx/
├── stepped-zone-slider/       # FR-501 — new custom-SVG primitive (reusable by future modules)
├── threshold-slider/          # FR-502 — new custom-SVG primitive
└── timer-ring/                # FR-503 — new custom-SVG primitive

frontend/src/App.tsx           # + routes: /actions, /actions/new, /actions/:id/edit, /actions/:id,
                                #   /settings/actions
```

**Structure Decision**: Web application (Option 2 shape), realized as one new .NET backend
module (`Nabadat.ActionManagement`, hosted inside the existing `Nabadat.TenantAdmin` process per
AD-01's single-runtime rule) plus one new frontend feature folder inside the existing
`frontend/` SPA — no new frontend app, no new backend host, no new E2E project. This exactly
mirrors the three already-shipped modules (`Nabadat.KpiManagement`, `Nabadat.UserManagement`,
`Nabadat.CustomerJourneyManagement`) and their corresponding `frontend/src/features/*`
counterparts; M-15 introduces no new architectural shape, only new instances of the established
one.

## Complexity Tracking

> Two items require justification: one is a spec-ratified business rule (not a design choice
> this plan is free to reject), the other is a deliberate simplification over the spec's own
> "proposed technical design."

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Last-write-wins concurrency (ERR-8) instead of Article 7.2's default `ETag`/`If-Match` optimistic concurrency | The spec's Assumption R-4 (stakeholder-ratified) explicitly mandates last-write-wins + a stale-save warning header, not locking or rejection on conflict — this is a product decision from the SRS, not an implementation shortcut. | Implementing `ETag`/`If-Match` per the Article 7.2 default would reject the second editor's save outright, contradicting R-4 and AC "Concurrent edit" (Story 4) which requires **both** writes to succeed with the audit trail preserving both actors. |
| `IKpiScoreReader` M-15-owned port + `NullKpiScoreReader` stub, instead of calling a real M-06 score API | M-06 (`Nabadat.KpiManagement`) has no score-computation engine in the codebase today (research.md §4, coordination-log.md C-01) — there is no real API to call. | Blocking this entire feature until M-06 ships an unscheduled score engine was rejected: the spec's own scope requires M-15 v1 to ship now, and this exact dependency-inversion shape is already the accepted precedent for M-16's identical `IActiveKpiCatalogReader` situation — not a novel pattern introduced by this plan. |
