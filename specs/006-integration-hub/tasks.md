# Tasks: M-13 Integration Hub

**Input**: Design documents from `specs/006-integration-hub/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Team**: 2 resources — **AbuKr** (backend, solo) and **Marwan** (frontend, solo). Backend work is
strictly sequential (research.md §1); the frontend story of a phase can run in parallel with the
next phase's backend work once its API contract is fixed.

**Tests**: Per CLAUDE.md "Unit Test Policy", unit tests are MANDATORY for every backend-bearing
story here — **no story in this feature declares `unit-tests: skipped`**, and every story ships
backend units. Each backend story emits **Unit Tests (write FIRST, must FAIL)** → **Red
Checkpoint** → **Implementation** → **Integration & API / Scenario tests**. Nine of the ten
stories are page-bearing and additionally emit **E2E (Browser) Tests 🎭** after their page tasks
(no Red Checkpoint) and a **Click-through Parity 🎨** task after the checkpoint. **US4 declares
`e2e-tests: skipped`** (headless Feature 0, no console screen) — it gets no E2E and no parity
subsection.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested, and
demoed independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1…US10)
- Every task carries an exact file path

## Path Conventions

- **Backend module**: `src/Nabadat.IntegrationHub/` — constitution AMENDMENT-009 / architecture
  Article 1A layout (`Api/`, `Application/<SubDomain>/`, `Domain/`, `Infrastructure/`), hosted
  inside the existing `Nabadat.TenantAdmin` process (AD-01).
- **Unit tests**: `tests/Nabadat.IntegrationHub.UnitTests/<SubDomain>/<Type>Tests.cs`
- **Integration tests**: `tests/Nabadat.IntegrationHub.IntegrationTests/{Infrastructure,Endpoints,Services,Scenarios}/`
- **E2E tests**: `tests/Nabadat.E2ETests/IntegrationHub/` — **appended to the existing shared E2E
  project**, no new project (CLAUDE.md E2E Test Policy rule 1).
- **Frontend**: `frontend/src/features/integration-hub/{components,hooks,pages}/`

## Frontend Task Rule

Before any UI task the agent MUST read the repo-root `CLAUDE.md` end to end (design system,
tokens, RTL logical properties, D1–D5 Two-Palette Rule, button hierarchy, DO / DO NOT) and follow
the **Component Sourcing Rule** — search `frontend/src/components/` (`ui/`, `cx/`) and reuse what
exists before building anything. Per plan.md's Frontend Design Gate this feature needs **zero new
custom-SVG components**: all eight screens build from existing shadcn primitives (`Table`,
`Dialog`, `Sheet`, `TabsListSegmented`, `Badge`, `Select`, `WizardStepper`).

**Click-through-blind (HARD RULE)**: build every page from `spec.md` + this design system.
**Never open, read, or copy from the click-through checkout while implementing** — a ported page
makes its parity run VOID (reported `NOT AUDITED`), not clean.

## Backend Module Folder Structure Rule

File paths land in the canonical layout only: controllers/DTOs → `Api/{Controllers,Contracts}/`;
use-case + data-access services → `Application/<SubDomain>/` with ports in
`Application/<SubDomain>/Interfaces/`; entities/value objects/published interfaces →
`Domain/{Entities,ValueObjects,Interfaces}/`; DbContext + `IEntityTypeConfiguration<T>` +
`_Baseline.sql` → `Infrastructure/Persistence/` + `Infrastructure/Migrations/`; every external
adapter → a `Infrastructure/<Concern>/` folder named for what it wraps. One type per file.

## Backend Data-Access Task Rule

Persistence follows DB-08 / database-constitution Article 7 (EF Core, M-10 reference pattern):
entity + `IEntityTypeConfiguration<T>` with explicit `HasColumnName`, tables added to
`IntegrationHub_Baseline.sql` — **never an EF migration**; data-access service + port (the unit
test mock seam); business service depending on the port, wrapping multi-write atomicity in
`ITenantDbContext.ExecuteAsync`, taking the clock via injected `TimeProvider`. Real Postgres
lives only in the IntegrationTests lane.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the module skeleton and its three test lanes.

- [ ] T001 Create the backend module project `src/Nabadat.IntegrationHub/Nabadat.IntegrationHub.csproj` (`net10.0`) with a project reference to `src/Nabadat.SurveyBuilder/Nabadat.SurveyBuilder.csproj` (the REAL `ISurveyRenderService` call, research.md §4.2), and register it in `Nabadat.TenantAdmin.sln`
- [ ] T002 Create the composition root `src/Nabadat.IntegrationHub/IntegrationHubServiceCollectionExtensions.cs` exposing `AddIntegrationHubModule(this IServiceCollection, IConfiguration)`, and call it from `src/Nabadat.TenantAdmin/Program.cs` (AD-01 single-runtime hosting)
- [ ] T003 [P] Create `tests/Nabadat.IntegrationHub.UnitTests/Nabadat.IntegrationHub.UnitTests.csproj` — xUnit v3 `1.*`, `xunit.runner.visualstudio` `3.*`, FluentAssertions **pinned `6.12.*`**, NSubstitute `5.*`, `Microsoft.Extensions.TimeProvider.Testing` `9.*`; register in the solution
- [ ] T004 [P] Create `tests/Nabadat.IntegrationHub.IntegrationTests/Nabadat.IntegrationHub.IntegrationTests.csproj` — same xUnit/FluentAssertions/NSubstitute pins plus `Testcontainers.PostgreSql` `4.*` and `Microsoft.AspNetCore.Mvc.Testing` `10.*`; register in the solution
- [ ] T005 [P] Add `ClosedXML` to `src/Nabadat.IntegrationHub/Nabadat.IntegrationHub.csproj` for SCR-07's Excel import/export (VR-F09, plan.md Primary Dependencies)
- [ ] T006 [P] Create the E2E folder `tests/Nabadat.E2ETests/IntegrationHub/` and add an `Integration Hub (M-13)` section to `tests/Nabadat.E2ETests/COVERAGE.md` — **no new E2E project**; the existing `Infrastructure/E2ETestBase.cs` harness is reused as-is

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The eight tables, the EF context, the module's DI wiring, the cross-module ports, and
the frontend feature shell — everything every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Domain foundations

- [ ] T007 [P] Create the value-object enums — one type per file — in `src/Nabadat.IntegrationHub/Domain/ValueObjects/`: `Scenario.cs` (SCN-01…05), `CredentialMechanism.cs` (ApiKey, OAuth2), `CredentialStatus.cs` (Active, Revoked), `DataType.cs` (the 13 types VR-T01…T13), `ParameterOrigin.cs` (BuiltIn, Custom), `ResultCode.cs` (E-1001…E-1500 + success codes)
- [ ] T008 [P] Create the eight Domain entities — one type per file — in `src/Nabadat.IntegrationHub/Domain/Entities/`: `Integration.cs`, `Credential.cs`, `ServiceChannel.cs`, `Parameter.cs`, `ChannelParameterAssignment.cs`, `ParameterMapping.cs`, `UnmappedValueOccurrence.cs`, `IntegrationRequestLog.cs`, exactly per data-model.md §1–8
- [ ] T009 [P] Create the consumed stub ports in `src/Nabadat.IntegrationHub/Domain/Interfaces/`: `ISurveyResolutionReader.cs`, `ISurveyDispatchGateway.cs` (M-02, research.md §4.3), `IResponseIngestionGateway.cs` (M-04, §4.4) — dependency-inversion stubs owned by M-13, mirroring M-15's `IKpiScoreReader` precedent
- [ ] T010 [P] Create the published forward contract `src/Nabadat.IntegrationHub/Domain/Interfaces/IParameterCatalogReader.cs` for M-14/M-15/M-16 (CMC-07, contracts/published-interfaces.md)

### Persistence foundations (DB-08 — no EF migrations)

- [ ] T011 Create `src/Nabadat.IntegrationHub/Application/Interfaces/ITenantDbContext.cs` — the eight `DbSet<T>` properties + `SaveChangesAsync` + `ExecuteAsync` (the atomicity seam)
- [ ] T012 Create `src/Nabadat.IntegrationHub/Infrastructure/Persistence/TenantDbContext.cs` implementing `ITenantDbContext`, resolving the tenant schema `tenant_{slug}` (AD-02)
- [ ] T013 [P] Create the eight `IEntityTypeConfiguration<T>` classes in `src/Nabadat.IntegrationHub/Infrastructure/Persistence/Configurations/` — explicit `HasColumnName` per property, FK relationships, and enum→int converters per data-model.md
- [ ] T014 Create `src/Nabadat.IntegrationHub/Infrastructure/Migrations/IntegrationHub_Baseline.sql` — the eight tables (DB-05 mechanism), including `integration_request_logs` with **DB-04 monthly partitioning** and its 90-day retention policy (NFR-8); register the file with `tools/Nabadat.Migrations` and `dotnet build` that tool before running it
- [ ] T015 Wire persistence + all module services into `IntegrationHubServiceCollectionExtensions.cs` (DbContext, `ITenantDbContext`, `TimeProvider`, the stub ports from T009)

### Cross-module adapters

- [ ] T016 [P] Create `src/Nabadat.IntegrationHub/Infrastructure/SurveyBuilderIntegration/RealSurveyRenderServiceAdapter.cs` wrapping M-01's already-shipped `ISurveyRenderService` for SCN-03 (research.md §4.2 — the SRS mis-labelled this "M-03"; corrected in coordination-log.md C-04)
- [ ] T017 [P] Create `src/Nabadat.IntegrationHub/Infrastructure/UserManagementIntegration/DataScopeHttpClient.cs` calling M-10's already-shipped `POST /api/v1/authorization/scope/parameters` (BR-10/CMC-06, research.md §4.1) — typed `HttpClient` with the API-05 error envelope
- [ ] T018 [P] Create the M-02/M-04 no-op stubs in `src/Nabadat.IntegrationHub/Infrastructure/ChannelDispatch/`: `NullSurveyResolutionReader.cs`, `NullSurveyDispatchGateway.cs`, `NullResponseIngestionGateway.cs`

### Test-lane foundations

- [ ] T019 Create `tests/Nabadat.IntegrationHub.IntegrationTests/Infrastructure/IntegrationHubApplicationFactory.cs` — `WebApplicationFactory<Program>` + `IAsyncLifetime`, boots Testcontainers Postgres, applies `IntegrationHub_Baseline.sql`, exposes a per-test `HttpClient` and seeding helpers (first-feature-in-module carve-out, Unit Test Policy rule 12); plus `TestSupport/` shared builders in `tests/Nabadat.IntegrationHub.UnitTests/TestSupport/`
- [ ] T020 Seed the **23 built-in parameters** (all enabled by default, BR-09) as baseline data in `IntegrationHub_Baseline.sql`, and add a seeding helper to the application factory — US1's channel contract and US2's catalogue both assume they already exist

### Frontend foundations

- [ ] T021 Create the frontend feature shell `frontend/src/features/integration-hub/{components,hooks,pages}/` and its API client `frontend/src/features/integration-hub/api.ts` reusing the `callJson` pattern from `tenants/api.ts` — API-05 error envelope, 204/empty-body handling, `Authorization: Bearer <session_token>`, and **integer-enum normalize helpers** at the response boundary (CLAUDE.md Backend Integration §1: assume .NET enums arrive as ints)
- [ ] T022 Register the eight M-13 routes in `frontend/src/App.tsx` — `/integration-hub/integrations`, `…/integrations/new`, `…/integrations/:id`, `/integration-hub/service-channels`, `…/service-channels/new`, `…/service-channels/:id`, `/integration-hub/parameters`, `/integration-hub/mappings`, `/integration-hub/logs`
- [ ] T023 Register the Integration Hub nav group in `frontend/src/components/app-sidebar.tsx` — add the items to `NAV_ITEMS` under a domain-matching **existing** category group (never appended to an unrelated group, never an "Other" bucket) and add each key to the P-01 and P-07 allowlists in `ROLE_NAV_KEYS`
- [ ] T024 [P] Add the EN + AR i18n blocks for the module to `frontend/src/i18n/{en,ar}.json` — Arabic written **natively in فصحى**, never translated from the English (CLAUDE.md Brand Voice); use `{{n}}` with a pre-formatted `.toLocaleString()` string for counts, never i18next's `{{count}}`

**Checkpoint**: Foundation ready — user story implementation can begin.

---

## Phase 3: User Story 1 — Define a Service Channel and its Parameter Contract (Priority: P1) 🎯 MVP

**Goal**: A CX Manager (P-01) creates a service channel with bilingual EN/AR names, a live-sanitised
channel ID, and a Supported/Required parameter contract over the 23 seeded built-ins. Nothing else
in the module is testable without a channel to attach an integration to.

**Independent Test**: `/integration-hub/service-channels` → **New service channel** → type EN/AR
names, type "My kiosk #1" and verify live sanitisation to "Mykiosk1" capped under 20 chars → toggle
**Supported** on a few built-ins, tick **Required** on a subset → **Create channel** → the channel
appears in the SCR-03 list with correct supported/required counts and an Active badge.

### Unit Tests for User Story 1 (REQUIRED — write FIRST, must FAIL before implementation) ⚠️

- [ ] T025 [P] [US1] Unit tests for `ChannelIdSanitizer` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ChannelIdSanitizerTests.cs` — `Sanitize("My kiosk #1")` → `"Mykiosk1"` (spaces and `#` stripped, case preserved); `Sanitize(19 valid chars + 1 more)` → truncated to 19 (VR-F04)
- [ ] T026 [P] [US1] Unit tests for `ChannelIdUniquenessValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ChannelIdUniquenessValidatorTests.cs` — `Validate(existingIds=["KIOSK-01"], id="kiosk-01")` → `Invalid("A channel with this ID already exists")`, case-insensitive per tenant (VR-F04)
- [ ] T027 [P] [US1] Unit tests for `ChannelIdLockGuard` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ChannelIdLockGuardTests.cs` — `IsLocked(channel, hasLoggedSuccessfulRequest=true)` → `true` and a subsequent `PUT` changing `channelId` is rejected server-side; `IsLocked(…, false)` → `false`, editable (BR-05)
- [ ] T028 [P] [US1] Unit tests for `ParameterContractDependencyRule` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ParameterContractDependencyRuleTests.cs` — `ApplyDependency(supported=false, required=true)` → `(false, false)`; `ApplyDependency(supported=true, required=false→true)` → `(true, true)` (FR-S4-04)
- [ ] T029 [P] [US1] Unit tests for `ChannelNameValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ChannelNameValidatorTests.cs` — `Validate(nameEn="", nameAr="جيد")` → `Invalid("Channel name · EN is required")`; EN ≤ 50 chars + unique per tenant (VR-F02); AR required (VR-F03)

### Red Checkpoint for User Story 1 (MANDATORY — gate between tests and implementation) 🔴

- [ ] T030R [US1] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. Expected valid red: **compile error**, since `src/Nabadat.IntegrationHub/Application/Channels/` does not exist yet. Paste the failing transcript as evidence, then commit the red baseline via `/speckit-git-commit` before reading or writing any implementation task. Non-parallel

### Implementation for User Story 1

- [ ] T031 [P] [US1] Implement `ChannelIdSanitizer` in `src/Nabadat.IntegrationHub/Application/Channels/ChannelIdSanitizer.cs` — strip everything outside `[A-Za-z0-9-]`, cap at 19 chars (VR-F04)
- [ ] T032 [P] [US1] Implement `ChannelIdUniquenessValidator` in `src/Nabadat.IntegrationHub/Application/Channels/ChannelIdUniquenessValidator.cs` — case-insensitive per tenant
- [ ] T033 [P] [US1] Implement `ChannelIdLockGuard` in `src/Nabadat.IntegrationHub/Application/Channels/ChannelIdLockGuard.cs` — locks the ID once the channel has logged its first 2xx request (BR-05)
- [ ] T034 [P] [US1] Implement `ParameterContractDependencyRule` in `src/Nabadat.IntegrationHub/Application/Channels/ParameterContractDependencyRule.cs`
- [ ] T035 [P] [US1] Implement `ChannelNameValidator` in `src/Nabadat.IntegrationHub/Application/Channels/ChannelNameValidator.cs`
- [ ] T036 [US1] Create the data-access port `IServiceChannelStore` in `src/Nabadat.IntegrationHub/Application/Channels/Interfaces/IServiceChannelStore.cs` and its implementation `ServiceChannelStore.cs` in `Application/Channels/` — depends on `ITenantDbContext`, write methods self-persist (this port is the unit-test mock seam)
- [ ] T037 [US1] Implement `ServiceChannelService` in `src/Nabadat.IntegrationHub/Application/Channels/ServiceChannelService.cs` — create/edit/list, composing T031–T036; wraps the channel + `ChannelParameterAssignment` rows in one `ITenantDbContext.ExecuteAsync` (atomic contract write); takes `TimeProvider` by injection (depends on T031–T036)
- [ ] T038 [P] [US1] Create the request/response DTOs in `src/Nabadat.IntegrationHub/Api/Contracts/` — `CreateServiceChannelRequest.cs`, `UpdateServiceChannelRequest.cs`, `ServiceChannelResponse.cs`, `ServiceChannelListItemResponse.cs` (supported/required/integration counts), one type per file
- [ ] T039 [US1] Implement `ServiceChannelsController` in `src/Nabadat.IntegrationHub/Api/Controllers/ServiceChannelsController.cs` — `GET`/`POST` `/api/v1/integration-hub/service-channels`, `PUT …/{id}`, cursor pagination (API-04), API-05 error envelope. **No `DELETE` route exists** (BR-07) (depends on T037, T038)
- [ ] T040 [US1] Emit the M-17 audit events `channel.created` / `channel.updated` / `channel.id_changed` from `src/Nabadat.IntegrationHub/Application/Events/` — actor, tenant, timestamp, entity, before/after summary (BR-21)
- [ ] T041 [P] [US1] Implement `useServiceChannels` in `frontend/src/features/integration-hub/hooks/useServiceChannels.ts` — list/create/update via `api.ts`, loading + error state
- [ ] T042 [US1] Build **SCR-03** `AllServiceChannelsPage` in `frontend/src/features/integration-hub/pages/AllServiceChannelsPage.tsx` — table in an `overflow-hidden rounded-lg border` card, `TableHeader` sticky (no `bg-background`), row actions as **icon-only ghost buttons in a `w-16 text-center` unlabelled column**, filter row `flex flex-col gap-4 sm:flex-row sm:items-end` with `sm:max-w-sm` search + `sm:w-48` selects using `flex flex-col gap-1.5`, skeleton/empty/error/access-denied states (FR-GBL-02), and **no delete control anywhere** (BR-07)
- [ ] T043 [US1] Build **SCR-04** `ServiceChannelFormPage` in `frontend/src/features/integration-hub/pages/ServiceChannelFormPage.tsx` — EN/AR name + description + Active toggle; channel-ID input sanitising live as typed with `maxlength=19`, rendered **read-only with the lock explanation** once locked (BR-05); unsaved-changes guard (FR-GBL-03); one filled primary CTA, every other action `variant="secondary"`, Cancel `variant="outline"`
- [ ] T044 [US1] Build the parameter-contract editor `frontend/src/features/integration-hub/components/ParameterContractEditor.tsx` — a Supported/Required row per built-in where clearing **Supported** clears and disables **Required**, plus the live contract-summary alert whose counts track the toggles (FR-S4-03/04)

### Integration & API / Scenario Tests for User Story 1 (run at the per-story checkpoint) 🐳

- [ ] T045 [P] [US1] API tests for `POST /api/v1/integration-hub/service-channels` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/ServiceChannels/CreateServiceChannelEndpointTests.cs` — valid EN/AR + ID + contract rows → 201 with the `channel.created` audit row; duplicate ID case-insensitive → 409 with the uniqueness message
- [ ] T046 [P] [US1] API tests for `PUT /api/v1/integration-hub/service-channels/{id}` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/ServiceChannels/UpdateServiceChannelEndpointTests.cs` — edit channel ID **before** first success → 200 and the endpoint path changes; edit **after** first 2xx → 409 (ID locked); `active=false` → 200
- [ ] T047 [P] [US1] API test for `GET /api/v1/integration-hub/service-channels` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/ServiceChannels/ListServiceChannelsEndpointTests.cs` — list reflects supported/required/integration counts; assert no `DELETE` route is routable (404/405, BR-07)

> `scenario-test: not-needed` per spec.md US1 — channel create/edit is single-endpoint; the
> deactivation-cascade is asserted end-to-end in US4's scenario test instead, not duplicated here.

### E2E (Browser) Tests for User Story 1 🎭

> Authored AFTER the pages exist, with the `e2e-testing` skill. Run at the checkpoint. NO Red Checkpoint.

- [ ] T048 [P] [US1] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/ServiceChannelTests.cs` inheriting `E2ETestBase` — `ServiceChannel_sanitizes_id_live_as_typed_and_caps_at_19_chars` (AC-S4-01), `ServiceChannel_locks_id_field_after_first_successful_request` (AC-S4-02), `ServiceChannel_required_toggle_disables_when_supported_is_off` (AC-S4-03), `ServiceChannel_blocks_save_on_duplicate_name_or_id` (VR-F02/F04), `ServiceChannel_list_shows_no_delete_action_anywhere` (BR-07), `ServiceChannel_it_admin_sees_read_only_view` (BR-24); select by `data-testid`/role, never translated text; add a `COVERAGE.md` row per test

**Build gate (MANDATORY before declaring the checkpoint reached)**: `dotnet test tests/Nabadat.IntegrationHub.UnitTests` → 0 failures; `dotnet test tests/Nabadat.IntegrationHub.IntegrationTests` → 0 failures (Docker up); `npm run build` in `frontend/` green; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ServiceChannelTests"` green (stack up + `E2E_BASE_URL` set).

**Checkpoint**: User Story 1 is fully functional and independently testable — the module's MVP.

### Click-through Parity for User Story 1 🎨

> **Owner: the frontend developer, run manually** — not an automatic step, so the defect list lands
> when someone is ready to triage it. **Preconditions:** (1) SCR-03/04 were implemented
> **click-through-blind** — if either page was ported or copied the run is VOID and is reported
> `NOT AUDITED`, not clean; (2) the click-through checkout is served and the product dev stack is up
> and signed in (paths in `.claude/skills/clickthrough-parity/reference.json`).

- [ ] T049P [US1] Run `/clickthrough-parity 006-integration-hub phase 3` over `/integration-hub/service-channels`, `…/new` and `…/:id`, and triage the report — the click-through is the source of truth. Expect real defects (mostly copy, placeholders, and layout chrome the spec does not describe). Apply the ones the frontend lead accepts with `--fix`; take every **Needs discussion** item (presence / placement / a deliberate business divergence, e.g. the FR-GBL-03 unsaved-changes guard the click-through lacks) to the design owner instead — `--fix` must never touch those. Record the result in `.claude/skills/clickthrough-parity/route-map.md`

---

## Phase 4: User Story 2 — Manage the Parameter Catalogue (Priority: P1)

**Goal**: A CX Manager governs the tenant's parameter catalogue — the 23 seeded built-ins plus custom
parameters — with bilingual names, a locked-after-first-use `snake_case` API field name, one of
thirteen data types (Range carrying min/max/unit), usage flags, and channel assignments.

**Independent Test**: `/integration-hub/parameters` → the "All · 23" tab shows every built-in enabled
→ **New parameter** → type EN name "Wait Time" and verify the API field auto-suggests `wait_time` →
select type **Range**, set min/max/unit → set usage flags → assign to a channel → **Create parameter**
→ the Custom parameter appears with its flags rendered as check/dash glyphs.

### Unit Tests for User Story 2 (REQUIRED — write FIRST, must FAIL) ⚠️

- [ ] T050 [P] [US2] Unit tests for `ApiFieldNameSuggester` in `tests/Nabadat.IntegrationHub.UnitTests/Parameters/ApiFieldNameSuggesterTests.cs` — `Suggest("Wait Time")` → `"wait_time"`; `Suggest("Été & Café!")` → non-`[a-z0-9\s]` characters **stripped** (no transliteration), yielding a valid `snake_case` candidate
- [ ] T051 [P] [US2] Unit tests for `ApiFieldNameUniquenessValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Parameters/ApiFieldNameUniquenessValidatorTests.cs` — `Validate(existingFields=["wait_time"], field="wait_time", includeDisabled=true)` → `Invalid("This API field name is already in use")`; unique across built-in + custom + enabled + **disabled** (VR-F06)
- [ ] T052 [P] [US2] Unit tests for `ApiFieldNameLockGuard` in `tests/Nabadat.IntegrationHub.UnitTests/Parameters/ApiFieldNameLockGuardTests.cs` — `IsLocked(parameter, hasReceivedRequest=true)` → `true` (BR-11); re-enabling a previously disabled parameter never frees or changes the locked field name (BR-09/BR-11 are independent axes)
- [ ] T053 [P] [US2] Unit tests for `RangeConfigValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Parameters/RangeConfigValidatorTests.cs` — Min required, Max required, `Validate(min=100, max=50)` → `Invalid("Minimum must be less than Maximum")` (VR-F07)
- [ ] T054 [P] [US2] Unit tests for `ParameterDisableImpactScanner` in `tests/Nabadat.IntegrationHub.UnitTests/Parameters/ParameterDisableImpactScannerTests.cs` — `ScanReferences(parameterId="service", scopeFilters=[…], channelContracts=[…])` → the **full** non-empty reference list feeding Dialog D-6 (all three consumer kinds at once, not just the first found, BR-10); `ScanReferences("unused_custom_param")` → empty → disable proceeds with no dialog
- [ ] T055 [P] [US2] Unit tests for `BuiltInParameterGuard` in `tests/Nabadat.IntegrationHub.UnitTests/Parameters/BuiltInParameterGuardTests.cs` — `Guard(builtIn=true, action=Delete)` → `throws InvalidOperationException`; `Guard(builtIn=true, action=Disable)` → allowed (BR-09)

### Red Checkpoint for User Story 2 🔴

- [ ] T056R [US2] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. Valid red: compile error (`Application/Parameters/` absent) or assertion failure once the types are stubbed. Paste the transcript and commit the red baseline before implementation. Non-parallel

### Implementation for User Story 2

- [ ] T057 [P] [US2] Implement `ApiFieldNameSuggester` in `src/Nabadat.IntegrationHub/Application/Parameters/ApiFieldNameSuggester.cs` — lowercase, spaces → `_`, strip invalid characters
- [ ] T058 [P] [US2] Implement `ApiFieldNameUniquenessValidator` in `src/Nabadat.IntegrationHub/Application/Parameters/ApiFieldNameUniquenessValidator.cs` — includes disabled parameters (VR-F06)
- [ ] T059 [P] [US2] Implement `ApiFieldNameLockGuard` in `src/Nabadat.IntegrationHub/Application/Parameters/ApiFieldNameLockGuard.cs` (BR-11)
- [ ] T060 [P] [US2] Implement `RangeConfigValidator` in `src/Nabadat.IntegrationHub/Application/Parameters/RangeConfigValidator.cs` (VR-F07)
- [ ] T061 [P] [US2] Implement `BuiltInParameterGuard` in `src/Nabadat.IntegrationHub/Application/Parameters/BuiltInParameterGuard.cs` (BR-09)
- [ ] T062 [US2] Implement `ParameterDisableImpactScanner` in `src/Nabadat.IntegrationHub/Application/Parameters/ParameterDisableImpactScanner.cs` — scans M-10 data-scope filters, rule builders, and channel contracts, returning every reference (BR-10)
- [ ] T063 [US2] Create the data-access port `IParameterStore` in `src/Nabadat.IntegrationHub/Application/Parameters/Interfaces/IParameterStore.cs` + `ParameterStore.cs` in `Application/Parameters/`, and implement the published `IParameterCatalogReader` (T010) over it
- [ ] T064 [US2] Implement `ParameterService` in `src/Nabadat.IntegrationHub/Application/Parameters/ParameterService.cs` — create/edit/enable/disable/list, composing T057–T063, atomic via `ExecuteAsync`, clock via `TimeProvider` (depends on T057–T063)
- [ ] T065 [US2] Implement `DataScopeContractPublisher` in `src/Nabadat.IntegrationHub/Application/Parameters/DataScopeContractPublisher.cs` — publishes parameter definitions to M-10 through `DataScopeHttpClient` (T017) on create/enable/disable (BR-10/CMC-06, a **REAL** already-shipped integration)
- [ ] T066 [P] [US2] Create the DTOs in `src/Nabadat.IntegrationHub/Api/Contracts/` — `CreateParameterRequest.cs`, `PatchParameterRequest.cs`, `ParameterResponse.cs`, `ParameterReferenceListResponse.cs` (the D-6 payload), one type per file
- [ ] T067 [US2] Implement `ParametersController` in `src/Nabadat.IntegrationHub/Api/Controllers/ParametersController.cs` — `GET` (with AND-combined `origin` + `type` filters), `POST`, `PATCH …/{id}`; a `PATCH { enabled:false }` on a referenced parameter returns the reference list so the client can render D-6; **no `DELETE` route exists** (BR-09) (depends on T064, T066)
- [ ] T068 [US2] Emit `parameter.created` / `parameter.updated` / `parameter.disabled` audit events from `Application/Events/` (BR-21)
- [ ] T069 [P] [US2] Implement `useParameters` in `frontend/src/features/integration-hub/hooks/useParameters.ts`
- [ ] T070 [US2] Build **SCR-05** `AllParametersPage` in `frontend/src/features/integration-hub/pages/AllParametersPage.tsx` — **`<TabsListSegmented>` with `<TabsCountPill>`** for All / Built-in / Custom (counts **global**, never narrowed by the filters below), an AND-combined type filter, and boolean usage-flag columns rendered as **`Check`/`Minus` Lucide glyphs in brand cyan** (`text-nb-cyan-700 dark:text-nb-cyan-300`) — never the literal `✓`/`—` characters, and never semantic green (a capability flag is not a health state)
- [ ] T071 [US2] Build **SCR-06** `ParameterDrawer` in `frontend/src/features/integration-hub/components/ParameterDrawer.tsx` — a `Sheet` with the pinned-header / `min-h-0 flex-1 overflow-y-auto` body / pinned-footer pattern; EN/AR names, auto-suggesting API-field input (read-only once locked), data-type select swapping the **Range card** ↔ the **List panel**, usage flags, channel assignments, unsaved-changes guard (FR-GBL-03); hints at `text-xs leading-relaxed`, validation errors stay `text-sm text-destructive`

### Integration & API Tests for User Story 2 🐳

- [ ] T072 [P] [US2] API tests for `POST /api/v1/integration-hub/parameters` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Parameters/CreateParameterEndpointTests.cs` — create a custom Range parameter → 201 + `parameter.created` audit; duplicate API field **including against a disabled parameter** → 409
- [ ] T073 [P] [US2] API tests for `PATCH /api/v1/integration-hub/parameters/{id}` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Parameters/PatchParameterEndpointTests.cs` — `{enabled:false}` on an unreferenced parameter → 200 with no reference list; on a channel-contract-referenced parameter → 200 **with** the reference list for D-6 (BR-10)
- [ ] T074 [P] [US2] API test for `GET /api/v1/integration-hub/parameters?origin=custom&type=range` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Parameters/ListParametersEndpointTests.cs` — combined AND filter; assert any delete-shaped call → 404/405 (BR-09)

> `scenario-test: not-needed` per spec.md US2 — create/enable/disable are single-endpoint operations.

### E2E (Browser) Tests for User Story 2 🎭

- [ ] T075 [P] [US2] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/ParameterCatalogueTests.cs` — `Parameters_type_switch_between_range_and_list_shows_correct_panel` (AC-S6-01), `Parameters_api_field_auto_suggests_from_english_name` (AC-S6-02), `Parameters_blocks_save_on_duplicate_api_field_including_disabled` (AC-S6-03), `Parameters_origin_and_type_filters_combine_with_AND` (AC-S5-01), `Parameters_disable_shows_impact_warning_when_referenced` (AC-S5-02), `Parameters_builtin_row_has_no_delete_action_and_locked_api_field` (BR-09), `Parameters_range_validation_blocks_min_greater_than_max` (VR-F07); update `COVERAGE.md`

**Build gate (MANDATORY)**: unit + integration projects green; `npm run build` green; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ParameterCatalogueTests"` green.

**Checkpoint**: US1 and US2 both work independently.

### Click-through Parity for User Story 2 🎨

> Owner: the frontend developer, run manually. Preconditions: SCR-05/06 built click-through-blind
> (a ported page is `NOT AUDITED`, not clean); click-through checkout served + dev stack signed in.

- [ ] T076P [US2] Run `/clickthrough-parity 006-integration-hub phase 4` over `/integration-hub/parameters` (SCR-05 plus the SCR-06 drawer, which has no route of its own) and triage the report. Apply accepted defects with `--fix`; escalate every **Needs discussion** item to the design owner — `--fix` must never touch those. Record the result in `.claude/skills/clickthrough-parity/route-map.md`

---

## Phase 5: User Story 3 — Onboard an Integration via the New/Edit Wizard (Priority: P1)

**Goal**: A Tenant IT Administrator (P-07) provisions an integration in a 3-step wizard — Step 1 name +
active channel + exactly one of five scenarios; Step 2 API Key **or** OAuth 2.0 client-credentials with
show-once credentials; Step 3 endpoint preview, accepted-parameters contract, and result-code catalogue.

**Independent Test**: `/integration-hub/integrations` → **New integration** → Step 1: name "Core Services
Bus — Survey Dispatch", pick the US1 channel, select **Dispatch via Nabadat** → Continue → Step 2: **API
key**, label it, **Generate new API key** → Dialog D-1 shows the plaintext once → **Done** → Continue →
Step 3: the endpoint preview shows method, path, and highlighted channel-ID token, and the accepted-
parameters table matches US1's contract → **Create integration** → SCR-01 shows the row with zero traffic
and "—" error rate.

### Unit Tests for User Story 3 (REQUIRED — write FIRST, must FAIL) ⚠️

- [ ] T077 [P] [US3] Unit tests for `IntegrationNameValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/IntegrationNameValidatorTests.cs` — `Validate(name="", …)` → `Invalid("Integration name is required")`; `Validate(existingNames=["Core Bus"], name="core bus")` → `Invalid` (**case-insensitive**, VR-F01); ≤ 100 chars
- [ ] T078 [P] [US3] Unit tests for `ScenarioSelectionRule` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/ScenarioSelectionRuleTests.cs` — `SelectScenario(current=SCN-01, attemptSecond=SCN-03)` → rejected; exactly one scenario field per integration, not a multi-select (BR-02)
- [ ] T079 [P] [US3] Unit tests for `ApiKeyGenerationService` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/ApiKeyGenerationServiceTests.cs` — `Generate(keyLabel="Core Bus Key")` returns the plaintext **once** and the stored value ≠ plaintext (hashed/encrypted, NFR-6); a later retrieval returns only the masked form; `Generate(existingActiveKey=K1, newLabel="K2")` → K1 implicitly revoked, K2 active (BR-16)
- [ ] T080 [P] [US3] Unit tests for `OAuthClientGenerationService` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/OAuthClientGenerationServiceTests.cs` — grant type is **always** `client_credentials` and token TTL **always** 15 minutes, neither configurable via input (BR-17); selected scopes applied
- [ ] T081 [P] [US3] Unit tests for `CredentialRevocationService` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/CredentialRevocationServiceTests.cs` — `Revoke(K1)` → immediate; a subsequent auth check for K1 → `Invalid` → maps to `401 E-1401`; no un-revoke operation exists
- [ ] T082 [P] [US3] Unit tests for `WizardDraftDiscardPolicy` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/WizardDraftDiscardPolicyTests.cs` — `DiscardOnCancel(generatedCredential=K1, wizardCancelled=true)` → K1 is never persisted or usable (BR-25)

### Red Checkpoint for User Story 3 🔴

- [ ] T083R [US3] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. Valid red: compile error (`Application/Integrations/` absent) or assertion failure once stubbed. Paste the transcript and commit the red baseline before implementation. Non-parallel

### Implementation for User Story 3

- [ ] T084 [P] [US3] Implement `IntegrationNameValidator` in `src/Nabadat.IntegrationHub/Application/Integrations/IntegrationNameValidator.cs` (VR-F01)
- [ ] T085 [P] [US3] Implement `ScenarioSelectionRule` in `src/Nabadat.IntegrationHub/Application/Integrations/ScenarioSelectionRule.cs` (BR-02)
- [ ] T086 [US3] Implement `ApiKeyGenerationService` in `src/Nabadat.IntegrationHub/Application/Integrations/ApiKeyGenerationService.cs` — cryptographically random key, hashed/encrypted at rest, plaintext returned exactly once; regeneration implicitly revokes the prior active key (BR-16, NFR-6)
- [ ] T087 [US3] Implement `OAuthClientGenerationService` in `src/Nabadat.IntegrationHub/Application/Integrations/OAuthClientGenerationService.cs` — `client_id`/`client_secret` hashed at rest; `client_credentials` + 15-minute TTL fixed **in code**, never surfaced as input (BR-17)
- [ ] T088 [US3] Implement `CredentialRevocationService` in `src/Nabadat.IntegrationHub/Application/Integrations/CredentialRevocationService.cs` — immediate, irreversible; no un-revoke method on the surface
- [ ] T089 [P] [US3] Implement `WizardDraftDiscardPolicy` in `src/Nabadat.IntegrationHub/Application/Integrations/WizardDraftDiscardPolicy.cs` (BR-25)
- [ ] T090 [US3] Create the data-access ports + implementations `IIntegrationStore`/`IntegrationStore` and `ICredentialStore`/`CredentialStore` in `src/Nabadat.IntegrationHub/Application/Integrations/{Interfaces/,}` — depend on `ITenantDbContext`
- [ ] T091 [US3] Implement `IntegrationService` in `src/Nabadat.IntegrationHub/Application/Integrations/IntegrationService.cs` — create/edit/list/get, endpoint-path provisioning from the selected channel, rejecting an **inactive** channel server-side (defense in depth, FR-S2-02); atomic integration + credential write via `ExecuteAsync` (depends on T084–T090)
- [ ] T092 [P] [US3] Create the DTOs in `src/Nabadat.IntegrationHub/Api/Contracts/` — `CreateIntegrationRequest.cs`, `UpdateIntegrationRequest.cs`, `IntegrationResponse.cs`, `GenerateCredentialRequest.cs`, `GeneratedCredentialResponse.cs` (the show-once payload), one type per file
- [ ] T093 [US3] Implement `IntegrationsController` in `src/Nabadat.IntegrationHub/Api/Controllers/IntegrationsController.cs` — `GET`/`POST` `/api/v1/integration-hub/integrations`, `PUT …/{id}`, `POST …/{id}/credentials`, `POST …/{id}/credentials/revoke`; **no `DELETE` route ever** (Status Lifecycle) (depends on T091, T092)
- [ ] T094 [US3] Emit `integration.created` / `integration.updated` / `credential.generated` / `credential.revoked` audit events from `Application/Events/` (BR-21)
- [ ] T095 [P] [US3] Implement `useIntegrations` in `frontend/src/features/integration-hub/hooks/useIntegrations.ts`
- [ ] T096 [US3] Build **SCR-02** `IntegrationWizardPage` in `frontend/src/features/integration-hub/pages/IntegrationWizardPage.tsx` using the shared **`WizardStepper`** from `@/components/ui/wizard-stepper` (never a hand-rolled row of numbered circles) — validation-gated, so pass **no** `onClick` for unreached steps; page header carries Cancel (`variant="outline"`) only, the step's primary action lives in the footer
- [ ] T097 [US3] Build Step 1 in `frontend/src/features/integration-hub/components/WizardStepBasics.tsx` — name, active-channels-only `Select` (FR-S2-02), and the five scenarios as **choice cards**: `role="radiogroup"` wrapping `role="radio"` buttons, each with a `size-9` rounded icon tile, a radio dot (selection never conveyed by border colour alone), a semibold label and a 12px description, gridded `sm:grid-cols-2 xl:grid-cols-5` (BR-02)
- [ ] T098 [US3] Build Step 2 in `frontend/src/features/integration-hub/components/WizardStepAuth.tsx` — mechanism switch showing/hiding the API-key vs OAuth field sets (FR-S2-04), OAuth scopes as **chips** not a stacked checkbox column, and Dialogs **D-1** (API key show-once) / **D-2** (OAuth client show-once) with `flex max-h-[90vh] flex-col` + `shrink-0` header/footer + a `min-h-0 flex-1 overflow-y-auto` body, and `sm:max-w-lg` (never a bare `max-w-lg`). **No expiry / sandbox / IP-allow-list / grant-type / token-lifetime fields exist** (`[PO-G13]`, BR-17)
- [ ] T099 [US3] Build Step 3 in `frontend/src/features/integration-hub/components/WizardStepReview.tsx` — endpoint preview in a **fixed dark code block** (`bg-nb-navy-800 dark:bg-nb-dark`, mono, `dir="ltr"`) with a Copy button and the channel-ID token highlighted, the accepted-parameters table from the channel contract, and the result-code catalogue; both the path token and the table re-render when the Step-1 channel changes (FR-S2-07/08)
- [ ] T100 [US3] Build **SCR-01** `AllIntegrationsPage` list portion in `frontend/src/features/integration-hub/pages/AllIntegrationsPage.tsx` — the table, the **New integration** primary CTA, and a new row rendering zero traffic with a "—" error rate (AC-S1-03). *(The stat tiles and error-rate colouring belong to US5.)*

### Integration & API / Scenario Tests for User Story 3 🐳

- [ ] T101 [P] [US3] API tests for `POST /api/v1/integration-hub/integrations` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Integrations/CreateIntegrationEndpointTests.cs` — SCN-01 + API key → 201 with endpoint provisioned and `integration.created` + `credential.generated` audit rows; OAuth + scopes → 201; duplicate name → 409 (VR-F01); deactivated channel supplied → 400/409
- [ ] T102 [P] [US3] API tests for the credential routes in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Integrations/CredentialEndpointTests.cs` — `POST …/{id}/credentials/revoke` → 200 + `credential.revoked`, and a subsequent API call with the old key → `401 E-1401`; `POST …/{id}/credentials` while one is active → 200 with the old key implicitly revoked (BR-16)
- [ ] T103 [P] [US3] API test for `PUT /api/v1/integration-hub/integrations/{id}` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Integrations/UpdateIntegrationEndpointTests.cs` — changing the service channel → 200 and the response's endpoint path reflects the new channel
- [ ] T104 [US3] Scenario test `IntegrationOnboardingScenarioTests` in `tests/Nabadat.IntegrationHub.IntegrationTests/Scenarios/Integrations/IntegrationOnboardingScenarioTests.cs` — `POST /integrations` (API key) → `GET /integrations/{id}` (endpoint + contract shape) → a **live call** to the provisioned endpoint (`202 ACCEPTED`, per US4) → `POST …/credentials/revoke` → repeat the call (`401 E-1401`). Carries the integration id + credential across 4+ calls; asserts the final state: exactly one `integration.created`, one `credential.generated`, one `credential.revoked`, in order

### E2E (Browser) Tests for User Story 3 🎭

- [ ] T105 [P] [US3] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/IntegrationWizardTests.cs` — `Wizard_switches_auth_fields_when_mechanism_changes` (AC-S2-01), `Wizard_api_key_dialog_never_shows_plaintext_again_after_done` (AC-S2-02), `Wizard_endpoint_and_contract_preview_update_when_channel_changes` (AC-S2-04), `Wizard_only_offers_one_scenario_selection_at_a_time` (BR-02), `Wizard_channel_select_excludes_inactive_channels` (FR-S2-02), `Wizard_cancel_discards_generated_credential_and_draft` (BR-25), `Wizard_blocks_step_advance_on_missing_required_field` (VR-F01/F10), `Integrations_new_row_shows_zero_traffic_and_dash_error_rate` (AC-S1-03); update `COVERAGE.md`

**Build gate (MANDATORY)**: unit + integration projects green (Docker up); `npm run build` green; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~IntegrationWizardTests"` green.

**Checkpoint**: US1–US3 all work independently. An integration now exists to receive traffic.

### Click-through Parity for User Story 3 🎨

> Owner: the frontend developer, run manually. Preconditions: SCR-01/02 built click-through-blind
> (a ported page is `NOT AUDITED`, not clean); click-through checkout served + dev stack signed in.

- [ ] T106P [US3] Run `/clickthrough-parity 006-integration-hub phase 5` over `/integration-hub/integrations`, `…/new` and `…/:id` (the 3-step wizard) and triage the report. Apply accepted defects with `--fix`; escalate every **Needs discussion** item — especially any step-order or scenario-card placement difference — to the design owner. Record the result in `.claude/skills/clickthrough-parity/route-map.md`

---

## Phase 6: User Story 4 — Process Inbound API Requests (Priority: P1) — headless Feature 0

**Goal**: The headless runtime every provisioned endpoint runs on. A caller sends the service channel ID
as the **only** mandatory path parameter (BR-03) plus free key–value pairs; the request passes the
ordered, atomic 8-step pipeline, then is handed to the scenario's downstream owner or returns the
requested artifact directly.

**Independent Test**: Using the US3 integration (SCN-01, API key), send a valid `POST` with all
channel-required parameters and a valid key → `202 ACCEPTED` with a `request_id`, visible in SCR-08
within 60 s. Then send a request missing a required parameter → the **whole** request rejected
`400 E-1002` naming the missing field, and nothing forwarded to M-02.

> **`e2e-tests: skipped`** per spec.md US4 — Feature 0 is an explicitly headless system feature with no
> admin-console screen; there is no browser flow to drive. Its *visibility* to a human (SCR-08) is
> covered by US5's E2E suite. **No E2E and no click-through-parity subsection is emitted for this phase.**

### Unit Tests for User Story 4 (REQUIRED — write FIRST, must FAIL) ⚠️

- [ ] T107 [P] [US4] Unit tests for `RequestValidationPipeline` in `tests/Nabadat.IntegrationHub.UnitTests/Requests/RequestValidationPipelineTests.cs` — the ordered, atomic 8-step pipeline (TLS → auth → rate limit → payload size → channel resolution → channel-active → required-params → type/validation) short-circuits on the **first** failure: `Process(request, authInvalid=true, payloadTooLarge=true)` → `401 E-1401` (auth wins, **not** `413`); no partial processing (FR-F0-02)
- [ ] T108 [P] [US4] Unit tests for `ResultCodeMapper` in `tests/Nabadat.IntegrationHub.UnitTests/Requests/ResultCodeMapperTests.cs` — every pipeline outcome maps to the normative catalogue (`E-1001`, `E-1002`, `E-1003`, `E-1004`, `E-1401`, `E-1413`, `E-1429`, `E-1500`, `202`, `200`) with the **exact** message copy patterns, incl. `"Required parameter 'mobile' is missing for service channel E-SERVICES-PORTAL."`
- [ ] T109 [P] [US4] Unit tests for `ChannelContractRequiredFieldChecker` in `tests/Nabadat.IntegrationHub.UnitTests/Requests/ChannelContractRequiredFieldCheckerTests.cs` — the **channel contract**, not the parameter-level default, is authoritative (BR-08); `Process(request={missing:"mobile"})` → `400 E-1002`
- [ ] T110 [P] [US4] Unit tests for `ParameterTypeValidator` and the 13 per-type validators in `tests/Nabadat.IntegrationHub.UnitTests/Requests/ParameterTypeValidatorTests.cs` — `Validate(type=Phone, value="07701")` → `Invalid` → `422 E-1003` with `"Value '07701' for 'mobile' failed validation rule for type Phone."`; `Validate(type=Phone, value="+962770123456")` → `Valid` (E.164, 8–15 digits after `+`); `Validate(type=Range, value=150, min=0, max=100)` → `Invalid` (inclusive bounds); `Validate(type=List, value="anything-unmapped")` → **`Valid`** (membership NOT enforced at ingestion, VR-T06/BR-12); plus the remaining types VR-T01…T13
- [ ] T111 [P] [US4] Unit tests for `UnregisteredParameterStore` in `tests/Nabadat.IntegrationHub.UnitTests/Requests/UnregisteredParameterStoreTests.cs` — `Process(request={extra:"loyalty_tier"})` succeeds, the pair is stored **raw** and flagged unregistered for the log detail, and is excluded from every report/dashboard/filter/rule builder (AC-F0-03, BR-14)
- [ ] T112 [P] [US4] Unit tests for `IdempotencyKeyResolver` in `tests/Nabadat.IntegrationHub.UnitTests/Requests/IdempotencyKeyResolverTests.cs` — keys on `(tenant, channelId, transaction_id)`; a repeat is accepted and writes a **new log entry** but triggers no second downstream dispatch/store (AC-F0-04, BR-18/F0.7). **No fixed retention window is asserted** — an accepted limitation per the 2026-07-27 clarification, not an engineered SLA
- [ ] T113 [P] [US4] Unit tests for `AllowedOriginsWhitelistStore` in `tests/Nabadat.IntegrationHub.UnitTests/Requests/AllowedOriginsWhitelistStoreTests.cs` — `Resolve(origin="https://evil.example", whitelist=["https://trusted.example"])` → refused. M-13 only **manages** this configuration for M-03's rendering endpoint to enforce at browser-load time; it never receives the browser's origin-bearing request (Clarifications 2026-07-27)
- [ ] T114 [P] [US4] Unit tests for `SurveyLinkExpiryCalculator` in `tests/Nabadat.IntegrationHub.UnitTests/Requests/SurveyLinkExpiryCalculatorTests.cs` — `ComputeExpiry(issuedAt=T, override=null)` → `T + 24h` (F0.8), override honoured per FR-S2-10; time supplied by an injected `FakeTimeProvider`, never `DateTime.UtcNow`

### Red Checkpoint for User Story 4 🔴

- [ ] T115R [US4] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. Valid red: compile error (`Application/Requests/` absent) or assertion failure once stubbed. Paste the transcript and commit the red baseline before implementation. Non-parallel

### Implementation for User Story 4

- [ ] T116 [P] [US4] Implement the 13 per-type validators in `src/Nabadat.IntegrationHub/Application/Requests/TypeValidators/` — one type per file (Text, Number, Boolean, Email, Phone, List, Range, Date, DateTime, Currency, Percentage, Url, Geolocation), VR-T01…T13
- [ ] T117 [US4] Implement `ParameterTypeValidator` in `src/Nabadat.IntegrationHub/Application/Requests/ParameterTypeValidator.cs` dispatching to T116 (depends on T116)
- [ ] T118 [P] [US4] Implement `ResultCodeMapper` in `src/Nabadat.IntegrationHub/Application/Requests/ResultCodeMapper.cs` — the normative catalogue and exact message copy (F0.3)
- [ ] T119 [P] [US4] Implement `ChannelContractRequiredFieldChecker` in `src/Nabadat.IntegrationHub/Application/Requests/ChannelContractRequiredFieldChecker.cs` (BR-08)
- [ ] T120 [P] [US4] Implement `UnregisteredParameterStore` in `src/Nabadat.IntegrationHub/Application/Requests/UnregisteredParameterStore.cs` (BR-14)
- [ ] T121 [P] [US4] Implement `IdempotencyKeyResolver` in `src/Nabadat.IntegrationHub/Application/Requests/IdempotencyKeyResolver.cs` (BR-18)
- [ ] T122 [P] [US4] Implement `AllowedOriginsWhitelistStore` in `src/Nabadat.IntegrationHub/Application/Requests/AllowedOriginsWhitelistStore.cs` (FR-S2-10)
- [ ] T123 [P] [US4] Implement `SurveyLinkExpiryCalculator` in `src/Nabadat.IntegrationHub/Application/Requests/SurveyLinkExpiryCalculator.cs` — injected `TimeProvider` (F0.8)
- [ ] T124 [US4] Implement `RequestValidationPipeline` in `src/Nabadat.IntegrationHub/Application/Requests/RequestValidationPipeline.cs` — the ordered 8 steps, short-circuiting atomically on the first failure (depends on T117–T123)
- [ ] T125 [US4] Implement the per-integration rate limiter (default **100 req/s**, Operations-configurable **without a code change**, NFR-4) and the **2 MB** payload cap enforced *before* any parameter parsing (NFR-3) in `src/Nabadat.IntegrationHub/Api/Middleware/` — `429 E-1429` and `413 E-1413` respectively
- [ ] T126 [US4] Implement `IntegrationRequestLogWriter` in `src/Nabadat.IntegrationHub/Application/Requests/IntegrationRequestLogWriter.cs` — writes one `integration_request_logs` row per attempt (including retries and rejections) into the DB-04 monthly partition, storing unregistered pairs raw
- [ ] T127 [US4] Implement the five scenario handlers in `src/Nabadat.IntegrationHub/Application/Requests/Scenarios/` — SCN-01 dispatch via `ISurveyDispatchGateway` → `202`; SCN-02 redirect link → `200` + `{survey_url, expires_at}`; SCN-03 JSON render via the **real** `RealSurveyRenderServiceAdapter` → `200` + survey definition JSON; SCN-04 iFrame embed → `200` + short-lived embed URL; SCN-05 response ingestion via `IResponseIngestionGateway` → `202`
- [ ] T128 [US4] Implement `InboundScenarioController` in `src/Nabadat.IntegrationHub/Api/Controllers/InboundScenarioController.cs` — the five endpoints per contracts/api-endpoints.md (`POST /v1/survey-requests/{channelId}`, `POST /v1/survey-links/{channelId}`, `POST /v1/survey-definitions/{channelId}`, `GET /v1/survey-embed/{channelId}`, `POST /v1/responses/{channelId}`), each requiring its BR-26 auth scope; error envelope `{ result_code, message, request_id }` (depends on T124–T127)
- [ ] T129 [US4] Map a downstream-module failure (M-02/M-03/M-04 unavailable) to `500 E-1500` with the retry-idempotent message — **never** surface the downstream error to the caller (F0.3, Error Handling)

### Integration & API / Scenario Tests for User Story 4 🐳

- [ ] T130 [P] [US4] One API test class per scenario in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Inbound/` — `DispatchScenarioEndpointTests.cs`, `SurveyLinkScenarioEndpointTests.cs`, `SurveyDefinitionScenarioEndpointTests.cs`, `SurveyEmbedScenarioEndpointTests.cs`, `ResponseIngestionScenarioEndpointTests.cs` — each asserting the normative result code and the downstream hand-off stub call
- [ ] T131 [P] [US4] Pipeline-order + guardrail tests in `tests/Nabadat.IntegrationHub.IntegrationTests/Services/Requests/PipelineOrderTests.cs` — a request crafted to fail two checks asserts the **earlier** step's code wins; N+1 requests in one second against a 100 req/s integration → `429`; a > 2 MB body → `413` **with zero parameter detail logged**; an inactive channel → `409 E-1004`
- [ ] T132 [P] [US4] Idempotent-retry test in `tests/Nabadat.IntegrationHub.IntegrationTests/Services/Requests/IdempotentRetryTests.cs` — two identical requests (same `transaction_id`) → **both logged**, exactly one downstream dispatch/store call recorded (BR-18)
- [ ] T133 [US4] Scenario test `InboundRequestLifecycleScenarioTests` in `tests/Nabadat.IntegrationHub.IntegrationTests/Scenarios/Requests/InboundRequestLifecycleScenarioTests.cs` — SCN-01: send → assert `202` → poll `GET /request-logs` until the entry appears (≤ 60 s) → retry the identical request → assert no duplicate downstream dispatch and exactly 2 log entries → deactivate the channel mid-test → repeat → assert `409 E-1004`. Final aggregate: 2 accepted + 1 rejected log entries, one downstream dispatch call total

**Build gate (MANDATORY)**: `dotnet test tests/Nabadat.IntegrationHub.UnitTests` and `dotnet test tests/Nabadat.IntegrationHub.IntegrationTests` both green (Docker up). **No frontend build or E2E gate applies — this story ships no UI** (`e2e-tests: skipped`).

**Checkpoint**: US1–US4 work independently. Requests now flow end to end; the module delivers value.

---

## Phase 7: User Story 5 — Monitor Integration Health and Investigate via Request Logs (Priority: P1)

**Goal**: A Tenant IT Administrator sees integration count, 24h traffic, and aggregate error rate at a
glance, then drills into request logs — filtering by status class, integration, and time window
(including Last hour) and expanding a row to see every parameter received (**PII-masked**) and the full
response returned.

**Independent Test**: Seed 6 integrations (1 inactive) with mixed successful/failed requests → open
`/integration-hub/integrations` → tiles read "6 / 5 active" with the correct 24h count and a correctly
colour-coded error rate → open `/integration-hub/logs` → apply "Client errors" + a specific integration
+ "Last hour" → only matching rows remain and per-chip counts reflect the window → expand a row → PII
renders masked and the full response detail shows.

### Unit Tests for User Story 5 (REQUIRED — write FIRST, must FAIL) ⚠️

- [ ] T134 [P] [US5] Unit tests for `IntegrationHealthTileCalculator` in `tests/Nabadat.IntegrationHub.UnitTests/Monitoring/IntegrationHealthTileCalculatorTests.cs` — `Compute(total=6, active=5, errors24h=0, requests24h=0)` → tile `"6 / 5 active"` and error rate `"—"` when there is zero traffic (FR-S1-05); rolling-24h window driven by an injected `FakeTimeProvider`
- [ ] T135 [P] [US5] Unit tests for `ErrorRateColourResolver` in `tests/Nabadat.IntegrationHub.UnitTests/Monitoring/ErrorRateColourResolverTests.cs` — `ColourFor(0.008)` → `D2`, `ColourFor(0.03)` → `D3`, `ColourFor(0.08)` → `D4`, plus the exact boundary cases at 1% and 5% per FR-S1-06's inclusive/exclusive convention documented in the calculator
- [ ] T136 [P] [US5] Unit tests for `IntegrationListFilter` in `tests/Nabadat.IntegrationHub.UnitTests/Monitoring/IntegrationListFilterTests.cs` — `Filter(search="CRM", channel="CALL-CENTER", rows=[…])` → the **intersection** only (AC-S1-02, AND combination)
- [ ] T137 [P] [US5] Unit tests for `RequestLogFilterCombinator` in `tests/Nabadat.IntegrationHub.UnitTests/Monitoring/RequestLogFilterCombinatorTests.cs` — `Combine(statusClass="4xx", integration="X", window="LastHour")` → AND-intersected result set **and counts scoped to the window** (AC-S8-01)
- [ ] T138 [P] [US5] Unit tests for `PiiMaskingFormatter` in `tests/Nabadat.IntegrationHub.UnitTests/Monitoring/PiiMaskingFormatterTests.cs` — `Mask(mobile="+962770123456")` → `"+9627•••••312"`; `Mask(name="Mona Al-Rashid")` → `"M••••• A•-R•••••"` (the exact SRS patterns); identical output for list, detail, **and export** views (FR-S8-03)
- [ ] T139 [P] [US5] Unit tests for `RejectedRequestDetailProjection` in `tests/Nabadat.IntegrationHub.UnitTests/Monitoring/RejectedRequestDetailProjectionTests.cs` — `Project(request, rejectedAtStage="Authentication")` → parameters panel = `"— request rejected before parameter parsing"`, never partial or garbled data (AC-S8-03)

### Red Checkpoint for User Story 5 🔴

- [ ] T140R [US5] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. Valid red: compile error (`Application/Monitoring/` absent) or assertion failure once stubbed. Paste the transcript and commit the red baseline before implementation. Non-parallel

### Implementation for User Story 5

- [ ] T141 [P] [US5] Implement `IntegrationHealthTileCalculator` in `src/Nabadat.IntegrationHub/Application/Monitoring/IntegrationHealthTileCalculator.cs` — injected `TimeProvider` for the rolling 24h window
- [ ] T142 [P] [US5] Implement `ErrorRateColourResolver` in `src/Nabadat.IntegrationHub/Application/Monitoring/ErrorRateColourResolver.cs` — returns the **D-scale level**, not a hex; document the boundary convention in the file (FR-S1-06)
- [ ] T143 [P] [US5] Implement `IntegrationListFilter` in `src/Nabadat.IntegrationHub/Application/Monitoring/IntegrationListFilter.cs`
- [ ] T144 [P] [US5] Implement `RequestLogFilterCombinator` in `src/Nabadat.IntegrationHub/Application/Monitoring/RequestLogFilterCombinator.cs`
- [ ] T145 [P] [US5] Implement `PiiMaskingFormatter` in `src/Nabadat.IntegrationHub/Application/Monitoring/PiiMaskingFormatter.cs` — the single masking path used by list, detail, and export alike; **zero unmasked-access code paths exist in Phase 1** (NFR-9)
- [ ] T146 [P] [US5] Implement `RejectedRequestDetailProjection` in `src/Nabadat.IntegrationHub/Application/Monitoring/RejectedRequestDetailProjection.cs`
- [ ] T147 [US5] Create the data-access port `IRequestLogStore` in `src/Nabadat.IntegrationHub/Application/Monitoring/Interfaces/IRequestLogStore.cs` + `RequestLogStore.cs` — cursor-only pagination (API-04), newest-first, querying across the monthly partitions
- [ ] T148 [US5] Implement `RequestLogService` in `src/Nabadat.IntegrationHub/Application/Monitoring/RequestLogService.cs` — list/detail/export composing T144–T147, masking applied before the data leaves the service (depends on T144–T147)
- [ ] T149 [P] [US5] Create the DTOs in `src/Nabadat.IntegrationHub/Api/Contracts/` — `RequestLogListItemResponse.cs`, `RequestLogDetailResponse.cs`, `IntegrationHealthTilesResponse.cs`, one type per file
- [ ] T150 [US5] Implement `RequestLogsController` in `src/Nabadat.IntegrationHub/Api/Controllers/RequestLogsController.cs` — `GET /api/v1/integration-hub/request-logs` (AND-combined status-class + integration + time-window incl. `last_hour`, cursor-paginated newest-first), `GET …/{id}`, `GET …/export`; gated on `m13.log.view` so **P-01 receives 403** (logs are P-07-exclusive) (depends on T148, T149)
- [ ] T151 [US5] Extend `GET /api/v1/integration-hub/integrations` in `IntegrationsController` to return the computed health tiles + FR-S1-02 filters (depends on T141–T143)
- [ ] T152 [P] [US5] Implement `useRequestLogs` in `frontend/src/features/integration-hub/hooks/useRequestLogs.ts`
- [ ] T153 [US5] Add the **SCR-01 stat tiles + error-rate badges** to `frontend/src/features/integration-hub/pages/AllIntegrationsPage.tsx` — tiles for total/active and rolling-24h traffic, an error-rate badge coloured on the **D-scale** (`< 1%` D2 · `1–5%` D3 · `> 5%` D4) **paired with an icon so colour is never the only indicator**, `"—"` when there is no traffic, and an AND-combined search + channel filter row
- [ ] T154 [US5] Build **SCR-08** `RequestLogsPage` in `frontend/src/features/integration-hub/pages/RequestLogsPage.tsx` — status-class filter chips with per-chip counts, integration select, time-window select (incl. Last hour), a newest-first cursor-paginated table whose HTTP-status badges use D2/D4/D5, and the **access-denied state for P-01** (no `m13.log.view` grant). The **Time** column stays `text-start` with an inner `<span dir="ltr">` — never `text-end`
- [ ] T155 [US5] Build the expandable log-detail panel `frontend/src/features/integration-hub/components/RequestLogDetail.tsx` — every received parameter (PII **masked**, unregistered pairs flagged), the full response returned, and the `"— request rejected before parameter parsing"` notice for auth-rejected requests; use the `grid-rows-[0fr]`/`grid-rows-[1fr]` expand animation, never `{condition && <div>}`

### Integration & API Tests for User Story 5 🐳

- [ ] T156 [P] [US5] API test for `GET /api/v1/integration-hub/integrations` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Monitoring/IntegrationHealthEndpointTests.cs` — computed tiles + FR-S1-02 filters
- [ ] T157 [P] [US5] API tests for `GET /api/v1/integration-hub/request-logs` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Monitoring/RequestLogsEndpointTests.cs` — AND-combined status-class + integration + `last_hour` filters, cursor-paginated newest-first; detail returns full parameter + response data with PII masked; **as a P-01 → 403** (P-07-exclusive)
- [ ] T158 [P] [US5] API test for `GET /api/v1/integration-hub/request-logs/export` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Monitoring/RequestLogExportEndpointTests.cs` — exports exactly the filtered view with PII masked **identically to the on-screen view** (FR-S8-04, BR-14)

> `scenario-test: not-needed` per spec.md US5 — read-only single-endpoint views; the "appears in logs
> within 60 s" cross-story assertion lives in US4's scenario test.

### E2E (Browser) Tests for User Story 5 🎭

- [ ] T159 [P] [US5] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/IntegrationMonitoringTests.cs` — `Integrations_stat_tiles_reflect_total_active_and_traffic` (AC-S1-01), `Integrations_search_and_channel_filter_combine_with_AND` (AC-S1-02), `Integrations_new_integration_shows_zero_traffic_and_dash_rate` (AC-S1-03)
- [ ] T160 [P] [US5] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/RequestLogsTests.cs` — `RequestLogs_filters_combine_with_AND_and_counts_reflect_window` (AC-S8-01), `RequestLogs_expanded_row_masks_pii_in_exact_format` (AC-S8-02), `RequestLogs_auth_rejected_row_shows_rejected_before_parsing_notice` (AC-S8-03), `RequestLogs_export_masks_pii_identically_to_screen` (FR-S8-04), `RequestLogs_cx_manager_role_is_denied_access` (Permissions Matrix); update `COVERAGE.md`

**Build gate (MANDATORY)**: unit + integration green; `npm run build` green; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~IntegrationMonitoringTests"` and `--filter "FullyQualifiedName~RequestLogsTests"` green.

**Checkpoint**: all five P1 stories are complete — the module's core loop is observable end to end.

### Click-through Parity for User Story 5 🎨

> Owner: the frontend developer, run manually. Preconditions: SCR-01 tiles and SCR-08 built
> click-through-blind (a ported page is `NOT AUDITED`, not clean); click-through served + stack signed in.

- [ ] T161P [US5] Run `/clickthrough-parity 006-integration-hub phase 7` over `/integration-hub/integrations` (stat tiles + list) and `/integration-hub/logs` (SCR-08 incl. the expanded row detail) and triage the report. Apply accepted defects with `--fix`; escalate every **Needs discussion** item — in particular any difference in the masked-PII rendering format, which is normative copy and must not be silently `--fix`ed — to the design owner. Record the result in `.claude/skills/clickthrough-parity/route-map.md`

---

## Phase 8: User Story 6 — Manage Parameter Mappings Inline (Priority: P2)

**Goal**: For any mapping-enabled parameter, a CX Manager translates raw backend values (`S001`) into
bilingual display values ("Visa Request" / "طلب فيزا"). Mappings resolve at **read time**, so an edit or
delete retroactively relabels historical data by design. Unmapped incoming values are never rejected —
stored raw and surfaced in a 7-day queue with one-click mapping.

**Independent Test**: Pick a mapping-enabled parameter with no mappings → send a US4 request carrying
`S014` → `/integration-hub/mappings` shows `S014` in the unmapped-values alert → **Map now** pre-fills a
draft row → fill EN/AR → **Save** → the mapping goes Active and every historical and future report
renders the new label immediately.

### Unit Tests for User Story 6 (REQUIRED — write FIRST, must FAIL) ⚠️

- [ ] T162 [P] [US6] Unit tests for `MappingSourceValueUniquenessValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Mappings/MappingSourceValueUniquenessValidatorTests.cs` — `Validate(existing=["S001"], newValue="S001")` → `Invalid("This source value already has a mapping")`; `Validate(existing=["S001"], newValue="s001")` → `Invalid` (**case-insensitive**, VR-F08)
- [ ] T163 [P] [US6] Unit tests for `UnmappedValueQueueService` in `tests/Nabadat.IntegrationHub.UnitTests/Mappings/UnmappedValueQueueServiceTests.cs` — `Enqueue(value="S014", firstSeenAt=now)` → in the 7-day queue; `Enqueue(value="S014", firstSeenAt=now-8days)` → absent (window expired, no repeat occurrence); `Dequeue(value="S014", mappingCreated=true)` → removed. Window driven by `FakeTimeProvider`
- [ ] T164 [P] [US6] Unit tests for `MappingResolver` in `tests/Nabadat.IntegrationHub.UnitTests/Mappings/MappingResolverTests.cs` — `Resolve("s001", {S001:{en,ar}})` resolves regardless of incoming casing; `Resolve("S014", {})` falls back to the **raw** value with original casing preserved for both EN and AR (F0.5); `Resolve("S001", mappings updated AFTER the response was stored)` → returns the **new** label (retroactive read-time resolution, BR-13)
- [ ] T165 [P] [US6] Unit tests for `MappingEnabledParameterFilter` in `tests/Nabadat.IntegrationHub.UnitTests/Mappings/MappingEnabledParameterFilterTests.cs` — `FilterMappingEnabled([{mappingSupport:true},{mappingSupport:false}])` → only the first is offered (BR-27)

### Red Checkpoint for User Story 6 🔴

- [ ] T166R [US6] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. Valid red: compile error (`Application/Mappings/` absent) or assertion failure once stubbed. Paste the transcript and commit the red baseline before implementation. Non-parallel

### Implementation for User Story 6

- [ ] T167 [P] [US6] Implement `MappingSourceValueUniquenessValidator` in `src/Nabadat.IntegrationHub/Application/Mappings/MappingSourceValueUniquenessValidator.cs` (VR-F08)
- [ ] T168 [P] [US6] Implement `MappingEnabledParameterFilter` in `src/Nabadat.IntegrationHub/Application/Mappings/MappingEnabledParameterFilter.cs` (BR-27)
- [ ] T169 [P] [US6] Implement `MappingResolver` in `src/Nabadat.IntegrationHub/Application/Mappings/MappingResolver.cs` — case-insensitive read-time lookup with raw-value fallback (F0.5)
- [ ] T170 [US6] Implement `UnmappedValueQueueService` in `src/Nabadat.IntegrationHub/Application/Mappings/UnmappedValueQueueService.cs` over `unmapped_value_occurrences`, injected `TimeProvider` for the 7-day window; wire the write side into `RequestValidationPipeline` so unmapped values are recorded at ingestion
- [ ] T171 [US6] Create the data-access port `IParameterMappingStore` in `src/Nabadat.IntegrationHub/Application/Mappings/Interfaces/IParameterMappingStore.cs` + `ParameterMappingStore.cs`
- [ ] T172 [US6] Implement `ParameterMappingService` in `src/Nabadat.IntegrationHub/Application/Mappings/ParameterMappingService.cs` — add/edit/delete/list composing T167–T171; **no version-history or restore surface exists** — the platform audit trail is the only change record (BR-13, `[PO-G12]`) (depends on T167–T171)
- [ ] T173 [P] [US6] Create the DTOs in `src/Nabadat.IntegrationHub/Api/Contracts/` — `CreateMappingRequest.cs`, `UpdateMappingRequest.cs`, `MappingResponse.cs`, `UnmappedQueueResponse.cs`, one type per file
- [ ] T174 [US6] Implement `ParameterMappingsController` in `src/Nabadat.IntegrationHub/Api/Controllers/ParameterMappingsController.cs` — `GET`/`POST` `/api/v1/integration-hub/parameters/{id}/mappings`, `PUT`/`DELETE …/{mappingId}`, `GET …/mappings/unmapped-queue` (depends on T172, T173)
- [ ] T175 [US6] Emit `mapping.added` / `mapping.edited` / `mapping.deleted` audit events from `Application/Events/` (BR-21)
- [ ] T176 [P] [US6] Implement `useMappings` in `frontend/src/features/integration-hub/hooks/useMappings.ts`
- [ ] T177 [US6] Build **SCR-07** `ParameterMappingsPage` in `frontend/src/features/integration-hub/pages/ParameterMappingsPage.tsx` — a parameter selector offering **only mapping-enabled parameters**, each rendered `"Name — api_field (n values)"` (FR-S7-01); the unmapped-values alert with **Map now**; and the mappings table. **No version-history or restore control anywhere** (BR-13)
- [ ] T178 [US6] Build the inline editor `frontend/src/features/integration-hub/components/MappingTableRow.tsx` — **Add value** appends a row with a "Draft" status badge and a **Save** button requiring a non-empty, parameter-unique source value; **Delete** opens Dialog **D-7** and takes effect at read time immediately. AR display values render RTL correctly in the table cell; give the inline value input an `aria-label` mirroring its placeholder (the sanctioned label-less exception for a chip/inline adder)

### Integration & API / Scenario Tests for User Story 6 🐳

- [ ] T179 [P] [US6] API tests for the mapping CRUD routes in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Mappings/ParameterMappingEndpointTests.cs` — `POST` add → 201 + `mapping.added`; duplicate source value within the parameter → 409 (VR-F08); `PUT` edit → 200 + `mapping.edited` and a **subsequent read of historical data reflects the new label**; `DELETE` → 200 + `mapping.deleted` and later reads of that source value fall back to raw
- [ ] T180 [P] [US6] API test for `GET …/parameters/{id}/mappings/unmapped-queue` in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Mappings/UnmappedQueueEndpointTests.cs` — returns values received in the trailing 7 days with no mapping
- [ ] T181 [US6] Scenario test `MappingReadTimeResolutionScenarioTests` in `tests/Nabadat.IntegrationHub.IntegrationTests/Scenarios/Mappings/MappingReadTimeResolutionScenarioTests.cs` — send a US4 request carrying unmapped `S014` → `GET` the queue (assert present) → `POST` a mapping → `GET` the queue (assert absent) → re-fetch the **earlier** request's log/report projection and assert it now renders the new display label, proving retroactive read-time resolution (F0.5). Final aggregate: exactly one `mapping.added` event and the historical projection updated

### E2E (Browser) Tests for User Story 6 🎭

- [ ] T182 [P] [US6] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/ParameterMappingsTests.cs` — `Mappings_unmapped_value_alert_shows_and_map_now_prefills_draft` (AC-S7-03), `Mappings_inline_add_row_requires_unique_nonempty_source_value` (VR-F08), `Mappings_delete_shows_confirmation_and_takes_effect_immediately` (D-7), `Mappings_no_version_history_or_restore_control_exists_anywhere` (BR-13), `Mappings_parameter_selector_only_lists_mapping_enabled_parameters` (FR-S7-01); update `COVERAGE.md`

**Build gate (MANDATORY)**: unit + integration green; `npm run build` green; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ParameterMappingsTests"` green.

**Checkpoint**: US1–US6 all work independently.

### Click-through Parity for User Story 6 🎨

> Owner: the frontend developer, run manually. Preconditions: SCR-07 built click-through-blind
> (a ported page is `NOT AUDITED`, not clean); click-through served + dev stack signed in.

- [ ] T183P [US6] Run `/clickthrough-parity 006-integration-hub phase 8` over `/integration-hub/mappings` (the inline add/edit/delete portion) and triage the report. Apply accepted defects with `--fix`; escalate every **Needs discussion** item to the design owner. Record the result in `.claude/skills/clickthrough-parity/route-map.md`

---

## Phase 9: User Story 7 — Bulk Import, Export, and Replace-All Parameter Mappings via Excel (Priority: P2)

**Goal**: A CX Manager exports a parameter's mappings to Excel (`source_value`, `display_en`,
`display_ar`), edits offline, and re-imports in **Merge** (default) or **Replace-all** mode. Import is
strictly all-or-nothing behind a row-level validation report; Replace-all is irreversible and requires an
explicit confirmation naming the consequence.

**Independent Test**: Export a parameter's mappings → introduce one invalid row → **Import from Excel** →
Merge → the import is rejected wholesale with a row-level report naming the bad row and reason, and **no**
valid row was applied. Fix and re-import successfully. Then **Replace all mappings…** → confirm the
destructive dialog → all prior mappings are gone, replaced by the imported set.

### Unit Tests for User Story 7 (REQUIRED — write FIRST, must FAIL) ⚠️

- [ ] T184 [P] [US7] Unit tests for `ExcelMappingExporter` in `tests/Nabadat.IntegrationHub.UnitTests/Mappings/ExcelMappingExporterTests.cs` — `Export([{S001,"Visa Request","طلب فيزا"}])` → a workbook with header row `source_value, display_en, display_ar` + one data row (FR-S7-05)
- [ ] T185 [P] [US7] Unit tests for `ExcelMappingImportValidator` in `tests/Nabadat.IntegrationHub.UnitTests/Mappings/ExcelMappingImportValidatorTests.cs` — `Validate(rows=[214 valid, 1 with empty source_value])` → `Invalid` with report `[{row:215, column:"source_value", reason:"required"}]` and **nothing applied** (AC-S7-01); `Validate([{S001,…},{S001,…}])` → `Invalid` naming the in-file duplicate (VR-F09)
- [ ] T186 [P] [US7] Unit tests for `ExcelMappingImportModeApplier` in `tests/Nabadat.IntegrationHub.UnitTests/Mappings/ExcelMappingImportModeApplierTests.cs` — `Apply(mode=Merge, existing=[S001,S002], imported=[S001(new label),S003])` → `[S001(new label), S002, S003]` (upsert + preserve untouched); `Apply(mode=ReplaceAll, existing=[S001,S002], imported=[S003])` → `[S003]` only
- [ ] T187 [P] [US7] Unit tests for `ImportRowCountGuard` in `tests/Nabadat.IntegrationHub.UnitTests/Mappings/ImportRowCountGuardTests.cs` — `GuardRowCount(10001)` → rejected **before any row is parsed** (NFR-16)
- [ ] T188 [P] [US7] Unit tests for `MappingsPerParameterGuard` in `tests/Nabadat.IntegrationHub.UnitTests/Mappings/MappingsPerParameterGuardTests.cs` — `GuardMappingCount(existing=4999, importing=2)` → rejected (would exceed the 5,000-per-parameter ceiling, NFR-16)

### Red Checkpoint for User Story 7 🔴

- [ ] T189R [US7] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. Valid red: compile error or assertion failure once the Excel types are stubbed. Paste the transcript and commit the red baseline before implementation. Non-parallel

### Implementation for User Story 7

- [ ] T190 [P] [US7] Implement `ExcelMappingExporter` in `src/Nabadat.IntegrationHub/Application/Mappings/ExcelMappingExporter.cs` using ClosedXML — exactly the three ratified columns
- [ ] T191 [P] [US7] Implement `ImportRowCountGuard` in `src/Nabadat.IntegrationHub/Application/Mappings/ImportRowCountGuard.cs` (NFR-16)
- [ ] T192 [P] [US7] Implement `MappingsPerParameterGuard` in `src/Nabadat.IntegrationHub/Application/Mappings/MappingsPerParameterGuard.cs` (NFR-16)
- [ ] T193 [US7] Implement `ExcelMappingImportValidator` in `src/Nabadat.IntegrationHub/Application/Mappings/ExcelMappingImportValidator.cs` — required columns, non-empty `source_value`, no in-file duplicates, producing the row-level report; **all-or-nothing gate** (VR-F09)
- [ ] T194 [US7] Implement `ExcelMappingImportModeApplier` in `src/Nabadat.IntegrationHub/Application/Mappings/ExcelMappingImportModeApplier.cs` — Merge upsert vs Replace-all delete-then-insert, the whole apply wrapped in one `ITenantDbContext.ExecuteAsync` so a partial import is impossible (depends on T193)
- [ ] T195 [US7] Add `POST …/parameters/{id}/mappings/import`, `GET …/mappings/export`, and the direct `POST …/mappings/replace-all` routes to `ParameterMappingsController`, emitting the `mapping.import` (row count + mode) and `mapping.replace_all` (rows removed/added) audit events (depends on T190–T194)
- [ ] T196 [US7] Build the Excel toolbar + Dialog **D-4** in `frontend/src/features/integration-hub/components/MappingImportDialog.tsx` — **Merge with existing pre-selected** as the default non-destructive mode, a file picker, and the row-level failure report rendered on rejection; `sm:max-w-lg` with the capped-height scroll pattern
- [ ] T197 [US7] Build Dialog **D-5** in `frontend/src/features/integration-hub/components/ReplaceAllMappingsDialog.tsx` — the confirmation text names the **exact current mapping count** and the parameter and states the action **cannot be undone**; the confirm action is `variant="destructive"`, Cancel is `variant="outline"`

### Integration & API / Scenario Tests for User Story 7 🐳

- [ ] T198 [P] [US7] API tests for import/export in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Mappings/MappingImportExportEndpointTests.cs` — `GET …/export` returns the 3-column file; `POST …/import {mode:"merge"}` valid → 200 upserted + `mapping.import` audit with row count + mode; one invalid row → 400/422 with the row-level report and a follow-up `GET` proving **zero** mappings changed; `{mode:"replace_all"}` → 200 with priors gone + `mapping.replace_all` audit; 10,001 rows → 400 with before/after `GET` showing zero change (NFR-16)
- [ ] T199 [US7] Scenario test `BulkMappingReplaceScenarioTests` in `tests/Nabadat.IntegrationHub.IntegrationTests/Scenarios/Mappings/BulkMappingReplaceScenarioTests.cs` — `GET` export (baseline) → `POST` import merge with one invalid row → assert mappings **unchanged** (all-or-nothing held) → fix → `POST` import again → assert applied → `POST` import `replace_all` with a fresh set → assert the prior merged set is entirely gone. Final aggregate: one `mapping.import` and one `mapping.replace_all` event with counts matching the actual before/after diff

### E2E (Browser) Tests for User Story 7 🎭

- [ ] T200 [P] [US7] Additional `[TestMethod]` blocks in `tests/Nabadat.E2ETests/IntegrationHub/ParameterMappingsTests.cs` (**same file as US6**) — `Mappings_export_downloads_three_ratified_columns` (FR-S7-05), `Mappings_import_all_or_nothing_shows_row_level_report_on_failure` (AC-S7-01), `Mappings_import_dialog_defaults_to_merge_mode` (D-4), `Mappings_replace_all_confirmation_names_count_and_is_irreversible` (D-5), `Mappings_import_over_10000_rows_is_rejected` (NFR-16); update `COVERAGE.md`

**Build gate (MANDATORY)**: unit + integration green; `npm run build` green; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ParameterMappingsTests"` green.

**Checkpoint**: US1–US7 all work independently.

### Click-through Parity for User Story 7 🎨

> Owner: the frontend developer, run manually. Preconditions: the SCR-07 Excel surface built
> click-through-blind; click-through served + dev stack signed in.

- [ ] T201P [US7] Run `/clickthrough-parity 006-integration-hub phase 9` over `/integration-hub/mappings` (the Excel import/export/replace-all surface: toolbar, D-4, D-5) and triage the report. Apply accepted defects with `--fix`; escalate every **Needs discussion** item — the D-5 destructive-confirmation copy is normative and must not be silently `--fix`ed — to the design owner. Record the result in `.claude/skills/clickthrough-parity/route-map.md`

---

## Phase 10: User Story 8 — Manage Credential Lifecycle (Priority: P2)

**Goal**: Beyond US3's initial generation, a Tenant IT Administrator revokes a compromised key
immediately or generates a replacement (which implicitly revokes the prior key). No sandbox/test
credentials, no expiry fields, no IP allow-lists; secrets are shown exactly once and never retrievable.

**Independent Test**: On an integration with an active API key, open Step 2 of the edit wizard →
**Revoke** → confirm Dialog D-3 (which names the masked key and the consequence) → the key is revoked
immediately and a caller request with it returns `401 E-1401`. Generate a new key and verify the old one
still fails identically while the new one succeeds.

### Unit Tests for User Story 8 (REQUIRED — write FIRST, must FAIL) ⚠️

- [ ] T202 [P] [US8] Extend `tests/Nabadat.IntegrationHub.UnitTests/Integrations/CredentialRevocationServiceTests.cs` with the standalone revoke-without-regeneration flow and the **no-un-revoke invariant** — `Revoke(K1)` → `K1.status = Revoked`; `Attempt(Unrevoke, K1)` → **no such operation exists** (compile-time / API-surface absence, not a runtime rejection); `Generate(newKey=K2, whileActive=K1)` → K1 Revoked, K2 Active with **no separate confirmation** for K1 (BR-16)
- [ ] T203 [P] [US8] Unit tests for `OAuthScopeEnforcer` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/OAuthScopeEnforcerTests.cs` — `EnforceScope(token.scopes=["survey-links:read"], calledEndpoint=SCN-01)` → rejected (insufficient scope, mapped to `401 E-1401` at the pipeline's authentication step); `EnforceScope(["survey-requests:write"], SCN-01)` → allowed; one case per BR-26 scenario→scope pair
- [ ] T204 [P] [US8] Unit tests for `CredentialFieldSetGuard` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/CredentialFieldSetGuardTests.cs` — `AssertFieldSet(apiKeyFields)` does **not** contain `expiry`, `sandbox`, `allowedSourceIps`; `AssertFieldSet(oauthFields)` does **not** contain `grantType`, `tokenLifetime`. This guards a ratified removal against future accidental regression (`[PO-G13]`, BR-17)

### Red Checkpoint for User Story 8 🔴

- [ ] T205R [US8] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. Note the **retrofit caveat**: `CredentialRevocationService` already exists from US3, so T202's additions must fail on **assertion**, not compile error — a pass against the existing implementation means the new cases are not exercising the new behaviour and must be strengthened. `OAuthScopeEnforcer`/`CredentialFieldSetGuard` are new, so their red is a compile error. Paste the transcript and commit the baseline. Non-parallel

### Implementation for User Story 8

- [ ] T206 [P] [US8] Implement `OAuthScopeEnforcer` in `src/Nabadat.IntegrationHub/Application/Integrations/OAuthScopeEnforcer.cs` — the scenario→required-scope map (BR-26); wire it into `RequestValidationPipeline`'s **authentication** step so a scope failure surfaces as `401 E-1401`, not a distinct code
- [ ] T207 [P] [US8] Implement `CredentialFieldSetGuard` in `src/Nabadat.IntegrationHub/Application/Integrations/CredentialFieldSetGuard.cs` — a static/config-level assertion over the console's credential field sets (`[PO-G13]`)
- [ ] T208 [US8] Extend `CredentialRevocationService` in `src/Nabadat.IntegrationHub/Application/Integrations/CredentialRevocationService.cs` with the standalone revoke path; confirm the service surface exposes **no** un-revoke method and the controller exposes no such route
- [ ] T209 [US8] Add the revoke/regenerate controls to `frontend/src/features/integration-hub/components/WizardStepAuth.tsx` for an existing integration, plus Dialog **D-3** — the confirmation names the **masked** key and states the consequence; the confirm is `variant="destructive"`, Cancel `variant="outline"`. Regenerating while a key is active shows **no extra confirmation** for the old key (BR-16)

### Integration & API Tests for User Story 8 🐳

- [ ] T210 [P] [US8] API tests in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Integrations/CredentialLifecycleEndpointTests.cs` — `POST …/{id}/credentials/revoke` without regenerating → 200 and a subsequent call with that key → `401 E-1401`; `POST …/credentials` while one is active → 200 with the old key **immediately** unusable, verified by a live call; a live call with an OAuth token whose scopes exclude the target scenario's scope → `401 E-1401`

> `scenario-test: not-needed` per spec.md US8 — US3's `IntegrationOnboardingScenarioTests` already
> exercises generate → revoke → repeat-call-rejected; this story adds no new cross-endpoint sequence.

### E2E (Browser) Tests for User Story 8 🎭

- [ ] T211 [P] [US8] Additional `[TestMethod]` blocks in `tests/Nabadat.E2ETests/IntegrationHub/IntegrationWizardTests.cs` (**same file as US3**) — `Wizard_revoke_dialog_names_masked_key_and_consequence` (D-3), `Wizard_generating_new_key_while_one_active_shows_no_extra_confirmation_for_old_key` (BR-16), `Wizard_auth_forms_never_render_expiry_sandbox_or_ip_allowlist_fields` (`[PO-G13]`), `Wizard_oauth_form_has_no_grant_type_or_token_lifetime_fields` (BR-17); update `COVERAGE.md`

**Build gate (MANDATORY)**: unit + integration green; `npm run build` green; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~IntegrationWizardTests"` green.

**Checkpoint**: US1–US8 all work independently.

### Click-through Parity for User Story 8 🎨

> Owner: the frontend developer, run manually. Preconditions: the SCR-02 Step-2 credential surface built
> click-through-blind; click-through served + dev stack signed in.

- [ ] T212P [US8] Run `/clickthrough-parity 006-integration-hub phase 10` over the SCR-02 Step-2 credential surface (revoke/regenerate controls and Dialog D-3) and triage the report. Apply accepted defects with `--fix`; escalate every **Needs discussion** item to the design owner — and note that the **absence** of expiry/sandbox/IP-allow-list/grant-type/token-lifetime fields is a ratified removal: if the click-through still shows any of them, that is a Needs-discussion item, **never** a `--fix` that re-adds the field. Record the result in `.claude/skills/clickthrough-parity/route-map.md`

---

## Phase 11: User Story 9 — Cross-Persona Read-Only Visibility and Permission Enforcement (Priority: P2)

**Goal**: Each persona has full manage access to their own screens with cross-persona **read-only**
visibility: P-01 may view (never edit) Integrations; P-07 may view (never edit) Service Channels,
Parameters, and Mappings. **Request Logs are P-07-exclusive — P-01 has no log access of any kind**
(BR-24 as corrected in SRS v1.2). Every sensitive action is permission-controlled and audited, enforced
server-side regardless of what the client renders.

**Independent Test**: As a P-01 session, `/integration-hub/integrations` renders read-only (no New/Edit/
Revoke controls) → `/integration-hub/logs` renders the **access-denied state** → then hit `POST
/integrations` and `GET /request-logs` directly and verify the server independently returns 403 for both.

### Unit Tests for User Story 9 (REQUIRED — write FIRST, must FAIL) ⚠️

- [ ] T213 [P] [US9] Unit tests for `PermissionKeyResolver` in `tests/Nabadat.IntegrationHub.UnitTests/Permissions/PermissionKeyResolverTests.cs` — `Resolve(P-01, "integration.manage")` → `Denied`; `Resolve(P-01, "integration.view")` → `Allowed`; `Resolve(P-07, "channel.manage")` → `Denied`; `Resolve(P-07, "log.view")` → `Allowed`; **`Resolve(P-01, "log.view")` → `Denied`** (logs are P-07-exclusive, unlike channels/parameters/mappings which P-07 gets read-only); covers the full persona × action matrix over `m13.integration.view/manage`, `m13.credential.manage`, `m13.log.view`, `m13.channel.view/manage`, `m13.parameter.view/manage`, `m13.mapping.manage/replace`
- [ ] T214 [P] [US9] Unit tests for `CrossPersonaViewGuard` in `tests/Nabadat.IntegrationHub.UnitTests/Permissions/CrossPersonaViewGuardTests.cs` — BR-24: P-01 gets `*.view`-only on P-07's screens and vice versa, with Request Logs excluded from the reciprocal grant
- [ ] T215 [P] [US9] Unit tests for `AuditEventEmitter` in `tests/Nabadat.IntegrationHub.UnitTests/Permissions/AuditEventEmitterTests.cs` — `Emit("credential.revoked", actor=U1, before={status:Active}, after={status:Revoked})` → exactly one event carrying actor, tenant, timestamp, entity, and before/after summary; `Emit("channel.id_changed", before={id:"OLD"}, after={id:"NEW"})` → one event; one case per **all 12 audited action families** in the Permissions Matrix

### Red Checkpoint for User Story 9 🔴

- [ ] T216R [US9] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. Valid red: compile error (`Application/Permissions/` absent) or assertion failure once stubbed. **`AuditEventEmitter` is a partial retrofit** — events are already emitted piecemeal from US1–US8's `Application/Events/`; if T215 passes on first run the cases are not exercising the unified emitter and must be strengthened. Paste the transcript and commit the baseline. Non-parallel

### Implementation for User Story 9

- [ ] T217 [P] [US9] Implement `PermissionKeyResolver` in `src/Nabadat.IntegrationHub/Application/Permissions/PermissionKeyResolver.cs` — the full M-13 action→permission-key map per the Permissions Matrix
- [ ] T218 [P] [US9] Implement `CrossPersonaViewGuard` in `src/Nabadat.IntegrationHub/Application/Permissions/CrossPersonaViewGuard.cs` (BR-24)
- [ ] T219 [US9] Consolidate audit emission into `AuditEventEmitter` in `src/Nabadat.IntegrationHub/Application/Events/AuditEventEmitter.cs` and route US1–US8's event writes through it, so all 12 action families emit one consistent shape (BR-21)
- [ ] T220 [US9] Apply the permission attributes/policies to **every** M-13 controller action in `src/Nabadat.IntegrationHub/Api/Controllers/` — write endpoints return **403 for the wrong persona and audit the attempt**, enforced server-side independently of any client rendering (defense in depth; never rely on hidden UI controls)
- [ ] T221 [US9] Gate the frontend on the same permission keys — hide or disable P-07-only actions in a P-01 session and vice versa (FR-GBL-05), and render the standard **access-denied state** on a direct route hit without the view grant (FR-GBL-02), across all eight screens in `frontend/src/features/integration-hub/pages/`
- [ ] T222 [US9] Restrict the sidebar entries per persona in `frontend/src/components/app-sidebar.tsx` — `ROLE_NAV_KEYS` must not surface **Request Logs** to P-01 at all (BR-24); route-level visibility is governed there, not by the route

### Integration & API Tests for User Story 9 🐳

- [ ] T223 [P] [US9] Permission API tests in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Permissions/CrossPersonaPermissionEndpointTests.cs` — `POST /integrations` as P-01 → 403 (audited); `POST /service-channels` as P-07 → 403 (audited); `GET /integrations` as P-01 → 200; `GET /service-channels` as P-07 → 200; **`GET /request-logs` as P-01 → 403**
- [ ] T224 [P] [US9] Audit-coverage test in `tests/Nabadat.IntegrationHub.IntegrationTests/Services/Permissions/AuditEventCoverageTests.cs` — each of the 12 Permissions-Matrix sensitive actions, performed successfully by its authorised persona, produces **exactly one** matching audit event with actor/tenant/timestamp/entity/before-after

> `scenario-test: not-needed` per spec.md US9 — permission checks are single-request assertions.

### E2E (Browser) Tests for User Story 9 🎭

- [ ] T225 [P] [US9] E2E tests in `tests/Nabadat.E2ETests/IntegrationHub/CrossPersonaPermissionsTests.cs` — `Integrations_cx_manager_sees_read_only_view_with_no_manage_controls` (FR-GBL-05), `ServiceChannels_it_admin_sees_read_only_view_with_no_manage_controls` (FR-GBL-05), `RequestLogs_direct_route_access_without_permission_shows_access_denied` (FR-GBL-02/05), `Mappings_direct_route_access_without_permission_shows_access_denied` (FR-GBL-02/05); use `SignInAsync` with the two personas, and note read-only rows use the `view-*` testid prefix rather than a disabled `edit-*`; update `COVERAGE.md`

**Build gate (MANDATORY)**: unit + integration green; `npm run build` green; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~CrossPersonaPermissionsTests"` green.

**Checkpoint**: US1–US9 all work independently.

### Click-through Parity for User Story 9 🎨

> Owner: the frontend developer, run manually. Preconditions: the read-only variants built
> click-through-blind; click-through served + **both** personas available in the dev stack.

- [ ] T226P [US9] Run `/clickthrough-parity 006-integration-hub phase 11` over the cross-persona read-only renderings of all eight screens and the access-denied states, and triage the report. Apply accepted defects with `--fix`; escalate every **Needs discussion** item to the design owner — in particular, if the click-through shows Request Logs to a P-01 persona that is a **business divergence** (BR-24 as corrected in SRS v1.2 makes logs P-07-exclusive), so it must be escalated, never `--fix`ed toward the click-through. Record the result in `.claude/skills/clickthrough-parity/route-map.md`

---

## Phase 12: User Story 10 — Activate and Deactivate Integrations and Service Channels (Priority: P3)

**Goal**: Either persona toggles their own entities between Active and Inactive without deleting them
(deletion never exists). Deactivating an integration suspends its endpoint; deactivating a channel
cascades `E-1004` rejections to every integration serving it and hides it from new-integration selection.

**Independent Test**: Deactivate an Active integration → its SCR-01 row shows the neutral "Inactive"
badge and "suspended" sub-line and a live call to its endpoint fails → reactivate → the badge reverts
and calls succeed again.

### Unit Tests for User Story 10 (REQUIRED — write FIRST, must FAIL) ⚠️

- [ ] T227 [P] [US10] Unit tests for `IntegrationStatusToggle` in `tests/Nabadat.IntegrationHub.UnitTests/Integrations/IntegrationStatusToggleTests.cs` — `Toggle(integration, Active→Inactive)` → `200` + `integration.deactivated` audit; `Toggle(Inactive→Active)` → `200` + `integration.activated`; `Attempt(Delete, integration)` → **no such state transition or endpoint exists**
- [ ] T228 [P] [US10] Unit tests for `ServiceChannelStatusToggle` in `tests/Nabadat.IntegrationHub.UnitTests/Channels/ServiceChannelStatusToggleTests.cs` — `Toggle(channel, Active→Inactive)` → audited, and the channel is **excluded from `GetActiveChannelsForSelector()`**'s result set (SCR-02's selector query)

### Red Checkpoint for User Story 10 🔴

- [ ] T229R [US10] Run `dotnet test tests/Nabadat.IntegrationHub.UnitTests`. Valid red: compile error (the toggle types absent) or assertion failure once stubbed. Paste the transcript and commit the red baseline before implementation. Non-parallel

### Implementation for User Story 10

- [ ] T230 [P] [US10] Implement `IntegrationStatusToggle` in `src/Nabadat.IntegrationHub/Application/Integrations/IntegrationStatusToggle.cs` — audited; no delete transition exists
- [ ] T231 [P] [US10] Implement `ServiceChannelStatusToggle` in `src/Nabadat.IntegrationHub/Application/Channels/ServiceChannelStatusToggle.cs` — audited; on deactivate, excluded from the active-channels selector query
- [ ] T232 [US10] Add `PATCH /api/v1/integration-hub/integrations/{id}` `{active}` to `IntegrationsController` and the `?active=true` selector filter to `ServiceChannelsController`; a call to an inactive integration's endpoint is rejected `401 E-1401` (credentials suspended while inactive, Status Lifecycle) and a call to an inactive channel's endpoint is rejected `409 E-1004` (depends on T230, T231)
- [ ] T233 [US10] Add the SCR-01 row-level status toggle to `frontend/src/features/integration-hub/pages/AllIntegrationsPage.tsx` — the neutral "Inactive" badge with the "suspended" sub-line, and **no delete control on any row, ever** (Status Lifecycle)
- [ ] T234 [US10] Confirm the SCR-04 **Active** toggle in `frontend/src/features/integration-hub/pages/ServiceChannelFormPage.tsx` drives the same endpoint, and that a deactivated channel disappears from SCR-02's channel select for **new** integrations while an existing integration serving it keeps the now-rejecting reference visible **with a warning** (SCR-02 edge case)

### Integration & API Tests for User Story 10 🐳

- [ ] T235 [P] [US10] API tests in `tests/Nabadat.IntegrationHub.IntegrationTests/Endpoints/Integrations/IntegrationStatusEndpointTests.cs` — `PATCH …/{id} {active:false}` → 200 and a subsequent live call → `401 E-1401`; `{active:true}` → 200 and calls succeed again; `GET /service-channels?active=true` excludes a just-deactivated channel

> `scenario-test: not-needed` per spec.md US10 — covered by the round-trip integration tests above and
> by US1's and US4's scenario tests for the channel-deactivation cascade.

### E2E (Browser) Tests for User Story 10 🎭

- [ ] T236 [P] [US10] Additional `[TestMethod]` blocks in `tests/Nabadat.E2ETests/IntegrationHub/IntegrationMonitoringTests.cs` (**same file as US5**) — `Integrations_deactivate_reactivate_round_trip_updates_badge_and_endpoint`, `Integrations_row_never_shows_a_delete_action`; and in `ServiceChannelTests.cs` (**same file as US1**) — `ServiceChannels_deactivated_channel_disappears_from_new_integration_selector`; update `COVERAGE.md`

**Build gate (MANDATORY)**: unit + integration green; `npm run build` green; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~IntegrationMonitoringTests"` and `--filter "FullyQualifiedName~ServiceChannelTests"` green.

**Checkpoint**: all ten user stories are complete and independently functional.

### Click-through Parity for User Story 10 🎨

> Owner: the frontend developer, run manually. Preconditions: the toggle surfaces built
> click-through-blind; click-through served + dev stack signed in.

- [ ] T237P [US10] Run `/clickthrough-parity 006-integration-hub phase 12` over the SCR-01 row status toggle and the SCR-04 Active toggle and triage the report. Apply accepted defects with `--fix`; escalate every **Needs discussion** item to the design owner. Record the result in `.claude/skills/clickthrough-parity/route-map.md`

---

## Phase 13: Polish & Cross-Cutting Concerns

**Purpose**: Verification passes that span every story, plus the release gate.

### Capacity, NFR, and performance verification

- [ ] T238 [P] Implement and test the per-tenant creation guardrails (VR-F13, NFR-16) — ≤ 200 custom parameters, ≤ 100 channels, ≤ 200 integrations — as a **console-side validation error** on each create action in `src/Nabadat.IntegrationHub/Application/{Parameters,Channels,Integrations}/`; **no inbound-API result code** is added, since these entities are created only through the console (SC-015)
- [ ] T239 [P] Verify NFR-2 with a performance regression test in `tests/Nabadat.IntegrationHub.IntegrationTests/Services/PerformanceTests.cs` — 95% of inbound API requests complete within 500 ms excluding downstream systems, on a seeded tenant (SC-013)
- [ ] T240 [P] Verify the 90-day request-log retention and DB-04 monthly partition rollover behaviour against `integration_request_logs` (NFR-8)
- [ ] T241 Verify NFR-4 in `tests/Nabadat.IntegrationHub.IntegrationTests/Services/Requests/RateLimitReconfigurationTests.cs` — a per-integration rate-limit change applied by Nabadat Operations takes effect **with no code deployment** (config-driven, not compiled), and requests already past the rate-limit check are unaffected by a concurrent limit change (Edge Cases)
- [ ] T242 Confirm NFR-17 last-write-wins concurrency in `tests/Nabadat.IntegrationHub.IntegrationTests/Services/ConcurrencyTests.cs` — the two named races (concurrent edits to a not-yet-locked channel ID; a replace-all triggered while another editor is mid-edit) resolve last-write-wins **with full audit records**, asserting the documented behaviour, **not** a locking mechanism

### Security and privacy audits

- [ ] T243 [P] Repo-wide audit for SC-008 across `src/Nabadat.IntegrationHub/` — grep every log sink, `Api/Contracts/` response schema, and export path to prove **zero** occurrences of a plaintext credential secret after its show-once dialog closes; record the findings in `specs/006-integration-hub/coordination-log.md` (NFR-6)
- [ ] T244 [P] Repo-wide audit for SC-009 across `src/Nabadat.IntegrationHub/Application/Monitoring/` and `Api/Contracts/` — prove every mobile/email/customer-name read path routes through `PiiMaskingFormatter`, i.e. **zero unmasked-access code paths** exist in Phase 1 for list, detail, and export; record the findings in `specs/006-integration-hub/coordination-log.md` (NFR-9/FR-S8-03)
- [ ] T245 **Resolve the open GP-03 question raised in plan.md's Constitution Check**: does a right-to-erasure request need to redact historical `IntegrationRequestLog.parameters_received` rows carrying a given contact's PII? spec.md is silent and it was not raised in either clarification round. Take it to the PO; if it needs a spec change, run `/speckit-clarify` rather than resolving it in code

### Bilingual, theme, and accessibility passes

- [ ] T246 [P] SC-012 bilingual parity pass — render all eight screens in AR (RTL) and EN (LTR); run the CLAUDE.md self-review regex `-\[#[0-9a-fA-F]{3,8}\]` over `frontend/src/features/integration-hub/` and require **0 hits** (hex in a Tailwind class never re-themes for a tenant), plus a physical-direction-property scan (`pl-`, `pr-`, `ml-`, `mr-`, `text-left`, `text-right`, `rounded-l-`, `rounded-r-`, `border-l-`, `border-r-`) requiring 0 hits
- [ ] T247 [P] Light + dark theme pass over all eight screens — verify no neutral SVG/track chrome is a hardcoded light-slate hex, that borders use the `border-border`/`border-input` tokens rather than low-alpha navy, and that every D-scale status badge pairs colour with an icon (NFR-10)
- [ ] T248 [P] SC-014 accessibility pass — automated axe scan of all eight screens in **both** LTR and RTL requiring 0 WCAG 2.1 AA violations; verify keyboard-only operation of every dialog/drawer (Esc closes), visible focus rings, and `prefers-reduced-motion` support (NFR-11)
- [ ] T249 [P] NFR-12 responsive pass over `frontend/src/features/integration-hub/pages/` — tiles collapse to two/one columns, the sidebar hides below tablet width, and every table scrolls horizontally inside its own `overflow-x-auto` container without the page body scrolling sideways

### Copy, contract, and catalogue audits

- [ ] T250 [P] Normative-copy audit — every result-code message emitted by `ResultCodeMapper` matches spec.md's exact copy patterns verbatim (F0.3), and the D-1/D-2/D-3/D-4/D-5/D-6/D-7 dialog copy matches the ratified text, in **both** EN and AR
- [ ] T251 [P] Arabic-copy review — confirm every AR string in `frontend/src/i18n/ar.json` for this module was written **natively in فصحى**, not translated from the English (CLAUDE.md Brand Voice), and that no AR body text sits at `text-xs`
- [ ] T252 [P] Cross-module contract audit — re-verify the two **real** integrations still hold (M-10's `POST /api/v1/authorization/scope/parameters` and M-01's `ISurveyRenderService`) and that the three M-02/M-04 stub ports remain no-ops with no accidental production coupling (contracts/published-interfaces.md)
- [ ] T253 [P] Confirm the 23 built-in parameters seeded in `IntegrationHub_Baseline.sql` match the SRS catalogue exactly — names, API field names, data types, and default usage flags — and that Searchable is absent (`[PO-G26]`)

### Documentation and closure

- [ ] T254 [P] Run `specs/006-integration-hub/quickstart.md` end to end against a fresh environment and correct any drift
- [ ] T255 [P] Update `.claude/skills/clickthrough-parity/route-map.md` with all nine M-13 page-bearing routes and their per-story audit results
- [ ] T256 Run `/speckit-analyze` for a final cross-artifact consistency check across spec.md, plan.md, and tasks.md

### Release gate

- [ ] T257 **Full-module click-through parity audit — run before the module is pushed.** `/clickthrough-parity 006-integration-hub` with a **bare feature and NO phase**. This is not a repeat of the per-story runs: only whole-module scope can see cross-page **placement** differences (the same control sitting on a different page than the design), because those need the module's full page map. Triage the report; `--fix` what the frontend lead accepts; escalate the Needs-discussion list. `record-audit.py` stamps the result, and that stamp is what unblocks `git push` to `main`/`master` via `.claude/hooks/parity-gate.py`
- [ ] T258 Full-solution verification — `dotnet test Nabadat.sln` green (unit + integration + contract), `npm run build` green in `frontend/`, and `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~IntegrationHub"` green with the stack up

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately
- **Foundational (Phase 2)**: depends on Setup — **BLOCKS every user story**
- **User Stories (Phases 3–12)**: all depend on Foundational
- **Polish (Phase 13)**: depends on every story you intend to ship

### User Story Dependencies

Unusually for this template, M-13's P1 stories have **real ordering constraints** — the module is a
pipeline, not a set of independent CRUD screens:

- **US1 (P1, Service Channels)** — depends only on Foundational. **The true entry point**: nothing
  else is testable without a channel.
- **US2 (P1, Parameter Catalogue)** — depends only on Foundational; the 23 built-ins are seeded in
  T020, so US1's contract editor works before US2 ships. Can run **parallel to US1**.
- **US3 (P1, Integration Wizard)** — needs **US1** (an active channel to attach to) and reads US2's
  catalogue for its Step-3 accepted-parameters preview.
- **US4 (P1, Inbound Requests)** — needs **US1 + US3** (a channel and a provisioned endpoint) and
  US2's parameter definitions for type validation.
- **US5 (P1, Monitoring & Logs)** — needs **US4** to have written `integration_request_logs` rows;
  its SCR-01 list portion is built in US3 (T100) and extended here (T153).
- **US6 (P2, Mappings)** — needs US2 (mapping-enabled parameters) and US4 (to populate the unmapped
  queue at ingestion).
- **US7 (P2, Excel bulk)** — extends **US6**; same page, same controller.
- **US8 (P2, Credential lifecycle)** — extends **US3**; same wizard step, same controller.
- **US9 (P2, Permissions)** — cross-cutting; it hardens every controller and page from US1–US8, so it
  lands **after** them, though its unit tests can be written earlier.
- **US10 (P3, Activate/Deactivate)** — needs US1 + US3; its consequences are already asserted
  cross-story in US1 (AC 5) and US4 (AC-F0-05), so only the toggle UX and reactivation path remain.

### Within Each User Story

- Unit tests are written and **must FAIL** (Red Checkpoint) before any implementation task is read
- Integration/scenario + E2E tests run at the per-story checkpoint, never between implementation tasks
- Click-through parity runs **after** that checkpoint is green, as its own assigned task — never
  before, and never automatically
- Entities → data-access store + port → business service → controller → frontend hook → pages
- Story complete before moving to the next priority

### Parallel Opportunities

- All Setup `[P]` tasks (T003–T006) run in parallel
- Foundational `[P]` tasks split into three independent tracks: Domain (T007–T010), adapters
  (T016–T018), and frontend shell (T021, T024)
- **All unit-test tasks within a story are `[P]`** — different files, no shared state
- **US1 and US2 can run fully in parallel** after Foundational — different `Application/` subdomains,
  different pages, different test folders
- **Team split (research.md §1)**: with one backend and one frontend engineer, the frontend tasks of
  story N run in parallel with the backend tasks of story N+1 once story N's API contract is fixed.
  Backend work itself is **strictly sequential** — there is no second backend engineer to split the
  write path from the read path

---

## Parallel Example: User Story 1

```bash
# All five unit-test tasks together (write FIRST, must fail):
Task: "Unit tests for ChannelIdSanitizer in tests/Nabadat.IntegrationHub.UnitTests/Channels/ChannelIdSanitizerTests.cs"
Task: "Unit tests for ChannelIdUniquenessValidator in .../Channels/ChannelIdUniquenessValidatorTests.cs"
Task: "Unit tests for ChannelIdLockGuard in .../Channels/ChannelIdLockGuardTests.cs"
Task: "Unit tests for ParameterContractDependencyRule in .../Channels/ParameterContractDependencyRuleTests.cs"
Task: "Unit tests for ChannelNameValidator in .../Channels/ChannelNameValidatorTests.cs"

# --- Red Checkpoint T030R (non-parallel) — commit the red baseline ---

# Then the five pure-logic implementations together:
Task: "Implement ChannelIdSanitizer in src/Nabadat.IntegrationHub/Application/Channels/ChannelIdSanitizer.cs"
Task: "Implement ChannelIdUniquenessValidator in .../Application/Channels/ChannelIdUniquenessValidator.cs"
Task: "Implement ChannelIdLockGuard in .../Application/Channels/ChannelIdLockGuard.cs"
Task: "Implement ParameterContractDependencyRule in .../Application/Channels/ParameterContractDependencyRule.cs"
Task: "Implement ChannelNameValidator in .../Application/Channels/ChannelNameValidator.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL** — blocks everything)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: create a service channel with a parameter contract, independently
5. Demo if ready

### Incremental Delivery

The honest increment boundary for this module is **US1 + US2 + US3 + US4** — a channel, a catalogue,
a provisioned endpoint, and a request that actually flows. Everything before US4 is configuration
with nothing calling it.

1. Setup + Foundational → foundation ready
2. US1 (+ US2 in parallel) → configuration surface demoable
3. US3 → an endpoint is provisioned
4. US4 → **a real request succeeds end to end — the first genuinely demoable increment**
5. US5 → the loop is observable; **all P1 work is done**
6. US6 → US7 → US8 → US9 (P2) → US10 (P3), each independently testable
7. Phase 13 → verification passes and the release gate

### Parallel Team Strategy (2 resources — AbuKr backend, Marwan frontend)

1. Both complete Setup + Foundational together (T021–T024 are Marwan's; T001–T020 are AbuKr's)
2. Thereafter, pipeline by one story: AbuKr runs story N's backend (unit tests → red → services →
   controller) while Marwan builds story N−1's pages against the now-fixed contract
3. Backend stories stay strictly sequential; the parity audits are Marwan's to run and triage

---

## Notes

- `[P]` = different files, no dependencies
- `[Story]` maps every task to its user story for traceability
- **US4 is the only story with no E2E and no click-through-parity subsection** — it declares
  `e2e-tests: skipped` because Feature 0 is headless with no console screen
- **No story in this feature declares `unit-tests: skipped`** — all ten are backend-bearing, so all
  ten carry a Unit Tests subsection and a Red Checkpoint
- Verify tests fail before implementing; commit the red baseline via `/speckit-git-commit`
- Commit after each task or logical group; stop at any checkpoint to validate a story independently
- **Build every page click-through-blind.** A page ported or copied from the click-through makes its
  parity run VOID — reported `NOT AUDITED`, never "0 defects"
