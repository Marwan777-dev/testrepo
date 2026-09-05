---

description: "Task list for M-15 Action Management (005-action-management)"

---

# Tasks: M-15 Action Management

**Input**: Design documents from `specs/005-action-management/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md, coordination-log.md)

**Prerequisites**: plan.md ✔, spec.md ✔ (10 user stories, P1-P3), research.md ✔, data-model.md ✔, contracts/ ✔

**Tests**: Per CLAUDE.md "Unit Test Policy" + "E2E Test Policy" — **mandatory** for every story below (spec.md declares full Unit/Integration/E2E coverage for every story; none carry a `skipped` declaration except US5's E2E lane, which spec.md explicitly skips with a stated reason). Each backend story gets a **Unit Tests (write FIRST, must FAIL)** subsection + a **Red Checkpoint**, then **Implementation**, then **Integration & API / Scenario Tests** at the per-story checkpoint. Page-bearing stories additionally get an **E2E (Browser) Tests** subsection *after* implementation (no Red Checkpoint).

**Team**: 3 resources per plan.md — **AbuKr** (backend, write path: US1, US4, US7, US9), **Atia** (backend, read/lifecycle path: US2, US3, US5, US6, US10), **Marawan** (frontend, all UI tasks, sequenced SCR-01 → SCR-02 → SCR-03 → Settings). Phase/task assignments below follow this split; either backend engineer may pick up Foundational tasks together.

**Domain name**: `Nabadat.ActionManagement` (AMENDMENT-008). **New tables**: `actions`, `kpi_targets`, `action_settings` (data-model.md). **Cross-module gap**: M-06 has no live/historical KPI score capability yet — `IKpiScoreReader` ships with a `NullKpiScoreReader` stub (research.md §4, coordination-log.md C-01); every task below that depends on a real score returns/asserts the stub's deterministic "no score" behaviour until C-01 ships.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps the task to a user story (US1..US10)
- File paths are exact, per plan.md's Project Structure

---

## Phase 1: Setup

**Purpose**: Project scaffolding — no story-specific code yet

- [ ] T001 Create the `Nabadat.ActionManagement` class library skeleton in `src/Nabadat.ActionManagement/Nabadat.ActionManagement.csproj` with the four layer folders `Api/`, `Application/`, `Domain/`, `Infrastructure/` (empty except `.gitkeep`), targeting the same `<TargetFramework>` as `Nabadat.KpiManagement.csproj`; add a project reference to `Nabadat.KpiManagement` (for `IKpiConfigReader`, research.md §4) and to the EF Core / Npgsql packages already pinned in `Nabadat.UserManagement.csproj`.
- [ ] T002 [P] Create `tests/Nabadat.ActionManagement.UnitTests/Nabadat.ActionManagement.UnitTests.csproj` (xUnit v3, FluentAssertions 6.12.\*, NSubstitute 5.\*, `Microsoft.Extensions.TimeProvider.Testing` 9.\* — versions copied from `Nabadat.KpiManagement.UnitTests.csproj`), referencing `Nabadat.ActionManagement`.
- [ ] T003 [P] Create `tests/Nabadat.ActionManagement.IntegrationTests/Nabadat.ActionManagement.IntegrationTests.csproj` (`Testcontainers.PostgreSql` 4.\*, `Microsoft.AspNetCore.Mvc.Testing` 10.\* — versions copied from `Nabadat.KpiManagement.IntegrationTests.csproj`), referencing `Nabadat.ActionManagement`.
- [ ] T004 Add `src/Nabadat.ActionManagement`, `tests/Nabadat.ActionManagement.UnitTests`, and `tests/Nabadat.ActionManagement.IntegrationTests` project entries to `Nabadat.TenantAdmin.sln` (mirror the existing `Nabadat.KpiManagement` block).
- [ ] T005 [P] Create the frontend feature skeleton `frontend/src/features/actions/{components,hooks,pages}/` (empty `.gitkeep` files), mirroring `frontend/src/features/kpi-management/`.
- [ ] T006 [P] Create the E2E module folder `tests/Nabadat.E2ETests/ActionManagement/` (empty `.gitkeep`), mirroring `tests/Nabadat.E2ETests/KpiManagement/` (CLAUDE.md E2E Test Policy — single shared project, module-named folders).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure every user story depends on. **No story work starts before this phase is green.**

- [ ] T007 Define Domain entities in `src/Nabadat.ActionManagement/Domain/Entities/{Action.cs,KpiTarget.cs,ActionSettings.cs,EventLog.cs}` per data-model.md §1-4 (one type per file; `EventLog.cs` maps onto the **shared** `event_log` table, mirroring `Nabadat.KpiManagement/Domain/Entities/EventLog.cs`).
- [ ] T008 [P] Define Domain value objects/enums in `src/Nabadat.ActionManagement/Domain/ValueObjects/{ActionStatus.cs,TargetLifecycleState.cs,DeactivationSource.cs,Outcome.cs,TimerState.cs}` per data-model.md §5-6.
- [ ] T009 Write `src/Nabadat.ActionManagement/Infrastructure/Migrations/ActionManagement_Baseline.sql` defining `actions`, `kpi_targets`, `action_settings` (data-model.md §1-3: unique index on `lower(action_name)`; unique index on `(action_id, kpi_id)`; indexes on `target_date`, `kpi_id`, `action_start_date`); add the shared `event_log` DDL only if not already present in the tenant baseline (guard with `CREATE TABLE IF NOT EXISTS`, matching how `Nabadat.KpiManagement`'s baseline does it).
- [ ] T010 Define `ITenantDbContext` in `src/Nabadat.ActionManagement/Application/Interfaces/ITenantDbContext.cs` (DbSets for `Action`, `KpiTarget`, `ActionSettings`, `EventLog` + `SaveChangesAsync` + `ExecuteAsync`/`ExecuteAsync<T>`, DB-08 rule 3-4).
- [ ] T011 Implement `TenantDbContext` + one `IEntityTypeConfiguration<T>` per entity in `src/Nabadat.ActionManagement/Infrastructure/Persistence/{TenantDbContext.cs,Configurations/ActionConfiguration.cs,Configurations/KpiTargetConfiguration.cs,Configurations/ActionSettingsConfiguration.cs,Configurations/EventLogConfiguration.cs}` — explicit `HasColumnName` per property, FK `KpiTarget → Action` only (data-model.md §5: `kpi_id` is an identifier, never a FK).
- [ ] T012 Implement the composition root `src/Nabadat.ActionManagement/ActionManagementServiceCollectionExtensions.cs` (`AddActionManagementModule(...)`) registering `ITenantDbContext`/`TenantDbContext`; leave per-story service registrations as `// TODO(USn)` markers to fill in per story below.
- [ ] T013 [P] Define the published port `IKpiScoreReader` in `src/Nabadat.ActionManagement/Domain/Interfaces/IKpiScoreReader.cs` per contracts/published-interfaces.md (`GetCurrentScoreAsync`, `GetHistoricalScoreAsync`, `GetNormalisedIndexAsync`).
- [ ] T014 [P] Implement the default stub `src/Nabadat.ActionManagement/Infrastructure/KpiIntegration/NullKpiScoreReader.cs` (returns `null` from every method deterministically, per research.md §4) and register it in T012's composition root as the default `IKpiScoreReader`.
- [ ] T015 [P] Define the forward-contract skeleton `src/Nabadat.ActionManagement/Domain/Interfaces/IActionOverlayReader.cs` + `ActionOverlayEntry` record per contracts/published-interfaces.md (no consumer yet — M-07 doesn't exist under `src/`, coordination-log.md C-04).
- [ ] T016 Implement the M-17 event-writing seam `src/Nabadat.ActionManagement/Application/Events/{ActionManagementEventPublisher.cs,Interfaces/IActionManagementEventPublisher.cs,EventLogFactory.cs}` covering the full INT-04 event-type catalogue (data-model.md §4), depending only on `ITenantDbContext`.
- [ ] T017 Create the integration-test fixture `tests/Nabadat.ActionManagement.IntegrationTests/Infrastructure/ActionManagementApplicationFactory.cs` (Testcontainers Postgres, applies `ActionManagement_Baseline.sql`, in-process `WebApplicationFactory<Program>`) — mirrors `Nabadat.KpiManagement.IntegrationTests`'s factory.
- [ ] T018 [P] Create the frontend API client `frontend/src/features/actions/api.ts` (typed fetch wrappers for the endpoints in contracts/api-endpoints.md, reusing the existing `callJson` helper pattern from `tenants/api.ts`).
- [ ] T019 [P] Register placeholder routes `/actions`, `/actions/new`, `/actions/:id/edit`, `/actions/:id`, `/settings/actions` in `frontend/src/App.tsx` pointing at not-yet-built page components (created per story below).
- [ ] T020 [P] Add an "Actions" entry to `NAV_ITEMS` in `frontend/src/components/layout/app-sidebar.tsx` under the existing category group matching this module's domain, plus the matching `ROLE_NAV_KEYS` allowlist entries for P-01/P-02/P-06.

**Checkpoint**: `dotnet build Nabadat.sln` succeeds; `Nabadat.ActionManagement` compiles with an empty-but-wired module; `npm run build` succeeds with the placeholder routes. User story work can now begin.

---

## Phase 3: User Story 1 — Create an Action with KPI Targets (Priority: P1) 🎯 MVP — [AbuKr backend / Marawan frontend]

**Goal**: A CX Program Manager can open Add Action, enter Name/dates/description, add ≥1 KPI Target with thresholds expressed as deltas over the Baseline, and Save — the Action persists, Baseline auto-captures, and it lands in the correct status tab.

**Independent Test**: `/actions` → **Add Action** → fill Name/Start/End + one KPI Target (KPI, Target Date, Upper Threshold > 0) → **Save action** → toast "Action saved" → back on `/actions` with the new card in the right tab; `action.created` + `baseline.captured` audit events exist.

### Unit Tests for User Story 1 (write FIRST, must FAIL) ⚠️

- [ ] T021 [P] [US1] Unit tests for `ThresholdValidator` in `tests/Nabadat.ActionManagement.UnitTests/Actions/ThresholdValidatorTests.cs` — VAL-201..211 required cases (spec.md US1 Unit Test Coverage).
- [ ] T022 [P] [US1] Unit tests for `ThresholdAutoSyncCalculator` in `tests/Nabadat.ActionManagement.UnitTests/Actions/ThresholdAutoSyncCalculatorTests.cs` — BR-004 auto-sync + clamp cases.
- [ ] T023 [P] [US1] Unit tests for `BaselineCaptureService` in `tests/Nabadat.ActionManagement.UnitTests/Actions/BaselineCaptureServiceTests.cs` — live-today capture, retro-dated historical capture, `NoBaselineScoreException` case (mock `IKpiScoreReader`).
- [ ] T024 [P] [US1] Unit tests for `ActionStatusCalculator` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/ActionStatusCalculatorTests.cs` — Planned/Active/Completed/Archived cases (inject `FakeTimeProvider`).
- [ ] T025 [P] [US1] Unit tests for `KpiOptionsFilter` in `tests/Nabadat.ActionManagement.UnitTests/Actions/KpiOptionsFilterTests.cs` — excludes already-chosen + M-06-deactivated KPIs.

### Red Checkpoint for User Story 1 🔴

- [ ] T026 [US1] Run `dotnet test tests/Nabadat.ActionManagement.UnitTests`. Expect compile errors (production types don't exist yet). Paste transcript, commit the red baseline via `/speckit-git-commit`.

### Implementation for User Story 1

- [ ] T027 [P] [US1] Create Action-creation DTOs in `src/Nabadat.ActionManagement/Application/Actions/Dtos/{ActionCreateCommand.cs,KpiTargetInput.cs,ActionDto.cs,KpiTargetDto.cs}`.
- [ ] T028 [US1] Implement `ThresholdValidator` in `src/Nabadat.ActionManagement/Application/Actions/Validators/ThresholdValidator.cs` to pass T021.
- [ ] T029 [P] [US1] Implement `ThresholdAutoSyncCalculator` in `src/Nabadat.ActionManagement/Application/Actions/ThresholdAutoSyncCalculator.cs` to pass T022.
- [ ] T030 [US1] Implement `BaselineCaptureService` in `src/Nabadat.ActionManagement/Application/Actions/BaselineCaptureService.cs` (depends on `IKpiScoreReader`, T013/T014) to pass T023.
- [ ] T031 [P] [US1] Implement `ActionStatusCalculator` in `src/Nabadat.ActionManagement/Application/Measurement/ActionStatusCalculator.cs` (takes `TimeProvider`) to pass T024.
- [ ] T032 [P] [US1] Implement `KpiOptionsFilter` in `src/Nabadat.ActionManagement/Application/Actions/KpiOptionsFilter.cs` (depends on `IKpiConfigReader`) to pass T025.
- [ ] T033 [US1] Implement `IActionService`/`ActionService` (create path only) in `src/Nabadat.ActionManagement/Application/Actions/{Interfaces/IActionService.cs,ActionService.cs}` — composes T028/T030/T032, self-persists via `ITenantDbContext.ExecuteAsync`, writes `action.created` + `baseline.captured` via T016 (depends on T027-T032).
- [ ] T034 [US1] Implement `POST /api/v1/actions` in `src/Nabadat.ActionManagement/Api/Controllers/ActionsController.cs` + `Api/Contracts/{CreateActionRequest.cs,ActionResponse.cs}` per contracts/api-endpoints.md (VAL-201..211 → 400/409, ERR-5 → 409, 403 for non-P-01) (depends on T033).
- [ ] T035 [US1] Wire `IActionService`, `ThresholdValidator`, `BaselineCaptureService`, `KpiOptionsFilter` into `ActionManagementServiceCollectionExtensions` (depends on T028-T034).
- [ ] T036 [P] [US1] Build the `ThresholdSlider` custom-SVG primitive in `frontend/src/components/cx/threshold-slider/` per CLAUDE.md FR-502 spec (default/set states, draggable flags, `role="slider"`, theme-aware neutral chrome).
- [ ] T037 [P] [US1] Build `KpiTargetFieldset` in `frontend/src/features/actions/components/KpiTargetFieldset.tsx` (KPI select with cross-Target disable, Target Date, Lower/Upper Threshold inputs + T036's slider, Current-score/Baseline label per FR-206).
- [ ] T038 [US1] Build `ActionForm` (create mode) in `frontend/src/features/actions/components/ActionForm.tsx` per FR-202/203 (Name/Start/End/Description panel + repeatable T037 fieldsets + Add KPI Target button, VAL-201..211 inline errors) (depends on T036-T037).
- [ ] T039 [US1] Build `AddActionPage`/wire `ActionFormPage` for the `/actions/new` route in `frontend/src/features/actions/pages/ActionFormPage.tsx`, replacing T019's placeholder (depends on T038).
- [ ] T040 [P] [US1] Add `useCreateAction` mutation hook in `frontend/src/features/actions/hooks/useActions.ts` (depends on T018).

### Integration & API / Scenario Tests for User Story 1 🐳

- [ ] T041 [P] [US1] API tests for `POST /api/v1/actions` in `tests/Nabadat.ActionManagement.IntegrationTests/Endpoints/ActionsEndpointTests.cs` — valid create, duplicate name (400/409), retro-dated happy, retro-dated missing-history (409 ERR-5), U=0 (400 VAL-210), non-P-01 (403).
- [ ] T042 [P] [US1] Scenario test `ActionCreationScenarioTests` in `tests/Nabadat.ActionManagement.IntegrationTests/Scenarios/ActionCreationScenarioTests.cs` — `POST` → `GET` list → `GET` detail; asserts `action.created` + one `baseline.captured` per Target.

### E2E (Browser) Tests for User Story 1 🎭

- [ ] T043 [P] [US1] E2E tests in `tests/Nabadat.E2ETests/ActionManagement/ActionAddEditTests.cs` (new file — Add-Action `[TestMethod]`s only) covering the 10 required scenarios from spec.md US1 (happy path, VAL-201/210 blocks, KPI-disable, baseline-label flip, slider default/set/clamp states, Add-KPI-Target-disabled, role guard). Author with the `e2e-testing` skill; update `COVERAGE.md`.

**Build gate**: `dotnet test tests/Nabadat.ActionManagement.UnitTests` and `...IntegrationTests` green; `npm run build` green; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ActionAddEditTests"` green.

**Checkpoint**: US1 fully functional and independently testable — the MVP creation path works end-to-end.

---

## Phase 4: User Story 2 — Monitor Actions on the All Actions page (Priority: P1) — [Atia backend / Marawan frontend]

**Goal**: `/actions` shows 4 tabs (Active/Planned/Completed/Archived) with counts, each Active card spotlighting its Lowest-Performing Target with a pace-coloured timer.

**Independent Test**: Seed 2 Active/3 Planned/3 Completed/1 Archived → tab counts read `2/3/3/1`; an Active card with raw Score 66.7%/Time 84.2% shows a red timer, 84% ring fill, "Score 67% · Time 84% — behind pace".

### Unit Tests for User Story 2 (write FIRST, must FAIL) ⚠️

- [ ] T044 [P] [US2] Unit tests for `LowestPerformingTargetSelector` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/LowestPerformingTargetSelectorTests.cs` — raw-negative wins, tie-breaks (earliest date, then KPI name), eligibility exclusions.
- [ ] T045 [P] [US2] Unit tests for `ScoreProgressCalculator` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/ScoreProgressCalculatorTests.cs`.
- [ ] T046 [P] [US2] Unit tests for `TimeProgressCalculator` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/TimeProgressCalculatorTests.cs` — BR-F1 zero-during-execution case.
- [ ] T047 [P] [US2] Unit tests for `TimerColourResolver` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/TimerColourResolverTests.cs` — Green/Yellow(±0.005)/Red/Grey/Empty.
- [ ] T048 [P] [US2] Unit tests for `DisplayClamper` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/DisplayClamperTests.cs`.
- [ ] T049 [P] [US2] Unit tests for `ActionCardStatusGrouper` in `tests/Nabadat.ActionManagement.UnitTests/Actions/ActionCardStatusGrouperTests.cs` — Archived sinks exclusively to its own tab.
- [ ] T050 [P] [US2] Unit tests for `ActionSearchFilter` in `tests/Nabadat.ActionManagement.UnitTests/Actions/ActionSearchFilterTests.cs` — case-insensitive substring match.
- [ ] T051 [P] [US2] Unit tests for `ZeroEligibleFallback` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/ZeroEligibleFallbackTests.cs` — FR-111.

### Red Checkpoint for User Story 2 🔴

- [ ] T052 [US2] Run `dotnet test tests/Nabadat.ActionManagement.UnitTests`. Expect compile errors. Paste transcript, commit red baseline.

### Implementation for User Story 2

- [ ] T053 [P] [US2] Implement `LowestPerformingTargetSelector` in `src/Nabadat.ActionManagement/Application/Measurement/LowestPerformingTargetSelector.cs` to pass T044.
- [ ] T054 [P] [US2] Implement `ScoreProgressCalculator` in `src/Nabadat.ActionManagement/Application/Measurement/ScoreProgressCalculator.cs` to pass T045.
- [ ] T055 [P] [US2] Implement `TimeProgressCalculator` in `src/Nabadat.ActionManagement/Application/Measurement/TimeProgressCalculator.cs` (takes `TimeProvider`) to pass T046.
- [ ] T056 [P] [US2] Implement `TimerColourResolver` in `src/Nabadat.ActionManagement/Application/Measurement/TimerColourResolver.cs` to pass T047.
- [ ] T057 [P] [US2] Implement `DisplayClamper` in `src/Nabadat.ActionManagement/Application/Measurement/DisplayClamper.cs` to pass T048.
- [ ] T058 [P] [US2] Implement `ActionCardStatusGrouper` in `src/Nabadat.ActionManagement/Application/Actions/ActionCardStatusGrouper.cs` to pass T049.
- [ ] T059 [P] [US2] Implement `ActionSearchFilter` in `src/Nabadat.ActionManagement/Application/Actions/ActionSearchFilter.cs` to pass T050.
- [ ] T060 [P] [US2] Implement `ZeroEligibleFallback` in `src/Nabadat.ActionManagement/Application/Measurement/ZeroEligibleFallback.cs` to pass T051.
- [ ] T061 [US2] Implement the cursor-paginated list query `IActionListQuery`/`ActionListQuery` in `src/Nabadat.ActionManagement/Application/Actions/{Interfaces/IActionListQuery.cs,ActionListQuery.cs}` composing T053/T058/T059/T060 (depends on T053-T060).
- [ ] T062 [US2] Implement `GET /api/v1/actions` in `ActionsController` + `Api/Contracts/ActionListResponse.cs` (tab/q/kpi/start_from/start_to/page_size/page_token per contracts/api-endpoints.md) (depends on T061).
- [ ] T063 [US2] Wire the new Application/Measurement services + `IActionListQuery` into `ActionManagementServiceCollectionExtensions` (depends on T053-T062).
- [ ] T064 [P] [US2] Build the `TimerRing` custom-SVG primitive in `frontend/src/components/cx/timer-ring/` per FR-503 (44×44px ring, colour states, tooltips, theme-aware chrome).
- [ ] T065 [P] [US2] Build the `SteppedZoneSlider` custom-SVG primitive (card variant) in `frontend/src/components/cx/stepped-zone-slider/` per FR-501 (hard-edged red/yellow/green zones, B/C markers).
- [ ] T066 [US2] Build `ActionCard` in `frontend/src/features/actions/components/ActionCard.tsx` (Active/Planned/Completed/Archived variants per FR-113..116, kebab menu, mini KPI labels) (depends on T064-T065).
- [ ] T067 [US2] Build `AllActionsPage` in `frontend/src/features/actions/pages/AllActionsPage.tsx` (tabs + count pills, search input, skeleton loading, per-tab empty states, pagination/infinite-scroll) replacing T019's placeholder for `/actions` (depends on T066).
- [ ] T068 [P] [US2] Add `useActions` query hook in `frontend/src/features/actions/hooks/useActions.ts` (depends on T018).

### Integration & Scenario Tests for User Story 2 🐳

- [ ] T069 [P] [US2] API tests for `GET /api/v1/actions` in `tests/Nabadat.ActionManagement.IntegrationTests/Endpoints/ActionListEndpointTests.cs` — tab grouping, `q=` cross-tab search + hint, empty tab, pagination.

*(Scenario test: `not-needed` per spec.md US2 — single-endpoint list view, no cross-endpoint state.)*

### E2E (Browser) Tests for User Story 2 🎭

- [ ] T070 [P] [US2] E2E tests in `tests/Nabadat.E2ETests/ActionManagement/AllActionsTests.cs` (new file) covering the 10 required scenarios from spec.md US2 (tab counts, red-timer/behind-pace card, auto-move to Completed, cross-tab search hint, zero-eligible fallback, Planned/Completed card variants, empty state, role-based hidden controls, pagination). Author with the `e2e-testing` skill; update `COVERAGE.md`.

**Build gate**: unit + integration green; `npm run build` green; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~AllActionsTests"` green.

**Checkpoint**: US1 + US2 both independently functional.

---

## Phase 5: User Story 3 — Drill into Action Details (Priority: P1) — [Atia backend / Marawan frontend]

**Goal**: `/actions/:id` renders the full Action breakdown — header, 4-date timeline, one row per KPI Target in the correct variant (active-unevaluated / evaluated / completed / planned / deactivated).

**Independent Test**: Open a Planned, Active, and Completed Action via `/actions/:id`; verify each renders its correct row variant per SRS §8.4-8.6; Archive/Unarchive refresh the header badge in place.

### Unit Tests for User Story 3 (write FIRST, must FAIL) ⚠️

- [ ] T071 [P] [US3] Unit tests for `ActionDetailProjection` in `tests/Nabadat.ActionManagement.UnitTests/Actions/ActionDetailProjectionTests.cs` — variant selection per row state.
- [ ] T072 [P] [US3] Unit tests for `OutcomeEvaluator` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/OutcomeEvaluatorTests.cs` — BR-O1..O4 incl. U=L equality.
- [ ] T073 [P] [US3] Unit tests for `TargetStartDeriver` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/TargetStartDeriverTests.cs` — `End + 1 day`.
- [ ] T074 [P] [US3] Unit tests for `LatestTargetDateCalculator` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/LatestTargetDateCalculatorTests.cs`.

### Red Checkpoint for User Story 3 🔴

- [ ] T075 [US3] Run `dotnet test tests/Nabadat.ActionManagement.UnitTests`. Expect compile errors. Paste transcript, commit red baseline.

### Implementation for User Story 3

- [ ] T076 [P] [US3] Implement `OutcomeEvaluator` in `src/Nabadat.ActionManagement/Application/Measurement/OutcomeEvaluator.cs` to pass T072.
- [ ] T077 [P] [US3] Implement `TargetStartDeriver` in `src/Nabadat.ActionManagement/Application/Measurement/TargetStartDeriver.cs` to pass T073.
- [ ] T078 [P] [US3] Implement `LatestTargetDateCalculator` in `src/Nabadat.ActionManagement/Application/Measurement/LatestTargetDateCalculator.cs` to pass T074.
- [ ] T079 [US3] Implement `ActionDetailProjection` in `src/Nabadat.ActionManagement/Application/Actions/ActionDetailProjection.cs` (composes T076-T078 + T053/T055/T056) to pass T071 (depends on T076-T078).
- [ ] T080 [US3] Implement `GET /api/v1/actions/{id}` in `ActionsController` + `Api/Contracts/ActionDetailResponse.cs` (404 ERR-6 for missing/foreign-tenant) (depends on T079).
- [ ] T081 [US3] Implement `POST /api/v1/actions/{id}/archive` and `POST /api/v1/actions/{id}/unarchive` in `ActionsController` (409 idempotency guard on re-archive) (depends on T033).
- [ ] T082 [US3] Wire `ActionDetailProjection` into `ActionManagementServiceCollectionExtensions` (depends on T076-T080).
- [ ] T083 [US3] Build the Target-row-variant components in `frontend/src/features/actions/components/{TargetRowActive.tsx,TargetRowCompleted.tsx,TargetRowPlanned.tsx,TargetRowDeactivated.tsx}` per FR-304..308 (reference-variant slider with L/U flags, per-row timer, outcome labels).
- [ ] T084 [US3] Build `ActionDetailsPage` in `frontend/src/features/actions/pages/ActionDetailsPage.tsx` (header block, 4-date timeline with derivation tooltip, Edit/Archive/Unarchive buttons, Target row list) replacing T019's placeholder for `/actions/:id` (depends on T083).
- [ ] T085 [P] [US3] Add `useAction` query hook + archive/unarchive mutations in `frontend/src/features/actions/hooks/useAction.ts` (depends on T018).

### Integration & Scenario Tests for User Story 3 🐳

- [ ] T086 [P] [US3] API tests for `GET /api/v1/actions/{id}`, `POST .../archive`, `POST .../unarchive` in `tests/Nabadat.ActionManagement.IntegrationTests/Endpoints/ActionDetailEndpointTests.cs` — full shape, foreign-tenant 404, archive/unarchive round-trip, re-archive 409, non-P-01 403.
- [ ] T087 [P] [US3] Scenario test `ActionArchivalScenarioTests` in `tests/Nabadat.ActionManagement.IntegrationTests/Scenarios/ActionArchivalScenarioTests.cs` — archive → get (badge+continuity) → unarchive → get (recomputed status).

### E2E (Browser) Tests for User Story 3 🎭

- [ ] T088 [P] [US3] E2E tests in `tests/Nabadat.E2ETests/ActionManagement/ActionDetailsTests.cs` (new file) covering the 10 required scenarios from spec.md US3 (active/completed/deactivated row variants, archived badge, Target Start tooltip, force-deactivated Activate-disabled, evaluated-target-on-active row, archive-refresh-in-place, deep-link redirects). Author with the `e2e-testing` skill; update `COVERAGE.md`.

**Build gate**: unit + integration green; `npm run build` green; E2E filter `ActionDetailsTests` green.

**Checkpoint**: US1 + US2 + US3 complete — this is the MVP surface (create, monitor, drill in).

---

## Phase 6: User Story 4 — Edit a Planned or Active Action (Priority: P2) — [AbuKr backend / Marawan frontend]

**Goal**: Edit mode reuses SCR-02 pre-filled; guarded edits (Start Date → DLG-2, End Date → DLG-4, thresholds mid-monitoring → DLG-3) recompute baselines/progress server-side and are audit-logged field-level.

**Independent Test**: Open an Active Action's Edit, change Start Date, confirm DLG-2, Save → baseline recaptured from M-06 history, `field_edited(start_date)` + `baseline.recaptured` audit events exist.

### Unit Tests for User Story 4 (write FIRST, must FAIL) ⚠️

- [ ] T089 [P] [US4] Unit tests for `EditGuardResolver` in `tests/Nabadat.ActionManagement.UnitTests/Actions/EditGuardResolverTests.cs` — DLG-2/3/4/none per field+status.
- [ ] T090 [P] [US4] Unit tests for `BaselineRecaptureService` in `tests/Nabadat.ActionManagement.UnitTests/Actions/BaselineRecaptureServiceTests.cs` — recapture success + `NoBaselineScoreException`.
- [ ] T091 [P] [US4] Unit tests for `EditPermissionResolver` in `tests/Nabadat.ActionManagement.UnitTests/Actions/EditPermissionResolverTests.cs` — Completed/Archived/role denials.
- [ ] T092 [P] [US4] Unit tests for `AuditFieldDiff` in `tests/Nabadat.ActionManagement.UnitTests/Events/AuditFieldDiffTests.cs` — one `field_edited` event per changed field.

### Red Checkpoint for User Story 4 🔴

- [ ] T093 [US4] Run `dotnet test tests/Nabadat.ActionManagement.UnitTests`. Expect compile errors. Paste transcript, commit red baseline.

### Implementation for User Story 4

- [ ] T094 [P] [US4] Implement `EditGuardResolver` in `src/Nabadat.ActionManagement/Application/Actions/EditGuardResolver.cs` to pass T089.
- [ ] T095 [P] [US4] Implement `BaselineRecaptureService` in `src/Nabadat.ActionManagement/Application/Actions/BaselineRecaptureService.cs` to pass T090.
- [ ] T096 [P] [US4] Implement `EditPermissionResolver` in `src/Nabadat.ActionManagement/Application/Actions/EditPermissionResolver.cs` to pass T091.
- [ ] T097 [P] [US4] Implement `AuditFieldDiff` in `src/Nabadat.ActionManagement/Application/Events/AuditFieldDiff.cs` to pass T092.
- [ ] T098 [US4] Extend `ActionService` with the edit path (guarded recompute via T094/T095, permission check via T096, audit via T097) and implement `PUT /api/v1/actions/{id}` in `ActionsController` (409 `action.read_only`/`action.archived`, `X-Nabadat-Stale-Save` header on last-write-wins) (depends on T094-T097).
- [ ] T099 [P] [US4] Build the DLG-2/DLG-3/DLG-4 confirm dialogs in `frontend/src/features/actions/components/{RecaptureBaselineDialog.tsx,ThresholdChangeDialog.tsx,MoveMonitoringStartDialog.tsx}` (shadcn `Dialog`, exact copy per spec FR-M06/DLG-2..4).
- [ ] T100 [US4] Extend `ActionForm` for edit-mode prefill (incl. deactivated Targets faded) and wire T099's dialogs on Start/End/threshold changes (depends on T099).
- [ ] T101 [US4] Wire `/actions/:id/edit` in `ActionFormPage` (edit mode) with the Completed/Archived deep-link redirect + NTF-6 toast (depends on T100).

### Integration & Scenario Tests for User Story 4 🐳

- [ ] T102 [P] [US4] API tests for `PUT /api/v1/actions/{id}` in `tests/Nabadat.ActionManagement.IntegrationTests/Endpoints/ActionEditEndpointTests.cs` — name-only edit, Start Date recapture, End Date retarget, threshold mid-monitoring, Completed/Archived 409, concurrent stale-save header, ERR-5, non-P-01 403.
- [ ] T103 [P] [US4] Scenario test `ActionEditGuardScenarioTests` in `tests/Nabadat.ActionManagement.IntegrationTests/Scenarios/ActionEditGuardScenarioTests.cs` — get → put(start_date) → get(recomputed) → audit-events (paired `field_edited`+`baseline.recaptured`).

### E2E (Browser) Tests for User Story 4 🎭

- [ ] T104 [P] [US4] E2E tests appended to `tests/Nabadat.E2ETests/ActionManagement/ActionAddEditTests.cs` (Edit-Action `[TestMethod]` block) covering the 8 required scenarios from spec.md US4 (prefill incl. deactivated targets, DLG-2/3/4 trigger + cancel-reverts, Completed/Archived redirect toasts, ERR-5 dialog). Update `COVERAGE.md`.

**Build gate**: unit + integration green; `npm run build` green; E2E filter `ActionAddEditTests` green.

**Checkpoint**: US1-US4 independently functional.

---

## Phase 7: User Story 5 — Automatic status transitions (Priority: P2) — [Atia backend]

**Goal**: Planned→Active→Completed fire automatically at day boundaries (tenant timezone); per-Target evaluation fires on each Target Date while later Targets keep the Action Active.

**Independent Test**: Advance `FakeTimeProvider` across Start Date, each Target Date, and the latest Target Date; assert SCR-01/03 group/render correctly at each step and `action.status_transitioned` + `outcome.evaluated` fire.

### Unit Tests for User Story 5 (write FIRST, must FAIL) ⚠️

- [ ] T105 [P] [US5] Unit tests for `PerTargetEvaluationCalculator` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/PerTargetEvaluationCalculatorTests.cs` — evaluates + stores outcome once `target_date < now`.
- [ ] T106 [P] [US5] Unit tests for `TimezoneDayBoundary` in `tests/Nabadat.ActionManagement.UnitTests/Measurement/TimezoneDayBoundaryTests.cs` — UTC→tenant-timezone day-boundary conversion.
- [ ] T107 [P] [US5] Extend `ActionStatusCalculatorTests` (T024's file) with the day-granular boundary cases from spec.md US5 (same-day-Active, latest-target-date-today-still-Active, day-passed-Completed).

### Red Checkpoint for User Story 5 🔴

- [ ] T108 [US5] Run `dotnet test tests/Nabadat.ActionManagement.UnitTests`. Expect compile errors for the two new types (T105/T106) and a possible assertion failure for the extended T107 cases. Paste transcript, commit red baseline.

### Implementation for User Story 5

- [ ] T109 [P] [US5] Implement `TimezoneDayBoundary` in `src/Nabadat.ActionManagement/Application/Measurement/TimezoneDayBoundary.cs` to pass T106; thread it through `ActionStatusCalculator` (T031) to pass T107's extended cases.
- [ ] T110 [US5] Implement `PerTargetEvaluationCalculator` in `src/Nabadat.ActionManagement/Application/Measurement/PerTargetEvaluationCalculator.cs` to pass T105; wire it into `ActionDetailProjection` (T079) and `ActionListQuery` (T061) so evaluation + `outcome.evaluated` audit fire lazily on read (depends on T109, T079, T061).

### Integration & Scenario Tests for User Story 5 🐳

- [ ] T111 [P] [US5] API tests using `FakeTimeProvider` in `tests/Nabadat.ActionManagement.IntegrationTests/Endpoints/ActionLifecycleEndpointTests.cs` — Active→Completed on clock advance, single-Target evaluation mid-Active, retro-dated-born-Completed→edit-409.
- [ ] T112 [P] [US5] Scenario test `ActionLifecycleScenarioTests` in `tests/Nabadat.ActionManagement.IntegrationTests/Scenarios/ActionLifecycleScenarioTests.cs` — full Planned→Active→per-Target-evaluated→Completed→edit-refused walk, asserting the exact `status_transitioned`/`outcome.evaluated` event sequence.

*(E2E: `skipped` per spec.md US5 — lifecycle transitions are time-based and verified via the integration scenario harness; visible surfaces already covered by US2/US3's E2E suites.)*

**Build gate**: unit + integration green.

**Checkpoint**: US1-US5 independently functional.

---

## Phase 8: User Story 6 — Archive and Unarchive an Action (Priority: P2) — [Atia backend / Marawan frontend]

**Goal**: Archive/Unarchive from any card kebab or the SCR-03 header; Archived is a standalone status with continuous measurement; Archive requires no confirmation.

**Independent Test**: Kebab → Archive on an Active card → card moves to Archived tab, timer keeps updating; kebab → Unarchive → card returns to its date-computed tab.

### Unit Tests for User Story 6 (write FIRST, must FAIL) ⚠️

- [ ] T113 [P] [US6] Unit tests for `ArchiveStateMachine` in `tests/Nabadat.ActionManagement.UnitTests/Actions/ArchiveStateMachineTests.cs` — all valid/invalid transitions (spec.md US6 Required cases).

### Red Checkpoint for User Story 6 🔴

- [ ] T114 [US6] Run `dotnet test tests/Nabadat.ActionManagement.UnitTests`. Expect compile error. Paste transcript, commit red baseline.

### Implementation for User Story 6

- [ ] T115 [US6] Implement `ArchiveStateMachine` in `src/Nabadat.ActionManagement/Application/Actions/ArchiveStateMachine.cs` to pass T113; wire into `ActionService`'s archive/unarchive path (T081) replacing the ad-hoc guard.
- [ ] T116 [US6] Add the kebab menu **Archive**/**Unarchive** items to `ActionCard` (T066) and confirm the SCR-03 header Archive/Unarchive buttons (T084) require no confirmation dialog (BR-009) (depends on T066, T084, T115).

*(Integration + Scenario tests: covered by US3's `ActionDetailEndpointTests`/`ActionArchivalScenarioTests` — no duplicate coverage per spec.md US6.)*

### E2E (Browser) Tests for User Story 6 🎭

- [ ] T117 [P] [US6] E2E tests appended to `AllActionsTests.cs` (kebab archive/unarchive) and `ActionDetailsTests.cs` (no-dialog archive-refresh-in-place) per spec.md US6's 3 required scenarios. Update `COVERAGE.md`.

**Build gate**: unit green; `npm run build` green; E2E filters `AllActionsTests`+`ActionDetailsTests` green.

**Checkpoint**: US1-US6 independently functional.

---

## Phase 9: User Story 7 — Manage KPI Target lifecycle (Priority: P3) — [AbuKr backend / Marawan frontend]

**Goal**: Activate/deactivate/delete individual KPI Targets; observe force-deactivation when M-06 deactivates the underlying KPI (BR-011), via the lazy `event_log` watermark consumer (research.md §4.3).

**Independent Test**: Toggle a Target off → faded, Delete visible; Delete (DLG-1) → removed, KPI freed. Simulate an M-06 `settings.changed(kpi, active:false)` event row → next read force-deactivates the matching Target across all Actions.

### Unit Tests for User Story 7 (write FIRST, must FAIL) ⚠️

- [ ] T118 [P] [US7] Unit tests for `TargetLifecycleStateMachine` in `tests/Nabadat.ActionManagement.UnitTests/Targets/TargetLifecycleStateMachineTests.cs` — manual/forced transitions, delete-only-when-deactivated (BR-012).
- [ ] T119 [P] [US7] Unit tests for `KpiForceDeactivationCascade` in `tests/Nabadat.ActionManagement.UnitTests/Targets/KpiForceDeactivationCascadeTests.cs` — cascades matching-KPI Targets only, emits one `target.deactivated(source=forced)` per affected Target.

### Red Checkpoint for User Story 7 🔴

- [ ] T120 [US7] Run `dotnet test tests/Nabadat.ActionManagement.UnitTests`. Expect compile errors. Paste transcript, commit red baseline.

### Implementation for User Story 7

- [ ] T121 [P] [US7] Implement `TargetLifecycleStateMachine` in `src/Nabadat.ActionManagement/Application/Targets/TargetLifecycleStateMachine.cs` to pass T118.
- [ ] T122 [US7] Implement `KpiForceDeactivationCascade` in `src/Nabadat.ActionManagement/Application/Targets/KpiForceDeactivationCascade.cs` to pass T119 — reads the shared `event_log` table filtered `event_type='settings.changed' AND entity_type='kpi'`, watermarked via `Action.last_kpi_event_watermark`/`ActionSettings.last_kpi_event_watermark` (data-model.md §1/§3), invoked lazily from the read paths below (depends on T121).
- [ ] T123 [US7] Implement `IKpiTargetService`/`KpiTargetService` in `src/Nabadat.ActionManagement/Application/Targets/{Interfaces/IKpiTargetService.cs,KpiTargetService.cs}` (activate/deactivate/delete, R-17 last-remaining-Target guard) and `PATCH`/`DELETE /api/v1/actions/{id}/targets/{targetId}` in `Api/Controllers/ActionTargetsController.cs` (depends on T121).
- [ ] T124 [US7] Invoke `KpiForceDeactivationCascade` (T122) at the top of `GET /api/v1/actions`, `GET /api/v1/actions/{id}`, and the target endpoints (T123) before returning data (depends on T122, T062, T080, T123).
- [ ] T125 [US7] Build the Target activate/deactivate toggle, Delete button, and DLG-1 confirm dialog in `frontend/src/features/actions/components/{TargetActiveToggle.tsx,DeleteTargetDialog.tsx}`, wired into `KpiTargetFieldset` (T037) and the SCR-03 deactivated row (`TargetRowDeactivated.tsx`, T083) (depends on T037, T083).

### Integration & Scenario Tests for User Story 7 🐳

- [ ] T126 [P] [US7] API tests for `PATCH`/`DELETE .../targets/{targetId}` in `tests/Nabadat.ActionManagement.IntegrationTests/Endpoints/ActionTargetEndpointTests.cs` — manual deactivate/reactivate, forced-reactivate-blocked (409), delete deactivated (200), delete-active-blocked (409 BR-012), delete-last-remaining-blocked (409 R-17); plus a cascade test that inserts a `settings.changed(kpi,active:false)` row directly into `event_log` and asserts the next `GET` force-deactivates the matching Target.
- [ ] T127 [P] [US7] Scenario test `KpiForceDeactivationScenarioTests` in `tests/Nabadat.ActionManagement.IntegrationTests/Scenarios/KpiForceDeactivationScenarioTests.cs` — cascade → reactivate-refused → KPI reactivated → reactivate-succeeds; asserts CSAT Targets untouched.

### E2E (Browser) Tests for User Story 7 🎭

- [ ] T128 [P] [US7] E2E tests appended to `ActionAddEditTests.cs` (target toggle/delete/DLG-1) and `ActionDetailsTests.cs` (deactivated-row Activate enabled/disabled) per spec.md US7's 7 required scenarios. Update `COVERAGE.md`.

**Build gate**: unit + integration green; `npm run build` green; E2E filters green.

**Checkpoint**: US1-US7 independently functional.

---

## Phase 10: User Story 8 — Search and filter across all tabs (Priority: P3) — [AbuKr backend / Marawan frontend]

**Goal**: KPI multi-select + Start-Date range filters AND-combine with search across all four tabs; no Status/Created-by filter (BR-021).

**Independent Test**: KPI filter `[NPS, CSAT]` → only Actions targeting either remain across all tabs; Start-Date range `2026-07-01..2026-07-31` → only matching Actions remain; toolbar has no Status/Created-by dropdown.

### Unit Tests for User Story 8 (write FIRST, must FAIL) ⚠️

- [ ] T129 [P] [US8] Unit tests for `ActionKpiFilter` in `tests/Nabadat.ActionManagement.UnitTests/Actions/ActionKpiFilterTests.cs` — any-match semantics.
- [ ] T130 [P] [US8] Unit tests for `ActionDateRangeFilter` in `tests/Nabadat.ActionManagement.UnitTests/Actions/ActionDateRangeFilterTests.cs` — both-bounds-optional.
- [ ] T131 [P] [US8] Unit tests for `FilterCombinator` in `tests/Nabadat.ActionManagement.UnitTests/Actions/FilterCombinatorTests.cs` — AND-combines search+kpi+date-range.

### Red Checkpoint for User Story 8 🔴

- [ ] T132 [US8] Run `dotnet test tests/Nabadat.ActionManagement.UnitTests`. Expect compile errors. Paste transcript, commit red baseline.

### Implementation for User Story 8

- [ ] T133 [P] [US8] Implement `ActionKpiFilter`, `ActionDateRangeFilter`, `FilterCombinator` in `src/Nabadat.ActionManagement/Application/Actions/{ActionKpiFilter.cs,ActionDateRangeFilter.cs,FilterCombinator.cs}` to pass T129-T131; wire `FilterCombinator` into `ActionListQuery` (T061) and the `kpi[]`/`start_from`/`start_to` params already scaffolded on `GET /api/v1/actions` (T062) (depends on T061, T062).
- [ ] T134 [US8] Build the KPI multi-select + Start-Date-range filter toolbar in `frontend/src/features/actions/components/ActionsFilterToolbar.tsx` (no Status/Created-by controls, BR-021), wired into `AllActionsPage` (T067) per the "Filter / Toolbar Row" pattern in CLAUDE.md (depends on T067).

*(Integration tests: covered by US2's `ActionListEndpointTests` — extend that file with the KPI-filter/date-range/no-status-filter cases per spec.md US8, no new file. Scenario: `not-needed`.)*

### E2E (Browser) Tests for User Story 8 🎭

- [ ] T135 [P] [US8] E2E tests appended to `AllActionsTests.cs` per spec.md US8's 4 required scenarios (cross-tab match hint, KPI-filter AND search, from/to both present, no Status/Created-by dropdown). Update `COVERAGE.md`.

**Build gate**: unit green; extended `ActionListEndpointTests` green; `npm run build` green; E2E filter `AllActionsTests` green.

**Checkpoint**: US1-US8 independently functional.

---

## Phase 11: User Story 9 — Configure tenant Settings → Actions (Priority: P3) — [AbuKr backend / Marawan frontend]

**Goal**: Settings → Actions exposes X (max upper threshold) and PAD (slider padding), tenant-wide, audit-logged, with the SET-1 guard.

**Independent Test**: Change X 20→30 → open slider scale-notes read "Scale 0-30"; attempt X=5 when largest saved U=8 → blocked with the exact guard message.

### Unit Tests for User Story 9 (write FIRST, must FAIL) ⚠️

- [ ] T136 [P] [US9] Unit tests for `SettingsUpdateValidator` in `tests/Nabadat.ActionManagement.UnitTests/Settings/SettingsUpdateValidatorTests.cs` — SET-1 range+guard, SET-2 range.
- [ ] T137 [P] [US9] Unit tests for `LargestSavedUpperCalculator` in `tests/Nabadat.ActionManagement.UnitTests/Settings/LargestSavedUpperCalculatorTests.cs` — incl. Archived Actions.

### Red Checkpoint for User Story 9 🔴

- [ ] T138 [US9] Run `dotnet test tests/Nabadat.ActionManagement.UnitTests`. Expect compile errors. Paste transcript, commit red baseline.

### Implementation for User Story 9

- [ ] T139 [P] [US9] Implement `SettingsUpdateValidator` and `LargestSavedUpperCalculator` in `src/Nabadat.ActionManagement/Application/Settings/{SettingsUpdateValidator.cs,LargestSavedUpperCalculator.cs}` to pass T136-T137.
- [ ] T140 [US9] Implement `IActionSettingsService`/`ActionSettingsService` in `src/Nabadat.ActionManagement/Application/Settings/{Interfaces/IActionSettingsService.cs,ActionSettingsService.cs}` (composes T139) and `GET`/`PUT /api/v1/settings/actions` in `Api/Controllers/ActionsSettingsController.cs` (depends on T139).
- [ ] T141 [US9] Build `ActionsSettingsPage` in `frontend/src/features/settings/pages/ActionsSettingsPage.tsx` (X/PAD fields, guard error surfacing) replacing T019's placeholder for `/settings/actions`, registered in the Settings landing page's section list (depends on T140).

### Integration Tests for User Story 9 🐳

- [ ] T142 [P] [US9] API tests for `GET`/`PUT /api/v1/settings/actions` in `tests/Nabadat.ActionManagement.IntegrationTests/Endpoints/ActionsSettingsEndpointTests.cs` — defaults, X-guard-blocked, PAD-out-of-range, non-P-01 403.

*(Scenario: `not-needed` per spec.md US9 — single-endpoint operations.)*

### E2E (Browser) Tests for User Story 9 🎭

- [ ] T143 [P] [US9] E2E tests in `tests/Nabadat.E2ETests/ActionManagement/ActionsSettingsTests.cs` (new file) covering the 5 required scenarios from spec.md US9. Author with the `e2e-testing` skill; update `COVERAGE.md`.

**Build gate**: unit + integration green; `npm run build` green; E2E filter `ActionsSettingsTests` green.

**Checkpoint**: US1-US9 independently functional.

---

## Phase 12: User Story 10 — Retro-date an Action for retrospective documentation (Priority: P3) — [Atia backend / Marawan frontend]

**Goal**: Document a past Action (create with past dates, or edit Start Date backward); Baseline pulled from M-06 history; ERR-5 blocks cleanly when no historical score exists.

**Independent Test**: Create with Start = 6 months ago, End = 5 months ago, Target Date = yesterday → born Completed, immediately read-only; retry with a Start Date M-06 has no history for → ERR-5 blocking dialog.

*(No new units — fully covered by US1's `BaselineCaptureService`/`ActionStatusCalculator`, T023/T024. No new integration coverage — covered by US1's `ActionsEndpointTests`, T041. No Red Checkpoint or new backend implementation task for this story.)*

### E2E (Browser) Tests for User Story 10 🎭

- [ ] T144 [P] [US10] E2E tests appended to `ActionAddEditTests.cs` (retro-dated flow) per spec.md US10's 3 required scenarios (born-Completed-read-only, historical-baseline-labelled-Baseline, ERR-5 blocking dialog). Update `COVERAGE.md`.

**Build gate**: `npm run build` green; E2E filter `ActionAddEditTests` (full file, all 3 stories' blocks) green.

**Checkpoint**: All 10 user stories independently functional — feature complete.

---

## Final Phase: Polish & Cross-Cutting Concerns

- [ ] T145 [P] Run `dotnet test Nabadat.sln` — full solution (all `Nabadat.ActionManagement.*` + previously-existing projects) green.
- [ ] T146 [P] Run `npm run build` in `frontend/` — typecheck + bundle green for the whole app, not just this feature.
- [ ] T147 Run the full Action Management E2E suite: `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ActionManagement"` — all files from T043/T070/T088/T104/T117/T128/T135/T143/T144 green.
- [ ] T148 [P] Automated axe-core accessibility scan of `/actions`, `/actions/new`, `/actions/:id` in both light/dark and LTR/RTL — zero serious/critical violations (NFR-4, SC-014).
- [ ] T149 [P] Bilingual parity pass: add native فصحى Arabic copy for every new Action Management string to `frontend/src/i18n/locales/`; run the CLAUDE.md self-review regex (`-\[#[0-9a-fA-F]{3,8}\]` and physical-direction-class scan) over `frontend/src/features/actions/`, `frontend/src/features/settings/pages/ActionsSettingsPage.tsx`, and `frontend/src/components/cx/{stepped-zone-slider,threshold-slider,timer-ring}/` — zero hits (SC-012).
- [ ] T150 [P] Performance check against NFR-5/SC-013 on a seeded tenant of 200 Actions: SCR-01 interactive < 2s, search/filter feedback < 100ms, slider drag at 60fps.
- [ ] T151 Walk every scenario in `quickstart.md` end-to-end against the completed feature; fix any drift found.
- [ ] T152 File the constitution amendment correcting M-15's owned-tables registry entry (Section 3) from the placeholder `action_plans`/`action_assignments`/`action_progress` to the real `actions`/`kpi_targets`/`action_settings` in `.specify/memory/constitution.md`, mirroring AMENDMENT-011/012; update `coordination-log.md` C-03 to `SHIPPED` once merged.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup — **blocks every user story**.
- **User Stories (Phase 3-12)**: all depend on Foundational. P1 stories (US1/US2/US3) should land before P2 (US4/US5/US6), which should land before P3 (US7/US8/US9/US10) — matching spec.md's own priority ordering — but stories are functionally independent once Foundational is done (see below).
- **Polish (Final Phase)**: depends on all 10 stories being complete.

### Cross-Story Dependencies (beyond the shared Foundational phase)

- **US2** reuses `ActionStatusCalculator` (US1, T031) and `IActionService.CreateAsync` output shape — build US1 first.
- **US3** reuses `ActionListQuery`'s Measurement calculators (US2, T053-T060) inside `ActionDetailProjection` — build US2 before US3 (both P1, sequential is natural).
- **US4** extends `ActionService` (US1, T033) with the edit path — build US1 first.
- **US5** wires into `ActionDetailProjection` (US3, T079) and `ActionListQuery` (US2, T061) — build US2+US3 first.
- **US6** extends the archive/unarchive endpoints already stood up by US3 (T081) — build US3 first.
- **US7** wires its cascade into US2's list endpoint (T062) and US3's detail endpoint (T080) — build US2+US3 first.
- **US8** extends US2's `ActionListQuery`/endpoint (T061/T062) — build US2 first.
- **US9** is fully independent (new controller, no dependency on Actions/Targets logic) — can start any time after Foundational.
- **US10** has no new backend work — build US1 first (reuses its services entirely).

### Within Each User Story

- Unit tests (parallel `[P]`) → Red Checkpoint (non-parallel, blocks implementation) → Implementation (respecting each task's own `(depends on ...)` note) → Integration/Scenario tests (parallel `[P]`, run at the per-story Docker-up checkpoint) → E2E tests (parallel `[P]`, after pages exist, no Red Checkpoint).

---

## Parallel Execution Examples

### Foundational (Phase 2) — after T007-T012 land, these can run together:

```text
Task: "Define IKpiScoreReader in src/Nabadat.ActionManagement/Domain/Interfaces/IKpiScoreReader.cs" (T013)
Task: "Implement NullKpiScoreReader in src/Nabadat.ActionManagement/Infrastructure/KpiIntegration/NullKpiScoreReader.cs" (T014)
Task: "Define IActionOverlayReader skeleton in src/Nabadat.ActionManagement/Domain/Interfaces/IActionOverlayReader.cs" (T015)
Task: "Create frontend API client in frontend/src/features/actions/api.ts" (T018)
Task: "Register placeholder routes in frontend/src/App.tsx" (T019)
Task: "Add Actions nav item in frontend/src/components/layout/app-sidebar.tsx" (T020)
```

### User Story 1 — Unit tests (all 5 in parallel, different files):

```text
Task: "Unit tests for ThresholdValidator in tests/Nabadat.ActionManagement.UnitTests/Actions/ThresholdValidatorTests.cs" (T021)
Task: "Unit tests for ThresholdAutoSyncCalculator in tests/Nabadat.ActionManagement.UnitTests/Actions/ThresholdAutoSyncCalculatorTests.cs" (T022)
Task: "Unit tests for BaselineCaptureService in tests/Nabadat.ActionManagement.UnitTests/Actions/BaselineCaptureServiceTests.cs" (T023)
Task: "Unit tests for ActionStatusCalculator in tests/Nabadat.ActionManagement.UnitTests/Measurement/ActionStatusCalculatorTests.cs" (T024)
Task: "Unit tests for KpiOptionsFilter in tests/Nabadat.ActionManagement.UnitTests/Actions/KpiOptionsFilterTests.cs" (T025)
```

### User Story 2 — Measurement calculators (all 8 in parallel, different files, after Red Checkpoint T052):

```text
Task: "Implement LowestPerformingTargetSelector" (T053)
Task: "Implement ScoreProgressCalculator" (T054)
Task: "Implement TimeProgressCalculator" (T055)
Task: "Implement TimerColourResolver" (T056)
Task: "Implement DisplayClamper" (T057)
Task: "Implement ActionCardStatusGrouper" (T058)
Task: "Implement ActionSearchFilter" (T059)
Task: "Implement ZeroEligibleFallback" (T060)
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 + 3 — all P1)

1. Complete Phase 1 (Setup) + Phase 2 (Foundational).
2. Complete Phase 3 (US1 — create), Phase 4 (US2 — monitor), Phase 5 (US3 — drill in), in that order (each builds on the previous's calculators).
3. **STOP and VALIDATE**: run `quickstart.md` Scenario 1 end-to-end.
4. This is the smallest deployable increment: CX Program Managers can create Actions, see them triaged on the All Actions page, and drill into any one for full detail.

### Incremental Delivery (matches spec.md priority order)

1. Setup + Foundational → foundation ready.
2. US1 → US2 → US3 → **MVP checkpoint**, demo-able.
3. US4 (edit) → US5 (auto transitions) → US6 (archive) → P2 checkpoint.
4. US7 (target lifecycle) → US8 (filters) → US9 (settings) → US10 (retro-date) → full v1 checkpoint.
5. Final Phase (polish) → ship.

### Two-Backend-Engineer Strategy (per plan.md / research.md §1)

With Foundational done together:

- **AbuKr** (write path): US1 → US4 → US7 → US9, in that order (each depends only on the previous write-path story or on Foundational).
- **Atia** (read/lifecycle path): US2 → US3 → US5 → US6 → US10, in that order (each depends only on the previous read-path story, Foundational, or — for US2's dependency on US1's `ActionStatusCalculator` — a quick sync point after US1's T031 lands).
- **Marawan** (frontend): builds the three custom-SVG primitives (T036 Threshold Slider during US1, T064-T065 Timer Ring + Stepped Zone Slider during US2) then each story's pages in the same P1→P2→P3 order, since every page's contract is fully specified in spec.md before the real endpoint exists (can build against contracts/api-endpoints.md + a mock).

---

## Notes

- `[P]` tasks touch different files with no unmet dependency.
- Every implementation task whose unit test doesn't yet exist references the Unit Tests subsection it must satisfy.
- Every backend story's Red Checkpoint is non-parallel and must be committed before its Implementation subsection starts.
- E2E files are shared across stories (`ActionAddEditTests.cs` spans US1/US4/US7/US10; `AllActionsTests.cs` spans US2/US6/US8; `ActionDetailsTests.cs` spans US3/US6/US7) — later stories append `[TestMethod]`s to an existing file rather than creating a new one.
- Commit after each task or logical group; verify unit tests fail before implementing (Red Checkpoint); stop at any Checkpoint to validate a story independently.
