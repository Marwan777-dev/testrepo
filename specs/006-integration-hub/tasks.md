# Tasks: M-13 Integration Hub

**Input**: Design documents from `/specs/006-integration-hub/`

**Prerequisites**: [plan.md](./plan.md) · [spec.md](./spec.md) · [research.md](./research.md) · [data-model.md](./data-model.md) · [contracts/](./contracts/) · [coordination-log.md](./coordination-log.md)

**Team**: AbuKr (backend, solo) · Marwan (frontend, solo) — research.md §1 mandates strictly-sequential backend ordering.

---

> ## ⚠️ Reconstruction notice — read before using task IDs
>
> **This file was regenerated on 2026-09-03 after the original `tasks.md` was lost.** It did not
> exist in `specs/006-integration-hub/` when `/speckit-tasks` ran, and the repo has no `.git`
> directory to recover it from. The original spanned **T001–T218** and is cross-referenced by
> `IMPLEMENTATION.md`, `TODO.md`, `coordination-log.md`, and past commit messages.
>
> **What is preserved verbatim** (IDs re-anchored from those surviving records, so every existing
> cross-reference still resolves):
> `T001` (module csproj) · `T003` (integration-test project) · `T004` (solution registration) ·
> `T006` (M-01 project reference) · `T009` (`IntegrationHub_Baseline.sql`) · `T011`
> (`ITenantDbContext`) · `T012` (`AddIntegrationHubModule`) · `T018`
> (`IntegrationHubApplicationFactory`) · `T020` (stub-port host wiring) · `T021` (sidebar nav) ·
> `T035` (first M-13 endpoint) · `T037` (`ServiceChannelForm.tsx`) · `T041`
> (`ServiceChannelsEndpointTests`) · `T042` (E2E `ServiceChannelTests`) · **`T043`–`T067`** (the
> entire US2 phase, pinned task-by-task by `IMPLEMENTATION.md`) · `T085` (SCR-02 wizard) · `T147`
> (FR-GBL-05 read-only rendering) · `T148` (FR-GBL-02 access-denied) · `T218` (final task).
>
> **What is a reconstruction**: every *other* ID. The surviving records do not pin them, so the
> descriptions here are derived from spec.md + plan.md + the code actually on disk. If an old
> commit message or note references an ID not in the list above, verify it against this file
> before trusting it.
>
> **Completion state (`[X]`) is derived from the working tree**, not from a task log — a file
> existing on disk is the evidence. Two consequences: (a) Red Checkpoints, which leave no artifact,
> are marked `[X]` only where `IMPLEMENTATION.md` records the run; (b) if a task was completed and
> later reverted, re-verify rather than trusting the checkbox.
>
> **Current state**: Phases 1–4 complete (Setup, Foundational, US1, US2 — backend + frontend + E2E,
> all green per `tests/Nabadat.E2ETests/COVERAGE.md`). Phase 5 (US3) has its **unit tests written**
> (`tests/Nabadat.IntegrationHub.UnitTests/Integrations/`, 827 lines across 6 files) but **no
> production code** — `src/Nabadat.IntegrationHub/Application/Integrations/` does not exist. US4–US10
> are not started.

---

**Tests**: Per CLAUDE.md "Unit Test Policy", unit tests are MANDATORY for every backend-bearing user
story here — spec.md declares no `unit-tests: skipped` for any of the ten. Each backend story emits
**Unit Tests (write FIRST, must FAIL)** → **Red Checkpoint** → **Implementation** → **Integration &
API / Scenario tests**. Page-bearing frontend stories add **E2E (Browser) Tests** after the page
tasks (no Red Checkpoint), then a **Click-through Parity** task run by hand. **US4 (Feature 0)
declares `e2e-tests: skipped`** — it is headless, with no browser flow.

**Organization**: Grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: `US1`…`US10` per spec.md
- Exact file paths in every description

## Path Conventions

- Backend module: `src/Nabadat.IntegrationHub/` (constitution AMENDMENT-009 / Article 1A layout)
- Unit tests: `tests/Nabadat.IntegrationHub.UnitTests/<SubDomain>/`
- Integration tests: `tests/Nabadat.IntegrationHub.IntegrationTests/{Infrastructure,Endpoints,Services,Scenarios}/`
- E2E: `tests/Nabadat.E2ETests/IntegrationHub/` (shared project — no new E2E project)
- Frontend: `frontend/src/features/integration-hub/{api.ts,components/,hooks/,pages/}`

## Frontend Task Rule

Before any UI task, read the repo-root `CLAUDE.md` end to end (design system, tokens, RTL logical
properties, D1–D5 Two-Palette Rule, DO / DO NOT) and follow the **Component Sourcing Rule** — search
`frontend/src/components/{ui,cx}/` and reuse before building. Per plan.md's Frontend Design Gate,
**this feature needs zero new custom-SVG components**; all eight screens build from existing shadcn
primitives. Implementation is **click-through-blind** (see the Click-through Parity subsections).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project skeletons and solution registration.

- [X] T001 Create the `src/Nabadat.IntegrationHub/` class library (`net10.0`) with `Nabadat.IntegrationHub.csproj`
- [X] T002 [P] Create `tests/Nabadat.IntegrationHub.UnitTests/` (xUnit v3, FluentAssertions 6.12.*, NSubstitute 5.*, `Microsoft.Extensions.TimeProvider.Testing` 9.*) per CLAUDE.md rule 14
- [X] T003 [P] Create `tests/Nabadat.IntegrationHub.IntegrationTests/` (xUnit v3, `Testcontainers.PostgreSql` 4.*, `Microsoft.AspNetCore.Mvc.Testing` 10.*) with a project reference to `Nabadat.IntegrationHub`
- [X] T004 Register all three projects in `Nabadat.TenantAdmin.sln`
- [X] T005 [P] Create `tests/Nabadat.E2ETests/IntegrationHub/` and add the `IntegrationHub/` section + ID block (`M13-E2E-*`) to `tests/Nabadat.E2ETests/COVERAGE.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Module wiring, schema, cross-module ports, and the frontend shell that every user story
depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 Add a `ProjectReference` to `src/Nabadat.SurveyBuilder/Nabadat.SurveyBuilder.csproj` from `Nabadat.IntegrationHub.csproj` — the real M-01 `ISurveyRenderService` dependency (research.md §4.2)
- [X] T007 [P] Create the value objects in `src/Nabadat.IntegrationHub/Domain/ValueObjects/`: `Scenario`, `CredentialMechanism`, `CredentialStatus`, `DataType` (13 members, closed list — never Duration/Identifier, `[PO-G17]`), `ParameterOrigin`, `ResultCode`, `ParameterWireValues`
- [X] T008 [P] Create the nine entities in `src/Nabadat.IntegrationHub/Domain/Entities/` per data-model.md §1–§8: `Integration`, `Credential`, `ServiceChannel`, `Parameter`, `ChannelParameterAssignment`, `ParameterMapping`, `UnmappedValueOccurrence`, `IntegrationRequestLog`, `EventLog` — one type per file
- [X] T009 Author `src/Nabadat.IntegrationHub/Infrastructure/Migrations/IntegrationHub_Baseline.sql` — all 8 owned tables + `event_log`, every CHECK/unique constraint enforcing its VR/BR, **DB-04 monthly partitioning** of `integration_request_logs` (prev 3 → next 12 months + `DEFAULT`), and the 23 seeded built-in parameters (FR-F0-10). **No EF migrations** (DB-08). Idempotent on re-run
- [X] T010 [P] Create one `IEntityTypeConfiguration<T>` per entity plus the enum converters in `src/Nabadat.IntegrationHub/Infrastructure/Persistence/Configurations/` — explicit `HasColumnName`, intra-module FKs only (Article 4.1: `Parameter.api_field` is an identifier reference to M-10, never a cross-module FK)
- [X] T011 Create `ITenantDbContext` in `src/Nabadat.IntegrationHub/Application/Interfaces/` (DbSets + `SaveChangesAsync` + `ExecuteAsync`) and the concrete `TenantDbContext` in `Infrastructure/Persistence/`
- [X] T012 Create `src/Nabadat.IntegrationHub/IntegrationHubServiceCollectionExtensions.cs` — `AddIntegrationHubModule(IConfiguration)` composition root
- [X] T013 Host wiring: add the M-13 `ProjectReference` to `src/Nabadat.TenantAdmin/Nabadat.TenantAdmin.csproj` and call `builder.Services.AddIntegrationHubModule(builder.Configuration)` in `src/Nabadat.TenantAdmin/Program.cs` — without this every M-13 controller 404s (TODO-M13-002)
- [X] T014 Extend `src/Nabadat.TenantAdmin/Development/DevTenantSchemaBootstrapper.cs` to read and apply `IntegrationHub_Baseline.sql` per tenant schema, gated on a `service_channels` sentinel; copy the baseline to the output `Migrations/` folder from the module csproj
- [X] T015 [P] Publish `IParameterCatalogReader` + `ParameterCatalogEntry` in `src/Nabadat.IntegrationHub/Domain/Interfaces/` — the forward contract for M-14/15/16 (contracts/published-interfaces.md)
- [X] T016 [P] Declare the three M-13-owned consumed ports in `Domain/Interfaces/`: `ISurveyResolutionReader`, `ISurveyDispatchGateway` (M-02, research.md §4.3), `IResponseIngestionGateway` (M-04, §4.4)
- [X] T017 [P] Create the `Null*` default adapters in `src/Nabadat.IntegrationHub/Infrastructure/ChannelDispatch/` plus the `Recorded*` test doubles, so every story is buildable standalone with a zero-code-change swap path when M-02/M-04 ship
- [X] T018 Create `tests/Nabadat.IntegrationHub.IntegrationTests/Infrastructure/IntegrationHubApplicationFactory.cs` — `WebApplicationFactory<Program>` + `IAsyncLifetime`, boots Testcontainers Postgres, applies `IntegrationHub_Baseline.sql`, exposes a per-test `HttpClient` + seeding helpers
- [X] T019 [P] Add the integration-test support types: `Infrastructure/JsonHttp.cs`, `Infrastructure/SeededUser.cs`, `Infrastructure/IntegrationHubIntegrationCollection.cs`
- [X] T020 Register the stub ports and the module's services in DI (`IntegrationHubServiceCollectionExtensions`), including `IExternalParameterReferenceReader` → `Infrastructure/CrossModule/NullExternalParameterReferenceReader` (TODO-M13-005)
- [X] T021 Frontend: register the Integration Hub navigation in `frontend/src/components/AppLayout.tsx` — **two adjacent `SidebarGroup`s** (`nav.integrationHub` → Integrations, Request logs; `nav.integrationHubDataModel` → Service channels, Parameters, Parameter mappings) per FR-GBL Navigation, with per-persona `ROLE_NAV_KEYS` entries. Request logs is P-07-only (no P-01 grant at all, BR-24)
- [X] T022 [P] Frontend: scaffold `frontend/src/features/integration-hub/{components,hooks,pages}/` and `http.ts` (auth header + API-05 error envelope parsing, reusing the `callJson` pattern)
- [X] T023 [P] Frontend: create `api.ts`, `dto.ts`, `integration-hub-api-error.ts`, `mapping-import-error.ts`
- [X] T024 Frontend: register the eight routes in `frontend/src/App.tsx` and add `pages/ScreenPlaceholderPages.tsx` + `components/ScreenPlaceholder.tsx` so every route is reachable from day one
- [X] T025 [P] Frontend: add the `integrationHub` i18n namespace to `frontend/src/i18n/locales/{en,ar}.json` — Arabic written natively in فصحى, never translated
- [X] T026 [P] Frontend: `hooks/useIntegrationHubAccess.ts` + `components/AccessDenied.tsx` — the shared permission/access-denied primitive every screen uses (FR-GBL-02/05)
- [X] T027 [P] Register `TimeProvider` in the module's DI and wire `FakeTimeProvider` into the unit-test support so no production code reads `DateTime.UtcNow` (Unit Test Policy rule 8)
- [X] T028 [P] Extend `tests/Nabadat.E2ETests/Infrastructure/E2ETenantDb.cs` with the M-13 seeding/teardown helpers the browser lane needs (service channels, parameters, contract rows)

**Checkpoint**: Foundation ready — user story implementation can begin.

---

## Phase 3: User Story 1 — Define a Service Channel and its Parameter Contract (Priority: P1) 🎯 MVP

**Goal**: A CX Manager creates a service channel with bilingual names, a live-sanitised channel ID,
and a supported/required parameter contract — the foundational configuration every other story
needs.

**Independent Test**: `/integration-hub/service-channels` → **New service channel** → EN/AR names +
channel ID "My kiosk #1" (sanitises live to "Mykiosk1", capped at 19 chars) → toggle **Supported**
on several built-ins, tick **Required** on a subset → **Create channel** → the row appears in SCR-03
with correct counts and an Active badge.

### Unit Tests for User Story 1 (write FIRST, must FAIL) ⚠️

- [X] T029 [P] [US1] Unit tests for `ChannelIdSanitizer` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ChannelIdSanitizerTests.cs` — `Sanitize("My kiosk #1")` → `"Mykiosk1"`; 20 valid chars → truncated to 19 (VR-F04)
- [X] T030 [P] [US1] Unit tests for `ChannelIdUniquenessValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ChannelIdUniquenessValidatorTests.cs` — case-insensitive per tenant: `existingIds=["KIOSK-01"], id="kiosk-01"` → `Invalid("A channel with this ID already exists")`
- [X] T031 [P] [US1] Unit tests for `ChannelIdLockGuard` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ChannelIdLockGuardTests.cs` — `hasLoggedSuccessfulRequest=true` → locked, server-side rejection of a stale client PUT; `false` → editable (BR-05)
- [X] T032 [P] [US1] Unit tests for `ParameterContractDependencyRule` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ParameterContractDependencyRuleTests.cs` — `(supported=false, required=true)` → `(false, false)`; Required settable only while Supported (FR-S4-04)
- [X] T033 [P] [US1] Unit tests for `ChannelNameValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ChannelNameValidatorTests.cs` — EN required ≤ 50 + unique (VR-F02); AR required (VR-F03)

### Red Checkpoint for User Story 1 🔴

- [X] T033R [US1] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. Valid red: compile error (no `Application/Channels/` types yet). Paste the transcript and commit the red baseline before any implementation task. Non-parallel

### Implementation for User Story 1

- [X] T034 [US1] Create the channel DTOs and the five rule types in `src/Nabadat.IntegrationHub/Application/Channels/` — `ChannelIdSanitizer`, `ChannelIdUniquenessValidator`, `ChannelIdLockGuard`, `ParameterContractDependencyRule`, `ChannelNameValidator`, plus `Dtos/` (`ServiceChannelCreateCommand`, `ServiceChannelUpdateCommand`, `ServiceChannelDto`, `ChannelContractRowDto`, `ChannelParameterAssignmentInput`, `ServiceChannelPage`, `ServiceChannelSaveResult`) and `ChannelValidationResult`/`ChannelValidationError`/`ChannelErrorCodes`
- [X] T035 [US1] Implement `IServiceChannelService`/`ServiceChannelService` (`Application/Channels/` + `Interfaces/`) and `ServiceChannelsController` + `Api/Contracts/` — `GET`/`POST` `/api/v1/integration-hub/service-channels`, `PUT .../{id}`. **The first M-13 endpoint**; multi-write atomicity (channel + contract rows) via `ITenantDbContext.ExecuteAsync`
- [X] T036 [US1] Frontend: `hooks/useServiceChannels.ts` + the channel calls in `api.ts`
- [X] T037 [US1] Frontend: `components/ServiceChannelForm.tsx` — SCR-04 create/edit (`/integration-hub/service-channels/new` and `/:id`): EN/AR names, live-sanitised channel ID with the BR-05 lock explanation, description, Active toggle (default On), live contract-summary alert (FR-S4-03), and the parameter-contract table with live filter + the Supported → Required dependency (FR-S4-04). Shipped copy per spec.md's SCR-04 field-details block
- [X] T038 [US1] Frontend: `pages/AllServiceChannelsPage.tsx` — SCR-03 list (FR-S3-01): name+description · monospace channel-ID chip · status · supported/required/integrations counts · Edit row action. **No delete affordance anywhere** (BR-07/FR-S3-02). Skeleton/empty/error/access-denied states (FR-GBL-02), footer guidance note
- [X] T039 [US1] Frontend: FR-GBL-03 unsaved-changes guard on SCR-04 — an `AlertDialog` (`data-testid="channel-unsaved-dialog"`) mirroring `KpiConfigPage`'s pattern; derive dirtiness by digesting the whole field set against a baseline captured on open, not per-`onChange` flags (see TODO-M13-006)
- [X] T040 [US1] Enforce VR-F13's 100-channel tenant ceiling on the create path (`validation.capacity_exceeded`, copy "You've reached the limit of ‹n› ‹entity› for this tenant.") and emit the `channel.created` / `channel.updated` / `channel.id_changed` audit events

### Integration & API Tests for User Story 1 🐳

- [X] T041 [US1] API tests in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/ServiceChannelsEndpointTests.cs` — create → 201 + `channel.created`; duplicate ID case-insensitive → 409; edit ID pre-lock → 200 (path changes) and post-first-2xx → 409; `active=false` → 200; `GET` list reflects supported/required/integration counts

> **Scenario test**: spec.md declares `scenario-test: not-needed` for US1 — the deactivation-cascade
> sequence is asserted end-to-end by US4's `InboundRequestLifecycleScenarioTests` instead.

### E2E (Browser) Tests for User Story 1 🎭

- [X] T042 [US1] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/ServiceChannelTests.cs` (M13-E2E-01…06) covering `ServiceChannel_sanitizes_id_live_as_typed_and_caps_at_19_chars`, `…_locks_id_field_after_first_successful_request`, `…_required_toggle_disables_when_supported_is_off`, `…_blocks_save_on_duplicate_name_or_id`, `…_list_shows_no_delete_action_anywhere`, `…_it_admin_sees_read_only_view`. Inherit `E2ETestBase`; tear down every seeded/created channel in `[TestCleanup]` (VR-F13 ceiling, TODO-M13-003); update `COVERAGE.md`

**Build gate**: `dotnet test tests/Nabadat.IntegrationHub.UnitTests` green · `dotnet test tests/Nabadat.IntegrationHub.IntegrationTests` green (Docker up) · `npm run build` green from `frontend/` · `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ServiceChannelTests"` green (stack up + `E2E_BASE_URL`).

**Checkpoint**: US1 fully functional and independently testable. ✅ **Reached** (COVERAGE.md: M13-E2E-01…06 passing)

### Click-through Parity for User Story 1 🎨

> **Owner: the frontend developer, run manually.** Preconditions: (1) SCR-03/04 were built
> click-through-blind — a ported page makes the run VOID and is reported `NOT AUDITED`, not clean;
> (2) the click-through checkout is served and the product dev stack is up and signed in (paths in
> `.claude/skills/clickthrough-parity/reference.json`).

- [ ] T042P [US1] Run `/clickthrough-parity 006-integration-hub phase 3` over `/integration-hub/service-channels`, `…/new`, `…/:id` and triage the report — the click-through is the source of truth. Apply accepted defects with `--fix`; take every **Needs discussion** item (presence / placement / a deliberate business divergence, e.g. the FR-GBL-03 guard the click-through lacks) to the design owner instead. Record the result in `.claude/skills/clickthrough-parity/route-map.md`

---

## Phase 4: User Story 2 — Manage the Parameter Catalogue (Priority: P1)

**Goal**: A CX Manager governs the 23 pre-seeded built-in parameters plus tenant-specific custom
ones — bilingual names, a lock-on-first-use `snake_case` API field, one of 13 data types (Range
sub-config, List → mappings), a validation rule, five usage flags, and channel assignments.

**Independent Test**: `/integration-hub/parameters` → "All · 23" tab shows every built-in enabled →
**New parameter** → EN "Wait Time" auto-suggests `wait_time` → type **Range** with min/max/unit →
flags → assign to a channel → **Create parameter** → the Custom row appears with its flags as
check/dash glyphs.

### Unit Tests for User Story 2 (write FIRST, must FAIL) ⚠️

- [X] T043 [P] [US2] Unit tests for `ApiFieldNameSuggester` in `tests/Nabadat.IntegrationHub.UnitTests/Parameters/ApiFieldNameSuggesterTests.cs` — `Suggest("Wait Time")` → `"wait_time"`; `"Été & Café!"` → invalid chars stripped, no transliteration; every output satisfies the DB CHECK (no leading digit, no doubled underscore)
- [X] T044 [P] [US2] Unit tests for `ApiFieldNameUniquenessValidator` in `…/Parameters/ApiFieldNameUniquenessValidatorTests.cs` — unique per tenant across built-in + custom + enabled + **disabled** (VR-F06); the validator receives the whole field list so it structurally cannot filter on `enabled`
- [X] T045 [P] [US2] Unit tests for `ApiFieldNameLockGuard` in `…/Parameters/ApiFieldNameLockGuardTests.cs` — locked once the first request carrying it arrived (BR-11); built-ins always locked
- [X] T046 [P] [US2] Unit tests for `RangeConfigValidator` in `…/Parameters/RangeConfigValidatorTests.cs` — Min/Max required, Min < Max: `Validate(min=100, max=50)` → `Invalid("Minimum must be less than Maximum")` (VR-F07)
- [X] T047 [P] [US2] Unit tests for `ParameterDisableImpactScanner` in `…/Parameters/ParameterDisableImpactScannerTests.cs` — returns **all** references across channel contracts, M-10 scope filters, and rules for Dialog D-6 (BR-10); empty list → disable proceeds with no dialog
- [X] T048 [P] [US2] Unit tests for `BuiltInParameterGuard` in `…/Parameters/BuiltInParameterGuardTests.cs` — `Guard(builtIn=true, action=Delete)` → throws; `Disable` allowed; rename/retype rejected (BR-09, `[PO-G27]`), plus a field-set guard on the `ParameterAction` enum

### Red Checkpoint for User Story 2 🔴

- [X] T049 [US2] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests` — valid red (compile error, no `Application/Parameters/` types). Transcript pasted; red baseline committed (`3bd0227`). Non-parallel

### Implementation for User Story 2

- [X] T050 [US2] Create the parameter DTOs in `src/Nabadat.IntegrationHub/Application/Parameters/Dtos/` — `ParameterCreateCommand`, `ParameterPatchCommand`, `ParameterDto`, `ParameterListFilter`, `ParameterPage`, `ParameterOriginCounts`, `ParameterSaveResult`
- [X] T051 [US2] Implement `ApiFieldNameSuggester` in `Application/Parameters/ApiFieldNameSuggester.cs`
- [X] T052 [US2] Implement `ApiFieldNameUniquenessValidator` in `Application/Parameters/ApiFieldNameUniquenessValidator.cs`
- [X] T053 [US2] Implement `ApiFieldNameLockGuard` in `Application/Parameters/ApiFieldNameLockGuard.cs`
- [X] T054 [US2] Implement `RangeConfigValidator` in `Application/Parameters/RangeConfigValidator.cs`
- [X] T055 [US2] Implement `ParameterDisableImpactScanner` in `Application/Parameters/ParameterDisableImpactScanner.cs` — stamps and orders all three reference kinds via `IExternalParameterReferenceReader` (external two-thirds stubbed, TODO-M13-005)
- [X] T056 [US2] Implement `BuiltInParameterGuard` + `ParameterAction` + `BuiltInParameterViolationException` in `Application/Parameters/`
- [X] T057 [US2] Implement `IParameterService`/`ParameterService` — list with AND-combined origin/type/search filters + global tab counts, create, patch (incl. `ScanReferencesAsync` for the D-6 withhold), `MappingSupportPolicy` per BR-27
- [X] T058 [US2] Implement `ParametersController` + `Api/Contracts/` — `GET`/`POST /api/v1/integration-hub/parameters`, `PATCH .../{id}`; **no DELETE route exists** (BR-09)
- [X] T059 [US2] Implement the real M-10 data-scope integration — `Application/Parameters/DataScopeContractPublisher.cs` + `Infrastructure/UserManagementIntegration/DataScopeHttpClient.cs` calling `POST /api/v1/authorization/scope/parameters` (research.md §4.1, CMC-06)
- [X] T060 [US2] Wire the parameter services, the data-scope client, and `IExternalParameterReferenceReader` into `IntegrationHubServiceCollectionExtensions`
- [X] T061 [US2] Frontend: `components/ParameterDrawer.tsx` — SCR-06 drawer over SCR-05 (✕ / scrim / Esc all funnel through one `requestClose()`): bilingual names (max 50), API-field auto-suggest with the BR-11 lock helper, the **13-type** select (read-only for built-ins), conditional Range card / List panel, validation rule, the five usage flags with ratified defaults (BR-27-driven Mapping support), channel-assignment pills, the `FlagGlyph` check/dash renderer, **and the FR-GBL-03 unsaved-changes guard** (derived `digest(form) !== baseline`, baseline re-captured on open and after save)
- [X] T062 [US2] Frontend: `hooks/useParameters.ts` + the parameter calls in `api.ts`
- [X] T063 [US2] Frontend: `pages/AllParametersPage.tsx` — SCR-05 list (FR-S5-01/02): `TabsListSegmented` origin tabs with global `TabsCountPill` counts, name/API-field search, type filter (all AND-combined), and the parameters table with icon-only ghost row actions
- [X] T064 [US2] Frontend: SCR-05 inline enable/disable toggle (FR-S5-03) guarded by **Dialog D-6** listing every reference before anything changes, audited
- [X] T065 [US2] API tests in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/ParametersEndpointTests.cs` 🐳 — create custom Range → 201 + `parameter.created`; duplicate API field incl. against a disabled parameter → 409; `PATCH {enabled:false}` unreferenced → 200; referenced → withheld + reference list; `?origin=custom&type=range` AND filter; any delete-shaped call → 404/405
- [X] T066 [US2] Integration test for the real M-10 call in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/DataScopeContractPublisherTests.cs` 🐳 — the published contract reaches `POST /api/v1/authorization/scope/parameters` end-to-end

> **Scenario test**: spec.md declares `scenario-test: not-needed` for US2.

### E2E (Browser) Tests for User Story 2 🎭

- [X] T067 [US2] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/ParameterCatalogueTests.cs` (M13-E2E-07…13) covering `Parameters_type_switch_between_range_and_list_shows_correct_panel`, `…_api_field_auto_suggests_from_english_name`, `…_blocks_save_on_duplicate_api_field_including_disabled`, `…_origin_and_type_filters_combine_with_AND`, `…_disable_shows_impact_warning_when_referenced`, `…_builtin_row_has_no_delete_action_and_locked_api_field`, `…_range_validation_blocks_min_greater_than_max`. Full teardown of everything seeded; update `COVERAGE.md`

**Build gate**: unit + integration projects green · `npm run build` green · `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ParameterCatalogueTests"` green.

**Checkpoint**: US1 AND US2 both work independently. ✅ **Reached** (COVERAGE.md: M13-E2E-07…13 passing)

### Click-through Parity for User Story 2 🎨

> Owner: the frontend developer, run manually. Same two preconditions as T042P.

- [ ] T067P [US2] Run `/clickthrough-parity 006-integration-hub phase 4` over `/integration-hub/parameters` (SCR-05 + the SCR-06 drawer) and triage the report. Apply accepted defects with `--fix`; escalate every **Needs discussion** item to the design owner. Record the result in `route-map.md`

---

## Phase 5: User Story 3 — Onboard an Integration via the New/Edit Wizard (Priority: P1)

**Goal**: A Tenant IT Administrator provisions a caller's endpoint through a 3-step wizard — name +
active channel + exactly one of five scenarios; API-key **or** OAuth client-credentials with
show-once secrets; then a review of the endpoint, the channel's accepted-parameters contract, and
the result-code catalogue.

**Independent Test**: `/integration-hub/integrations` → **New integration** → Step 1: name "Core
Services Bus — Survey Dispatch", the US1 channel, the **Dispatch via Nabadat** card → Step 2: API
key + label + **Generate new API key** → Dialog D-1 shows the plaintext once → Step 3: endpoint
preview with the highlighted channel-ID token + accepted-parameters table matching US1's contract →
**Create integration** → the SCR-01 row shows zero traffic and "—" error rate.

**Status**: unit tests exist on disk; **no production code** — `Application/Integrations/` is absent.

### Unit Tests for User Story 3 (write FIRST, must FAIL) ⚠️

- [X] T068 [P] [US3] Unit tests for `IntegrationNameValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/IntegrationNameValidatorTests.cs` — required, **case-insensitively** unique per tenant, ≤ 100 chars (VR-F01)
- [X] T069 [P] [US3] Unit tests for `ScenarioSelectionRule` in `…/Integrations/ScenarioSelectionRuleTests.cs` — exactly one of SCN-01…05; a second scenario needs a second integration (BR-02)
- [X] T070 [P] [US3] Unit tests for `ApiKeyGenerationService` in `…/Integrations/ApiKeyGenerationServiceTests.cs` — plaintext returned exactly once, stored value ≠ plaintext, later reads return only the masked form; generating while `K1` is active implicitly revokes `K1` (BR-16)
- [X] T071 [P] [US3] Unit tests for `OAuthClientGenerationService` in `…/Integrations/OAuthClientGenerationServiceTests.cs` — grant type always `client_credentials`, token TTL always 15 minutes, neither configurable via input (BR-17); selected scopes applied
- [X] T072 [P] [US3] Unit tests for `CredentialRevocationService` in `…/Integrations/CredentialRevocationServiceTests.cs` — immediate revocation; subsequent auth check → `Invalid` → `401 E-1401`; no un-revoke operation exists
- [X] T073 [P] [US3] Unit tests for `WizardDraftDiscardPolicy` in `…/Integrations/WizardDraftDiscardPolicyTests.cs` — a credential generated mid-wizard is never persisted/usable when the wizard is cancelled (BR-25)

### Red Checkpoint for User Story 3 🔴

- [ ] T074 [US3] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. The six test files above exist but `src/Nabadat.IntegrationHub/Application/Integrations/` does not, so the expected red state is a **compile error**. Re-confirm and paste the transcript, then commit the red baseline before T075. Non-parallel

### Implementation for User Story 3

- [ ] T075 [US3] Create `src/Nabadat.IntegrationHub/Application/Integrations/Dtos/` — `IntegrationCreateCommand`, `IntegrationUpdateCommand`, `IntegrationDto`, `IntegrationPage`, `CredentialGenerationResult` (carries the show-once plaintext), `CredentialDto`, plus `IntegrationValidationResult`/`IntegrationErrorCodes`
- [ ] T076 [US3] Implement `IntegrationNameValidator` in `Application/Integrations/IntegrationNameValidator.cs` (VR-F01)
- [ ] T077 [US3] Implement `ScenarioSelectionRule` in `Application/Integrations/ScenarioSelectionRule.cs` — a single `Scenario` field, never a multi-select (BR-02)
- [ ] T078 [US3] Implement `ApiKeyGenerationService` in `Application/Integrations/ApiKeyGenerationService.cs` — hash/encrypt at rest (NFR-6), show-once, implicit revocation of the prior active key (BR-16); takes the injected `TimeProvider`
- [ ] T079 [US3] Implement `OAuthClientGenerationService` in `Application/Integrations/OAuthClientGenerationService.cs` — `client_id`/`client_secret` hashed at rest, fixed `client_credentials` grant + 15-minute TTL in code, scopes from BR-26's five values
- [ ] T080 [US3] Implement `CredentialRevocationService` in `Application/Integrations/CredentialRevocationService.cs` — one-way Active → Revoked, no un-revoke API surface
- [ ] T081 [US3] Implement `WizardDraftDiscardPolicy` in `Application/Integrations/WizardDraftDiscardPolicy.cs` (BR-25)
- [ ] T082 [US3] Implement `IIntegrationService`/`IntegrationService` in `Application/Integrations/` (+ `Interfaces/`) — create integration + credential **in one `ITenantDbContext.ExecuteAsync` transaction**; revoke-old + generate-new atomically (BR-16); VR-F13's 200-integration ceiling; server-side rejection of a deactivated channel (defense in depth, FR-S2-02)
- [ ] T083 [US3] Implement `IntegrationsController` + `Api/Contracts/` — `GET`/`POST /api/v1/integration-hub/integrations`, `PUT .../{id}`, `POST .../{id}/credentials`, `POST .../{id}/credentials/revoke` per contracts/api-endpoints.md; emit `integration.created`, `integration.updated`, `credential.generated`, `credential.revoked` audit events
- [ ] T084 [US3] Frontend: `hooks/useIntegrations.ts` + the integration/credential calls in `api.ts`
- [ ] T085 [US3] Frontend: `components/IntegrationWizard.tsx` + `pages/IntegrationWizardPage.tsx` — the SCR-02 3-step shell at `/integration-hub/integrations/new` and `…/:id` (FR-S2-01): **`WizardStepper` from `@/components/ui/wizard-stepper`** (never a hand-rolled circle row), Back/Continue/Create footer (`justify-between`, Back `variant="outline"`, one filled primary), cancel-discard, state reset on re-entry, edit-mode pre-fill, **and the FR-GBL-03 unsaved-changes guard** using the derived-digest shape twice proven on SCR-04/SCR-06 (closes the last third of TODO-M13-006). Validation-gated: pass no `onClick` for unreached steps
- [ ] T086 [US3] Frontend: Step 1 (FR-S2-02/03) — `name` (VR-F01), `serviceChannel` select defaulting to the first **active** channel and rendering "Name — CHANNEL-ID", `description`, and the five scenario **choice cards** (`role="radiogroup"` / `role="radio"`, `size-9` icon tile + radio dot + semibold label + 12px description, `sm:grid-cols-2 xl:grid-cols-5`), no default selection in create mode. Shipped copy per spec.md's Step-1 block
- [ ] T087 [US3] Frontend: Step 2 (FR-S2-04/05/06) — mechanism radio switching the visible config; **API key**: `keyLabel` + read-only masked `currentKey` with **Revoke**; **OAuth**: `clientName`, read-only `tokenEndpoint`, scope chips (default `survey-requests:write`). Dialogs **D-1** (API key generated), **D-2** (client credentials), **D-3** (revoke confirmation naming the masked key and the consequence). **The field sets must NOT contain** expiry, sandbox, allowed-source-IPs, grant-type, or token-lifetime fields (`[PO-G13]`, BR-17)
- [ ] T088 [US3] Frontend: Step 3 (FR-S2-07/08/09) — endpoint preview re-rendering on scenario/channel change with the highlighted channel-ID token and a **Copy** button that flips to "Copied ✓"; the accepted-parameters table re-rendered from the selected channel's contract; the result-codes card rendering the FR-F0-03 catalogue. Code sample in a fixed dark block (`bg-nb-navy-800 dark:bg-nb-dark`, mono, `dir="ltr"`)
- [ ] T089 [US3] Frontend: FR-S2-10 conditional security configuration — the SCN-04 **Allowed origins** list and the SCN-02 **Link expiry** override (default 24h), shown only after the matching scenario is selected
- [ ] T090 [US3] Frontend: `pages/AllIntegrationsPage.tsx` — the SCR-01 table (FR-S1-03/04): integration name + credential-kind/created-date sub-line · monospace channel chip · scenario badge · auth badge · status · Requests·24h · error-rate badge or "—" · last activity · row actions (View logs, Edit). Header **New integration** and **View request logs**. *(Stat tiles and filters land in US5.)*
- [ ] T091 [US3] API tests in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/IntegrationsEndpointTests.cs` 🐳 — create with API key → 201 + `integration.created` + `credential.generated`; create with OAuth + scopes → 201; revoke → 200 + `credential.revoked`, old key → `401 E-1401`; generate while active → old implicitly revoked; duplicate name → 409; deactivated channel → 400/409; `PUT` channel change → endpoint path updates
- [ ] T092 [US3] Scenario test `tests/Nabadat.IntegrationHub.IntegrationTests/Scenarios/IntegrationOnboardingScenarioTests.cs` 🐳 — `POST /integrations` → `GET /integrations/{id}` → a live call to the provisioned endpoint (`202 ACCEPTED`) → `POST .../credentials/revoke` → repeat call (`401 E-1401`). Asserts exactly one `integration.created`, one `credential.generated`, one `credential.revoked`, in order

### E2E (Browser) Tests for User Story 3 🎭

- [ ] T093 [US3] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/IntegrationWizardTests.cs` — `Wizard_switches_auth_fields_when_mechanism_changes`, `Wizard_api_key_dialog_never_shows_plaintext_again_after_done`, `Wizard_endpoint_and_contract_preview_update_when_channel_changes`, `Wizard_only_offers_one_scenario_selection_at_a_time`, `Wizard_channel_select_excludes_inactive_channels`, `Wizard_cancel_discards_generated_credential_and_draft`, `Wizard_blocks_step_advance_on_missing_required_field`, `Integrations_new_row_shows_zero_traffic_and_dash_error_rate`. Full teardown (VR-F13's 200-integration ceiling); update `COVERAGE.md`

**Build gate**: unit + integration projects green · `npm run build` green · `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~IntegrationWizardTests"` green.

**Checkpoint**: US1–US3 all work independently.

### Click-through Parity for User Story 3 🎨

> Owner: the frontend developer, run manually. Same two preconditions as T042P.

- [ ] T093P [US3] Run `/clickthrough-parity 006-integration-hub phase 5` over `/integration-hub/integrations`, `…/new`, `…/:id` and triage the report. Apply accepted defects with `--fix`; escalate **Needs discussion** items. Record the result in `route-map.md`

---

## Phase 6: User Story 4 — Process Inbound API Requests (Priority: P1)

**Goal**: The headless runtime behind every provisioned endpoint — an ordered, atomic 8-step
validation pipeline, the normative result-code catalogue, and the five scenario hand-offs.

**Independent Test**: With US3's SCN-01 integration, `POST` a valid request with all channel-required
parameters and a valid API key → `202 ACCEPTED` + `request_id`, visible in SCR-08 within 60s. Then
omit a required parameter → `400 E-1002` naming the missing field, nothing forwarded to M-02.

> **E2E**: spec.md declares `e2e-tests: skipped — Feature 0 is an explicitly headless system feature
> with no admin-console screen.` Its human-visible surface is covered by US5's E2E suite. **No E2E
> subsection and no Click-through Parity subsection for this phase.**

### Unit Tests for User Story 4 (write FIRST, must FAIL) ⚠️

- [ ] T094 [P] [US4] Unit tests for `RequestValidationPipeline` in `tests/Nabadat.IntegrationHub.UnitTests/Requests/RequestValidationPipelineTests.cs` — short-circuits at the **first** failing step in the normative order; `authInvalid=true, payloadTooLarge=true` → `401 E-1401` (not `413`); nothing forwarded downstream on any failure
- [ ] T095 [P] [US4] Unit tests for `ResultCodeMapper` in `…/Requests/ResultCodeMapperTests.cs` — each outcome → its code **and its exact message copy pattern** (FR-F0-03), e.g. `E-1002` → `"Required parameter 'mobile' is missing for service channel E-SERVICES-PORTAL."`
- [ ] T096 [P] [US4] Unit tests for `ChannelContractRequiredFieldChecker` in `…/Requests/ChannelContractRequiredFieldCheckerTests.cs` — the **channel contract** is authoritative on requiredness, not the parameter-level default (BR-08)
- [ ] T097 [P] [US4] Unit tests for `ParameterTypeValidator` + the 13 per-type validators in `…/Requests/ParameterTypeValidatorTests.cs` — VR-T01…T13 boundary matrices: `Phone("07701")` → `Invalid`; `Phone("+962770123456")` → `Valid`; `Range(150, min=0, max=100)` → `Invalid` (inclusive); `List("anything-unmapped")` → `Valid` (membership not enforced, VR-T06/BR-12); Geolocation lat/long bounds; Percentage default 0–100
- [ ] T098 [P] [US4] Unit tests for `UnregisteredParameterStore` in `…/Requests/UnregisteredParameterStoreTests.cs` — unknown keys stored raw, flagged "unregistered", excluded from reports/dashboards/filters/rule builders (BR-14, AC-F0-03)
- [ ] T099 [P] [US4] Unit tests for `IdempotencyKeyResolver` in `…/Requests/IdempotencyKeyResolverTests.cs` — keys on `(tenant, channelId, transaction_id)`; a repeat writes a new log entry without re-triggering downstream side effects (BR-18/F0.7)
- [ ] T100 [P] [US4] Unit tests for `AllowedOriginsWhitelistStore` in `…/Requests/AllowedOriginsWhitelistStoreTests.cs` — `Resolve(origin="https://evil.example", whitelist=["https://trusted.example"])` → refused (FR-S2-10, SCN-04)
- [ ] T101 [P] [US4] Unit tests for `SurveyLinkExpiryCalculator` in `…/Requests/SurveyLinkExpiryCalculatorTests.cs` — `ComputeExpiry(issuedAt=T, override=null)` → `T + 24h` (F0.8), override honoured; uses `FakeTimeProvider`

### Red Checkpoint for User Story 4 🔴

- [ ] T102 [US4] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`, verify red (compile error — no `Application/Requests/` types), paste the transcript, commit the red baseline. Non-parallel

### Implementation for User Story 4

- [ ] T103 [US4] Implement `ResultCodeMapper` in `src/Nabadat.IntegrationHub/Application/Requests/ResultCodeMapper.cs` — the full FR-F0-03 catalogue with exact developer-oriented English message copy (no localisation)
- [ ] T104 [US4] Implement `ParameterTypeValidator` + the 13 per-type validators in `Application/Requests/` — the type list is **closed**; Duration and Identifier must not exist anywhere (`[PO-G17]`)
- [ ] T105 [US4] Implement `ChannelContractRequiredFieldChecker` in `Application/Requests/ChannelContractRequiredFieldChecker.cs` (BR-08)
- [ ] T106 [US4] Implement `UnregisteredParameterStore` in `Application/Requests/UnregisteredParameterStore.cs` (BR-14/FR-F0-06)
- [ ] T107 [US4] Implement `IdempotencyKeyResolver`, `AllowedOriginsWhitelistStore`, and `SurveyLinkExpiryCalculator` in `Application/Requests/` — no bounded idempotency retention index (BR-18, plan.md Complexity Tracking)
- [ ] T108 [US4] Implement `RequestValidationPipeline` in `Application/Requests/RequestValidationPipeline.cs` — the ordered 8-step pipeline (TLS → auth → rate limit → payload size → channel resolution → channel-active → required params → type/validation), atomic short-circuit, `+ Interfaces/IRequestValidationPipeline.cs`
- [ ] T109 [US4] Implement inbound authentication in `Api/Middleware/` — `X-Api-Key` header **and** OAuth bearer validation against hashed credentials, plus scope resolution per BR-26; a per-integration rate limiter (default 100 req/s, **Operations-configurable with no code change**, NFR-4) and the 2 MB payload cap enforced *before* any parameter parsing (NFR-3/VR-F11); plain HTTP refused (NFR-5)
- [ ] T110 [US4] Implement `Api/Controllers/InboundScenarioController.cs` — the five SCN-01…05 endpoints per contracts/api-endpoints.md, each returning its normative artifact, plus the `IntegrationRequestLog` write path (every request logged with the full field list, FR-S8-05, incl. the rejection stage)
- [ ] T111 [US4] Implement `Infrastructure/SurveyBuilderIntegration/RealSurveyRenderServiceAdapter.cs` — wraps M-01's real `ISurveyRenderService` for SCN-03, relaying the definition JSON unchanged as an opaque schema (CMC-02, research.md §4.2)
- [ ] T112 [US4] API tests in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/InboundScenario*EndpointTests.cs` 🐳 — one class per scenario asserting the correct result code + the downstream hand-off stub call; plus the pipeline-order test (a request failing two checks asserts the **earlier** code wins), the idempotent-retry test (2 log entries, 1 downstream call), the rate-limit test (`429` on N+1), and the payload-cap test (`413` with zero parameter log detail)
- [ ] T113 [US4] Scenario test `tests/Nabadat.IntegrationHub.IntegrationTests/Scenarios/InboundRequestLifecycleScenarioTests.cs` 🐳 — send → `202` → poll `GET /request-logs` until the entry appears (≤ 60s) → retry with the same `transaction_id` → assert no duplicate downstream dispatch and exactly 2 log entries → deactivate the channel → repeat → `409 E-1004`. Final aggregate: 2 accepted + 1 rejected log entry, 1 downstream dispatch

**Build gate**: `dotnet test tests/Nabadat.IntegrationHub.UnitTests` and `…IntegrationTests` green (Docker up). No frontend gate — this story ships no UI.

**Checkpoint**: the module's core loop works end-to-end: a real request succeeds against US1–US3's configuration.

---

## Phase 7: User Story 5 — Monitor Integration Health and Investigate via Request Logs (Priority: P1)

**Goal**: A Tenant IT Administrator sees integration health at a glance (SCR-01 tiles) and
investigates failures in SCR-08 — status-class / integration / time-window filters, expandable
detail with PII masking, and a masked export of the filtered view.

**Independent Test**: Seed 6 integrations (1 inactive) with mixed traffic → `/integration-hub/integrations`
shows "6 / 5 active", the correct 24h count, and a colour-coded error rate → `/integration-hub/logs`
→ chips "Client errors" + one integration + "Last hour" → only matching rows, per-chip counts scoped
to the window → expand a row → PII masked, full response shown.

### Unit Tests for User Story 5 (write FIRST, must FAIL) ⚠️

- [ ] T114 [P] [US5] Unit tests for `IntegrationHealthTileCalculator` in `tests/Nabadat.IntegrationHub.UnitTests/Monitoring/IntegrationHealthTileCalculatorTests.cs` — `Compute(total=6, active=5, errors24h=0, requests24h=0)` → tile "6 / 5 active", error rate `"—"` (FR-S1-05)
- [ ] T115 [P] [US5] Unit tests for `ErrorRateColourResolver` in `…/Monitoring/ErrorRateColourResolverTests.cs` — `0.008 → D2`, `0.03 → D3`, `0.08 → D4`, plus the exact-1% and exact-5% boundaries per FR-S1-06's documented convention
- [ ] T116 [P] [US5] Unit tests for `IntegrationListFilter` in `…/Monitoring/IntegrationListFilterTests.cs` — `Filter(search="CRM", channel="CALL-CENTER")` → intersection only (AC-S1-02, AND)
- [ ] T117 [P] [US5] Unit tests for `RequestLogFilterCombinator` in `…/Monitoring/RequestLogFilterCombinatorTests.cs` — status class + integration + window (incl. `LastHour`) AND-combined, counts scoped to the window (AC-S8-01)
- [ ] T118 [P] [US5] Unit tests for `PiiMaskingFormatter` in `…/Monitoring/PiiMaskingFormatterTests.cs` — `Mask("+962770123456")` → `"+9627•••••312"`; `Mask("Mona Al-Rashid")` → `"M••••• A•-R•••••"`; identical output for list, detail, and export (FR-S8-03)
- [ ] T119 [P] [US5] Unit tests for `RejectedRequestDetailProjection` in `…/Monitoring/RejectedRequestDetailProjectionTests.cs` — `rejectedAtStage="Authentication"` → parameters panel = `"— request rejected before parameter parsing"` (AC-S8-03)

### Red Checkpoint for User Story 5 🔴

- [ ] T120 [US5] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`, verify red (no `Application/Monitoring/` types), paste the transcript, commit. Non-parallel

### Implementation for User Story 5

- [ ] T121 [US5] Implement `IntegrationHealthTileCalculator` and `ErrorRateColourResolver` in `src/Nabadat.IntegrationHub/Application/Monitoring/` — the rolling-24h aggregates come from `integration_request_logs` (FR-S1-01/05/06)
- [ ] T122 [US5] Implement `IntegrationListFilter` and `RequestLogFilterCombinator` in `Application/Monitoring/`
- [ ] T123 [US5] Implement `PiiMaskingFormatter` and `RejectedRequestDetailProjection` in `Application/Monitoring/` — masking applied on the **server** so there is no unmasked-access code path in Phase 1 (NFR-9/SC-009)
- [ ] T124 [US5] Implement `IRequestLogService`/`RequestLogService` + `Api/Controllers/RequestLogsController.cs` — `GET /request-logs` (cursor-paginated, newest-first, AND-combined filters per API-04), `GET /request-logs/{id}`, `GET /request-logs/export`; the whole controller gated on `m13.log.view` (**P-07-exclusive**, BR-24)
- [ ] T125 [US5] Frontend: SCR-01 stat tiles (Integrations · Requests·24h · Error rate) with their sub-texts, plus live name search AND-combined with the service-channel filter, added to `pages/AllIntegrationsPage.tsx` — the filter row follows CLAUDE.md's toolbar shape (`sm:items-end`, bounded `sm:max-w-sm` search, `sm:w-48` selects, `flex flex-col gap-1.5` around each Select)
- [ ] T126 [US5] Frontend: `components/RequestLogTable.tsx` + `pages/RequestLogsPage.tsx` — SCR-08 (FR-S8-01…05): status-class chips (All/2xx/4xx/5xx), integration select, time select defaulting to **Last 24 hours**, per-window counts; expandable row detail showing *Parameters received* (registered + unregistered) and *Response returned*; the masking/retention info alert; the auth-rejected notice; and **Export** of the current filtered view
- [ ] T127 [US5] API tests in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/RequestLogsEndpointTests.cs` 🐳 — `GET /integrations` with computed tiles + FR-S1-02 filters; `GET /request-logs` AND-combined filters incl. `last_hour`, cursor-paginated newest-first; detail with masked PII; `export` masked identically; `GET /request-logs` **as P-01 → 403**

> **Scenario test**: spec.md declares `scenario-test: not-needed` for US5.

### E2E (Browser) Tests for User Story 5 🎭

- [ ] T128 [US5] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/IntegrationMonitoringTests.cs` (`Integrations_stat_tiles_reflect_total_active_and_traffic`, `…_search_and_channel_filter_combine_with_AND`, `…_new_integration_shows_zero_traffic_and_dash_rate`) and `RequestLogsTests.cs` (`RequestLogs_filters_combine_with_AND_and_counts_reflect_window`, `…_expanded_row_masks_pii_in_exact_format`, `…_auth_rejected_row_shows_rejected_before_parsing_notice`, `…_export_masks_pii_identically_to_screen`, `…_cx_manager_role_is_denied_access`). Update `COVERAGE.md`

**Build gate**: unit + integration green · `npm run build` green · `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~IntegrationMonitoringTests|FullyQualifiedName~RequestLogsTests"` green.

**Checkpoint**: the P1 set (US1–US5) is complete — configure, provision, call, and observe.

### Click-through Parity for User Story 5 🎨

> Owner: the frontend developer, run manually. Same two preconditions as T042P.

- [ ] T128P [US5] Run `/clickthrough-parity 006-integration-hub phase 7` over `/integration-hub/integrations` (tiles + filters) and `/integration-hub/logs` and triage the report. Apply accepted defects with `--fix`; escalate **Needs discussion** items. Record the result in `route-map.md`

---

## Phase 8: User Story 6 — Manage Parameter Mappings Inline (Priority: P2)

**Goal**: A CX Manager translates raw backend values into bilingual display values that resolve at
**read time**, with unmapped incoming values surfaced in a 7-day queue with one-click mapping.

**Independent Test**: Pick a mapping-enabled parameter with no mappings → send a US4 request carrying
`S014` → `/integration-hub/mappings` → the unmapped-values alert lists `S014` → **Map now** pre-fills
a draft row → fill EN/AR → **Save** → the mapping is Active and every historical report renders the
new label immediately.

### Unit Tests for User Story 6 (write FIRST, must FAIL) ⚠️

- [ ] T129 [P] [US6] Unit tests for `MappingSourceValueUniquenessValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Mappings/MappingSourceValueUniquenessValidatorTests.cs` — required, unique within the parameter **case-insensitively** (VR-F08)
- [ ] T130 [P] [US6] Unit tests for `MappingResolver`, `MappingEnabledParameterFilter`, and `UnmappedValueQueueService` in `…/Mappings/` — read-time resolution (an edited label retroactively relabels historical values, FR-F0-05); only BR-27 mapping-enabled parameters are listed (FR-S7-01); the queue holds a 7-day occurrence window and a deleted-then-re-received value re-enters it

### Red Checkpoint for User Story 6 🔴

- [ ] T131 [US6] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`, verify red (no `Application/Mappings/` types), paste the transcript, commit. Non-parallel

### Implementation for User Story 6

- [ ] T132 [US6] Implement `Application/Mappings/` — `MappingSourceValueUniquenessValidator`, `MappingResolver`, `MappingEnabledParameterFilter`, `UnmappedValueQueueService`, `IParameterMappingService`/`ParameterMappingService` — and `Api/Controllers/ParameterMappingsController.cs`: `GET /parameters/{id}/mappings`, `GET .../unmapped-queue`, `POST`/`PUT`/`DELETE .../mappings[/{mappingId}]`. Audit `mapping.added` / `mapping.edited` / `mapping.deleted`
- [ ] T133 [US6] Frontend: `components/ParameterMappingTable.tsx` + `pages/ParameterMappingsPage.tsx` — SCR-07 (FR-S7-01…04): the mapping-enabled parameter selector, the unmapped-values **warning alert** with **Map now** pre-fill (hidden when the queue is empty), the mapping table with an inline **Draft** add-row (`Draft` badge + Save, RTL Arabic display input), row edit, delete behind **Dialog D-7**, the source-system badge, and the footer information line
- [ ] T134 [US6] API tests in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/ParameterMappingsEndpointTests.cs` 🐳 (CRUD + queue + VR-F08 uniqueness + `m13.mapping.manage` enforcement) and the scenario test `Scenarios/MappingReadTimeResolutionScenarioTests.cs` 🐳 — a value arrives unmapped → appears in the queue → is mapped → the same historical row now resolves to the new label; editing the label relabels it again

### E2E (Browser) Tests for User Story 6 🎭

- [ ] T135 [US6] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/ParameterMappingsTests.cs` — the unmapped-queue → **Map now** → draft-row → save flow, inline edit, and the D-7 delete confirmation. Update `COVERAGE.md`

**Build gate**: unit + integration green · `npm run build` green · `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ParameterMappingsTests"` green.

**Checkpoint**: US1–US6 all work independently.

### Click-through Parity for User Story 6 🎨

> Owner: the frontend developer, run manually. Same two preconditions as T042P.

- [ ] T135P [US6] Run `/clickthrough-parity 006-integration-hub phase 8` over `/integration-hub/mappings` and triage the report. Apply accepted defects with `--fix`; escalate **Needs discussion** items. Record the result in `route-map.md`

---

## Phase 9: User Story 7 — Bulk Import, Export, and Replace-All Parameter Mappings via Excel (Priority: P2)

**Goal**: Excel export (`source_value`, `display_en`, `display_ar`), strictly all-or-nothing import
in Merge or Replace-all mode with a row-level validation report, and an irreversible Replace-all
behind an explicit confirmation naming the consequence.

**Independent Test**: Export → introduce one invalid row → **Import from Excel** in Merge mode →
the import is rejected wholesale with a row-level report and **nothing** is applied → fix and
re-import successfully → **Replace all mappings…** → confirm D-5 → all prior mappings are gone.

### Unit Tests for User Story 7 (write FIRST, must FAIL) ⚠️

- [ ] T136 [P] [US7] Unit tests for `ExcelMappingExporter` and `ExcelMappingImportValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Mappings/` — export produces the header row + one data row per mapping; `Validate(rows=[214 valid, 1 empty source_value])` → `Invalid`, report `[{row: 215, column: "source_value", reason: "required"}]`, nothing applied (AC-S7-01); an in-file duplicate `source_value` → rejected with the duplicate named (VR-F09)
- [ ] T137 [P] [US7] Unit tests for `ExcelMappingImportModeApplier`, `ImportRowCountGuard`, and `MappingsPerParameterGuard` in `…/Mappings/` — `Apply(Merge, existing=[S001,S002], imported=[S001(new),S003])` → `[S001(new), S002, S003]`; `Apply(ReplaceAll, …, imported=[S003])` → `[S003]`; `GuardRowCount(10001)` → rejected **before parsing**; `GuardMappingCount(existing=4999, importing=2)` → rejected (NFR-16)

### Red Checkpoint for User Story 7 🔴

- [ ] T138 [US7] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`, verify red, paste the transcript, commit. Non-parallel

### Implementation for User Story 7

- [ ] T139 [US7] Implement the Excel types in `Application/Mappings/` using **ClosedXML** (plan.md Primary Dependencies) and add `GET /parameters/{id}/mappings/export`, `POST .../mappings/import`, and `POST .../mappings/replace-all` to `ParameterMappingsController` — import is transactional and all-or-nothing; audit `mapping.import` (mode + row count) and `mapping.replace_all` (rows removed/added). Replace-all requires `m13.mapping.replace`
- [ ] T140 [US7] Frontend: SCR-07 **Dialog D-4** (import — template-columns copy, Merge/Replace-all radio with **Merge pre-selected**, row-level failure report) and **Dialog D-5** (replace-all — names the exact current mapping count and the parameter, states it cannot be undone, destructive filled confirm), plus the **Export to Excel** action and the footer **Replace all mappings…** button
- [ ] T141 [US7] API tests in `…/Endpoints/ParameterMappingsImportEndpointTests.cs` 🐳 (export shape; merge import → 200 + upserts + `mapping.import`; one invalid row → 400/422 + report + a follow-up `GET` proving zero changes; replace-all → prior set gone + `mapping.replace_all`; 10,001 rows → 400 before parsing), the scenario test `Scenarios/BulkMappingReplaceScenarioTests.cs` 🐳, **and** the additional `[TestMethod]` blocks in `tests/Nabadat.E2ETests/IntegrationHub/ParameterMappingsTests.cs` 🎭 — `Mappings_export_downloads_three_ratified_columns`, `…_import_all_or_nothing_shows_row_level_report_on_failure`, `…_import_dialog_defaults_to_merge_mode`, `…_replace_all_confirmation_names_count_and_is_irreversible`, `…_import_over_10000_rows_is_rejected`

**Build gate**: unit + integration green · `npm run build` green · `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ParameterMappingsTests"` green.

**Checkpoint**: US1–US7 all work independently.

### Click-through Parity for User Story 7 🎨

> Owner: the frontend developer, run manually. Same two preconditions as T042P.

- [ ] T141P [US7] Run `/clickthrough-parity 006-integration-hub phase 9` over `/integration-hub/mappings` (the D-4/D-5 dialogs and the export/replace-all toolbar) and triage the report. Record the result in `route-map.md`

---

## Phase 10: User Story 8 — Manage Credential Lifecycle (Priority: P2)

**Goal**: Ongoing revoke/regenerate outside the onboarding wizard, plus the invariants that guard
the ratified field-set removals — no sandbox, no expiry, no IP allow-list, no grant-type or
token-lifetime fields — and scope-limited endpoint access.

**Independent Test**: On an integration with an active API key, open Step 2 in edit mode → **Revoke**
→ confirm D-3 → a caller request with that key returns `401 E-1401`. Generate a new key → the old
one still fails identically while the new one succeeds.

### Unit Tests for User Story 8 (write FIRST, must FAIL) ⚠️

- [ ] T142 [P] [US8] Unit tests for `OAuthScopeEnforcer` and `CredentialFieldSetGuard` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/` — `EnforceScope(["survey-links:read"], SCN-01)` → rejected; `EnforceScope(["survey-requests:write"], SCN-01)` → allowed (BR-26); `AssertFieldSet(apiKeyFields)` contains no `expiry`/`sandbox`/`allowedSourceIps`; `AssertFieldSet(oauthFields)` contains no `grantType`/`tokenLifetime` (`[PO-G13]`, BR-17). Extends `CredentialRevocationServiceTests` with the standalone revoke-without-regeneration flow and the "no un-revoke" invariant

### Red Checkpoint for User Story 8 🔴

- [ ] T143 [US8] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`, verify red, paste the transcript, commit. Non-parallel

### Implementation for User Story 8

- [ ] T144 [US8] Implement `Application/Integrations/OAuthScopeEnforcer.cs` and `CredentialFieldSetGuard.cs`; wire the enforcer into T109's inbound authentication step so an insufficient scope maps to `401 E-1401`; confirm the standalone `POST /integrations/{id}/credentials/revoke` path works without a regeneration and emits `credential.revoked`
- [ ] T145 [US8] API tests in `…/Endpoints/CredentialLifecycleEndpointTests.cs` 🐳 (revoke without regenerating → 200 then `401 E-1401` on a live call; generate while active → old key immediately unusable, verified by a live call; an OAuth token missing the target scope → `401 E-1401`) **and** the additional `[TestMethod]` blocks in `tests/Nabadat.E2ETests/IntegrationHub/IntegrationWizardTests.cs` 🎭 — `Wizard_revoke_dialog_names_masked_key_and_consequence`, `Wizard_generating_new_key_while_one_active_shows_no_extra_confirmation_for_old_key`, `Wizard_auth_forms_never_render_expiry_sandbox_or_ip_allowlist_fields`, `Wizard_oauth_form_has_no_grant_type_or_token_lifetime_fields`

> **Scenario test**: spec.md declares `scenario-test: not-needed` for US8 — US3's `IntegrationOnboardingScenarioTests` already walks generate → revoke → repeat-call-rejected.

**Build gate**: unit + integration green · `npm run build` green · `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~IntegrationWizardTests"` green.

**Checkpoint**: US1–US8 all work independently.

### Click-through Parity for User Story 8 🎨

> Owner: the frontend developer, run manually. Same two preconditions as T042P.

- [ ] T145P [US8] Run `/clickthrough-parity 006-integration-hub phase 10` over SCR-02 Step 2 in edit mode (revoke/regenerate) and triage the report. Record the result in `route-map.md`

---

## Phase 11: User Story 9 — Cross-Persona Read-Only Visibility and Permission Enforcement (Priority: P2)

**Goal**: Each persona manages its own screens and gets **read-only** visibility on the other's —
except **Request Logs, which are P-07-exclusive with no P-01 grant at all** (BR-24 as corrected in
SRS v1.2). Every sensitive action is permission-controlled and audited, enforced server-side
regardless of what the client renders.

**Independent Test**: As P-01, `/integration-hub/integrations` renders read-only (no New/Edit/Revoke)
and `/integration-hub/logs` renders the **access-denied state**; then hit `POST /integrations` and
`GET /request-logs` directly and confirm the server returns 403 for both.

### Unit Tests for User Story 9 (write FIRST, must FAIL) ⚠️

- [ ] T146 [P] [US9] Unit tests for `PermissionKeyResolver`, `CrossPersonaViewGuard`, and `AuditEventEmitter` in `tests/Nabadat.IntegrationHub.UnitTests/Permissions/` — `Resolve(P-01, "integration.manage")` → `Denied`; `Resolve(P-01, "integration.view")` → `Allowed`; `Resolve(P-07, "channel.manage")` → `Denied`; `Resolve(P-07, "log.view")` → `Allowed`; **`Resolve(P-01, "log.view")` → `Denied`** (no cross-persona view grant on logs); `Emit("credential.revoked", before={status:Active}, after={status:Revoked})` → one event with actor, tenant, timestamp, entity, before/after; `Emit("channel.id_changed", …)` likewise

### Red Checkpoint for User Story 9 🔴

- [ ] T146R [US9] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`, verify red (no `Application/Permissions/` types), paste the transcript, commit. Non-parallel

### Implementation for User Story 9

- [ ] T147 [US9] Frontend: FR-GBL-05 read-only rendering across all eight screens — every action the current persona lacks is hidden or disabled, driven by `hooks/useIntegrationHubAccess.ts`. Read-only personas get the `<Eye>` + `view-` testid row action, never a disabled pencil (CLAUDE.md row-actions rule), so E2E can prove the permission split
- [ ] T148 [US9] Frontend: FR-GBL-02 access-denied state on direct-route access without the view permission — `components/AccessDenied.tsx` rendered for every route the persona cannot view, including `/integration-hub/logs` for P-01 (never a partial or broken render)
- [ ] T149 [US9] Implement `Application/Permissions/PermissionKeyResolver.cs` — the nine `m13.*` keys from the Permissions Matrix (`integration.view/manage`, `credential.manage`, `log.view`, `channel.view/manage`, `parameter.view/manage`, `mapping.manage/replace`) — and register them with M-10 (CMC-06)
- [ ] T150 [US9] Add authorization filters to every M-13 write endpoint so a persona lacking `*.manage` gets **403 regardless of client rendering**, and audit the denied attempt (SC-010)
- [ ] T151 [US9] Implement `Application/Permissions/CrossPersonaViewGuard.cs` (BR-24) and apply it to the read paths — P-01 view-only on Integrations; P-07 view-only on Channels/Parameters/Mappings; **Request Logs P-07-exclusive**
- [ ] T152 [US9] Implement `Application/Events/AuditEventEmitter.cs` and emit across all **12 audited action families** in the Permissions Matrix — integration created/updated · activated/deactivated · credential generated · revoked · channel created/updated · channel ID changed · channel activated/deactivated · parameter created/updated · parameter enabled/disabled · mapping added/edited/deleted · mapping import · mapping replace-all (SC-011)
- [ ] T153 [US9] Frontend: audit the FR-GBL-03 unsaved-changes guard across **SCR-02, SCR-04, and SCR-06** so all three use one consistent derived-digest shape — closes TODO-M13-006. If T085's guard already lands it, this task is a consistency pass, not a rebuild
- [ ] T154 [US9] API tests in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/PermissionsEndpointTests.cs` 🐳 — `POST /integrations` as P-01 → 403 (audited); `POST /service-channels` as P-07 → 403; `GET /integrations` as P-01 → 200; `GET /service-channels` as P-07 → 200; `GET /request-logs` as P-01 → 403; and every sensitive action performed by its authorized persona emits exactly one matching audit event (12 families)

### E2E (Browser) Tests for User Story 9 🎭

- [ ] T155 [US9] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/CrossPersonaPermissionsTests.cs` — `Integrations_cx_manager_sees_read_only_view_with_no_manage_controls`, `ServiceChannels_it_admin_sees_read_only_view_with_no_manage_controls`, `RequestLogs_direct_route_access_without_permission_shows_access_denied`, `Mappings_direct_route_access_without_permission_shows_access_denied`. Update `COVERAGE.md`

> **Scenario test**: spec.md declares `scenario-test: not-needed` for US9.

**Build gate**: unit + integration green · `npm run build` green · `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~CrossPersonaPermissionsTests"` green.

**Checkpoint**: US1–US9 all work independently.

### Click-through Parity for User Story 9 🎨

> Owner: the frontend developer, run manually. Same two preconditions as T042P.

- [ ] T155P [US9] Run `/clickthrough-parity 006-integration-hub phase 11` over all eight routes **in their read-only persona rendering** and triage the report. Record the result in `route-map.md`

---

## Phase 12: User Story 10 — Activate and Deactivate Integrations and Service Channels (Priority: P3)

**Goal**: The toggle UX and reactivation path for both entity types, with no delete transition
anywhere.

**Independent Test**: Deactivate an Active integration → its SCR-01 row shows the neutral "Inactive"
badge and "suspended" sub-line and a live call to its endpoint now fails → reactivate → the badge
reverts and calls succeed again.

### Unit Tests for User Story 10 (write FIRST, must FAIL) ⚠️

- [ ] T156 [P] [US10] Unit tests for `IntegrationStatusToggle` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/IntegrationStatusToggleTests.cs` — Active ⇄ Inactive audited (`integration.deactivated` / `integration.activated`); `Attempt(Delete, integration)` → no such state transition exists
- [ ] T157 [P] [US10] Unit tests for `ServiceChannelStatusToggle` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ServiceChannelStatusToggleTests.cs` — Active ⇄ Inactive audited; on deactivate the channel is excluded from `GetActiveChannelsForSelector()`

### Red Checkpoint for User Story 10 🔴

- [ ] T158 [US10] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`, verify red, paste the transcript, commit. Non-parallel

### Implementation for User Story 10

- [ ] T159 [US10] Implement `Application/Integrations/IntegrationStatusToggle.cs` and `PATCH /api/v1/integration-hub/integrations/{id}` `{ active }` — an inactive integration's endpoint rejects calls with `401 E-1401` (credentials suspended, Status Lifecycle)
- [ ] T160 [US10] Implement `Application/Channels/ServiceChannelStatusToggle.cs` and the `GET /service-channels?active=true` selector query used by SCR-02, so a deactivated channel disappears from new-integration selection while existing integrations keep the now-rejecting reference (BR-07)
- [ ] T161 [US10] Frontend: the SCR-01 row-level status toggle with the neutral "Inactive" badge and "suspended" sub-line, and **no delete control anywhere** (Status Lifecycle)
- [ ] T162 [US10] Frontend: the SCR-04 Active toggle wired to T160, plus the SCR-02 edge-case warning shown when an existing integration references a now-inactive channel
- [ ] T163 [US10] API tests in `…/Endpoints/StatusToggleEndpointTests.cs` 🐳 — `PATCH /integrations/{id} {active:false}` → 200 then a live call rejected; `{active:true}` → 200 then calls succeed; `GET /service-channels?active=true` excludes a just-deactivated channel

### E2E (Browser) Tests for User Story 10 🎭

- [ ] T164 [US10] E2E `[TestMethod]` blocks added to `tests/Nabadat.E2ETests/IntegrationHub/IntegrationMonitoringTests.cs` — `Integrations_deactivate_reactivate_round_trip_updates_badge_and_endpoint`, `Integrations_row_never_shows_a_delete_action`
- [ ] T165 [US10] E2E `[TestMethod]` block added to `tests/Nabadat.E2ETests/IntegrationHub/ServiceChannelTests.cs` — `ServiceChannels_deactivated_channel_disappears_from_new_integration_selector`. Update `COVERAGE.md`

> **Scenario test**: spec.md declares `scenario-test: not-needed` for US10.

**Build gate**: full-solution `dotnet test Nabadat.sln` green · `npm run build` green · `dotnet test tests/Nabadat.E2ETests` green.

**Checkpoint**: all ten user stories are independently functional.

### Click-through Parity for User Story 10 🎨

> Owner: the frontend developer, run manually. Same two preconditions as T042P.

- [ ] T165P [US10] Run `/clickthrough-parity 006-integration-hub phase 12` over the SCR-01 row toggle and the SCR-04 Active toggle and triage the report. Record the result in `route-map.md`

---

## Phase 13: Polish & Cross-Cutting Concerns

**Purpose**: NFR and Success-Criteria verification, deferred-item closure, documentation, and the
whole-module release gate.

### NFR verification

- [ ] T166 [P] NFR-2/SC-013 — performance regression test on a seeded tenant proving 95% of inbound API requests complete within 500ms excluding downstream systems
- [ ] T167 NFR-4 — bind the per-integration rate limit to configuration so Nabadat Operations can change it **without a code deployment**; add a test proving an in-flight request already past the check is unaffected by a concurrent limit change
- [ ] T168 [P] NFR-5 — integration test proving plain HTTP is refused and TLS 1.2+ is enforced on both the API and the console
- [ ] T169 NFR-6/SC-008 — repo-wide audit that no plaintext credential secret appears in any log sink, API response schema, or export after its show-once dialog closes
- [ ] T170 NFR-8 — implement 90-day request-log retention by **DETACHING** old monthly partitions (never row-level `DELETE`, per DB-04) and assert it in an integration test. Closes half of TODO-M13-004
- [ ] T171 NFR-8 — implement the monthly partition roll-forward job for `integration_request_logs`, including the documented Postgres caveat that rows already landed in `DEFAULT` block attaching a real partition for that month. Closes the other half of TODO-M13-004
- [ ] T172 [P] NFR-9 — tenant-isolation integration test across all eight owned tables, proving no cross-tenant read or write path exists
- [ ] T173 [P] NFR-16/SC-015 — guard tests for all five ceilings: 200 custom parameters, 100 channels, 200 integrations (VR-F13 console errors) and 5,000 mappings / 10,000 import rows (`MappingsPerParameterGuard`, `ImportRowCountGuard`)
- [ ] T174 [P] NFR-17 — concurrency test proving last-write-wins with a full audit record for two concurrent edits of the same not-yet-locked channel ID, and for a replace-all racing an inline edit
- [ ] T175 Add `ResetIntegrationHubStateAsync()` to `tests/Nabadat.IntegrationHub.IntegrationTests/Infrastructure/IntegrationHubApplicationFactory.cs` — truncates `channel_parameter_assignments`, `integration_request_logs`, `credentials`, `integrations`, `service_channels` while **leaving `parameters` intact**, and asserts the built-in count is still 23 afterwards. Call it from every endpoint test class's arrange step. Closes TODO-M13-003 (backend half)
- [ ] T176 One-off prune of the shared `e2e` tenant's ~73 leftover service channels, and a review that every M-13 E2E file tears down what it seeds. Closes TODO-M13-003 (browser half)

### Success-criteria verification

- [ ] T177 [P] SC-003 — audit that 100% of pipeline failures reject atomically with the correct code and zero partial downstream side effects
- [ ] T178 [P] SC-004 — verify idempotency correctness: any retry count of an identical `(tenant, channelId, transaction_id)` produces exactly one downstream dispatch/store
- [ ] T179 [P] SC-005 — verify zero data loss for unmapped List values and unregistered key–value pairs across US4 and US6
- [ ] T180 [P] SC-006 — verify retroactive mapping correctness with zero stale-label reads after an edit and after a replace-all
- [ ] T181 SC-011 — audit-completeness sweep asserting all 12 audited action families emit actor, tenant, timestamp, entity, and before/after summary
- [ ] T182 SC-016 — SCN-05 durability: assert M-13's `202` through to a confirmed downstream stored record. **Blocked on M-04** (coordination-log C-02) — until it ships, assert against `RecordedResponseIngestion` and document the boundary in `coordination-log.md` rather than claiming the criterion met
- [ ] T183 SC-001 — time the golden path of US3 and confirm a new integration's endpoint is callable in under 5 minutes from opening the wizard
- [ ] T184 SC-002 — on a seeded 20-integration tenant, confirm an unhealthy integration (error rate > 5%) is identifiable within 10 seconds of SCR-01 load

### Bilingual, theme, and accessibility passes

- [ ] T185 [P] SCR-01 — verify LTR/RTL parity and light/dark rendering (NFR-10, SC-012)
- [ ] T186 [P] SCR-02 — verify LTR/RTL parity and light/dark rendering across all three wizard steps and D-1/D-2/D-3
- [ ] T187 [P] SCR-03 — verify LTR/RTL parity and light/dark rendering
- [ ] T188 [P] SCR-04 — verify LTR/RTL parity and light/dark rendering, including the RTL Arabic name input
- [ ] T189 [P] SCR-05 — verify LTR/RTL parity and light/dark rendering, including the tab strip and flag glyphs
- [ ] T190 [P] SCR-06 — verify LTR/RTL parity and light/dark rendering of the drawer, Range card, and List panel
- [ ] T191 [P] SCR-07 — verify LTR/RTL parity and light/dark rendering, including RTL Arabic display values in table cells and D-4/D-5/D-7
- [ ] T192 [P] SCR-08 — verify LTR/RTL parity and light/dark rendering, including the expanded detail panel and masked values
- [ ] T193 SC-012 — run the RTL logical-property scan over `frontend/src/features/integration-hub/`: zero `pl-*`/`pr-*`/`ml-*`/`mr-*`/`left-*`/`right-*`/`text-left`/`text-right`/`rounded-l-*`/`rounded-r-*`/`border-l-*`/`border-r-*`
- [ ] T194 Run CLAUDE.md's theming self-review over the feature folder: `-\[#[0-9a-fA-F]{3,8}\]` **must return 0**; judgment-check every `style={{…#hex}}` hit (only the SCR-02 Step-3 code block's fixed dark surface is sanctioned)
- [ ] T195 [P] SC-014 — automated axe scan of SCR-01/02/03/04 in both LTR and RTL; 0 WCAG 2.1 AA violations
- [ ] T196 [P] SC-014 — automated axe scan of SCR-05/06/07/08 in both LTR and RTL; 0 WCAG 2.1 AA violations
- [ ] T197 NFR-11 — keyboard operability pass: Esc closes the SCR-06 drawer and every dialog D-1…D-7, focus-visible rings on all interactive elements, `prefers-reduced-motion` respected
- [ ] T198 NFR-12 — responsive pass across the eight screens: tiles collapse to two/one columns, the sidebar hides below tablet width, tables scroll horizontally inside their own container

### Copy, contract, and catalogue audits

- [ ] T199 NFR-13 — confirm every destructive action (D-3 revoke, D-5 replace-all, D-6 disable, D-7 delete mapping) sits behind an explicit confirmation naming the consequence
- [ ] T200 FR-GBL-01 — verify server-side pagination beyond 50 rows on all five tables, with the specified default orders (integrations/channels/parameters by creation, logs newest-first, mappings by entry order) and no user-facing column sorting in Phase 1
- [ ] T201 FR-GBL-02 — verify skeleton, empty (with guidance + primary CTA), error-with-retry, and access-denied states exist on all eight screens
- [ ] T202 FR-GBL-04 — verify success toasts on create/save/import/revoke, error toasts on failed generation, and that all inline validation copy matches VR-F12's patterns ("‹Field› is required" / "‹Value› is already in use")
- [ ] T203 Arabic copy review of the whole `integrationHub` i18n namespace — natively written فصحى, never translated from English; no `text-xs` on Arabic body paragraphs; `leading-relaxed` on Arabic prose
- [ ] T204 Audit every screen's shipped copy against spec.md's per-screen `[UI]` blocks (SCR-01 tile sub-texts, SCR-03 footer note, SCR-04 helpers, SCR-05 footer note, SCR-06 flag descriptions, SCR-07 alert pattern, SCR-08 masking alert)
- [ ] T205 Audit the inbound API's result-code message copy against FR-F0-03's exact normative patterns, including the `E-1401`-on-revocation and `E-1500` retry-idempotent strings
- [ ] T206 Contract test proving the data-type list is closed — **Duration and Identifier appear nowhere**, including the SCR-06 type select (`[PO-G17]`), mirroring `CredentialFieldSetGuard`'s shape
- [ ] T207 Verify the seeded built-in catalogue against FR-F0-10 — exactly 23 parameters with the specified `api_field`, data type, and BR-27 mapping-support state; all enabled by default (BR-23); type read-only for every one (`[PO-G27]`)
- [ ] T208 Verify all nine `m13.*` permission keys are registered with M-10 and that the Permissions Matrix in spec.md matches what `PermissionKeyResolver` resolves

### Documentation and closure

- [ ] T209 [P] Document the console API and the five inbound scenario endpoints in `docs/` — request/response shapes, the result-code catalogue, auth mechanisms, and scopes
- [ ] T210 [P] Update `specs/006-integration-hub/contracts/api-endpoints.md` to replace the illustrative inbound paths with the final shipped ones (the paths were always illustrative; the semantics were normative)
- [ ] T211 [P] Update `specs/006-integration-hub/coordination-log.md` — close or restate C-01/C-02 (M-02/M-04) with the current stub state and the zero-code-change swap path
- [ ] T212 [P] Update TODO-M13-005 with the concrete ask filed to the M-10 owner for the data-scope reverse lookup (`GetAssignmentsReferencingParameterAsync`), so BR-10's external two-thirds has an owner
- [ ] T213 Backfill `specs/006-integration-hub/IMPLEMENTATION.md` for Phases 1–3 (T001–T042), which predate the file, and add sections for every phase completed after US2
- [ ] T214 Run `specs/006-integration-hub/quickstart.md` end to end and fix any drift
- [ ] T215 Full-solution gate: `dotnet test Nabadat.sln` → 0 failures (unit + integration + contract, Docker up)
- [ ] T216 Frontend gate: `npm run build` from `frontend/` green, plus a bundle-size sanity check on the new feature folder
- [ ] T217 Full browser gate: `dotnet test tests/Nabadat.E2ETests` green with the stack up and `E2E_BASE_URL` set; confirm every `COVERAGE.md` row for `IntegrationHub/` is ✅
- [ ] T218 **Full-module click-through parity audit — run before the module is pushed.** `/clickthrough-parity 006-integration-hub` with a **bare feature and NO phase**. This is not a repeat of the per-story runs: only whole-module scope can see cross-page **placement** differences (the same control sitting on a different page than the design), because those need the module's full page map. Triage the report; `--fix` what the frontend lead accepts; escalate the Needs-discussion list. `record-audit.py` stamps the result, which is what unblocks `git push` to `main`/`master` (`.claude/hooks/parity-gate.py`)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → no dependencies
- **Foundational (Phase 2)** → depends on Setup; **BLOCKS every user story**
- **User Stories (Phases 3–12)** → all depend on Foundational
- **Polish (Phase 13)** → depends on every story being complete

### User Story Dependencies

The P1 set is a genuine chain, not an accident of ordering:

- **US1 (P1)** — after Foundational. No story dependencies. **MVP.**
- **US2 (P1)** — after Foundational. Independent of US1 (the 23 built-ins are seeded by T009), but US1's contract table renders the catalogue US2 governs.
- **US3 (P1)** — needs **US1** (an active channel to attach to). Its unit tests are already written.
- **US4 (P1)** — needs **US1 + US3** (a channel and a provisioned endpoint to call).
- **US5 (P1)** — needs **US4** (request logs must exist to aggregate).
- **US6 (P2)** — needs **US2** (mapping-enabled parameters) and, for the unmapped queue, **US4**.
- **US7 (P2)** — needs **US6**.
- **US8 (P2)** — needs **US3** (credentials are generated there); scope enforcement rides on **US4**'s auth step.
- **US9 (P2)** — cross-cutting; needs every screen to exist to render read-only, so it lands after US1–US8.
- **US10 (P3)** — needs **US1 + US3**; its consequences are already asserted in US1 and US4.

Because both engineers are solo (research.md §1), the backend runs strictly sequentially. The
frontend can lead where a story's endpoints already exist.

### Within Each User Story

1. Unit tests written and **failing** (Red Checkpoint, committed) before any implementation
2. Domain/DTOs → rules → services → controllers
3. Backend before the frontend screens that consume it
4. Integration/scenario + E2E at the **per-story checkpoint**, never between implementation tasks
5. Click-through parity **after** the checkpoint is green, as its own assigned task — never before, never automatically

### Parallel Opportunities

- All `[P]` Setup tasks (T002, T003, T005)
- All `[P]` Foundational tasks — T007/T008, T010, T015/T016/T017, T019, T022/T023, T025/T026/T027/T028
- Every unit-test task within a story (different files) — e.g. T029–T033, T043–T048, T068–T073, T094–T101, T114–T119
- Independent per-rule implementation tasks once the red baseline is committed — e.g. T051–T056, T103–T107
- The Polish phase's per-screen and per-NFR passes (T166–T208 `[P]` items)

---

## Parallel Example: User Story 4 unit tests

```bash
# All eight US4 unit-test files are independent — write them together, then run one red checkpoint:
Task: "Unit tests for RequestValidationPipeline in tests/Nabadat.IntegrationHub.UnitTests/Requests/RequestValidationPipelineTests.cs"
Task: "Unit tests for ResultCodeMapper in tests/Nabadat.IntegrationHub.UnitTests/Requests/ResultCodeMapperTests.cs"
Task: "Unit tests for ChannelContractRequiredFieldChecker in tests/…/Requests/ChannelContractRequiredFieldCheckerTests.cs"
Task: "Unit tests for ParameterTypeValidator in tests/…/Requests/ParameterTypeValidatorTests.cs"
Task: "Unit tests for UnregisteredParameterStore in tests/…/Requests/UnregisteredParameterStoreTests.cs"
Task: "Unit tests for IdempotencyKeyResolver in tests/…/Requests/IdempotencyKeyResolverTests.cs"
Task: "Unit tests for AllowedOriginsWhitelistStore in tests/…/Requests/AllowedOriginsWhitelistStoreTests.cs"
Task: "Unit tests for SurveyLinkExpiryCalculator in tests/…/Requests/SurveyLinkExpiryCalculatorTests.cs"

# Then, non-parallel:
dotnet test tests/Nabadat.IntegrationHub.UnitTests   # T102 — must be RED
```

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **stop and validate** → demo.
A CX Manager can define a service channel and its parameter contract. ✅ **Already delivered.**

### The real "module is alive" milestone

US1 + US2 + US3 + US4 together. Nothing in this module demonstrably works until a **real inbound
request succeeds** against a configured channel and a provisioned endpoint — that is the point of
US4's placement at P1 rather than later. US5 then makes that success observable.

### Incremental delivery

1. Setup + Foundational → foundation ready ✅
2. US1 → demo ✅
3. US2 → demo ✅
4. **US3 → US4 → US5** → the module delivers its core value (next up; US3's red baseline is already written)
5. US6 → US7 → business-friendly labelling and bulk operations
6. US8 → US9 → operational credential hygiene and the multi-role guardrail
7. US10 → the lifecycle toggles
8. Polish → NFR/SC verification, then T218 unblocks the push to `main`

### Solo-per-lane strategy

With one backend and one frontend engineer (research.md §1), the practical cadence is: AbuKr lands a
story's backend through its integration tests; Marwan follows one story behind on the screens, then
runs that story's E2E and its click-through parity task. The parity report is triaged by the
frontend lead, not applied blindly — every **Needs discussion** item is a business decision.

---

## Notes

- `[P]` = different files, no dependencies on incomplete tasks
- `[Story]` maps each task to its spec.md user story for traceability
- **Build the pages click-through-blind.** Never open or copy from `clickthrough-reference/` while implementing — a ported page makes its parity run report "identical" regardless of real drift, and is recorded `NOT AUDITED`, never "0 defects"
- Verify unit tests fail for the right reason before implementing; commit the red baseline
- Commit after each task or logical group; stop at any checkpoint to validate the story independently
- Backend module structure is fixed by constitution AMENDMENT-009 / Article 1A — a new top-level folder kind needs an amendment, not a task
- Persistence is EF Core only (DB-08): no raw SQL, no `IUnitOfWork`, no EF migrations — tables land in `IntegrationHub_Baseline.sql`
- Time is injected (`TimeProvider`); `DateTime.UtcNow` must not appear in tested production code
