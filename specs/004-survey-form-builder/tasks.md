---
description: "Task list for Survey & Form Builder (M-01) — dependency-ordered per user story"
---

# Tasks: Survey & Form Builder (M-01)

**Input**: Design documents from [/specs/004-survey-form-builder/](./)
**Module**: `M-01` → `Nabadat.SurveyBuilder` (constitution AMENDMENT-008)
**Team allocation** (from user input): backend — **abukr** (Surveys / Sections / QuestionsSets / Questions / Routing sub-domains + `_Baseline.sql`) and **attia** (Templates / Translations / Appearance / Preview / Report / Analytics / RenderPlan / HtmlSanitisation sub-domains + ES read adapters + M-04 coordination); frontend — **marwan** (entire `frontend/src/features/surveys/` tree + `SurveyBuilder/` E2E folder). Owner initials appear inline on each task in the form `(A)` / `(B)` / `(F)`.

**Prerequisites**: [plan.md](./plan.md) (required), [spec.md](./spec.md) (required for user stories), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md).

**Tests**: Per CLAUDE.md "Unit Test Policy", **unit tests are MANDATORY for every backend-bearing user story** in this feature (US1–US6, US8, US9). US7 (Preview) legitimately declares `unit-tests: skipped — preview is a client-side renderer`, so it emits no Unit / Red-Checkpoint / Integration subsections — only the E2E block. Every page-bearing story (US1–US9) emits an E2E subsection *after* implementation (no Red Checkpoint on the E2E lane).

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `- [ ] [ID] [P?] [Story?] Description (owner)`

- **[P]**: Task can run in parallel (different files, no dependencies on incomplete tasks).
- **[Story]**: `US1`…`US9` for user-story phase tasks. Setup / Foundational / Polish tasks have no story label.
- **owner**: `(A)` abukr, `(B)` attia, `(F)` marwan.
- File paths are absolute-from-repo-root and land in the canonical module folders per architecture-constitution Article 1A.

## Test Generation Rules (BINDING — CLAUDE.md "Unit Test Policy" + "E2E Test Policy")

- xUnit v3 + FluentAssertions 6.12.x + NSubstitute 5.x for units; `FakeTimeProvider` for any `TimeProvider`. One test class per file. Naming `<Subject>_<expected>_when_<condition>`.
- Backend stories emit **Unit Tests → Red Checkpoint → Implementation → Integration & API / Scenario** in that order. Red Checkpoint is non-parallel and MUST precede any implementation task.
- Integration & API tests run at the per-story checkpoint (not between implementation tasks) — they need Docker (Testcontainers Postgres + Elasticsearch).
- E2E (Playwright over MSTest) runs at the per-story checkpoint and is authored with the `e2e-testing` skill; no Red Checkpoint on the E2E lane.
- If a backend story lacks coverage blocks AND lacks a skip declaration → halt (spec defect). Every US1–US9 story in this feature is covered — verified in [checklists/requirements.md](./checklists/requirements.md).

## Frontend Task Rule *(applies whenever a task ships UI under `frontend/`)*

Every frontend task assumes the repo-root `CLAUDE.md` design system is loaded: Component Sourcing Rule (search `frontend/src/components/` first), Two-Palette Rule (`nb-*` chrome / `d{n}-*` KPI status), logical RTL properties only (`ps-*`, `me-*`), 16 px radius ceiling, one-blue action-button rule, dark-mode ladder. Every new `.tsx` file is verified by the two regex sweeps in CLAUDE.md § Theming self-review before it can land.

## Backend Module Folder Structure Rule

All backend paths land in the canonical `src/Nabadat.SurveyBuilder/` layout (four layer folders — Api / Application / Domain / Infrastructure). One type per file. A new bounded concern = a new `Application/<SubDomain>/` folder + its mirror `tests/Nabadat.SurveyBuilder.UnitTests/<SubDomain>/`.

## Backend Data-Access Task Rule

Persistence follows DB-08 / database-constitution Article 7. Every table-touching story emits: entity + `IEntityTypeConfiguration<T>` in `Infrastructure/Persistence/Configurations/` + row added to `_Baseline.sql` (never EF migrations) + `<Aggregate>Store` port in `Application/<Domain>/Interfaces/` + business service depending on the port. `ITenantDbContext.ExecuteAsync` is the sole transaction boundary. `TimeProvider` is injected.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project skeleton — create the module + test projects, wire composition, scaffold the frontend feature.

- [X] T001 Create `src/Nabadat.SurveyBuilder/Nabadat.SurveyBuilder.csproj` targeting .NET 10 with references to `Nabadat.UserManagement` published-interface project (for `IPermissionChecker`), `Nabadat.CustomerJourneyManagement` (for `IJourneyReader`), `Nabadat.KpiManagement` (for `IKpiCatalogReader`), `Nabadat.TenantAdmin` (for `ITenantSettingsReader` + `ITenantDesignGuidelinesReader`), `EFCore.Npgsql` 10, `Elastic.Clients.Elasticsearch` 8.x, `Ganss.Xss` 9.x, `Microsoft.Extensions.Hosting` 10. (A)
- [X] T002 [P] Create `tests/Nabadat.SurveyBuilder.UnitTests/Nabadat.SurveyBuilder.UnitTests.csproj` (xUnit v3, FluentAssertions 6.12.x, NSubstitute 5.x, Microsoft.Extensions.TimeProvider.Testing 9.x) mirroring the M-10 reference test project pins per CLAUDE.md rule 14. (A)
- [X] T003 [P] Create `tests/Nabadat.SurveyBuilder.IntegrationTests/Nabadat.SurveyBuilder.IntegrationTests.csproj` (xUnit v3 + FluentAssertions + Testcontainers.PostgreSql 4.x + Testcontainers.Elasticsearch 4.x + Microsoft.AspNetCore.Mvc.Testing 10). (B)
- [X] T004 [P] Create `tests/Nabadat.SurveyBuilder.ContractTests/Nabadat.SurveyBuilder.ContractTests.csproj` (xUnit v3 + FluentAssertions + NSubstitute) for `ISurveyRenderService` + `IActiveSurveyReader` contract tests consumed by M-02 / M-04. (B)
- [X] T005 [P] Scaffold `frontend/src/features/surveys/` with `api/`, `pages/`, `components/`, `hooks/`, `i18n/` sub-folders (empty index files); add `en.json` / `ar.json` seeded with keys for the sidebar entries "Surveys" and "Templates" (Modern Standard Arabic per NFR-2). (F)
- [X] T006 [P] Register the new module in `Nabadat.TenantAdmin/Program.cs` via `builder.Services.AddSurveyBuilderModule(builder.Configuration)`; leave the extension method body empty (T007 populates it). (A)
- [X] T007 Create `src/Nabadat.SurveyBuilder/SurveyBuilderServiceCollectionExtensions.cs` — composition root exposing `AddSurveyBuilderModule(IServiceCollection, IConfiguration)`; register controllers, sanitiser policy version, and DI bindings for every port introduced in later phases. Marker comments per sub-domain block (Surveys, Sections, …) so later tasks add registrations in-place. (A)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-cutting infrastructure every user story depends on. **⚠️ CRITICAL**: no user story may begin until Phase 2 completes.

### Baseline SQL + EF context wiring (backend)

- [X] T008 Create `src/Nabadat.SurveyBuilder/Infrastructure/Migrations/_Baseline.sql` — DDL for all 9 owned tables (`surveys`, `sections`, `questions_sets`, `questions`, `routing_maps`, `themes`, `survey_translations`, `templates`, `template_snapshots`) per [data-model.md](./data-model.md) §2.1–2.9 (corrected count — `/speckit-analyze` 2026-07-15 found this previously said "10" while listing 9; there is no separate `question_translations` table, only `survey_translations`, which also carries per-question translatable keys); include every index specified there (idx_surveys_status_updated_at, idx_surveys_bound_journey_id, idx_surveys_owner_user_id, idx_surveys_name_en_lower, idx_sections_survey_id_order, idx_questions_sets_section_id, idx_questions_survey_id, idx_questions_section_id_order, idx_questions_set_id_order (partial), idx_questions_kpi_code (partial), unique idx on `(source_question_id, answer_key)` for routing_maps, idx_routing_maps_survey_id, idx_routing_maps_target_question_id, unique idx on themes.survey_id, unique idx on `(survey_id, locale)` for survey_translations, idx_templates_class_name_en, GIN idx on templates.tags, GIN idx on templates.sectors). **No `tenant_id` columns; DB-02.** (A)
- [X] T009 Create `src/Nabadat.SurveyBuilder/Application/Interfaces/ITenantDbContext.cs` — exposes `DbSet<Survey>`, `DbSet<Section>`, `DbSet<QuestionsSet>`, `DbSet<Question>`, `DbSet<RoutingMap>`, `DbSet<Theme>`, `DbSet<SurveyTranslation>`, `DbSet<Template>`, `DbSet<TemplateSnapshot>` (types are declared in Domain — T010–T012), `SaveChangesAsync`, and the transaction boundary `ExecuteAsync(Func<Task>)` / `ExecuteAsync<T>(Func<Task<T>>)`. (A)
- [X] T010 [P] Create `src/Nabadat.SurveyBuilder/Application/Interfaces/ICurrentTenant.cs` (read-only per AD-07; concrete impl lives in the host layer — already provided by `Nabadat.TenantAdmin`). (A)
- [X] T011 [P] Create `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/TenantDbContext.cs` implementing `ITenantDbContext` — schema-per-tenant via `optionsBuilder.UseNpgsql(connString).MigrationsHistoryTable("__ef_history", tenantSchema)`; NO EF migrations (DB-08 rule 6) — the context maps onto `_Baseline.sql`. Includes `ExecuteAsync` (opens a transaction, executes, calls `SaveChangesAsync`, commits, rolls back on throw). (A)
- [X] T012 [P] Create `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Configurations/` folder with placeholder `.editorconfig` — the per-entity `IEntityTypeConfiguration<T>` files land inside per-story phases. (A)

### Domain value objects + status machine

- [X] T013 [P] Create `src/Nabadat.SurveyBuilder/Domain/ValueObjects/SurveyStatus.cs` — enum `Draft | PendingReview | Active | Paused | Archived` + a static `AllowedTransitions(from, to, actorRole, isDestructive)` returning `bool` per the Status Transition Matrix (BR-1.4). (A)
- [X] T014 [P] Create `src/Nabadat.SurveyBuilder/Domain/ValueObjects/QuestionType.cs` + `QuestionSubType.cs` — enums per Question Type Catalogue in spec.md; `IsRoutingEligible(type, subType, insideSet)` static per FR-9.5 + slider exclusion. (A)
- [X] T015 [P] Create `src/Nabadat.SurveyBuilder/Domain/ValueObjects/ActivePeriod.cs` (`{days, hours}` nullable, serialised to jsonb via a value converter). (A)
- [X] T016 [P] Create `src/Nabadat.SurveyBuilder/Domain/ValueObjects/KpiBinding.cs` (record: `KpiCode, Perspective?, BoundJourneyOn, StageId?, TouchpointId?`) with static `IsValid(...)` computing the FR-8.4 constraints locally. (A)
- [X] T017 [P] Create `src/Nabadat.SurveyBuilder/Domain/ValueObjects/LayoutMode.cs` (enum `single | section | question | count` + `RequiresQuestionsPerPage(mode)`). (A)

### Cross-module port declarations (Domain/Interfaces)

- [X] T018 [P] Create `src/Nabadat.SurveyBuilder/Domain/Interfaces/ISurveyRenderService.cs` — the M-01 published interface consumed by M-02 / M-04 per [contracts/published-interface.md](./contracts/published-interface.md). Include `RenderPlan`, `RenderSection`, `RenderItem`, `RenderQuestion`, `RenderSetSample`, `RoutingTarget`, `SurveyDefinition`, `SurveyId`, `RespondentContext`, `LocaleCode` records — one per file if any grows large. (B)
- [X] T019 [P] Create `src/Nabadat.SurveyBuilder/Domain/Interfaces/IActiveSurveyReader.cs` + `IActiveSurveyDefinitionProbe.cs` per contracts/published-interface.md. (B)
- [ ] T020 [P] Declare the reverse-dependency interfaces M-01 depends on but does NOT own — add TypeForwardedTo references in `Nabadat.SurveyBuilder.csproj` OR create local wrapper files if not already exposed by the owning modules: `IJourneyReader` (M-16), `IKpiCatalogReader` (M-06), `ITenantSettingsReader` + `ITenantDesignGuidelinesReader` (M-11), `IPermissionChecker` (M-10), `IEventLogWriter` (M-17), `INotificationDispatcher` (M-09), `IFileStorageService`. **⚠ Confirmed by `/speckit-analyze` (2026-07-15) — this is not a conditional "if missing" check, every port below except `IEventLogWriter` (M-17, already exists and is used by other modules) is currently absent from the repo.** `IChannelSurveyRulesReader` (needed by T071, US1 MVP), `INotificationDispatcher` (needed by T116, US2), `ITenantSettingsReader`/`ITenantDesignGuidelinesReader` (needed by T080, US1 MVP) have no owning module at all yet (M-02, M-09, M-11 don't exist under `src/`); `IJourneyReader`/`IKpiCatalogReader`/`IPermissionChecker` need to be added to the M-16/M-06/M-10 modules, which do exist. **Halt and resolve per-port before proceeding** — see plan.md § "Cross-module dependencies to unblock before US1 / US2 ship" for the full blocker table; the resolution approach (stub-and-swap vs. wait for the owning module vs. descope) is an open decision not made by this task. (A + B split — A owns M-16/M-06; B owns M-10/M-11/M-17/M-09/M-04.)
- [X] T021 Coordinate with the M-04 owner to ship `IResponsePurgeService.PurgeSurveyResponsesAsync(SurveyId, ActorId, CorrelationId, CancellationToken)` in `Nabadat.ResponseCollection.Domain.Interfaces` per [research.md § 4.5](./research.md#45-iresponsepurgeservice-m-04-new-port). Track in the cross-module coordination log. Blocks US1's destructive Return-to-Draft path (BR-1.6) but does NOT block the rest of US1. **Same coordination is still needed for `IChannelSurveyRulesReader` (M-02), `INotificationDispatcher` (M-09), and the two M-11 readers (see plan.md's blocker table) — not yet assigned to a task.** (B)
- [X] T022 File **constitution AMENDMENT-012** per [contracts/published-interface.md](./contracts/published-interface.md) — (a) correct M-01's owned-tables list in Section 3 to the actual 9-table Feature 004 set; (b) register **four** new Section 4 events: `survey.responses.purged` (source `M-04`, downstream `M-05, M-06, M-07`) **and** `survey.created` / `survey.status.changed` / `survey.submitted_for_review` (source `M-01`, no downstream) — the latter three are referenced by T044, T102, T110, T124, T125 but were previously undeclared (`/speckit-analyze` 2026-07-15 finding). Blocking prerequisite for shipping BR-1.6 to production **and** for T044/T102/T110/T124/T125 to legally emit their events. (B)

### ETag / Idempotency / API-05 / error-envelope middleware

- [X] T023 Create `src/Nabadat.SurveyBuilder/Api/Middleware/EtagMiddleware.cs` — extracts `row_version` from response DTOs and sets `ETag: W/"<n>"`; on writes, reads `If-Match: W/"<n>"` from the incoming request and injects a `RequestETag` into the DI-scoped `ICurrentETag`; on mismatch, 409 `<aggregate>.conflict` per API-05. (A)
- [X] T024 [P] Create `src/Nabadat.SurveyBuilder/Api/Middleware/IdempotencyKeyMiddleware.cs` — stores `(Idempotency-Key, request-hash) → response snapshot` in an `IIdempotencyStore` port (impl in Infrastructure) with 24 h TTL; replays the same response on repeat within TTL per APIs-constitution Article 7.1. (A)
- [X] T025 [P] Create `src/Nabadat.SurveyBuilder/Api/Middleware/ApiErrorEnvelopeMiddleware.cs` — catches all M-01 exceptions and produces `{"error":{"code","message","correlation_id","tenant_id"}}` per API-05; error-code namespaces per [research.md § 9](./research.md#9-idempotency-etag-scope-and-api-05-error-codes). (A)
- [X] T026 Wire T023/T024/T025 into the ASP.NET Core pipeline in `SurveyBuilderServiceCollectionExtensions` — order: correlation-id → tenant-context → error-envelope → idempotency-key → etag. (A)

### Sanitiser adapter

- [X] T027 [P] Create `src/Nabadat.SurveyBuilder/Application/HtmlSanitisation/Interfaces/IHtmlSanitiser.cs` — port with `SanitisedResult Sanitise(string input, SanitiserPolicyVersion policyVersion)`. (B)
- [X] T028 [P] Create `src/Nabadat.SurveyBuilder/Application/HtmlSanitisation/SanitiserPolicyVersion.cs` — record with `PolicyVersion` int + immutable allowlist per [research.md § 1](./research.md#1-html-sanitiser-for-welcome--thank-you-rich-text-editor). (B)
- [X] T029 Create `src/Nabadat.SurveyBuilder/Infrastructure/HtmlSanitisation/GannsHtmlSanitiserAdapter.cs` — Ganss.Xss v9 implementation of `IHtmlSanitiser`; configures the v1 allowlist (allowed tags, attributes, URL schemes, stripped tags) exactly as [research.md § 1](./research.md#1-html-sanitiser-for-welcome--thank-you-rich-text-editor) enumerates. Registers `SanitiserPolicyV1` singleton via DI. (B)

### Application factory + shared test infrastructure

- [X] T030 Create `tests/Nabadat.SurveyBuilder.IntegrationTests/Infrastructure/SurveyBuilderApplicationFactory.cs` — `WebApplicationFactory<Program>` boots the module in-process; Testcontainers Postgres 16 applies `_Baseline.sql` per test class; seeding helpers (`SeedDraftSurvey`, `SeedActiveSurvey`, `SeedTemplate`, `SeedSection`, `SeedQuestion`, `SeedResponse`). Mirrors the M-10 `UserManagementApplicationFactory` reference. (B)
- [X] T031 [P] Create `tests/Nabadat.SurveyBuilder.IntegrationTests/Infrastructure/EsTestcontainer.cs` — Testcontainers Elasticsearch 8.x fixture; seeding helpers for `tenant_{tenantId}_responses` + `tenant_{tenantId}_analytics` documents used by Report / Analytics integration tests. (B)
- [X] T032 [P] Create `tests/Nabadat.SurveyBuilder.UnitTests/TestSupport/InMemorySurveyStore.cs`, `InMemorySectionStore.cs`, `InMemoryQuestionsSetStore.cs`, `InMemoryQuestionStore.cs`, `InMemoryTemplateStore.cs`, `InMemoryTranslationStore.cs`, `InMemoryRoutingMapStore.cs`, `InMemoryThemeStore.cs`, `RecordingTenantDbContext.cs` (its `ExecuteAsync` runs the delegate; asserts a wrapping transaction), `TestTime.cs` (`FakeTimeProvider` anchor). (A)

### Frontend scaffold + sidebar wiring

- [X] T033 [P] **⚠ Confirmed by `/speckit-analyze` (2026-07-15): there is no `AppRouter.tsx` or `NAV_ITEMS`/`ROLE_NAV_KEYS` config in this repo — routing and nav are hand-coded, not table-driven. Verify the current shape of `frontend/src/App.tsx` and `frontend/src/components/layout/AppLayout.tsx` before starting this task; the description below is written against the CLAUDE.md convention, which this repo does not yet follow.** Register the `/surveys`, `/surveys/new`, `/surveys/:id/*`, `/templates`, `/templates/:id/edit` routes as new `<Route>` elements inside the existing `<Routes>` tree in `frontend/src/App.tsx` (alongside `/journeys`, `/kpi-management`, etc.), nested under the existing `<Route element={<AuthGuard />}><Route element={<AppLayout />}>` wrapper so auth + layout apply; each route lazy-loads a page component from `features/surveys/pages/`. (F) — **done 2026-07-20**: 5 routes registered in `App.tsx` under AuthGuard→AppLayout, lazy-loaded (React.lazy + Skeleton fallback, separate chunks verified in build); `SurveyEditorRoutes.tsx` owns the `/surveys/:id/*` sub-route table (index → settings per FR-1.4).
- [X] T034 [P] **⚠ Same caveat as T033 — no `app-sidebar.tsx` exists.** Add sidebar entries for Surveys and Templates to `frontend/src/components/layout/AppLayout.tsx`, following its existing pattern: a new `<SidebarGroup>` (or an entry in an existing group, e.g. alongside `nav.kpiEngine`) containing `<SidebarMenuItem>`s gated by a permission boolean (matching `canViewUsers` / `canAuthorJourneys` / `canViewKpis`), with `nav.surveys` / `nav.templates` i18n keys added to `frontend/src/i18n/locales/en.json` + `ar.json`. Scope visibility per persona (P-01, P-03 full; P-02, P-06 read-only) using the same permission-boolean pattern already used for the other nav groups — there is no separate `ROLE_NAV_KEYS` allowlist file to edit. (F) — **done 2026-07-20**: new "Voice of Customer" SidebarGroup (Surveys + Templates, ClipboardList/LayoutTemplate icons) gated by `canViewSurveys` (P-01/P-02/P-03/P-06); `nav.templates` + `nav.voc` added to en/ar locales (فصحى).
- [X] T035 [P] Create `frontend/src/features/surveys/api/etag.ts` — the `If-Match` header helper wrapping `callJson()` (see CLAUDE.md § Backend Integration for the fetch pattern). Handles 412 conflict → throws a typed `ETagConflictError` the UI catches to open `EtagConflictDialog`. (F) — **done 2026-07-20**: `callJsonWithEtag` transport (Bearer + ETag capture + If-Match + Idempotency-Key); `ETagConflictError` thrown on 412 AND 409 `.conflict` codes; `SurveysApiError` carries the API-05 envelope.
- [X] T036 [P] Create `frontend/src/features/surveys/hooks/useUnsavedChangesGuard.ts` — Router-level `beforeUnload` guard blocking navigation when the current form is dirty (NFR-5 Q1). Reused by every form page. (F) — **done 2026-07-20**: `beforeunload` guard + `confirmIfDirty()` helper. In-app `<Link>` blocking impossible on the declarative router (`useBlocker` needs a data router) — tracked as TODO-M01-027.
- [X] T037 [P] Create `frontend/src/features/surveys/hooks/useSurveyEtag.ts` — hook holding the current survey's ETag; wraps read fetches to capture the ETag from response headers and provides a `withIfMatch(mutator)` helper for writes. (F) — **done 2026-07-20**: ref+state ETag holder with `captureFrom(read)` / `withIfMatch(mutate)` / `setEtag` / `reset`.
- [X] T038 [P] Create `frontend/src/features/surveys/hooks/useSurveyEditLock.ts` — computes the effective edit-lock state from `(surveyStatus, callerRole, callerUserId, submittedBy)` per BR-15.1 + Q8 team-owned rules. Returns `{canEdit, reason}`. (F) — **done 2026-07-20**: pure `computeEditLock` (exported for tests) + session-bound hook; encodes BR-15.1, Q8 team-owned Drafts, BR-1.5/1.6 Active/Paused locks, FR-1.14 Archived, read-only personas; typed `reason` union for banner copy.

**Checkpoint**: Foundational phase complete. Every user story can now begin. **No user-story tasks may start before every task in Phase 2 is checked complete.**

---

## Phase 3: User Story 1 — Author, save and publish a basic survey (Priority: P1) 🎯 MVP

**Goal**: A P-01 or P-03 opens the Library, picks a build method, completes Settings, enters the Builder, adds at least one section with one question (including one KPI question bound to Stage → Touchpoint), and sets the survey Active — with all supporting rules (F1 library filters, F3 settings validation, F4 appearance, F5 build-method chooser, F8 builder, layered KPI binding, comments/sentiment toggles, Publish gate BR-1.7, Pause-with-rules confirmation FR-1.10, Archive/Unarchive per BR-1.3, ETag on every write per Q1).

**Independent Test**: Tester with P-01 credentials creates "Post-visit satisfaction", picks the "Branch Visit" journey, adds one Scale + one KPI (CSAT) bound to a Stage → Touchpoint, sets Active, and verifies (a) library row shows correct Type/Journey/Status, (b) row-click deep-link opens Settings pre-filled, (c) audit-log entry via M-17.

### Unit Tests for User Story 1 (write FIRST, must FAIL before implementation) ⚠️

- [X] T039 [P] [US1] Unit tests for `SurveyValidator` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/SurveyValidatorTests.cs` — cover the spec.md US1 Required cases: `Validate(new SurveyDraft { NameEn = "" }) → Invalid("survey.name_en.required")`; `Validate(new SurveyDraft { NameEn = "Post-visit", BoundJourney = null }) → Valid, SurveyType = "SeasonalRelational"`; `Validate(new SurveyDraft { NameEn = new string('x', 201), … }) → Invalid("survey.name_en.max_length")`. (A)
- [X] T040 [P] [US1] Unit tests for `SurveyTypeSyncService` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/SurveyTypeSyncServiceTests.cs` — `OnBoundJourneyChanged(survey, journeyId) → survey.SurveyType = "Transactional"` (BR-3.3); `OnBoundJourneyChanged(survey, null) → survey.SurveyType = "SeasonalRelational"`. (A)
- [X] T041 [P] [US1] Unit tests for `StatusTransitionPolicy` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/StatusTransitionPolicyTests.cs` — the full Status Transition Matrix from spec.md; must include `Allowed(current: "Archived", next: "Active") → false`, `Allowed(current: "Archived", next: "Draft") → true` (BR-1.3, FR-1.14), `Allowed(current: "Draft", next: "Active") → false when survey has unpublished pending review` (§3.15 lock). (A)
- [X] T042 [P] [US1] Unit tests for `PublishGateService` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/PublishGateServiceTests.cs` — BR-1.7 (Q9); `EnsureContent(survey with sections=0) → Rejects("publish.requires_content", missing_sections=true)`; `EnsureContent(survey with sections=1, questions=0) → Rejects("publish.requires_content", missing_questions=true)`; `EnsureContent(paused_survey → Active) → skipped (not gated)`. (A)
- [X] T043 [P] [US1] Unit tests for `RulesCountProjection` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/RulesCountProjectionTests.cs` — `Read(surveyId) → returns the count from IChannelSurveyRulesReader` (mocked); Pause with `count > 0` requires confirmation. (A)
- [X] T044 [P] [US1] Unit tests for `AuditWriter` port callers via `SurveyLifecycleService` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/SurveyLifecycleServiceTests.cs` — every status transition emits exactly one M-17 `IEventLogWriter.WriteAsync(...)` call with the correct `EventType` (`survey.published`, `survey.archived`, or audit sub-event) and payload. (A)
- [X] T045 [P] [US1] Unit tests for `QuestionValidator` (per-type + sub-type invariants) in `tests/Nabadat.SurveyBuilder.UnitTests/Questions/QuestionValidatorTests.cs` — `Validate(type: Scale, subType: null) → Invalid("question.subtype.required")` (FR-8.8); `Validate(type: Scale, subType: "slider", sliderSteps: 0) → Invalid("scale.slider.steps.min")`; every combination in Question Type Catalogue has ≥ 1 positive case + ≥ 1 negative case. (A)
- [X] T046 [P] [US1] Unit tests for `KpiBindingValidator` in `tests/Nabadat.SurveyBuilder.UnitTests/Questions/KpiBindingValidatorTests.cs` — `Validate(kpi: "CSAT", boundJourneyOn: true, stage: null, touchpoint: "TP-1") → Invalid("kpi.touchpoint.requires_stage")`; `Validate(kpi: "CSAT", boundJourneyOn: false, stage: "S1", touchpoint: "T1") → Warn+Strip("kpi.binding_ignored_when_bound_journey_off")` (BR-8.2); `Validate(kpi: "CSAT", boundJourneyOn: true, stage: "S1", touchpoint: null) → Valid` (FR-8.4). (A)
- [X] T047 [P] [US1] Unit tests for `KpiBindingChangePolicy` in `tests/Nabadat.SurveyBuilder.UnitTests/Questions/KpiBindingChangePolicyTests.cs` — `OnKpiChanged(question, newKpi) → retains Touchpoint if IJourneyReader.IsBindingValidAsync returns true; clears otherwise; clears Stage if invalid` (BR-8.5). (A)
- [X] T048 [P] [US1] Unit tests for `CommentFieldFlagPolicy` + `SentimentFlagPolicy` in `tests/Nabadat.SurveyBuilder.UnitTests/Questions/CommentAndSentimentFlagPolicyTests.cs` — `CommentFieldFlagPolicy.Apply(question: {comments: true}) → HasCommentField=true, CommentRequired=false, CommentMaxLength=200, CommentLabel="Comments", CommentTravelsToNlp=true` (FR-8.9); `SentimentFlagPolicy.Apply(question: {type: SingleSelect, sentiment: true}) → Warn("sentiment.ignored_for_non_text")` (FR-8.11). (A)
- [X] T049 [P] [US1] Unit tests for `DestructiveReturnToDraftService` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/DestructiveReturnToDraftServiceTests.cs` — happy path invokes `IResponsePurgeService` after the M-01 transaction commits; failure of the purge triggers compensation (revert to prior status); non-destructive Pending → Draft does not call the purge; audit event carries `purged_response_count`. Uses NSubstitute on `IResponsePurgeService`. (A)
- [X] T050 [P] [US1] Unit tests for `AppearanceService` in `tests/Nabadat.SurveyBuilder.UnitTests/Appearance/AppearanceServiceTests.cs` — Inherited mode resolves every token from `ITenantDesignGuidelinesReader`; Customize unlocks; `Save(theme)` with `background_type = Image` requires a `file_handle`. (B)
- [X] T051 [P] [US1] Unit tests for `HtmlSanitiserAdapter` in `tests/Nabadat.SurveyBuilder.UnitTests/HtmlSanitisation/HtmlSanitiserAdapterTests.cs` — Q3 allowlist positive/negative cases: `Sanitise("<p>hi</p>") → "<p>hi</p>"`; `Sanitise("<script>alert(1)</script>") → ""`; `Sanitise("<a href=\"javascript:alert(1)\">x</a>") → "<a>x</a>"` (scheme stripped); `Sanitise("<iframe>") → ""`; `Sanitise("<a onclick=\"...\">") → "<a>"`. (B)

### Red Checkpoint for User Story 1 (MANDATORY) 🔴

- [X] T052 [US1] Run `dotnet test tests/Nabadat.SurveyBuilder.UnitTests --filter "FullyQualifiedName~Surveys|FullyQualifiedName~Questions|FullyQualifiedName~Appearance|FullyQualifiedName~HtmlSanitisation"`. **Valid red states**: compile error (production types don't yet exist) or assertion failure (once types are stubbed). Paste the transcript, then commit the red baseline via `/speckit-git-commit`. Non-parallel. Must precede any US1 implementation task. (A + B)

### Implementation for User Story 1 — Domain entities + configurations

- [X] T053 [P] [US1] Create `src/Nabadat.SurveyBuilder/Domain/Entities/Survey.cs` — every column from [data-model.md § 2.1](./data-model.md#21-surveys) + factory constructor + status-transition method + `IncrementRowVersion()`. (A)
- [X] T054 [P] [US1] Create `src/Nabadat.SurveyBuilder/Domain/Entities/Section.cs` per [data-model.md § 2.2](./data-model.md#22-sections). (A)
- [X] T055 [P] [US1] Create `src/Nabadat.SurveyBuilder/Domain/Entities/Question.cs` per [data-model.md § 2.4](./data-model.md#24-questions); `type_payload` as `QuestionTypePayload` polymorphic record. (A)
- [X] T056 [P] [US1] Create `src/Nabadat.SurveyBuilder/Domain/Entities/Theme.cs` per [data-model.md § 2.6](./data-model.md#26-themes). (B)
- [X] T057 [P] [US1] Create `src/Nabadat.SurveyBuilder/Domain/ValueObjects/QuestionTypePayload.cs` — polymorphic base + `ScalePayload`, `InputFieldPayload`, `SingleSelectPayload`, `MultiSelectPayload`, `YesNoPayload`, `MatrixPayload`, `RankingPayload`, `KpiPayload` records (one per file if any exceeds 30 LOC); `$type` discriminator per [research.md § 5](./research.md#5-question-type-catalogue--ef-mapping-strategy). (A)
- [X] T058 [P] [US1] Create `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Configurations/SurveyConfiguration.cs` — explicit `HasColumnName` per column + FK to nothing + jsonb converter for `ActivePeriod`; concurrency token = `row_version`. (A)
- [X] T059 [P] [US1] Create `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Configurations/SectionConfiguration.cs`. (A)
- [X] T060 [P] [US1] Create `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Configurations/QuestionConfiguration.cs` — jsonb converter for `type_payload` using the `QuestionTypePayload` polymorphic JSON. (A)
- [X] T061 [P] [US1] Create `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Configurations/ThemeConfiguration.cs`. (B)
- [X] T062 [P] [US1] Create `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Configurations/ValueConverters.cs` — shared JSON converters (`ActivePeriodConverter`, `QuestionTypePayloadConverter`, `BackgroundConfigConverter`). (A)

### Implementation for User Story 1 — Stores (per-aggregate data-access services)

- [X] T063 [P] [US1] Create `src/Nabadat.SurveyBuilder/Application/Surveys/Interfaces/ISurveyStore.cs` + `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Stores/SurveyStore.cs` implementing it (Add / Get / Update / Search / GetForETag). Depends on `ITenantDbContext` only. (A)
- [X] T064 [P] [US1] Create `src/Nabadat.SurveyBuilder/Application/Sections/Interfaces/ISectionStore.cs` + `Infrastructure/Persistence/Stores/SectionStore.cs`. (A)
- [X] T065 [P] [US1] Create `src/Nabadat.SurveyBuilder/Application/Questions/Interfaces/IQuestionStore.cs` + `Infrastructure/Persistence/Stores/QuestionStore.cs` (includes `MoveQuestion(...)` — used later in US3). (A)
- [X] T066 [P] [US1] Create `src/Nabadat.SurveyBuilder/Application/Appearance/Interfaces/IThemeStore.cs` + `Infrastructure/Persistence/Stores/ThemeStore.cs`. (B)

### Implementation for User Story 1 — Business services

- [X] T067 [US1] Create `src/Nabadat.SurveyBuilder/Application/Surveys/SurveyValidator.cs` — matching the tests in T039. (A)
- [X] T068 [US1] Create `src/Nabadat.SurveyBuilder/Application/Surveys/SurveyTypeSyncService.cs` — matching the tests in T040. (A)
- [X] T069 [US1] Create `src/Nabadat.SurveyBuilder/Application/Surveys/StatusTransitionPolicy.cs` — matching T041. (A)
- [X] T070 [US1] Create `src/Nabadat.SurveyBuilder/Application/Surveys/PublishGateService.cs` — matching T042; depends on `ISurveyStore` for count checks. (A)
- [X] T071 [US1] Create `src/Nabadat.SurveyBuilder/Application/Surveys/RulesCountProjection.cs` + `Interfaces/IChannelSurveyRulesReader.cs` (published by M-02); read-only projection returning `rules_count` per survey. (A)
- [X] T072 [US1] Create `src/Nabadat.SurveyBuilder/Application/Surveys/DestructiveReturnToDraftService.cs` — matches T049 tests; wraps M-01 write in `ITenantDbContext.ExecuteAsync`; calls `IResponsePurgeService` after commit; compensates on failure; audits via `IEventLogWriter`. Returns 501 `survey.return_to_draft.purge_service_unavailable` when `IResponsePurgeService` is not yet registered (until T021 completes). (A)
- [X] T073 [US1] Create `src/Nabadat.SurveyBuilder/Application/Surveys/SurveyLifecycleService.cs` — orchestrates all self-serve transitions (Pause / Reactivate / Archive / Unarchive / destructive Return-to-Draft); composes `SurveyValidator`, `StatusTransitionPolicy`, `PublishGateService`, `RulesCountProjection`, `DestructiveReturnToDraftService`, `SurveyTypeSyncService`, `IEventLogWriter`. Emits `survey.published` on any transition into Active and `survey.archived` on Archive. (A)
- [X] T074 [US1] Create `src/Nabadat.SurveyBuilder/Application/Surveys/SurveyCommandService.cs` — Create / Update / Clone (FR-1.8 copy-all-data, starts with `responses_count = 0`) / Get / Search. Sanitises `welcome_html` / `thanks_html` via `IHtmlSanitiser` on every save; persists `sanitiser_policy_version = 1`. (A)
- [X] T075 [US1] Create `src/Nabadat.SurveyBuilder/Application/Questions/QuestionValidator.cs` matching T045. (A)
- [X] T076 [US1] Create `src/Nabadat.SurveyBuilder/Application/Questions/KpiBindingValidator.cs` matching T046. (A)
- [X] T077 [US1] Create `src/Nabadat.SurveyBuilder/Application/Questions/KpiBindingChangePolicy.cs` matching T047. (A)
- [X] T078 [US1] Create `src/Nabadat.SurveyBuilder/Application/Questions/CommentFieldFlagPolicy.cs` + `SentimentFlagPolicy.cs` matching T048. (A)
- [X] T079 [US1] Create `src/Nabadat.SurveyBuilder/Application/Questions/QuestionCommandService.cs` — Create / Update / Delete a question; enforces every validator above; uses `IJourneyReader.IsBindingValidAsync` on writes; delegates cascade behaviour on delete to `SectionCascadeService` (built in US3). (A)
- [X] T080 [US1] Create `src/Nabadat.SurveyBuilder/Application/Appearance/AppearanceService.cs` — Inherited resolution via `ITenantDesignGuidelinesReader`; Customize path saves a `Theme` row; logo upload via `IFileStorageService.UploadAsync` (ClamAV + CMK envelope encryption). (B)

### Implementation for User Story 1 — API Controllers

- [X] T081 [US1] Create `src/Nabadat.SurveyBuilder/Api/Controllers/SurveysController.cs` — routes per [contracts/surveys.md](./contracts/surveys.md): `GET /api/v1/surveys` (list with filters), `GET /api/v1/surveys/{id}`, `POST /api/v1/surveys` (create + `Idempotency-Key`), `PUT /api/v1/surveys/{id}` (settings save + `If-Match`), `POST /api/v1/surveys/{id}/clone`, `POST /api/v1/surveys/{id}/status` (all self-serve transitions), `GET /api/v1/surveys/{id}/render-plan`. Every write returns the new ETag. Every list endpoint uses cursor pagination (API-04). Every non-2xx uses the API-05 envelope. Declares `required_permission`, `required_scope`, `default_personas` per API-03 in `[Authorize]` attributes. (A)
- [X] T082 [US1] Create `src/Nabadat.SurveyBuilder/Api/Filters/PublishGateFilter.cs` + `EditLockFilter.cs` — invoked before mutating endpoints per BR-1.7 / BR-15.1. Configured in the controller via attributes. (A)
- [X] T083 [P] [US1] Create `src/Nabadat.SurveyBuilder/Api/Controllers/SurveyThemesController.cs` — F4: `GET /api/v1/surveys/{id}/theme`, `PUT /api/v1/surveys/{id}/theme` (Customize mode save), `POST /api/v1/surveys/{id}/theme/logo` (multipart upload → `IFileStorageService`). (B)
- [X] T084 [P] [US1] Create `src/Nabadat.SurveyBuilder/Api/Contracts/` DTOs for all routes above — one per file: `CreateSurveyRequest.cs`, `UpdateSurveyRequest.cs`, `SurveyView.cs`, `SurveyListItem.cs`, `SurveyListResponse.cs`, `SurveyStatusChangeRequest.cs`, `CloneSurveyRequest.cs`, `RenderPlanResponse.cs`, `ThemeView.cs`, `UpdateThemeRequest.cs`. (A)

### Implementation for User Story 1 — Frontend

- [X] T085 [US1] Create `frontend/src/features/surveys/api/surveys-api.ts` — typed `callJson` wrappers for every SurveysController route; imports `etag.ts` helpers. Types match `Api/Contracts/*` shapes. (F) — **done 2026-07-20**: every SurveysController route + theme routes + multipart logo upload; int-enum normalizers both directions (host has no JsonStringEnumConverter), enum NAMES on list query strings, camelCase wire.
- [X] T086 [P] [US1] Create `frontend/src/features/surveys/pages/SurveyLibraryPage.tsx` — F1: shadcn `Table` inside a bordered card with `overflow-hidden` (per CLAUDE.md table pattern); sticky header (`bg-muted` default); Type/Status/Journey filters as shadcn `Select` (each in a `flex flex-col gap-1.5 sm:w-48` wrapper); English-name search bounded `sm:max-w-sm`; per-row action icons for Preview / Report / Analytics + overflow menu (Edit / Change status / Clone / Sections / Archive); "No results" state per FR-1.3; SurveyStatusPill component using D-level tokens (Active=D1, Paused=D3, Archived=D5-muted, PendingReview=D2, Draft=neutral). One-blue-rule: "Add Survey" is the sole filled primary. (F) — **done 2026-07-20**: debounced FR-1.2 search, Type/Status/Journey filters per the filter-row shape, sticky-header table in overflow-hidden card, row actions + overflow menu, status transitions with 409 confirm dialogs, FR-1.3 empty states, one-blue Add Survey.
- [X] T087 [P] [US1] Create `frontend/src/features/surveys/pages/BuildMethodPage.tsx` — F5 chooser tile grid (From scratch / From a template / Build with AI); each tile reuses `ui/card.tsx` at `rounded-lg`; keyboard-navigable; Arabic copy in `ar.json` (فصحى, native, per NFR-2). Persistence semantics per FR-5.5: no survey row until Continue out of Settings. (F) — **done 2026-07-20**: 3-tile chooser (scratch → /surveys/new/settings create-mode, template → /templates, AI disabled "coming soon"); keyboard-navigable; persists nothing per FR-5.5.
- [X] T088 [P] [US1] Create `frontend/src/features/surveys/pages/SurveySettingsPage.tsx` — F3 form: English name (required), description, bound_journey Select (options from `IJourneyReader` via a `GET /api/v1/journeys` endpoint already exposed by M-16), welcome/thank-you `RichTextEditor` (client-side using @base-ui with a `</> HTML` source toggle), layout Select with FR-3.3 warning modal on `question` / `count`, active_period `{days, hours}` fields, shuffle + shuffle_mode + routing_on (Q8/Q1 conflict dialog on 412); Continue button persists via `POST /api/v1/surveys` on first save, `PUT` afterwards; unsaved-changes navigation guard via `useUnsavedChangesGuard`. (F) — **done 2026-07-20**: full F3 form incl. derived type (BR-3.3), RichTextEditor (contentEditable + `</>` HTML toggle), FR-3.3 layout warning dialog, active-period fields, POST-then-PUT with Idempotency-Key/If-Match, edit-lock banner, unsaved-changes guard, EtagConflictDialog on 412/409.
- [X] T089 [P] [US1] Create `frontend/src/features/surveys/pages/SurveyAppearancePage.tsx` — F4: split pane (Controls list scrollable + `LivePreviewFrame` pinned right per FR-4.2); Inherited mode locks controls; Customize unlocks; theme changes update the preview within ~100 ms (SC-003). Uses `nb-*` brand tokens for the customization surface (never `d{n}-*`). (F) — **done 2026-07-20**: FR-4.2 split pane (scrollable controls + sticky preview), Inherited locks / Customize unlocks, instant preview via local state (SC-003), logo file staged then uploaded on Save.
- [X] T090 [P] [US1] Create `frontend/src/features/surveys/pages/SurveyBuilderPage.tsx` — F8: drag-and-drop canvas with `SectionColumn` per section + `QuestionCard` per question + `QuestionsSetCard` for sets (set-body wiring lands in US3 — for US1 palette shows all 7 answer types + KPI under "Metric"); question settings drawer opens from card; comments toggle → "Comments field" badge; sentiment toggle hidden for non-text types. Header carries Survey settings / status Select (excludes PendingReview per FR-8.12) / Translate / Preview / Save as template / Save survey / Question-routing toggle. Publish button disabled when `sections_count=0` OR `questions_count=0` (BR-1.7) with tooltip "Add at least one section and one question before publishing". One-blue-rule: Save survey is the sole primary. Uses `@dnd-kit/core` for drag-and-drop. (F) — **done 2026-07-20**: @dnd-kit canvas (palette→section drops), question drawer Sheet (RTL-aware side), many-actions toolbar row, status Select excluding PendingReview (FR-8.12), BR-1.7 publish gate tooltip, one-blue Save survey. Canvas persistence deferred to US3 → landed with T152–T154 (TODO-M01-028).
- [X] T091 [P] [US1] Create `frontend/src/features/surveys/components/QuestionPalette.tsx` — 8 draggable tiles in the specified order (Scale / Input Field / Single select / Multi-select / Yes/No / Single-select matrix / Ranking) plus KPI under a "Metric" heading (FR-8.1); each tile uses `ui/card.tsx` at `rounded-md` (12 px inner tile). Icons from Lucide. (F) — **done 2026-07-20**: 7 tiles in FR-8.1 order + KPI under "Metric"; draggable AND clickable (no drag-only interaction); Lucide icons.
- [X] T092 [P] [US1] Create `frontend/src/features/surveys/components/QuestionCard.tsx` — canvas card with type-specific settings tab (`ui/tabs`) for text/description, sub-type Select, per-type settings (Scale slider/points/labels, Input Field type, Single/Multi options, YesNo labels, Matrix mode+rows+columns, Ranking items, KPI representation); toggles for Required / Show comments field / Apply sentiment analysis. "Routing set" badge slot (populated in US4). All logical properties. (F) — **done 2026-07-20**: settings tab (text/description/sub-type FR-8.8 + per-type settings incl. chip-adder lists), Required/comments/sentiment toggles (FR-8.11 eligibility), Comments-field + Routing-set badges, KPI tab hosting KpiBindingEditor.
- [X] T093 [P] [US1] Create `frontend/src/features/surveys/components/KpiBindingEditor.tsx` — layered editor per FR-8.4: KPI Select (options from `/api/v1/kpi-catalog`), Perspective Select (options from KPI), Bound journey Toggle (default ON for KPI questions), Stage Select (required before Touchpoint), Touchpoint Select (filtered by KPI+Journey+Stage). Off-state disables Stage/Touchpoint and clears them via `KpiBindingChangePolicy` on save. (F) — **done 2026-07-20**: layered KPI→Perspective→journey-toggle→Stage→Touchpoint per FR-8.4; touchpoints filtered to those measuring the KPI; toggle-off clears+disables Stage/Touchpoint (BR-8.2); options from M-06 `listKpis`/`getKpi` + M-16 `listStages`/`getJourney`.
- [X] T094 [P] [US1] Create `frontend/src/features/surveys/components/SurveyStatusPill.tsx` — D-level colours: Active=D1-light+D1-dark text, Paused=D3-light+D3-dark, PendingReview=D2-light+D2-dark, Draft=neutral (`bg-muted`), Archived=D5-light muted. Both light + dark verified. (F) — **done 2026-07-20**: D-level tokens (Active=D1, PendingReview=D2, Paused=D3, Draft=neutral, Archived=D5 muted), light+dark variants, label always rendered (never colour-only).
- [X] T095 [P] [US1] Create `frontend/src/features/surveys/components/AppearanceControls.tsx` (F4 controls list) + `LivePreviewFrame.tsx` (Desktop/Mobile chrome for US1; WhatsApp/Email chrome added in US7). (F) — **done 2026-07-20**: AppearanceControls (mode switch, colour picker+hex, background Select, logo upload) + LivePreviewFrame (Desktop/Mobile chrome; theme colour applied as inline-style DATA per the documented exception).
- [X] T096 [P] [US1] Create `frontend/src/features/surveys/components/DestructiveReturnToDraftDialog.tsx` — BR-1.6 blocking confirmation with response count `N` (fetched from the 409 payload's `details.responses_count`). Cancel = `variant="outline"`; primary = `variant="destructive"` with text "Return to Draft & delete responses". Per CLAUDE.md dialog rule uses `sm:max-w-md` + `flex max-h-[90vh] flex-col` (body scrolls). (F) — **done 2026-07-20**: BR-1.6 blocking dialog with exact response count from the 409 payload; Cancel=outline, destructive primary "Return to Draft & delete responses"; sm:max-w-md + max-h-[90vh] flex-col.
- [X] T097 [P] [US1] Create `frontend/src/features/surveys/components/PauseWithRulesDialog.tsx` — FR-1.10 blocking confirmation showing exact rule count from the 409 payload; primary is `variant="default"` "Pause survey". (F) — **done 2026-07-20**: FR-1.10 dialog with exact rules count from the 409 payload; primary "Pause survey".
- [X] T098 [P] [US1] Create `frontend/src/features/surveys/components/PublishGateBanner.tsx` — non-modal disabled-tooltip on the Publish button when BR-1.7 would fail; tooltip text "Add at least one section and one question before publishing". (F) — **done 2026-07-20**: `publishGateBlocked()` + tooltip wrapper (TooltipProvider; span anchor since disabled elements swallow pointer events).
- [X] T099 [P] [US1] Create `frontend/src/features/surveys/components/EtagConflictDialog.tsx` — Q1 stale-etag 412 handler; offers "Reload latest" and "Copy my changes" (copy the local form values to clipboard) actions. (F) — **done 2026-07-20**: "Reload latest" + "Copy my changes" (clipboard JSON of local form values, with copied state).

### Integration & API / Scenario Tests for User Story 1 (run at the per-story checkpoint) 🐳

- [X] T100 [P] [US1] API tests for SurveysController in `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Surveys/SurveyEndpointTests.cs` — every route from contracts/surveys.md: `POST /surveys` (create returns 201 + ETag), `PUT /surveys/{id}` (returns 200 + new ETag, 409 on stale ETag), `POST /status {"to":"Active"}` (Publish-gate 409 when empty; 200 when content present), `POST /status {"to":"Paused"}` (409 requires_rules_confirmation when rules_count > 0), `POST /status {"to":"Active"}` from Archived (409 archived.only_unarchive_allowed), `POST /status {"to":"Draft"}` from Archived (200 unarchive), `GET /surveys` filter combinations, `POST /questions` KPI with `{stage: null, touchpoint: "T1"}` (400 kpi.touchpoint.requires_stage), row-click deep-link `GET /surveys/{id}` returns settings payload. (A)
- [X] T101 [P] [US1] API tests for SurveyThemesController in `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Appearance/ThemeEndpointTests.cs` — GET / PUT theme; POST /theme/logo (multipart) end-to-end with a fake `IFileStorageService`. (B)
- [X] T102 [P] [US1] Scenario test `tests/Nabadat.SurveyBuilder.IntegrationTests/Scenarios/SurveyLifecycleFromDraftToActiveScenarioTests.cs` — walks the full journey: create Draft → add section → add question with KPI binding → set Active (as P-01) → verify Active row in library + audit-log entries emitted (`survey.created`, `survey.status.changed` → `survey.published` per constitution § 4). Uses `SurveyBuilderApplicationFactory` + `SeedActiveSurvey` helper. (A)

### E2E (Browser) Tests for User Story 1 🎭

- [ ] T103 [P] [US1] E2E tests for the Library in `tests/Nabadat.E2ETests/SurveyBuilder/SurveyLibraryTests.cs` — happy path (filters + row click open Settings); unauthorized user (P-02) sees read-only badges only; Pause confirmation modal renders exact rule count and blocks until Confirm; Archived row shows only "Unarchive" in status menu. Update `COVERAGE.md`. Uses the `e2e-testing` skill. (F)
- [ ] T104 [P] [US1] E2E tests for Build method + Settings in `tests/Nabadat.E2ETests/SurveyBuilder/SurveyBuildMethodTests.cs` + `SurveySettingsTests.cs` — chooser precedes Settings for new; bypassed on edit/clone; required-field validation for English name; layout warning modal appears on "one question per page" switch. (F)
- [ ] T105 [P] [US1] E2E tests for Appearance in `tests/Nabadat.E2ETests/SurveyBuilder/SurveyAppearanceTests.cs` — Inherited mode locks all controls; Customize unlocks; live preview updates within 100 ms of a change. (F)
- [ ] T106 [P] [US1] E2E tests for Builder in `tests/Nabadat.E2ETests/SurveyBuilder/SurveyBuilderTests.cs` — palette shows the 7 answer types in the specified order + KPI under "Metric"; drag-and-drop moves questions between sections; sentiment toggle hidden for non-text types; auth redirect (signed-out user hitting `/surveys` is redirected to `/login`; P-02 hitting builder controls sees them disabled with `aria-label`). (F)

**Build gate for US1**: `dotnet test tests/Nabadat.SurveyBuilder.UnitTests` green (0 failures). `dotnet test tests/Nabadat.SurveyBuilder.IntegrationTests` green (Docker up; Postgres + ES containers healthy). `npm run build` in `frontend/` green. `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~SurveyLibraryTests|SurveyBuildMethodTests|SurveySettingsTests|SurveyAppearanceTests|SurveyBuilderTests"` green with `E2E_BASE_URL` set + backend + Vite up.

**Checkpoint**: US1 fully functional and testable independently. **MVP boundary — the module can ship with only US1 and satisfy the "smallest viable surface" criterion.**

---

## Phase 4: User Story 2 — Approval & publishing workflow (Priority: P1)

**Goal**: P-03 submits a Draft; survey enters Pending review + read-only for P-03; M-09 notifies every P-01 (Q7 broadcast); P-01 publishes or returns-to-draft with remarks; the `PublishOwnSurveys` grant lets qualified P-03 users skip the review step.

**Independent Test**: P-03 creates a Draft, submits it → Pending review; editors read-only; M-09 notification emitted; P-01 opens the deep-link Settings, publishes → Active; audit trail carries submit + publish + remarks.

### Unit Tests for User Story 2 (write FIRST, must FAIL) ⚠️

- [X] T107 [P] [US2] Unit tests for `ApprovalStateMachine` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/ApprovalStateMachineTests.cs` — `Submit(draft, actorRole: "P-03") → transitions Draft → PendingReview, SubmitOutcome { NotificationTo = <reviewers>, EditLockOwner = "P-03" }`; `Publish(pendingReview, actorRole: "P-03", grant: null) → Forbidden`; `Publish(pendingReview, actorRole: "P-03", grant: "PublishOwnSurveys", ownerId: sameAsActor) → Active`; `Publish(pendingReview, actorRole: "P-01") → Active`; `ReturnToDraft(pendingReview, actorRole: "P-01", remarks: "Fix Arabic") → Draft, RemarksPersisted = true`. (B)
- [X] T108 [P] [US2] Unit tests for `EditLockPolicy` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/EditLockPolicyTests.cs` — `CanEdit(user: P-03, survey: {status: PendingReview, submittedBy: P-03}) → false` (BR-15.1); `CanEdit(user: P-01, survey: {status: PendingReview}) → true`. (B)
- [X] T109 [P] [US2] Unit tests for `PublishAuthorizationService` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/PublishAuthorizationServiceTests.cs` — `Authorize(actor, survey) → Forbidden` for P-03 without grant on their own draft in Draft state (must submit first); permitted with grant on own draft. (B)
- [X] T110 [P] [US2] Unit tests for `ReviewNotificationBuilder` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/ReviewNotificationBuilderTests.cs` — `Build(survey, submitter) → NotificationBroadcast { scope: "tenant", permission: "survey.publish", deep_link: "/surveys/{id}", template: "survey.submitted_for_review" }` (Q7 broadcast fanout). (B)
- [X] T111 [P] [US2] Unit tests for `AuditEventFactory` in `tests/Nabadat.SurveyBuilder.UnitTests/Surveys/AuditEventFactoryTests.cs` — every approval-workflow action emits exactly one M-17 event with the correct payload shape (actor, timestamp, remarks, correlation_id, previous_status, new_status). (B)

### Red Checkpoint for User Story 2 (MANDATORY) 🔴

- [X] T112 [US2] Run `dotnet test tests/Nabadat.SurveyBuilder.UnitTests --filter "FullyQualifiedName~ApprovalStateMachine|EditLockPolicy|PublishAuthorization|ReviewNotification|AuditEventFactory"`. Verify red / green-baseline; paste transcript; commit. (B) — **RED verified 2026-07-16** (committed `b108e6e`): compile error (CS0246/CS0234), production types + `IPermissionChecker` absent — valid red per Unit Test Policy rule 7.

### Implementation for User Story 2

- [X] T113 [P] [US2] Create `src/Nabadat.SurveyBuilder/Application/Surveys/ApprovalStateMachine.cs` — Submit / Publish / ReturnToDraft methods matching T107. (B)
- [X] T114 [P] [US2] Create `src/Nabadat.SurveyBuilder/Application/Surveys/EditLockPolicy.cs` matching T108. (B)
- [X] T115 [P] [US2] Create `src/Nabadat.SurveyBuilder/Application/Surveys/PublishAuthorizationService.cs` matching T109; consults `IPermissionChecker.HasGrantAsync("PublishOwnSurveys")` + `survey.owner_user_id == caller.UserId`. (B) — port `Domain/Interfaces/IPermissionChecker.cs` declared here (T020 pending); concrete M-10 impl + DI wiring out of scope (T118/host).
- [X] T116 [P] [US2] Create `src/Nabadat.SurveyBuilder/Application/Surveys/ReviewNotificationBuilder.cs` + `Interfaces/INotificationDispatcher.cs` (M-09 published — declared in T020) — `BroadcastAsync(scope, permission, deep_link, template)` fans out per Q7. (B) — port placed at `Domain/Interfaces/INotificationDispatcher.cs` (Article 1A cross-module port location); concrete M-09 impl + DI wiring out of scope (T118/host).
- [X] T117 [P] [US2] Create `src/Nabadat.SurveyBuilder/Application/Surveys/AuditEventFactory.cs` matching T111. (B) — **T113–T117 GREEN verified 2026-07-16**: US2 filter (17 tests) passes in isolation (`src` builds 0 errors; shared UnitTests project temporarily uncompilable due to concurrent US1 WIP — verified via isolated project).
- [X] T118 [US2] Create `src/Nabadat.SurveyBuilder/Application/Surveys/ApprovalWorkflowService.cs` — orchestrates Submit / Publish / ReturnToDraft; composes `ApprovalStateMachine`, `PublishAuthorizationService`, `ReviewNotificationBuilder`, `AuditEventFactory`, `PublishGateService` (BR-1.7 also gates Submit); depends on `ITenantDbContext.ExecuteAsync`. (B) — **done 2026-07-16** (`src` builds 0 errors); Application command DTOs `SubmitForReviewCommand`/`PublishSurveyCommand`/`ReturnForRevisionCommand` added; maps US2 `ApprovalAuditEvent` → M-17 `SurveyAuditEvent`. Cross-module ports `IPermissionChecker`/`INotificationDispatcher` dev-stubbed (TODO-M01-014).
- [X] T119 [US2] Create `src/Nabadat.SurveyBuilder/Api/Controllers/SurveyLifecycleController.cs` — routes per [contracts/approval-workflow.md](./contracts/approval-workflow.md): `POST /api/v1/surveys/{id}/submit`, `POST /api/v1/surveys/{id}/publish` (with `Idempotency-Key`), `POST /api/v1/surveys/{id}/return-to-draft`. Applies `EditLockFilter` to distinguish PendingReview from destructive-Return-to-Draft paths (the latter uses `SurveysController.POST /status`). (B) — **done 2026-07-16**: If-Match ETag on every write, API-05 via middleware, actor from `ISessionContextAccessor`; `Idempotency-Key` handled by the module middleware; `EditLockFilter` on return-to-draft.
- [X] T120 [P] [US2] Create `src/Nabadat.SurveyBuilder/Api/Contracts/` DTOs — `SubmitSurveyRequest.cs`, `PublishSurveyRequest.cs`, `ReturnToDraftRequest.cs`, `ApprovalActionResult.cs`. (B) — **done 2026-07-16**.
- [X] T121 [P] [US2] Extend `frontend/src/features/surveys/api/surveys-api.ts` with `submit / publish / returnToDraft` helpers. (F) — **done 2026-07-20**: `submitSurvey`/`publishSurvey`/`returnSurveyToDraft` → normalized `ApprovalActionResult` (endpoint serialises status as PascalCase STRING, unlike the int-enum DTOs — normalizer covers both); publish carries Idempotency-Key, all carry If-Match.
- [X] T122 [P] [US2] Extend `SurveySettingsPage.tsx` — display the "Pending review" banner when `useSurveyEditLock` says the caller is edit-locked; show the "Publish" action button (variant="default") for reviewers; show "Return to draft" with a remarks Textarea in a `Sheet`; disable every field when locked. Consumes the M-09 notification deep-link `/surveys/{id}?from=review-notification`. (F) — **done 2026-07-20**: reviewer (P-01) actions on PendingReview — Publish becomes the one filled primary (Save demotes to secondary), Return-to-draft Sheet with required remarks (FR-15.3, inline error + focus), `?from=review-notification` arrival banner; edit-lock banner + field disabling already covered locked P-03 (US1).
- [X] T123 [P] [US2] Extend `SurveyLibraryPage.tsx` — add PendingReview as a filterable status; add the Publish quick-action to the row overflow menu when caller is P-01 and status = PendingReview. (F) — **done 2026-07-20**: PendingReview was already filterable (US1); added the P-01 quick-Publish row action for PendingReview rows via the lifecycle `POST /publish` endpoint (NOT `POST /status`, which rejects that transition).

### Integration & API / Scenario Tests for User Story 2 🐳

- [X] T124 [P] [US2] API tests in `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Surveys/SurveyLifecycleEndpointTests.cs` — `POST /submit` transitions Draft → PendingReview, `event_log` gets `survey.submitted_for_review`, M-09 broadcast event emitted; `POST /publish` returns 403 when caller is P-03 without grant; `POST /publish` succeeds for P-01 and status becomes Active; `POST /return-to-draft` with `{remarks}` returns 200, remarks in audit log; `PUT /surveys/{id}` returns 403 for P-03 on their own survey while in PendingReview. (B) — **done 2026-07-16, 4 tests GREEN** (Docker/Testcontainers). The `PUT → 403` (BR-15.1) assertion is OMITTED — that write-lock isn't wired (EditLockPolicy unused in Api); tracked as **TODO-M01-015**. Observable via factory test doubles (DbEventLogWriter → event_log, CapturingNotificationDispatcher, StubPermissionChecker).
- [X] T125 [P] [US2] Scenario test `tests/Nabadat.SurveyBuilder.IntegrationTests/Scenarios/SurveyApprovalWorkflowScenarioTests.cs` — walks P-03 saves Draft → P-03 submits → M-09 notification emitted to P-01 → P-01 lands on Settings deep-link → P-01 publishes → survey Active + audit trail complete. Also exercises the self-publish-grant variant. (B) — **done 2026-07-16, 2 tests GREEN** (standard review flow + self-publish-grant variant).

### E2E (Browser) Tests for User Story 2 🎭

- [ ] T126 [P] [US2] E2E tests in `tests/Nabadat.E2ETests/SurveyBuilder/SurveyApprovalTests.cs` — standard review flow (P-03 submits → sees Pending banner → P-01 receives notification → deep-links to Settings → publishes → status becomes Active in the library); locked while pending (P-03 opens the survey, editors are read-only, banner visible); self-publish grant (P-03 sees Publish directly and completes the flow without a reviewer notification); Return-to-draft (P-01 returns with remarks; P-03 gets back to Draft; remarks appear in audit view). Update `COVERAGE.md`. (F)

**Build gate for US2**: unit + integration + E2E green.

**Checkpoint**: US2 complete; approval flow governs P-03 surveys.

---

## Phase 5: User Story 3 — Sections + rotating Questions Sets with low-response ordering (Priority: P2)

**Goal**: Author structures a large question bank into sections + Questions Sets; per-set `selection_mode` (Random / Prioritize low-response) + `count`; low-response ordering algorithm cascades set → section → survey (FR-10.4). Cascade delete + routing/translation cleanup (FR-2.5–2.8) land here.

**Independent Test**: Two sections; Set of 10 in section 1 with `selection_mode = "low_response"` and `count = 3`; F2 view shows "shows 3 of 10". Render-plan test walks the algorithm on a fixture with three sections whose lowest-response questions are (7, 4, 12) → order = [section2 (4), section1 (7), section3 (12)].

### Unit Tests for User Story 3 (write FIRST, must FAIL) ⚠️

- [X] T127 [P] [US3] Unit tests for `SectionValidator` in `tests/Nabadat.SurveyBuilder.UnitTests/Sections/SectionValidatorTests.cs`. (A) — **done 2026-07-16, RED (compile error: `SectionValidator`/`SectionDraft` not created until T137).**
- [X] T128 [P] [US3] Unit tests for `SectionDeletionGuard` + `SectionCascadeService` in `tests/Nabadat.SurveyBuilder.UnitTests/Sections/SectionCascadeServiceTests.cs` — `CanDelete(survey.Sections.Count == 1) → true` (last section deletable, FR-2.3); `Delete(nonEmptySection, confirmed: false) → Blocked("section.delete.requires_confirmation")`; `Delete(nonEmptySection, confirmed: true) → cascades all standalone questions and sets` (FR-2.5); routing reset (FR-2.7) + translation purge (FR-2.8) verified via NSubstitute on `IRoutingMapStore` + `ITranslationStore`. (A) — **done 2026-07-16, RED (compile error: `SectionCascadeService`/`SectionDeletionGuard` + ports `IQuestionsSetStore`/`IRoutingMapStore`/`ITranslationStore` not created until T138). Contract pinned in test XML doc.**
- [X] T129 [P] [US3] Unit tests for `QuestionsSetValidator` in `tests/Nabadat.SurveyBuilder.UnitTests/QuestionsSets/QuestionsSetValidatorTests.cs` — `Validate(new Set { Count = 6, Questions.Count = 5 }) → Invalid("questionsset.count.exceeds_size")`; empty set with `count = 0` is Valid. (A) — **done 2026-07-16, RED (compile error: `QuestionsSetValidator`/`QuestionsSetDraft` not created until T139). `Questions.Count` modelled as `SetSize` int on the draft.**
- [X] T130 [P] [US3] Unit tests for `QuestionDeletionService` in `tests/Nabadat.SurveyBuilder.UnitTests/Questions/QuestionDeletionServiceTests.cs` — `Delete(q) → resets inbound routing targets to next-question default (FR-2.7) and purges all-locale translations (FR-2.8)`. (A) — **done 2026-07-16, RED (compile error: `QuestionDeletionService`/`QuestionDeletionCommand` not created until T140).**
- [X] T131 [P] [US3] Unit tests for `LowResponseOrderingService` in `tests/Nabadat.SurveyBuilder.UnitTests/QuestionsSets/LowResponseOrderingServiceTests.cs` — `OrderSections(sections, responseCounts) → [<section with lowest question first>, …]` for fixture with three sections whose lowest-response questions are (7, 4, 12) → order = [section2 (4), section1 (7), section3 (12)]; `WithinSet.PickCandidates(set, count: 3, responseCounts) → 3 least-answered eligible questions`. (A) — **done 2026-07-16, RED (compile error: `LowResponseOrderingService` + `OrderingSection`/`OrderingSet` DTOs not created until T141). `PickCandidates(set, count, responseCounts)` exposed on the service.**
- [X] T132 [P] [US3] Unit tests for `QuestionMoveService` in `tests/Nabadat.SurveyBuilder.UnitTests/Questions/QuestionMoveServiceTests.cs` — `Move(from: sectionA, to: setB, order: 2) → persistsAllFields(section_id, set_id, order)`; cross-set move that lands the question inside a set removes any pre-existing routing for that question. (A) — **done 2026-07-16, RED (compile error: `QuestionMoveService`/`MoveQuestionCommand` + `IRoutingMapStore.RemoveAllForQuestionAsync` not created until T142).**

### Red Checkpoint for User Story 3 (MANDATORY) 🔴

- [X] T133 [US3] Run `dotnet test tests/Nabadat.SurveyBuilder.UnitTests --filter "FullyQualifiedName~Sections|FullyQualifiedName~QuestionsSets|FullyQualifiedName~QuestionMoveService|FullyQualifiedName~QuestionDeletionService"`. Verify red; paste transcript; commit. (A) — **done 2026-07-16, RED verified — `Build FAILED`; every failure is a `CS0234`/`CS0246` "production type/namespace not created yet" error for the T137–T142 types + ports (no logic errors in the tests). Red-baseline commit SKIPPED per user instruction ("dont commit"). NOTE: pre-existing US4 `Routing/*Tests.cs` in this project also reference the not-yet-created `Application.Routing` namespace, so the project was already non-compiling before this pass.**

### Implementation for User Story 3

- [X] T134 [P] [US3] Create `src/Nabadat.SurveyBuilder/Domain/Entities/QuestionsSet.cs` per [data-model.md § 2.3](./data-model.md#23-questions_sets). (A) — **done 2026-07-16 (+ `QuestionsSetSelectionMode` enum). TODO-M01-007 updated.**
- [X] T135 [P] [US3] Create `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Configurations/QuestionsSetConfiguration.cs`. (A) — **done 2026-07-16 (wired into `TenantDbContext.OnModelCreating`).**
- [X] T136 [P] [US3] Create `src/Nabadat.SurveyBuilder/Application/QuestionsSets/Interfaces/IQuestionsSetStore.cs` + `Infrastructure/Persistence/Stores/QuestionsSetStore.cs`. (A) — **done 2026-07-16 (+ `GetBySectionAsync`; registered in DI).**
- [X] T137 [P] [US3] Create `src/Nabadat.SurveyBuilder/Application/Sections/SectionValidator.cs` **and `SectionCommandService.cs`** (section create/update: append-order + validate-then-write in `ExecuteAsync`, backing `POST`/`PATCH /sections`) matching T127 — mirrors T139's validator + service pairing for Questions Sets. (A) — **done 2026-07-16 (+ `SectionDraft`/`SectionValidationResult`; T127 green). `SectionCommandService` recorded here 2026-07-19 to close TODO-M01-018 (it was shipped in the T147 pass but assigned to no task).**
- [X] T138 [P] [US3] Create `src/Nabadat.SurveyBuilder/Application/Sections/SectionCascadeService.cs` matching T128; wraps writes in `ExecuteAsync`. (A) — **done 2026-07-16 (+ `SectionDeletionGuard`/`SectionCascadeResult`/`SectionCascadeCommand`; uses real `IRoutingMapStore.DeleteByTargetQuestionAsync` + `ITranslationStore.PurgeQuestionKeysAsync`; T128 green).**
- [X] T139 [P] [US3] Create `src/Nabadat.SurveyBuilder/Application/QuestionsSets/QuestionsSetValidator.cs` + `QuestionsSetService.cs` matching T129. (A) — **done 2026-07-16 (validator + create/update/delete service; T129 green).**
- [X] T140 [P] [US3] Create `src/Nabadat.SurveyBuilder/Application/Questions/QuestionDeletionService.cs` matching T130. (A) — **done 2026-07-16 (FR-2.7 `DeleteByTargetQuestionAsync` + FR-2.8 purge; T130 green).**
- [X] T141 [P] [US3] Create `src/Nabadat.SurveyBuilder/Application/QuestionsSets/LowResponseOrderingService.cs` matching T131. (A) — **done 2026-07-16 (+ `OrderingSection`/`OrderingSet` DTOs; T131 green).**
- [X] T142 [US3] Create `src/Nabadat.SurveyBuilder/Application/Questions/QuestionMoveService.cs` matching T132 (referenced from T065's `IQuestionStore.MoveQuestion`; the service orchestrates side effects — routing invalidation, order compaction — inside `ExecuteAsync`). (A) — **done 2026-07-16 (set-move strips routing via `DeleteBySource/TargetQuestionAsync`; T132 green); order compaction added 2026-07-19 in `QuestionStore.MoveAsync` (TODO-M01-020) — reindexes source + destination `(section_id, set_id)` to contiguous unique order.**
- [X] T143 [US3] Create `src/Nabadat.SurveyBuilder/Application/RenderPlan/SurveyRenderService.cs` — implements `ISurveyRenderService.GetRenderPlanAsync`; composes `LowResponseOrderingService` + `IResponseCountReader` (from `tenant_{tenantId}_analytics` per [research.md § 7](./research.md#7-low-response-ordering-algorithm-fr-104)) + `RoutingMapStore.GetForSurvey`. p95 ≤ 50 ms for ≤ 100 questions. (B) — **done 2026-07-16 (implements the interface; routing uses `IRoutingMapStore.GetBySurveyAsync`; delegates `GetActiveSurveyDefinitionAsync` to T144).**
- [X] T144 [US3] Create `src/Nabadat.SurveyBuilder/Application/RenderPlan/SurveyDefinitionAssembler.cs` — implements `ISurveyRenderService.GetActiveSurveyDefinitionAsync`; assembles the full Survey view with the locale bundle inlined. (B) — **done 2026-07-16 (minimal fields per the under-specified `SurveyDefinition`; rich content is TODO-M01-008).**
- [X] T145 [P] [US3] Create `src/Nabadat.SurveyBuilder/Infrastructure/Elasticsearch/ResponseCountReader.cs` — implements `IResponseCountReader` reading `tenant_{tenantId}_analytics` docs of type `question_response_counts`; HTTPS 9200. (B) — **done 2026-07-16 (real ES reader, graceful empty-on-error; wired only when `Elasticsearch:Uri` set, else `UnavailableResponseCountReader`. Live-ES wiring is TODO-M01-017).**
- [X] T146 [P] [US3] Create `src/Nabadat.SurveyBuilder/Domain/Interfaces/IActiveSurveyReader.cs` implementation `src/Nabadat.SurveyBuilder/Application/RenderPlan/ActiveSurveyReader.cs` — returns `ActiveSurveyState(Status, ActivatedAt, ExpiresAt)`. (B) — **done 2026-07-16; TODO-M01-016 RESOLVED 2026-07-19: `Survey.ActivatedAt` + `activated_at` column added and stamped on entry into Active, so `ActiveSurveyReader` now surfaces `ActivatedAt` and derives `ExpiresAt = ActivatedAt + ActivePeriod` (null ⇒ never auto-expires).**
- [X] T147 [P] [US3] Create `src/Nabadat.SurveyBuilder/Api/Controllers/SectionsController.cs` — routes per [contracts/sections-and-sets.md](./contracts/sections-and-sets.md): POST/PATCH/DELETE /sections; `confirm=true` semantics on DELETE. (A) — **done 2026-07-16 (backed by new `SectionCommandService` — TODO-M01-018 — + `SectionCascadeService`; If-Match + API-05).**
- [X] T148 [P] [US3] Create `src/Nabadat.SurveyBuilder/Api/Controllers/QuestionsSetsController.cs` — POST/PATCH/DELETE /sections/{sid}/sets. (A) — **done 2026-07-16 (backed by `QuestionsSetService`; If-Match + confirm=true).**
- [X] T149 [P] [US3] Create `src/Nabadat.SurveyBuilder/Api/Controllers/QuestionsController.cs` — routes per [contracts/questions.md](./contracts/questions.md) minus routing endpoints (handled in US4): POST/PUT/DELETE questions + `POST /questions/{qid}/move`. (A) — **done 2026-07-16 (`QuestionCommandService` + `QuestionDeletionService` + `QuestionMoveService`).**
- [X] T150 [P] [US3] Register a diagnostics-only `POST /api/v1/surveys/{id}/render-plan` controller in `SurveyRenderPlanController.cs` (per contracts/surveys.md); also exposes the `ISurveyRenderService` via published-interface DI so M-02 / M-04 can call it in-process (constitution AD-01). (B) — **done 2026-07-19. Created `SurveyRenderPlanController` (POST diagnostics) AND wired the canonical `GET …/render-plan` (SurveysController) to the real `ISurveyRenderService` (already DI-registered) — enriched `RenderPlanResponse` to the contract shape (items with kind + routing map); service now 404s when missing/not-Active. Resolves TODO-M01-019. Added 2 HTTP-level tests to `RenderPlanEndpointTests` (auth on `RenderPlanApplicationFactory`) — 3 render-plan integration tests GREEN (Postgres + ES).**
- [X] T151 [P] [US3] Create `src/Nabadat.SurveyBuilder/Api/Contracts/` DTOs for the new routes — `CreateSectionRequest.cs`, `UpdateSectionRequest.cs`, `SectionView.cs`, `CreateQuestionsSetRequest.cs`, `UpdateQuestionsSetRequest.cs`, `QuestionsSetView.cs`, `CreateQuestionRequest.cs`, `UpdateQuestionRequest.cs`, `QuestionView.cs`, `MoveQuestionRequest.cs`. (A) — **done: all 10 DTO files created 2026-07-16 (T147–T149 controllers) + verified building clean this pass. The render-plan DTOs (`RenderPlanResponse`/`RenderPlanSection`/`RenderPlanItem`/`RenderPlanDiagnosticsRequest`) were enriched under T150.**
- [X] T152 [P] [US3] Extend `frontend/src/features/surveys/api/` with `sections-api.ts`, `questions-sets-api.ts`, `questions-api.ts`. (F) — **done 2026-07-20**: three modules matching the shipped controllers — creates need no If-Match, PATCH/DELETE carry the child row's ETag, `?confirm=` delete flows surface 409 cascade details, `$type`-first polymorphic payloads, int↔string enum converters, `builderQuestionToInput` mapper.
- [X] T153 [P] [US3] Extend `SurveyBuilderPage.tsx` — `QuestionsSetCard.tsx` component: sub-window inside a section with title, description, selection_mode Select, count Number input (auto-validates ≤ set size), drag-and-drop ordering across sections/sets. F2 library structure view — "shows k of n" summary + selection mode label. (F) — **done 2026-07-20**: `QuestionsSetCard.tsx` (title/description/selection-mode/count auto-clamped ≤ member count, "shows k of n" + mode badges, droppable body) wired into the builder with create/debounced-PATCH/FR-2.6 confirm-delete; palette tiles drop straight into sets.
- [X] T154 [P] [US3] Create `frontend/src/features/surveys/components/SectionColumn.tsx` — drag-and-drop container using `@dnd-kit/sortable`; destructive-confirmation dialog on non-empty delete listing cascaded questions and sets (per FR-2.5). (F) — **done 2026-07-20**: `SectionColumn.tsx` with @dnd-kit/sortable question rows (grip handle) + FR-2.5 destructive dialog listing the exact cascade (standalone/sets/set-questions from the 409 details); builder wires reorder + cross-section/into-set moves through `POST …/move`.

### Integration & API / Scenario Tests for User Story 3 🐳

- [X] T155 [P] [US3] API tests `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Sections/SectionEndpointTests.cs` — POST/PATCH/DELETE sections (including delete of the last remaining section returns 200; delete of a non-empty section without `confirm=true` returns 409 with `details: {standalone_questions, questions_sets, set_questions}`; with `confirm=true` cascades). (A) — **done 2026-07-19, 6 tests GREEN** (Docker/Postgres). The 409 `details` breakdown was not emitted by the shipped `SectionCascadeService`/`SectionsController` (T138/T147 shipped code + status only) — completed this pass: `SectionCascadeResult.Blocked` now carries `{standalone_questions, questions_sets, set_questions}`, surfaced via `SurveyBuilderException.Details`.
- [X] T156 [P] [US3] API tests `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/QuestionsSets/QuestionsSetEndpointTests.cs`. (A) — **done 2026-07-19, 6 tests GREEN**: create/patch/delete + `count <= member-count` ceiling (400 `questionsset.count.exceeds_size`) + FR-2.6 confirm gate (409 `questionsset.delete.requires_confirmation` with `details: {questions_count}` — `QuestionsSetDeletionResult.Blocked` completed this pass) + cascade.
- [X] T157 [P] [US3] API tests `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Questions/QuestionMoveEndpointTests.cs` — cross-section / cross-set move; order compacts. (A) — **done 2026-07-19, 4 tests GREEN**: cross-section move compacts the source and inserts the moved question at the target index in the destination; same-section reorder stays contiguous; move-into-set inserts among existing members AND strips inbound+outbound routing (FR-9.5); 404 on missing. **TODO-M01-020 RESOLVED 2026-07-19**: `QuestionStore.MoveAsync` now reindexes both containers to a contiguous, unique `(section_id, set_id, order)` (contracts/questions.md) inside the existing `ExecuteAsync`; these tests assert the full sibling sequences.
- [X] T158 [P] [US3] API tests `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/RenderPlan/RenderPlanEndpointTests.cs` — `GET /api/v1/surveys/{id}/render-plan?respondentId=…` returns the correct low-response order for a fixture with three sections whose lowest-response questions are (7, 4, 12); seeds `tenant_{tenantId}_analytics` via `EsTestcontainer`. (B) — **done 2026-07-19, GREEN** (Docker Postgres + Elasticsearch). Section order = [s2(4), s1(7), s3(12)]. **Driven through the published `ISurveyRenderService`** (AD-01 seam, via new `RenderPlanApplicationFactory` wiring the real `ResponseCountReader` to a live ES container), NOT the HTTP route — the `GET …/render-plan` endpoint is still the US1 minimal stub (no ordering); wiring it is **T150 (pending, out of the T155–T161 range) — see TODO-M01-019**.
- [X] T159 [P] [US3] Scenario test `tests/Nabadat.SurveyBuilder.IntegrationTests/Scenarios/QuestionsSetLowResponseOrderingScenarioTests.cs` — create survey → add 3 sections each with a Set → seed response counts via test fixture → request render-plan → assert survey-wide-lowest section is served first. (B) — **done 2026-07-19, GREEN**: asserts the full Set→Section→Survey cascade — survey-wide-lowest section served first AND each `low_response` set samples its least-answered member. Same `RenderPlanApplicationFactory` (Postgres + ES); driven via `ISurveyRenderService` (HTTP wiring = T150, TODO-M01-019).
- [X] T160 [P] [US3] Contract tests for `ISurveyRenderService` in `tests/Nabadat.SurveyBuilder.ContractTests/SurveyRenderServiceContractTests.cs` — validates the return shape for M-02 / M-04 consumers. (B) — **done 2026-07-19, 3 tests GREEN** (NSubstitute, no Docker): `RenderPlan` echoes survey id/layout, standalone→`RenderQuestion`, set→`RenderSetSample`, routing overrides project to `question_id→answer_key→RoutingTarget(EndsSurvey)`; `GetActiveSurveyDefinitionAsync` non-null for Active / null otherwise.
- [X] T161 [P] [US3] Contract tests for `IActiveSurveyReader` in `tests/Nabadat.SurveyBuilder.ContractTests/ActiveSurveyReaderContractTests.cs`. (B) — **done 2026-07-19, 6 tests GREEN**: surfaces the live `SurveyStatus`; missing survey → terminal `Archived`; derives `ExpiresAt = ActivatedAt + ActivePeriod`; null expiry when the period is null or the survey is not yet activated (TODO-M01-016 RESOLVED 2026-07-19).

### E2E (Browser) Tests for User Story 3 🎭

- [ ] T162 [P] [US3] E2E tests `tests/Nabadat.E2ETests/SurveyBuilder/SectionsAndSetsTests.cs` — F2 structure view lists sections + sets with live "shows k of n"; Add section / Add set / Delete work; delete last section succeeds after confirmation; deleting a non-empty section shows a destructive confirmation listing the cascaded questions and sets; drag-and-drop reorders sections, sets and questions; order persists on reload; auth redirect; empty state when a new section has no questions. (F)

**Build gate for US3**: unit + integration + contract + E2E green.

**Checkpoint**: US3 complete; render-plan seam ready for M-02/M-04 consumption.

---

## Phase 6: User Story 4 — Answer routing / skip logic (Priority: P2)

**Goal**: F9 answer routing enabled only with one-question-per-page layout; enabling disables + locks shuffle; layout switch turns routing off; eligible question types (Single select, Scale except Slider, Yes/No, KPI) that are standalone expose routing editor; "Routing set" badge; routing×Questions-Sets rules per FR-9.5.

**Independent Test**: Enable one-question-per-page layout, toggle Question routing on, confirm shuffle-disabled prompt, open routing editor on a KPI question, set "Score = 1" → "End survey", save; preview run answering "1" jumps directly to thank-you.

### Unit Tests for User Story 4 (write FIRST, must FAIL) ⚠️

- [X] T163 [P] [US4] Unit tests for `RoutingEligibilityService` in `tests/Nabadat.SurveyBuilder.UnitTests/Routing/RoutingEligibilityServiceTests.cs` — `IsEligible(question: {type: MultiSelect}) → false`; `IsEligible(question: {type: Scale}) → true`; `IsEligible(question: {type: Scale, subType: "slider"}) → false`; `IsEligible(question: {type: SingleSelect, inSet: true}) → false` (FR-9.5). (A)
- [X] T164 [P] [US4] Unit tests for `LayoutRoutingCoupler` in `tests/Nabadat.SurveyBuilder.UnitTests/Routing/LayoutRoutingCouplerTests.cs` — `OnLayoutChanged(survey, next: "single_page") → survey.RoutingOn = false`; `OnRoutingEnabled(survey) → survey.ShuffleOn = false, survey.ShuffleLocked = true`. (A)
- [X] T165 [P] [US4] Unit tests for `RoutingConflictDetector` in `tests/Nabadat.SurveyBuilder.UnitTests/Routing/RoutingConflictDetectorTests.cs` — `Detect(routes) → CycleDetected` when a route points back to a prior question. (A)
- [X] T166 [P] [US4] Unit tests for `RoutingDefaultTargeter` in `tests/Nabadat.SurveyBuilder.UnitTests/Routing/RoutingDefaultTargeterTests.cs` — `Default(question, nextInOrder) → nextInOrder.Id`; default not persisted. (A)

### Red Checkpoint for User Story 4 (MANDATORY) 🔴

- [X] T167 [US4] Run `dotnet test tests/Nabadat.SurveyBuilder.UnitTests --filter "FullyQualifiedName~Routing"`. Verify red; commit. (A) — verified RED (compile error: `Application.Routing` namespace + 4 service types absent; valid per Unit Test Policy §7). **Commit skipped per user instruction.**

### Implementation for User Story 4

- [X] T168 [P] [US4] Create `src/Nabadat.SurveyBuilder/Domain/Entities/RoutingMap.cs` per [data-model.md § 2.5](./data-model.md#25-routing_maps). (A)
- [X] T169 [P] [US4] Create `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Configurations/RoutingMapConfiguration.cs`. (A)
- [X] T170 [P] [US4] Create `src/Nabadat.SurveyBuilder/Application/Routing/Interfaces/IRoutingMapStore.cs` + `Infrastructure/Persistence/Stores/RoutingMapStore.cs`. (A)
- [X] T171 [P] [US4] Create `src/Nabadat.SurveyBuilder/Application/Routing/RoutingEligibilityService.cs` matching T163. (A)
- [X] T172 [P] [US4] Create `src/Nabadat.SurveyBuilder/Application/Routing/LayoutRoutingCoupler.cs` matching T164. (A) — added derived `Survey.ShuffleLocked => RoutingOn` (EF-ignored, no migration) per FR-9.1; also exposed on `SurveyView`.
- [X] T173 [P] [US4] Create `src/Nabadat.SurveyBuilder/Application/Routing/RoutingConflictDetector.cs` matching T165. (A) — with supporting `RoutingEdge`, `RoutingConflictKind`, `RoutingConflictResult` (one type per file).
- [X] T174 [P] [US4] Create `src/Nabadat.SurveyBuilder/Application/Routing/RoutingDefaultTargeter.cs` matching T166. (A)
- [X] T175 [US4] Create `src/Nabadat.SurveyBuilder/Application/Routing/RoutingConfigurationService.cs` — Save / Get per-question routing maps; enforces eligibility + no-cycles + layout coupling; invalidates outdated routes on question delete (FR-2.7 driver). (A)
- [X] T176 [P] [US4] Create `src/Nabadat.SurveyBuilder/Api/Controllers/SurveyRoutingController.cs` — routes per [contracts/questions.md](./contracts/questions.md): `POST /api/v1/surveys/{id}/routing` (survey-level toggle with FR-9.1 confirmation), `PUT /api/v1/surveys/{id}/questions/{qid}/routing`, `GET /api/v1/surveys/{id}/questions/{qid}/routing`. (A) — PUT/GET return `RoutingMapView` (the three T177 DTOs); ETag on `question.row_version` (PUT) / `survey.row_version` (POST).
- [X] T177 [P] [US4] Create `src/Nabadat.SurveyBuilder/Api/Contracts/` DTOs — `EnableRoutingRequest.cs`, `RoutingMapView.cs`, `UpdateRoutingMapRequest.cs`. (A)
- [X] T178 [P] [US4] Extend `frontend/src/features/surveys/api/` with `routing-api.ts`. (F) — **done 2026-07-20**: `toggleSurveyRouting` (returns refreshed SurveyView incl. `shuffleLocked`, If-Match = survey ETag) + `getQuestionRouting`/`saveQuestionRouting` (sparse map, `__end` sentinel, If-Match = question ETag).
- [X] T179 [P] [US4] Create `frontend/src/features/surveys/components/RoutingMapEditor.tsx` — per-answer Go-to Select rows; default = "next question" (rendered but not persisted); "End survey" option; opens as a `Sheet` from the question card. Reuses `useSurveyEtag`. (F) — **done 2026-07-20**: Sheet with one Go-to row per answer key (keys derived per type: scale points / options / yes-no), default "Next question" rendered but never persisted, "End survey" option, targets = later standalone questions; saves with the question ETag and reports `hasRouting` back to the canvas.
- [X] T180 [P] [US4] Extend `SurveyBuilderPage.tsx` — routing toggle in header (disabled unless layout = one-question-per-page with tooltip explaining the requirement, FR-9.1); confirmation modal on enable ("Enable question routing? — Cancel / Enable routing"); on confirm, shuffle is turned off and locked. "Routing set" badge on question cards when routes exist. Set questions display no routing control (FR-9.5). (F) — **done 2026-07-20**: toggle disabled unless layout = question with FR-9.1 tooltip; enable-confirmation modal; server locks shuffle (`shuffleLocked` from response); Routing-set badge + routing shortcut on eligible standalone rows only (FR-9.5 — set members show no control).

### Integration & API / Scenario Tests for User Story 4 🐳

- [X] T181 [P] [US4] API tests `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Routing/RoutingEndpointTests.cs` — `POST /surveys/{id}/routing` returns 409 when layout ≠ question; toggling on returns 200 + `shuffleLocked=true` in the response payload; `PUT /surveys/{id}/questions/{qid}/routing` persists the per-answer map; `GET` returns it verbatim; a set question cannot be a routing target (returns 400 `routing.target_ineligible`); slider Scale cannot be a routing source (returns 409 `routing.source_ineligible`). (A)

### E2E (Browser) Tests for User Story 4 🎭

- [ ] T182 [P] [US4] E2E tests `tests/Nabadat.E2ETests/SurveyBuilder/RoutingTests.cs` — toggle disabled unless layout = one-question-per-page (tooltip visible); confirmation modal appears; Cancel returns to previous state; Confirm disables/locks shuffle; routing editor lists one row per answer with correct default target ("next question"); "Routing set" badge appears on cards with a saved map; preview run for the "Score 1 → End survey" scenario ends the survey when 1 is chosen. (F)

**Build gate for US4**: unit + integration + E2E green.

**Checkpoint**: US4 complete.

---

## Phase 7: User Story 5 — Templates: built-in library and tenant-authored (Priority: P2)

**Goal**: Save an existing survey as a template (FR-7.4 full snapshot including journey/stage/touchpoint bindings); instantiate a survey from a template (FR-6.3); built-in templates locked (FR-7.1); template deletion snapshot-no-link (BR-7.1 / Q4).

**Independent Test**: P-01 saves a survey with journey binding as a template; instantiates a new survey; asserts settings, appearance, questions AND journey/stage/touchpoint bindings all carried; editing/deleting the template does not affect the source survey.

### Unit Tests for User Story 5 (write FIRST, must FAIL) ⚠️

- [X] T183 [P] [US5] Unit tests for `TemplateSnapshotBuilder` in `tests/Nabadat.SurveyBuilder.UnitTests/Templates/TemplateSnapshotBuilderTests.cs` — `Build(survey) → snapshot includes {journeyId, stageId, touchpointId} on every question` (copy-all, FR-7.4). (B)
- [X] T184 [P] [US5] Unit tests for `TemplateAuthorizationService` in `tests/Nabadat.SurveyBuilder.UnitTests/Templates/TemplateAuthorizationServiceTests.cs` — `CanEdit(template: {class: "BuiltIn"}, actor: P-01) → false` (FR-7.1). (B)
- [X] T185 [P] [US5] Unit tests for `TemplateSearchIndexer` in `tests/Nabadat.SurveyBuilder.UnitTests/Templates/TemplateSearchIndexerTests.cs` — `Match(term: "onboarding", template: {name: "Onboarding pulse", tags: []}) → true`; `Match("onboarding", {name: "Post-visit", tags: ["Onboarding"]}) → true` (FR-6.2). (B)
- [X] T186 [P] [US5] Unit tests for `TemplateInstantiator` in `tests/Nabadat.SurveyBuilder.UnitTests/Templates/TemplateInstantiatorTests.cs` — `CreateSurveyFrom(template) → survey with same settings, questions, appearance AND journey/stage/touchpoint bindings`; new survey has no back-reference (BR-7.1). (B)

### Red Checkpoint for User Story 5 (MANDATORY) 🔴

- [X] T187 [US5] Run `dotnet test tests/Nabadat.SurveyBuilder.UnitTests --filter "FullyQualifiedName~Templates"`. Verify red; commit. (B) — **Red verified 2026-07-19**: compile error (CS0234/CS0246) — `Application.Templates` namespace + `TemplateSnapshotBuilder`/`TemplateAuthorizationService`/`TemplateSearchIndexer`/`TemplateInstantiator`/`SurveySnapshot` types absent (valid red per Unit Test Policy rule 7; T188–T194 not yet implemented). Commit skipped per user instruction.

### Implementation for User Story 5

- [X] T188 [P] [US5] Create `src/Nabadat.SurveyBuilder/Domain/Entities/Template.cs` + `TemplateSnapshot.cs` per [data-model.md § 2.8-2.9](./data-model.md#28-templates). (B)
- [X] T189 [P] [US5] Create `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Configurations/TemplateConfiguration.cs`. (B)
- [X] T190 [P] [US5] Create `src/Nabadat.SurveyBuilder/Application/Templates/Interfaces/ITemplateStore.cs` + `Infrastructure/Persistence/Stores/TemplateStore.cs`. (B)
- [X] T191 [P] [US5] Create `src/Nabadat.SurveyBuilder/Application/Templates/TemplateSnapshotBuilder.cs` matching T183. (B)
- [X] T192 [P] [US5] Create `src/Nabadat.SurveyBuilder/Application/Templates/TemplateAuthorizationService.cs` matching T184. (B)
- [X] T193 [P] [US5] Create `src/Nabadat.SurveyBuilder/Application/Templates/TemplateSearchService.cs` matching T185. (B)
- [X] T194 [P] [US5] Create `src/Nabadat.SurveyBuilder/Application/Templates/TemplateInstantiator.cs` matching T186; wraps in `ExecuteAsync`; on completion the new Survey is Draft with `owner_user_id = caller`. (B)
- [X] T195 [US5] Create `src/Nabadat.SurveyBuilder/Application/Templates/TemplateCommandService.cs` — Create / Update / RebuildFromSurvey / Delete / Instantiate / Preview. Enforces BR-7.1 no-cascade on delete. (B)
- [X] T196 [P] [US5] Create `src/Nabadat.SurveyBuilder/Api/Controllers/TemplatesController.cs` — routes per [contracts/templates.md](./contracts/templates.md). (B)
- [X] T197 [P] [US5] Create `src/Nabadat.SurveyBuilder/Api/Contracts/` DTOs — `CreateTemplateRequest.cs`, `UpdateTemplateRequest.cs`, `RebuildTemplateRequest.cs`, `InstantiateTemplateRequest.cs`, `TemplateView.cs`, `TemplateListItem.cs`, `TemplateListResponse.cs`. (B)
- [X] T198 [P] [US5] Extend `frontend/src/features/surveys/api/` with `templates-api.ts`. (F) — **done 2026-07-20**: `templates-api.ts` — list/get/create/patch/delete/instantiate matching TemplatesController; `class` int-enum in views but NAME on the query string; instantiate returns the new survey id for the Settings redirect.
- [X] T199 [P] [US5] Create `frontend/src/features/surveys/pages/TemplatePickerPage.tsx` — F6: sorted customized-first, then built-in (FR-6.1); built-in cards show sector chips + padlock; customized cards show tag chips; tag search (FR-6.2); "Use this template" fires instantiate. (F) — **done 2026-07-20**: `TemplatePickerPage.tsx` — server sort (customized-first FR-6.1), built-in cards = sector chips + padlock, customized = tag chips, debounced name-or-tag search (FR-6.2), "Use this template" → instantiate → `/surveys/{id}/settings`. Replaces the T033 `TemplateLibraryPage` placeholder (deleted; App route re-pointed).
- [X] T200 [P] [US5] Create `frontend/src/features/surveys/pages/TemplateEditorPage.tsx` — F7: name/description/tags editable for Customized; disabled with notice for BuiltIn (FR-7.1); Edit questions opens the builder in template context. Class + Primary sector inputs are NOT part of the authoring form (FR-7.3). (F) — **done 2026-07-20**: `TemplateEditorPage.tsx` — name EN/AR + description + tags chip-adder editable for Customized; all controls disabled + read-only notice for BuiltIn (FR-7.1); Class/Primary-sector display-only (FR-7.3); "Edit questions" instantiates a working copy and opens its builder (snapshot model; backend `rebuild-from-survey` recaptures). Replaces the T033 `TemplateEditPage` placeholder.

### Integration & API / Scenario Tests for User Story 5 🐳

- [X] T201 [P] [US5] API tests `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Templates/TemplateEndpointTests.cs` — POST from a survey returns 201 with all data captured; POST /instantiate returns a new survey id whose questions and journey/stage/touchpoint bindings match the template exactly; PATCH returns 403 on a built-in template; GET /templates?search=onboarding matches name or tag. (B)
- [X] T202 [P] [US5] Scenario test `tests/Nabadat.SurveyBuilder.IntegrationTests/Scenarios/TemplateCreateAndInstantiateScenarioTests.cs` — P-01 creates survey with journey binding → saves as template → instantiates new survey → asserts settings/appearance/questions AND journey/stage/touchpoint bindings all carried; then DELETE the template and re-fetch the instantiated survey (still intact — BR-7.1). (B)

### E2E (Browser) Tests for User Story 5 🎭

- [ ] T203 [P] [US5] E2E tests `tests/Nabadat.E2ETests/SurveyBuilder/TemplatesTests.cs` — template picker orders customized-first; built-in cards show padlock + sector chips; tag search filters correctly; preview opens without creating a survey; edit disabled for built-in with a notice. (F)

**Build gate for US5**: unit + integration + E2E green.

**Checkpoint**: US5 complete.

---

## Phase 8: User Story 6 — Translate workspace (Priority: P2)

**Goal**: F11 workspace exposes every localizable string (survey name, welcome/thanks, section titles, question text/description, option labels, scale labels, reason items, per-question comment-field label); Arabic RTL; source + target side-by-side; save without Arabic name is allowed (BR-3.2).

**Independent Test**: Open Translate workspace; enter Arabic values for name + welcome + one option label + one scale label; preview renders RTL; report's localised string uses the Arabic value.

### Unit Tests for User Story 6 (write FIRST, must FAIL) ⚠️

- [X] T204 [P] [US6] Unit tests for `TranslationBundleBuilder` in `tests/Nabadat.SurveyBuilder.UnitTests/Translations/TranslationBundleBuilderTests.cs`. (B)
- [X] T205 [P] [US6] Unit tests for `TranslatableStringExtractor` in `tests/Nabadat.SurveyBuilder.UnitTests/Translations/TranslatableStringExtractorTests.cs` — `Extract(survey) → bundle` with keys covering nameEn/nameAr, welcome, thanks, per-question text/description/options/scale-labels/reason-items, section titles, per-question comment-field label. (B)
- [X] T206 [P] [US6] Unit tests for `LocaleFallbackPolicy` in `tests/Nabadat.SurveyBuilder.UnitTests/Translations/LocaleFallbackPolicyTests.cs` — `Resolve(bundle, locale: "ar", key: "welcome") → English fallback when Arabic missing` (BR-3.2). (B)

### Red Checkpoint for User Story 6 (MANDATORY) 🔴

- [X] T207 [US6] Run `dotnet test tests/Nabadat.SurveyBuilder.UnitTests --filter "FullyQualifiedName~Translations"`. Verify red; commit. (B) — verified red (CS0246 compile error: all Translations types absent, valid red for the right reason). **Red-baseline commit intentionally skipped per user instruction ("dont commit").**

### Implementation for User Story 6

- [X] T208 [P] [US6] Create `src/Nabadat.SurveyBuilder/Domain/Entities/SurveyTranslation.cs` per [data-model.md § 2.7](./data-model.md#27-survey_translations). (B)
- [X] T209 [P] [US6] Create `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Configurations/TranslationConfiguration.cs`. (B)
- [X] T210 [P] [US6] Create `src/Nabadat.SurveyBuilder/Application/Translations/Interfaces/ITranslationStore.cs` + `Infrastructure/Persistence/Stores/TranslationStore.cs`. (B)
- [X] T211 [P] [US6] Create `src/Nabadat.SurveyBuilder/Application/Translations/TranslatableStringExtractor.cs` matching T205. (B)
- [X] T212 [P] [US6] Create `src/Nabadat.SurveyBuilder/Application/Translations/LocaleFallbackPolicy.cs` matching T206. (B)
- [X] T213 [US6] Create `src/Nabadat.SurveyBuilder/Application/Translations/TranslationBundleService.cs` — Get / Put per-locale; on question/section delete purges affected keys via a hook called by `SectionCascadeService.Delete` + `QuestionDeletionService.Delete` (FR-2.8). (B) — real EF `TranslationStore.PurgeQuestionKeysAsync` now backs the hook (replaced the no-op stub). Also created `TranslationBundleBuilder` (+ `ResolvedTranslationBundle`/`TranslationBundle`/`LocaleCoverage`/`TranslationBundleResult`) — resolves GAP TODO-M01-021 (builder had no task). Unit tests T204–T206 green (22 passed).
- [X] T214 [P] [US6] Create `src/Nabadat.SurveyBuilder/Api/Controllers/SurveyTranslationsController.cs` — routes per [contracts/translations.md](./contracts/translations.md). (B)
- [X] T215 [P] [US6] Create `src/Nabadat.SurveyBuilder/Api/Contracts/` DTOs — `TranslationBundleView.cs`, `PutTranslationBundleRequest.cs`, `LocaleSummary.cs` (+ `TranslationLocalesResponse.cs`). (B)
- [X] T216 [P] [US6] Extend `frontend/src/features/surveys/api/` with `translations-api.ts`. (F) — **done 2026-07-20**: `translations-api.ts` — locales+coverage list, resolved bundle GET (keys + missingKeys), bundle PUT with If-Match (explicit Save per Q1).
- [X] T217 [P] [US6] Create `frontend/src/features/surveys/pages/TranslateWorkspacePage.tsx` — side-by-side EN | AR editor per FR-11.2; AR column `dir="rtl"` on the input; missing-keys coverage indicator; per-key Textarea for long content; Auto-save on blur is NOT applied (Q1 no autosave — an explicit Save button per locale bundle). All logical properties. (F) — **done 2026-07-20**: `TranslateWorkspacePage.tsx` — side-by-side EN|AR grid (FR-11.2), `dir="rtl" lang="ar"` inputs, missing-key badges + live coverage counter, long-content keys get taller Textareas, ONE explicit Save per bundle (no autosave), unsaved-changes guard + ETag conflict dialog; wired at `/surveys/:id/translate`.

### Integration & API / Scenario Tests for User Story 6 🐳

- [X] T218 [P] [US6] API tests `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Translations/TranslationEndpointTests.cs` — `GET /translations/ar` returns the bundle; missing keys resolve to English fallback in the resolved view; `PUT /translations/ar` persists the Arabic values and echoes them on the next GET; save without Arabic name is allowed (English-only proceed, BR-3.2). (B) — 7 tests green vs Testcontainers Postgres (GET fallback + all-missing, PUT persist+echo+fallback, merge, coverage list, English-only proceed, unknown-key 400, not-configured 400). Route is the path form `/translations/{locale}` (contract), not the `?locale=` query the task text sketched.

### E2E (Browser) Tests for User Story 6 🎭

- [ ] T219 [P] [US6] E2E tests `tests/Nabadat.E2ETests/SurveyBuilder/TranslateTests.cs` — workspace lists every localisable string with side-by-side source/target; Arabic column renders RTL (`dir="rtl"` verified); save without Arabic name is allowed. (F)

**Build gate for US6**: unit + integration + E2E green.

**Checkpoint**: US6 complete.

---

## Phase 9: User Story 7 — Multi-channel preview (Priority: P2)

**Goal**: F12 preview — Desktop (default) / Mobile / WhatsApp / Email chrome; welcome/thanks render live from editors; pagination follows layout; section titles above questions (FR-12.4).

**Independent Test**: Open preview on a saved survey; Desktop default; switch to WhatsApp re-renders with WhatsApp chrome; layout change reflects in pagination.

**Backend note**: US7 declares `unit-tests: skipped — preview is a client-side renderer of persisted survey state` and `integration-tests: skipped — no server-owned behaviour beyond GET /api/surveys/{id}`. **No Unit / Red-Checkpoint / Integration subsections**. Only implementation + E2E.

### Implementation for User Story 7

- [X] T220 [P] [US7] Create `src/Nabadat.SurveyBuilder/Api/Controllers/SurveyPreviewController.cs` — `GET /api/v1/surveys/{id}/preview?channel=…&locale=…` per [contracts/report-and-analytics.md § GET /preview](./contracts/report-and-analytics.md#get-apiv1surveysidpreview). Returns the resolved survey view with theme tokens + locale bundle inlined. (B)
- [X] T221 [P] [US7] Create `src/Nabadat.SurveyBuilder/Application/Preview/PreviewPayloadBuilder.cs` — assembles the payload from `ISurveyStore`, `ITenantDesignGuidelinesReader`, `ITranslationStore`; applies `LocaleFallbackPolicy` for missing keys. (B) — resolves theme tokens via `AppearanceService` (which wraps `ITenantDesignGuidelinesReader` and also handles Customize mode); invalid channel → `400 preview.channel.invalid`. (+ `PreviewPayload` result record.)
- [X] T222 [P] [US7] Create `src/Nabadat.SurveyBuilder/Api/Contracts/PreviewView.cs`. (B)
- [X] T223 [P] [US7] Extend `frontend/src/features/surveys/api/` with `preview-api.ts`. (F) — **done 2026-07-20**: `preview-api.ts` — GET /preview?channel&locale → full client render payload (camelCase; reuses the survey/section/question normalizers).
- [X] T224 [P] [US7] Create `frontend/src/features/surveys/pages/PreviewPage.tsx` — full-page route showing `LivePreviewFrame` with channel tabs (Desktop | Mobile | WhatsApp | Email). Client-side re-renders the frame with channel chrome around the same payload. Section titles rendered as headings above each block (FR-12.4). WhatsApp and Email chrome are third-party mockups — inline `style` with hex is permitted here (per CLAUDE.md Theming self-review "third-party device mockups"); document the exemption inline. (F) — **done 2026-07-20**: `PreviewPage.tsx` — channel tabs Desktop|Mobile|WhatsApp|Email re-rendering the SAME payload client-side, EN|AR locale switch resolving bundle keys (survey.welcome / section.{id}.title / question.{id}.text) with EN fallback, section headings per FR-12.4, per-type read-only answer visuals, empty state; wired at `/surveys/:id/preview`.
- [X] T225 [P] [US7] Extend `frontend/src/features/surveys/components/LivePreviewFrame.tsx` to add the WhatsApp + Email chrome variants — reuse the F4 Desktop/Mobile design but wrap in the third-party chrome shell. Same iframe content; different outer frame. (F) — **done 2026-07-20**: `LivePreviewFrame.tsx` extended — `channels` prop + WhatsApp chrome (teal header/wallpaper — fixed inline hex under the documented third-party-mockup exemption, noted inline) and Email reader chrome; F4 Desktop/Mobile unchanged; accepts full survey content via `children`.

### E2E (Browser) Tests for User Story 7 🎭

- [ ] T226 [P] [US7] E2E tests `tests/Nabadat.E2ETests/SurveyBuilder/PreviewTests.cs` — default channel = Desktop web; switch Mobile / WhatsApp / Email → chrome changes; content survives; layout change reflects in pagination (four modes verified); empty state when a survey has no questions. (F)

**Build gate for US7**: `npm run build` + E2E green.

**Checkpoint**: US7 complete.

---

## Phase 10: User Story 8 — Survey Report (Priority: P3)

**Goal**: F13 Report — metric cards (Responses / Completion rate / Median time / Touchpoints), KPI gauges with target markers + period delta, period filter, per-question result views chosen by type. Reads exclusively from ES (AD-04).

**Independent Test**: Load report on a survey seeded with responses; change period to "Last 7 days" and assert (a) response count matches fixture, (b) CSAT gauge shows the average, (c) each per-question card renders the correct visual per type per FR-13.3, (d) responses collected after the active period do not appear.

### Unit Tests for User Story 8 (write FIRST, must FAIL) ⚠️

- [X] T227 [P] [US8] Unit tests for `HeadlineCsatCalculator` in `tests/Nabadat.SurveyBuilder.UnitTests/Report/HeadlineCsatCalculatorTests.cs` — `Compute([81m, 76m]) → 78.5m`; `Compute([]) → null`. (B)
- [X] T228 [P] [US8] Unit tests for `PeriodResolver` in `tests/Nabadat.SurveyBuilder.UnitTests/Report/PeriodResolverTests.cs` — `Resolve("last_7_days", now) → {From: now.AddDays(-7), To: now}`. (B)
- [X] T229 [P] [US8] Unit tests for `PerQuestionViewSelector` in `tests/Nabadat.SurveyBuilder.UnitTests/Report/PerQuestionViewSelectorTests.cs` — `Pick(type: MultiSelect) → BarWithCountsAndPct`; `Pick(type: Scale, subType: Labels) → GaugeOnly`. (B)
- [X] T230 [P] [US8] Unit tests for `ResponseWindowFilter` in `tests/Nabadat.SurveyBuilder.UnitTests/Report/ResponseWindowFilterTests.cs` — `Include(response, activePeriod) → false when response.SubmittedAt > survey.SentAt + activePeriod` (FR-13.6). (B)
- [X] T231 [P] [US8] Unit tests for `VerbatimSampler` in `tests/Nabadat.SurveyBuilder.UnitTests/Report/VerbatimSamplerTests.cs` — `Sample(responses, limit: 100) → newest-first up to 100` (FR-13.7). (B)

### Red Checkpoint for User Story 8 (MANDATORY) 🔴

- [X] T232 [US8] Run `dotnet test tests/Nabadat.SurveyBuilder.UnitTests --filter "FullyQualifiedName~Report"`. Verify red; commit. (B) — red verified 2026-07-19 (CS0246 compile errors: `Application/Report/` types don't exist yet — valid red per Unit Test Policy rule 7). **Commit intentionally skipped per user instruction ("dont commit"); commit the red baseline before T233.**

### Implementation for User Story 8

- [X] T233 [P] [US8] Create `src/Nabadat.SurveyBuilder/Application/Report/HeadlineCsatCalculator.cs` matching T227. (B)
- [X] T234 [P] [US8] Create `src/Nabadat.SurveyBuilder/Application/Report/PeriodResolver.cs` matching T228. (B)
- [X] T235 [P] [US8] Create `src/Nabadat.SurveyBuilder/Application/Report/PerQuestionViewSelector.cs` matching T229. (B)
- [X] T236 [P] [US8] Create `src/Nabadat.SurveyBuilder/Application/Report/ResponseWindowFilter.cs` matching T230. (B)
- [X] T237 [P] [US8] Create `src/Nabadat.SurveyBuilder/Application/Report/VerbatimSampler.cs` matching T231. (B)
- [X] T238 [P] [US8] Create `src/Nabadat.SurveyBuilder/Application/Report/Interfaces/IReportAggregator.cs` — ES query port. (B)
- [X] T239 [P] [US8] Create `src/Nabadat.SurveyBuilder/Infrastructure/Elasticsearch/ReportAggregator.cs` — implements `IReportAggregator` reading `tenant_{tenantId}_analytics` + `tenant_{tenantId}_responses`; HTTPS 9200; permission-scoped filter clauses applied server-side before the ES query is dispatched (APIs-constitution Article 4.5). (B)
- [X] T240 [P] [US8] Create `src/Nabadat.SurveyBuilder/Infrastructure/Elasticsearch/EsQueryBuilder.cs` — shared query-clause helpers (period range, tenant permission scope, per-question filter). (B)
- [X] T241 [P] [US8] Create `src/Nabadat.SurveyBuilder/Infrastructure/Elasticsearch/EsClientFactory.cs` — singleton `ElasticsearchClient` from `Elastic.Clients.Elasticsearch` 8.x. (B)
- [X] T242 [US8] Create `src/Nabadat.SurveyBuilder/Application/Report/ReportService.cs` — composes the above; entry point for the controller. (B)
- [X] T243 [P] [US8] Create `src/Nabadat.SurveyBuilder/Api/Controllers/SurveyReportController.cs` — routes per [contracts/report-and-analytics.md](./contracts/report-and-analytics.md): `GET /api/v1/surveys/{id}/report`, `GET /api/v1/surveys/{id}/report/verbatims`. Declares `required_permission = "survey.report.read"`. (B)
- [X] T244 [P] [US8] Create `src/Nabadat.SurveyBuilder/Api/Contracts/` DTOs — `ReportView.cs`, `MetricCards.cs`, `HeadlineKpi.cs`, `PerQuestionResult.cs`, `VerbatimSampleResponse.cs`. (B)
- [X] T245 [P] [US8] Extend `frontend/src/features/surveys/api/` with `report-api.ts`. (F) — **done 2026-07-20**: `report-api.ts` — report + verbatims wrappers; maps the report DTOs' explicit snake_case wire (unlike the camelCase survey DTOs) to camel domain in one place; FR-13.1 period vocabulary + custom from/to.
- [X] T246 [P] [US8] Create `frontend/src/features/surveys/pages/ReportPage.tsx` — full-page route; metric cards (`ReportMetricCard.tsx`) show Responses / Completion rate / Median time / Touchpoints; KPI gauges custom SVG per CLAUDE.md § KPI Gauge Design Spec (dual-ring semicircular with red/amber/green zones, needle dot, target marker, tri-tile segment grid below); period filter with all FR-13.1 windows + custom range; per-question views wired per FR-13.3 (bar+gauge for KPI, donut for single-select/YesNo, bar for multi-select, aggregate gauge + style visual for Scale, verbatim table with "show more" up to 100 for Text/Paragraph, value-distribution line for Number/Date/Time). Every headline value coloured by `perfColor(value, kpiId)` per CLAUDE.md. Uses `shadcn/charts` + custom SVG per the Data Visualization spec. (F) — **done 2026-07-20**: `ReportPage.tsx` — 4 metric cards, CSAT/NPS/CES gauges (NPS −100..+100, CES via `perfColor(v,"ces")`), FR-13.1 period filter + custom range, FR-13.3 per-question routing (KPI/Scale→gauge+bars, single/YesNo→donut, multi→respondent-base bars, Text/Paragraph→verbatims, Number/Date→line; Matrix/Ranking→explicit no-visual note per TODO-M01-024); wired at `/surveys/:id/report`.
- [X] T247 [P] [US8] Create supporting components `ReportMetricCard.tsx`, `KpiGaugeSvg.tsx` (custom SVG per KPI Gauge Design Spec), `DistributionDonut.tsx` (Recharts wrapper), `MultiSelectBarChart.tsx`, `VerbatimTable.tsx` (with "show more" button firing `GET /report/verbatims?limit=100`), `NumericDistributionLine.tsx`. (F) — **done 2026-07-20**: 6 components — `KpiGaugeSvg` (dual-ring semicircle per the Gauge Spec: padded viewBox, 3-zone inner ring, needle dot w/ stroke-card ring, target T-marker, theme-aware neutral chrome), `ReportMetricCard`, `DistributionDonut` (ChartContainer+Pie, centre label, legend), `MultiSelectBarChart` (respondent-base %, FR-13.5), `VerbatimTable` (show-more → GET /verbatims?limit=100; LTR timestamps in start-aligned cells), `NumericDistributionLine`.

### Integration & API / Scenario Tests for User Story 8 🐳

- [X] T248 [P] [US8] API tests `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Report/ReportEndpointTests.cs` — `GET /report?period=last_7_days` returns metric cards, KPI gauges, and per-question payloads; late responses (submitted post-expiry, marked in a seeded fixture) are excluded from `/report`. Uses `EsTestcontainer` with seeded fixture docs. (B)

### E2E (Browser) Tests for User Story 8 🎭

- [ ] T249 [P] [US8] E2E tests `tests/Nabadat.E2ETests/SurveyBuilder/ReportTests.cs` — period filter switches all cards and charts; multi-select bars display "N respondents" base line and allow totals > 100%; verbatim responses list — "show more" reveals up to 100; median time is always visible; empty state when no responses exist. (F)

**Build gate for US8**: unit + integration + E2E green.

**Checkpoint**: US8 complete.

---

## Phase 11: User Story 9 — Survey Analytics (Priority: P3)

**Goal**: F14 Analytics — sent → opened → started → finished funnel; per-channel completion rates; responses trend; up/down deltas vs previous period of equal length (FR-14.3, FR-14.5 — deltas suppressed when no prior data).

**Independent Test**: Load Analytics with fixture where prior 7 days = 100→50 (50%), current 7 days = 200→120 (60%); assert funnel counts, per-stage % of Sent, stage-to-stage conversion chips, and ▲ +10 pp delta on Overall Completion Rate; a brand-new survey shows no deltas.

### Unit Tests for User Story 9 (write FIRST, must FAIL) ⚠️

- [X] T250 [P] [US9] Unit tests for `FunnelCalculator` in `tests/Nabadat.SurveyBuilder.UnitTests/Analytics/FunnelCalculatorTests.cs` — `Compute({Sent: 200, Opened: 160, Started: 130, Finished: 120}) → {OpenedPct: 80m, StartedPct: 65m, FinishedPct: 60m, OpenedToSent: 80m, StartedToOpened: 81.25m, FinishedToStarted: 92.31m}` (rounding rules preserved). (B)
- [X] T251 [P] [US9] Unit tests for `PeriodDeltaCalculator` in `tests/Nabadat.SurveyBuilder.UnitTests/Analytics/PeriodDeltaCalculatorTests.cs` — `Delta(current: 60m, prior: 50m, kind: "rate") → +10pp`; `Delta(current: 200, prior: 100, kind: "count") → +100%`; `Delta(current: X, prior: null) → null` (FR-14.5). (B)
- [X] T252 [P] [US9] Unit tests for `ChannelBreakdownCalculator` in `tests/Nabadat.SurveyBuilder.UnitTests/Analytics/ChannelBreakdownCalculatorTests.cs`. (B)
- [X] T253 [P] [US9] Unit tests for `TrendGranularityResolver` in `tests/Nabadat.SurveyBuilder.UnitTests/Analytics/TrendGranularityResolverTests.cs` — `Resolve(period: "last_7_days") → "daily"`; `Resolve("last_year") → "monthly"`. (B)

### Red Checkpoint for User Story 9 (MANDATORY) 🔴

- [X] T254 [US9] Run `dotnet test tests/Nabadat.SurveyBuilder.UnitTests --filter "FullyQualifiedName~Analytics"`. Verify red; commit. (B) — red verified (compile error: Analytics types absent, valid red per Unit Test Policy rule 7). Commit intentionally skipped per user instruction ("dont commit").

### Implementation for User Story 9

- [X] T255 [P] [US9] Create `src/Nabadat.SurveyBuilder/Application/Analytics/FunnelCalculator.cs` matching T250. (B)
- [X] T256 [P] [US9] Create `src/Nabadat.SurveyBuilder/Application/Analytics/PeriodDeltaCalculator.cs` matching T251. (B)
- [X] T257 [P] [US9] Create `src/Nabadat.SurveyBuilder/Application/Analytics/ChannelBreakdownCalculator.cs` matching T252. (B)
- [X] T258 [P] [US9] Create `src/Nabadat.SurveyBuilder/Application/Analytics/TrendGranularityResolver.cs` matching T253. (B)
- [X] T259 [P] [US9] Create `src/Nabadat.SurveyBuilder/Application/Analytics/Interfaces/IAnalyticsAggregator.cs` — ES query port. (B)
- [X] T260 [P] [US9] Create `src/Nabadat.SurveyBuilder/Infrastructure/Elasticsearch/AnalyticsAggregator.cs` — implements the port reading `tenant_{tenantId}_analytics`. (B)
- [X] T261 [US9] Create `src/Nabadat.SurveyBuilder/Application/Analytics/AnalyticsService.cs` — composes the calculators. (B)
- [X] T262 [P] [US9] Create `src/Nabadat.SurveyBuilder/Api/Controllers/SurveyAnalyticsController.cs` — `GET /api/v1/surveys/{id}/analytics?period=…&granularity=…`. (B)
- [X] T263 [P] [US9] Create `src/Nabadat.SurveyBuilder/Api/Contracts/` DTOs — `AnalyticsView.cs`, `FunnelStage.cs`, `ChannelBreakdown.cs`, `TrendBucket.cs` (+ supporting `AnalyticsPeriodView`, `AnalyticsFunnelView`, `OverallCompletionRateView` per the contract's nested shape). (B)
- [X] T264 [P] [US9] Extend `frontend/src/features/surveys/api/` with `analytics-api.ts`. (F) — **done 2026-07-20**: `analytics-api.ts` — snake_case wire → camel domain; documents the contract's scale mix (funnel %, channel/trend ratios in [0,1]).
- [X] T265 [P] [US9] Create `frontend/src/features/surveys/pages/AnalyticsPage.tsx` — full-page route; period + granularity selectors; `AnalyticsFunnel` (stacked horizontal bars per CLAUDE.md Funnel Visualizations spec, colour progression blue-lite → blue), `AnalyticsChannelBars` (segment breakdown pattern), `AnalyticsTrendChart` (Recharts `LineChart` inside `ChartContainer` with `ReferenceDot` for events); ▲ green / ▼ red delta indicators per FR-14.3 with % (counts) vs pp (rates); deltas suppressed when null (FR-14.5). (F) — **done 2026-07-20**: `AnalyticsPage.tsx` — period + granularity selectors (+ custom range), funnel/channels/trend cards with empty states; wired at `/surveys/:id/analytics`.
- [X] T266 [P] [US9] Create supporting components `AnalyticsFunnel.tsx`, `AnalyticsChannelBars.tsx`, `AnalyticsTrendChart.tsx`, `DeltaIndicator.tsx`. (F) — **done 2026-07-20**: 4 components — `AnalyticsFunnel` (stacked rounded bars, cyan light→deep progression, conv.% per step, overall-completion badge), `AnalyticsChannelBars` (bar ∝ max sent, perfColor completion %, sorted desc), `AnalyticsTrendChart` (ChartContainer LineChart, dual-axis completion+sent, dots, legend; ReferenceDot/Line event markers behind an `events` prop — no M-01 data source yet, TODO-M01-029), `DeltaIndicator` (▲green/▼red, % vs pp, null-suppressed per FR-14.5).

### Integration & API / Scenario Tests for User Story 9 🐳

- [X] T267 [P] [US9] API tests `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Analytics/AnalyticsEndpointTests.cs` — `GET /analytics?period=last_7_days&granularity=daily` returns funnel + deltas + channels + trend; `?period=last_1_day` on a new survey returns deltas as null. Uses `EsTestcontainer` with fixture docs. (B)

### E2E (Browser) Tests for User Story 9 🎭

- [ ] T268 [P] [US9] E2E tests `tests/Nabadat.E2ETests/SurveyBuilder/AnalyticsTests.cs` — period + granularity switches recompute every card, bar and line within 100 ms; deltas render with correct ▲ green / ▼ red glyph and % vs pp units; new survey suppresses deltas (no glyphs, no 0%); empty state / permission redirect. (F)

**Build gate for US9**: unit + integration + E2E green.

**Checkpoint**: All user stories now independently functional.

---

## Phase 12: Polish & Cross-Cutting Concerns

- [ ] T269 [P] Run [quickstart.md](./quickstart.md) scenarios 1–9 end-to-end against the local dev stack. Capture any regressions as follow-up bugs; every scenario should pass without spec deviation. (A + B + F — jointly on the shared dev environment.)
- [ ] T270 [P] Run the CLAUDE.md § Theming self-review regex sweeps across `frontend/src/features/surveys/` — `-\[#[0-9a-fA-F]{3,8}\]` must return zero matches; every `style={{…}}` hex must be an intentionally-fixed third-party mockup (Preview WhatsApp/Email chrome only). (F)
- [ ] T271 [P] Run the full solution unit + integration + contract + E2E suite: `dotnet test Nabadat.sln`; `npm run build` in `frontend/`; `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~SurveyBuilder"`. Zero failures. (A + B + F)
- [ ] T272 [P] Extend `tests/Nabadat.E2ETests/SurveyBuilder/COVERAGE.md` — one row per E2E `[TestMethod]` mapping to the spec.md acceptance-scenario ID it verifies. Verify no gaps (compare to US1–US9 Acceptance Scenarios lists). (F)
- [ ] T273 [P] Confirm constitution **AMENDMENT-012** is ratified (T022 filed it) and that all four events — `survey.responses.purged`, `survey.created`, `survey.status.changed`, `survey.submitted_for_review` — appear in `.specify/memory/constitution.md` Section 4, and that M-01's owned-tables entry in Section 3 matches the 9-table Feature 004 set. If ratification is still pending, keep `POST /status {to:"Draft", confirm:true, fromActive:true}` returning 501 per T072 until it lands. (B)
- [ ] T274 [P] Accessibility scan across every new page: `axe-core` via Playwright in `tests/Nabadat.E2ETests/SurveyBuilder/AccessibilityAxeTests.cs`; 0 critical / 0 serious per SC-008. Verify every icon-only button has `aria-label`; every error message uses `role="alert"`. (F)
- [ ] T275 [P] Performance check: measure Report + Analytics period-filter switch p95 latency on the E2E fixture; must be ≤ 100 ms per SC-003. Emit a `perf-log.md` in the branch. (F)
- [ ] T276 [P] Freeze the sanitiser allowlist v1 in `SanitiserPolicyVersion.cs` and document it in `docs/security/html-sanitiser-allowlist-v1.md` (create the docs folder if absent) — any future expansion is a deliberate, tracked change per Q3. (B)
- [ ] T277 [P] Refresh the CLAUDE.md SPECKIT plan pointer if any downstream feature branches diverged during the build; verify the marker still points to `specs/004-survey-form-builder/plan.md`. (A)
- [ ] T278 [P] **Added by `/speckit-analyze` (2026-07-15) — SC-006 had no corresponding task.** Emit a telemetry/audit marker proving the Pause-with-rules confirmation modal (FR-1.10) was shown on every Active → Paused transition: `SurveyLifecycleService`'s Pause path writes an M-17 event (or an `event_log` payload flag) recording `{survey_id, rules_count, modal_shown: bool, actor, timestamp}` whenever `rules_count > 0`; add an integration test asserting the flag is `true` for every Pause with `rules_count > 0` and absent (not `false`) when `rules_count == 0` (no modal expected, FR-1.11 "Pause with 0 rules" edge case). Satisfies SC-006 ("0 surveys paused unintentionally ... traced from telemetry that logs whether the modal was invoked"). (A)
- [ ] T279 Final commit + PR — squash-merge policy per the team's convention; include the constitution AMENDMENT-012 reference in the PR body. Do NOT push before all build gates are green. (A + B + F)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001–T007. `T001` and `T007` are sequential (both edit `Nabadat.SurveyBuilder.csproj` / `SurveyBuilderServiceCollectionExtensions.cs`); the [P]-marked T002–T006 run in parallel after `T001`. All of Phase 1 completes before Phase 2.
- **Foundational (Phase 2)**: T008–T038. Within: `T008` (baseline SQL) must precede any store/context work; `T009–T012` (context wiring) must complete before any store; the domain value objects `T013–T017` are [P] and independent; `T018–T022` (cross-module ports + amendment coordination) run in parallel with the middleware block `T023–T026`; `T030–T031` (test infrastructure) block every integration-lane task later; `T033–T038` (frontend scaffold) run in parallel with the backend work. **All of Phase 2 completes before Phase 3.**
- **User Story phases (Phases 3–11)**: All depend on Phase 2. Within each phase, the order is strictly: Unit Tests → **Red Checkpoint (non-parallel)** → Implementation → Integration & API / Scenario → E2E.
- **Polish (Phase 12)**: All prior phases green.

### User Story Dependencies

- **US1 (P1)**: no dependency on other stories. **MVP boundary.**
- **US2 (P1)**: depends on US1 (needs surveys to submit/publish; extends `SurveysController`).
- **US3 (P2)**: depends on US1 (surveys + sections exist); adds sets + low-response + render-plan.
- **US4 (P2)**: depends on US1 (surveys + questions exist) + US3 for the `RoutingEligibilityService.IsEligible(inSet: bool)` case.
- **US5 (P2)**: depends on US1 (has a survey to snapshot) + US3 (structure to preserve) + US4 (routing map to preserve — but the routing-preserved test is skippable until US4 lands; US5 unit tests use a survey without routing).
- **US6 (P2)**: depends on US1 (has content to translate) + US3 (section titles are translatable) — the `SectionCascadeService` hook to purge translations comes from US3 (T138 pulls in T213's `TranslationBundleService.PurgeQuestionKeys` via a port; the port is declared in US3 and implemented in US6).
- **US7 (P2)**: depends on US1 (survey view exists) + US6 (locale bundle to inline). No unit / integration lanes.
- **US8 (P3)**: depends on US1 (surveys) + US3 (structure); ES fixture required.
- **US9 (P3)**: depends on US1 (surveys) + US8 (shares `EsQueryBuilder` + `EsClientFactory`).

### Within Each User Story

- Unit tests **must fail** at the Red Checkpoint before any implementation task runs.
- Entities before configurations before stores before services before controllers.
- Frontend api-wrappers before pages before components (or in parallel where component files don't depend on api types).
- Integration & E2E run only at the per-story checkpoint (Docker required for integration).

### Parallel Opportunities

- **Phase 1**: T002 (unit test project), T003 (integration test project), T004 (contract test project), T005 (frontend scaffold), T006 (module registration marker) all parallel after T001.
- **Phase 2**: within the baseline block (`T008–T012`), only T008 is sequential; within the cross-module ports block (`T018–T022`), all are parallel; within the middleware block (`T023–T026`), T023/T024/T025 parallel then T026 sequential; within the sanitiser block (`T027–T029`), T027/T028 parallel then T029 sequential; within the test infrastructure (`T030–T032`), all parallel; within the frontend scaffold (`T033–T038`), all parallel.
- **Phase 3 (US1)**: all 13 unit-test tasks (T039–T051) parallel; the ~40 implementation tasks split into per-file [P] blocks (T053–T062 entities+configs, T063–T066 stores, T067–T080 services, T081–T084 API, T085–T099 frontend); integration+E2E (T100–T106) parallel at checkpoint.
- **Phases 4–11**: the same pattern — all unit tests parallel, all entity/config/store/service files that don't touch the same file are parallel, all integration + E2E tasks parallel.

**With 2 backend devs + 1 frontend dev, realistic parallelism inside a story**: `abukr` and `attia` split by sub-domain per the plan.md team allocation (A vs B tags on each task); `marwan` runs the entire F lane. Within-story task-count implies ~3–5 concurrent tasks at any moment.

---

## Parallel Example: User Story 1 (MVP)

```bash
# After Phase 2 completes, launch all US1 unit tests in parallel (owner in parentheses):
T039 (A) SurveyValidatorTests
T040 (A) SurveyTypeSyncServiceTests
T041 (A) StatusTransitionPolicyTests
T042 (A) PublishGateServiceTests
T043 (A) RulesCountProjectionTests
T044 (A) SurveyLifecycleServiceTests
T045 (A) QuestionValidatorTests
T046 (A) KpiBindingValidatorTests
T047 (A) KpiBindingChangePolicyTests
T048 (A) CommentAndSentimentFlagPolicyTests
T049 (A) DestructiveReturnToDraftServiceTests
T050 (B) AppearanceServiceTests
T051 (B) HtmlSanitiserAdapterTests

# Red Checkpoint (T052) — non-parallel. Commit red baseline.

# Launch all US1 entity + config + store tasks in parallel (A dominates):
T053–T066 across two developers.

# Business services partially sequential (some depend on validators):
T067–T080.

# Controllers + DTOs (parallel):
T081–T084 (A) / T083 (B).

# Frontend (F lane, parallel):
T085 (F) api wrappers → then T086–T099 pages+components in parallel.

# At checkpoint — integration + E2E in parallel:
T100 (A) T101 (B) T102 (A) T103–T106 (F).
```

---

## Implementation Strategy

### MVP First (US1 only)

1. Complete Phase 1: Setup — day 1 (T001–T007).
2. Complete Phase 2: Foundational — days 1–3 (T008–T038). **Critical — no user story may start before Phase 2 is fully green.**
3. Complete Phase 3: US1 — days 4–~10 (T039–T106). Includes the destructive Return-to-Draft path only when M-04 has shipped `IResponsePurgeService` (T021). Until then, `POST /status {to:"Draft", fromActive:true}` returns 501 per T072.
4. **STOP + VALIDATE**: run quickstart.md scenarios 1 and 2 end-to-end. Deploy to staging if desired.

### Incremental Delivery

1. **MVP** = Setup + Foundational + US1 (T001–T106).
2. Add **US2** (approval workflow, T107–T126) → deploy → live use by P-03 authors.
3. Add **US3** (sections + sets + low-response, T127–T162) → render-plan endpoint becomes available; M-02 can start integrating.
4. Add **US4** (routing, T163–T182) → richer branching flows unlocked.
5. Add **US5** (templates, T183–T203) → tenant template library.
6. Add **US6** (translations, T204–T219) → bilingual UI complete.
7. Add **US7** (preview, T220–T226) → author confidence + demo surface.
8. Add **US8** (report, T227–T249) → per-survey analytics available.
9. Add **US9** (analytics, T250–T268) → the full analytics surface.
10. **Polish** (T269–T279) → final validation + accessibility + performance.

### Parallel Team Strategy (with the user's team of 3)

- **Setup + Foundational (Phase 1–2)**: all three developers collaborate on their assigned tracks (backend A/B split + frontend scaffold in parallel). **~3 days**.
- **Phases 3–11**: at any moment, both backend devs advance the current story's backend lane per their A/B split while `marwan` (F lane) advances the current story's frontend lane. Cross-story overlap is possible for Phase 5+ (e.g., `abukr` finishes US4 backend while `attia` starts US5 template snapshotting; `marwan` catches up to whichever story shipped its backend last). Each story is a green checkpoint before the next starts on the frontend lane. **~7–9 stories × ~3–5 days each = 6–8 weeks total for the full feature.**
- **Cross-module coordination**: T021 (M-04 `IResponsePurgeService`) and T022 (constitution AMENDMENT-012) are external blockers. Track them on day 1 of the branch; they should not block Setup + Foundational, but they DO block US1's destructive-return path from shipping to production.

---

## Notes

- `[P]` tasks = different files, no dependencies on incomplete tasks in the same phase.
- `[Story]` label maps task to a specific user story for traceability (checklist enforceable via `git grep`).
- Owner tag `(A/B/F)` = the person on the team the task is scoped to. Cross-owner tasks are marked jointly (e.g., T279 A + B + F).
- Every backend-bearing US emits Unit → Red Checkpoint → Implementation → Integration/API/Scenario → E2E in that order. US7 skips Unit + Integration (declared skips in spec.md). Every US emits E2E.
- Commit after each task or logical group; **commit the Red Checkpoint separately** so `git show <red-commit>` shows the failing baseline before any production code existed.
- Stop at any checkpoint to validate the story independently.
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence.
