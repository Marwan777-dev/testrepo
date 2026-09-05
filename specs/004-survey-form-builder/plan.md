# Implementation Plan: Survey & Form Builder (M-01)

**Branch**: `004-survey-form-builder` | **Date**: 2026-07-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from [`/specs/004-survey-form-builder/spec.md`](./spec.md)

**Module ID → Domain Name** (constitution AMENDMENT-008): **`M-01` → `Nabadat.SurveyBuilder`**. Canonical module ID `M-01` is used in prose, event names, and permission scopes; the code artefact family is `Nabadat.SurveyBuilder` (project, root namespace, tests). This mapping is stable and reproduced in the Structure Decision below.

**Team allocation** (from user input): two backend developers — **abukr** and **attia** — and one frontend developer — **marwan**. The backend and frontend lanes proceed in parallel; the story-priority order in [spec.md § User Scenarios & Testing](./spec.md#user-scenarios--testing-mandatory) is the shared cadence. See [Team Allocation & Delivery Cadence](#team-allocation--delivery-cadence) at the end of this document.

---

## Summary

M-01 is the **authoring module** of Nabadat: the surface where a P-01 / P-03 composes surveys (sections → standalone questions + Questions Sets), binds KPI questions to M-16 touchpoints, tailors appearance, translates content, previews across channels, moves surveys through the Draft → Pending review → Active lifecycle, and reads a per-survey Report + Analytics that is fed by other modules. The nine clarifications resolved during `/speckit-clarify` (Q1–Q9) pin down the persistence model (**explicit Save + optimistic ETag locking, no autosave**), the destructive **Return-to-Draft-to-edit** behaviour (Q6 — hard-delete responses + invalidate in-flight sessions on re-Publish, so no Survey `version` column is needed), the **Publish content-gate** (Q9 — ≥1 section + ≥1 question), and the security posture of the welcome/thank-you rich-text editor (Q3 — Full-HTML5-minus-unsafe sanitiser).

Technically, M-01 lands as a single `Nabadat.SurveyBuilder` module following the canonical **four-layer folder structure** (`Api`, `Application`, `Domain`, `Infrastructure` — architecture-constitution Article 1A) with **EF Core over `ITenantDbContext` + per-aggregate data-access services** (DB-08 / database-constitution Article 7). Owned tables live in the `tenant_{slug}` schema and are provisioned by the module's `_Baseline.sql`. Cross-module contracts are strictly:

- **Read** — M-06 (KPI catalogue), M-16 (journeys / stages / touchpoints), M-11 (Tenant Design Guidelines, `post_expiry_feedback_collection` tenant setting), M-10 (permissions & the "Publish own surveys" grant).
- **Emit** — `survey.published`, `survey.archived` to M-17 (constitution Section 4).
- **Consume upstream** — none synchronously; M-01 exposes a `render-plan` published-interface method that M-02 / M-04 call at dispatch time.

The frontend lives inside the existing `frontend/` React 19 + Vite SPA per CLAUDE.md's design system (shadcn / `@base-ui/react`, Tailwind 4, `nb-*` brand palette + D1–D5 semantic palette, RTL-first with logical properties). No new SPA workspace is created.

---

## Technical Context

**Language / Version**: C# 13 on **.NET 10** (backend); TypeScript 5 on **React 19** with Vite 7 (frontend). See [`frontend/package.json`](../../frontend/package.json) for the pinned SPA toolchain.

**Primary Dependencies**:

- Backend — ASP.NET Core (minimal APIs / controllers), **EF Core 10** (Npgsql provider), MediatR-free direct-injection style matching the M-10 reference, xUnit v3 + FluentAssertions 6.12.x + NSubstitute 5.x + `Microsoft.Extensions.TimeProvider.Testing` 9.x (test lanes; CLAUDE.md "Unit Test Policy" rule 14).
- Frontend — React 19, `@base-ui/react`, Tailwind CSS v4, shadcn/ui, Recharts (via `ChartContainer`), Lucide icons, React Router 7, `next-themes` for light/dark, per the frontend `CLAUDE.md`.
- HTML sanitiser — **Ganss.Xss** (`HtmlSanitizer` NuGet package) at server ingress on every welcome/thank-you save, configured with a Full-HTML5-minus-unsafe allowlist (Q3 — no `<script>`, no `on*` handlers, no `javascript:` URLs, no `<iframe>`). Allowlist config lives in `SurveyBuilderServiceCollectionExtensions` and is versioned.

**Storage**:

- PostgreSQL 16+ tenant schema `tenant_{slug}` for the operational data (Survey / Section / QuestionsSet / Question / Theme / Template / Translation / Response). No `tenant_id` columns (AD-02).
- **Elasticsearch 8+** (indices `tenant_{tenantId}_responses`, `tenant_{tenantId}_analytics`) is read-only from M-01's perspective for the Report (F13) and Analytics (F14) surfaces — computation is owned by M-05 / M-06 / M-07; M-01 renders. **No M-01 code queries PostgreSQL for report/analytics data** (AD-04).
- **Shared file storage** (Zone 2) for the survey logo uploaded via F4 Appearance — ClamAV-scanned per database-constitution Article 6, envelope-encrypted under the tenant CMK (GP-02).

**Testing**:

- Unit — `tests/Nabadat.SurveyBuilder.UnitTests/` (xUnit v3, in-memory fakes + NSubstitute; no I/O; per-task gate).
- Integration — `tests/Nabadat.SurveyBuilder.IntegrationTests/{Endpoints,Services,Scenarios,Infrastructure}/` with `SurveyBuilderApplicationFactory` (Testcontainers Postgres + module `_Baseline.sql` + `WebApplicationFactory<Program>`).
- Contract — `tests/Nabadat.SurveyBuilder.ContractTests/` for the M-01 published interface consumed by M-02 / M-04 (`ISurveyRenderService`, `IActiveSurveyReader`).
- E2E — extended in the existing `tests/Nabadat.E2ETests/` project per the CLAUDE.md **E2E Test Policy**: new folder `tests/Nabadat.E2ETests/SurveyBuilder/` with a class per US1–US9 story, running via Microsoft.Playwright.MSTest against a live Vite dev server.

**Target Platform**: Linux containers on Kubernetes (SaaS) and Docker Compose (on-prem); browser SPA supporting the latest two versions of Chromium/Edge/Firefox/Safari (constitution §2.2 operating environment; SRS §6 NFR). Arabic (four dialects) + English UI, RTL-first.

**Project Type**: Backend module (`Nabadat.SurveyBuilder`) + frontend feature workspace inside the existing `frontend/` SPA.

**Performance Goals** (from spec §Success Criteria + NFR-1):

- Library and builder open in **< 1.5 s** on standard tenant volumes (SC-002).
- Live preview and any single configuration change render within **~100 ms** of change (SC-003).
- Report period-filter switch recomputes all cards + charts within **100 ms** (US8/US9 E2E scenarios).
- `render-plan` published-interface method (F10, called per dispatch by M-02) MUST return within **50 ms p95** for surveys up to 100 questions across 20 sections and 20 Questions Sets (self-imposed for M-02 SLA safety).

**Constraints** (from constitution + spec):

- No autosave (Q1) — every write is an explicit user Save, `If-Match: <etag>` optimistic locking on mutable resources; 412 on mismatch (API Article 7.2).
- No `<script>` / `on*` / `javascript:` / `<iframe>` in stored welcome/thank-you HTML (Q3, sanitised at ingress; API Article 5.3 — errors never leak internals).
- No Survey `version` column — Return-to-Draft-to-edit hard-deletes responses (Q6, BR-1.6); atomic (`ITenantDbContext.ExecuteAsync`) purge + status change + M-04 in-flight session invalidation.
- Publish gate — 409 `publish.requires_content` when `sections_count = 0` OR total `questions_count = 0` (Q9, BR-1.7). Paused → Active is exempt.
- Cross-module data access — **published interfaces only**; no direct table reads across module boundaries (AD-01 / architecture-constitution Article 3). M-01 reads M-16 via `IJourneyReader`, M-06 via `IKpiCatalogReader`, M-11 via `ITenantSettingsReader` + `ITenantDesignGuidelinesReader`, M-10 via `IPermissionChecker` (all in `Domain/Interfaces/` of the owning module).
- **Explicit Save + ETag** — every write endpoint requires `If-Match`; the ETag is a monotonic version counter stored on the row (not the DB row-version) so the client can compute it locally after a save.

**Scale / Scope** (from spec):

- Per-tenant survey volume: unbounded at the module level (constitution has no cap); real-world enterprise tenants carry 50–500 active surveys, up to ~50 000 total (Draft + Archived).
- Per-survey structure: no hard cap in the spec; the `render-plan` performance goal assumes ≤100 questions / ≤20 sections / ≤20 Questions Sets, which covers the enterprise/gov survey shapes described by the SRS.
- Concurrent editors on the same Draft: bounded by the ETag conflict flow (Q1); a stale ETag returns 412 with a conflict payload for the UI to reload.
- Late-response cap in the F13 verbatim table: **last 100** responses via "show more" (FR-13.7).

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Core Governing Principles (GP-01 – GP-05)

- [x] **GP-01 — Single Source of Truth.** PostgreSQL `tenant_{slug}` schema is authoritative for every M-01 entity. Elasticsearch (indices `tenant_{tenantId}_responses` / `tenant_{tenantId}_analytics`) is a *read-side* projection consumed by the Report / Analytics UI; a total ES loss is a rebuild event, not data loss. M-01 never writes to ES.
- [x] **GP-02 — Customer-Controlled Encryption.** Uploaded survey logos (F4 `survey_logo`) are envelope-encrypted under the tenant CMK before persistence (file-storage layer). No high-sensitivity fields in M-01's relational tables (survey names / question text are business content, not PII); CMK revocation therefore has no effect on textual survey data but *does* render logo files unreadable, which is the expected GP-02 pass condition.
- [x] **GP-03 — Right to Erasure.** Responses collected against a survey (owned by M-04, not M-01) are subject to M-04's erasure flow. M-01 owns *no* PII directly. The **destructive Return-to-Draft-to-edit** (Q6 / BR-1.6) hard-deletes response rows in the M-04 tables via the M-04 published-interface method `IResponsePurgeService.PurgeSurveyResponsesAsync(surveyId, actor, correlationId)`; this MUST also purge post-expiry rows in the M-07 store (a single call — M-04 orchestrates the ES delete via `survey.responses.purged` event to M-05 / M-06 / M-07 consumers). No M-01 code path touches other tenants' data.
- [x] **GP-04 — Tenant / Scope Isolation.** Every M-01 endpoint runs inside a request-scoped `ITenantDbContext` bound to the caller's `tenant_{slug}` schema (AD-02); no `tenant_id` columns in M-01 tables; the schema boundary is the only isolation mechanism (AD-07 tenant context immutability enforced upstream at the gateway).
- [x] **GP-05 — Constitution Compliance Gate.** This Constitution Check is completed and passed prior to Phase 0 research.

### Architecture Decisions (AD-01 – AD-07)

- [x] **AD-01 — Modular monolith.** M-01 exposes a **published interface** `ISurveyRenderService` (in `Nabadat.SurveyBuilder.Domain.Interfaces`) with the members M-02 / M-04 need at dispatch/collection time: `Task<RenderPlan> GetRenderPlanAsync(SurveyId id, RespondentContext respondent, CancellationToken ct)` and `Task<SurveyDefinition> GetActiveSurveyDefinitionAsync(SurveyId id, CancellationToken ct)`. It publishes `survey.published` and `survey.archived` domain events via M-17. It never references another module's concrete types or reads another module's tables.
- [x] **AD-02 — Schema-per-tenant.** M-01 tables carry no `tenant_id`. The connection pool bound to `tenant_{slug}` is the isolation mechanism.
- [x] **AD-03 — No caching layer.** No Redis. Report/Analytics queries hit ES directly; the sanitiser allowlist and design-guidelines resolution use plain DI-scoped values (no in-process cache).
- [x] **AD-04 — ES for read-side analytics.** F13 and F14 read from `tenant_{tenantId}_analytics` / `tenant_{tenantId}_responses` **only** — no PostgreSQL query serves the Report or Analytics UI.
- [x] **AD-05 — Single codebase, two deployment modes.** No `ENABLE_*` flag branches inside M-01. Tenant setting `post_expiry_feedback_collection` (Q5 — live-evaluated by M-04) is a value read at runtime, not a compile-time switch.
- [x] **AD-06 — Phase 2 tables provisioned at Phase 1.** M-01 owns no Phase 2 tables; N/A.
- [x] **AD-07 — Tenant context immutable per request.** M-01 reads `ICurrentTenant` from `Application/Interfaces/` (never mutates it). Web request pipeline resolves it once at the gateway.

### Frontend Design Gate *(applies — the feature ships extensive UI)*

- [x] Repo-root `CLAUDE.md` read in full (design system + RTL + brand palette + D1–D5 + Component Sourcing + DO/DO NOT lists + brand voice + backend integration + dev workflow).
- [x] Reuse existing components from `frontend/src/components/`. Confirmed additions vs. reuse in [research.md § Frontend Component Reuse](./research.md#frontend-component-reuse) — every net-new component is justified.
- [x] Two-Palette Rule applied. Sanitiser preview iframes, welcome/thank-you editors, and Appearance customisation all use `nb-*` brand tokens + `d{n}-*` for KPI status (per-question card badges, Publish-gate error banner uses `bg-destructive` = D5). No raw hex.
- [x] Both light AND dark themes verified in every mockup; RTL and LTR both work (logical properties only: `ps-*`, `me-*`, `text-start`).

### Backend Data-Access Gate

- [x] **EF Core only.** All persistence via EF Core over `ITenantDbContext`. No `NpgsqlConnection` / `NpgsqlCommand` / `FromSql*` / `ExecuteSql*` in feature code. The only hand-written SQL is `Nabadat.SurveyBuilder/Infrastructure/Migrations/_Baseline.sql`.
- [x] **SQL baseline + one config per entity + no EF migrations.** `_Baseline.sql` creates the 9 tables (`surveys`, `sections`, `questions_sets`, `questions`, `routing_maps`, `themes`, `survey_translations`, `templates`, `template_snapshots`) per data-model.md §2.1–2.9. (Neither `question_translations` nor `template_questions` is a separate physical table — per-question translation strings are jsonb keys inside `survey_translations`, and template content lives in the `template_snapshots` jsonb blob; `/speckit-analyze` 2026-07-15 corrected an earlier draft of this list that included both as phantom tables.) Each entity gets one `IEntityTypeConfiguration<T>` in `Infrastructure/Persistence/Configurations/` with explicit `HasColumnName`.
- [x] **Contexts** — `ITenantDbContext` (in Application) exposes the M-01 `DbSet<>`s + `ExecuteAsync`. M-01 does not own any control-plane table (the `post_expiry_feedback_collection` setting is M-11's, read via M-11's published interface `ITenantSettingsReader`), so **no `ControlPlaneDbContext` in M-01**.
- [x] **Per-aggregate data-access services.** Each aggregate root (`Survey`, `Section`, `QuestionsSet`, `Question`, `Template`, `Theme`, `Translation`) is fronted by a `<Aggregate>Store` port in `Application/<SubDomain>/Interfaces/` + `Infrastructure/Persistence/<Aggregate>Store.cs` implementation. Business services (`SurveyLifecycleService`, `ApprovalWorkflowService`, `SectionCascadeService`, `QuestionValidationService`, `LowResponseOrderingService`, `RoutingConfigurationService`, `TemplateSnapshotService`, `TranslationBundleService`, `AppearanceService`) depend on ports — unit-test seam.
- [x] **Time injected** — every service takes `System.TimeProvider` via constructor DI; `FakeTimeProvider` in tests.
- [x] **Cross-database atomicity** — none required. M-01 writes only tenant tables. The destructive purge (BR-1.6) is an atomic `ExecuteAsync` block: status update + delete inside M-01, then a **single synchronous call** to the M-04 published interface (executed after the M-01 transaction commits) that hard-deletes response rows and invalidates in-flight tokens. If the M-04 call fails, the M-01 status change is compensated: revert to prior status + surface a 503. This intentionally splits the two commits per DB-08 rule 2 ("no distributed transaction; bridge via event or split saves").

### Backend Module Structure Gate

- [x] **Single library.** `src/Nabadat.SurveyBuilder/Nabadat.SurveyBuilder.csproj` with the four top-level folders `Api/`, `Application/`, `Domain/`, `Infrastructure/`. Composition root: `SurveyBuilderServiceCollectionExtensions.AddSurveyBuilderModule(IServiceCollection, IConfiguration)`.
- [x] **Dependency direction inward-only.** `Api → Application → Domain`; `Infrastructure → Application → Domain`; `Domain` references nothing; `Api` and `Infrastructure` never reference each other. Wiring lives only in `SurveyBuilderServiceCollectionExtensions`.
- [x] **Interface placement.** `Application/Interfaces/ITenantDbContext.cs` (EF port), `Application/Interfaces/ICurrentTenant.cs`, `Application/<SubDomain>/Interfaces/*.cs` (per-sub-domain service + store ports), `Domain/Interfaces/ISurveyRenderService.cs` + other **published cross-module interfaces**, `Api/Interfaces/ICurrentSessionAccessor.cs`.
- [x] **One type per file.** Enforced in review.

### Test Enforcement (CLAUDE.md Unit Test Policy + E2E Test Policy)

- [x] Every backend-bearing user story in [spec.md](./spec.md) carries a populated **Unit Test Coverage** block with literal input/output cases (US1, US2, US3, US4, US5, US6, US8, US9 verified).
- [x] Every story with HTTP / DB / event side-effects carries a populated **Integration Test Coverage** block (same list).
- [x] Multi-step / state-carrying stories carry a `scenario-test:` line (US1, US2, US3, US5); single-endpoint stories declare `scenario-test: not-needed — <reason>`.
- [x] Every page-bearing frontend story carries an **E2E Test Coverage** block with `[TestMethod]`-mapped scenarios (US1–US9).
- [x] US7 (preview) legitimately declares `unit-tests: skipped` and `integration-tests: skipped`.
- [x] `Nabadat.SurveyBuilder.UnitTests` + `.IntegrationTests` projects will be created as the **Foundational task** (first-feature-in-module carve-out per CLAUDE.md rule 12). `SurveyBuilderApplicationFactory` is authored as part of that same task.
- [x] Contract-test project `Nabadat.SurveyBuilder.ContractTests` created for the M-01 published interface (`ISurveyRenderService`, `IActiveSurveyReader`) since M-02 and M-04 depend on it.

**Gate status: PASS.** No violations require justification in *Complexity Tracking*.

---

## Project Structure

### Documentation (this feature)

```text
specs/004-survey-form-builder/
├── plan.md              # THIS FILE
├── research.md          # Phase 0 — technology & pattern decisions
├── data-model.md        # Phase 1 — entities, relationships, invariants
├── quickstart.md        # Phase 1 — validation guide
├── contracts/           # Phase 1 — REST route contracts
│   ├── surveys.md
│   ├── sections-and-sets.md
│   ├── questions.md
│   ├── templates.md
│   ├── translations.md
│   ├── report-and-analytics.md
│   ├── approval-workflow.md
│   └── published-interface.md
├── spec.md              # Already exists (source of truth for behaviour)
├── checklists/          # Already exists
└── tasks.md             # NOT created by /speckit-plan — /speckit-tasks output
```

### Source Code (repository root)

Backend module — canonical Article 1A layout, populated per the SubDomain groupings the feature needs:

```text
src/Nabadat.SurveyBuilder/
├── Nabadat.SurveyBuilder.csproj
├── SurveyBuilderServiceCollectionExtensions.cs   # composition root: AddSurveyBuilderModule(...)
├── Api/
│   ├── Controllers/
│   │   ├── SurveysController.cs                  # F1 library, F3 settings, F5 build-method, status transitions
│   │   ├── SectionsController.cs                 # F2 sections CRUD
│   │   ├── QuestionsSetsController.cs            # F2/F10 sets CRUD + settings
│   │   ├── QuestionsController.cs                # F8 questions CRUD, move, routing map
│   │   ├── SurveyRoutingController.cs            # F9 survey-level routing toggle
│   │   ├── SurveyThemesController.cs             # F4 appearance
│   │   ├── SurveyTranslationsController.cs       # F11 translate workspace
│   │   ├── SurveyReportController.cs             # F13 report
│   │   ├── SurveyAnalyticsController.cs          # F14 analytics
│   │   ├── SurveyPreviewController.cs            # F12 multi-channel preview payload
│   │   ├── SurveyLifecycleController.cs          # US2 submit / publish / return-to-draft
│   │   ├── TemplatesController.cs                # F6/F7 template picker + authoring + instantiate
│   │   └── SurveyRenderPlanController.cs         # M-02/M-04 dispatch-time endpoint (still HTTP-callable for admin diagnostics)
│   ├── Contracts/                                # request/response DTOs — one per file
│   ├── Middleware/                               # ETag/If-Match middleware for M-01 writes
│   ├── Accessors/                                # CurrentSessionAccessor bridging JWT → application layer
│   ├── Filters/                                  # PublishGateFilter (BR-1.7), EditLockFilter (BR-15.1)
│   └── Interfaces/                               # Api-layer accessor ports
├── Application/
│   ├── Interfaces/
│   │   ├── ITenantDbContext.cs                   # M-01 DbSet<>s + ExecuteAsync
│   │   └── ICurrentTenant.cs
│   ├── Surveys/                                  # aggregate root: Survey
│   │   ├── SurveyLifecycleService.cs             # Draft ↔ Pending ↔ Active ↔ Paused ↔ Archived
│   │   ├── ApprovalWorkflowService.cs            # US2 submit / publish / return-to-draft
│   │   ├── DestructiveReturnToDraftService.cs    # BR-1.6 purge + invalidate atomically
│   │   ├── PublishGateService.cs                 # BR-1.7 content-invariant check
│   │   ├── SurveyValidator.cs                    # SurveyDraft field validation
│   │   ├── SurveyTypeSyncService.cs              # BR-3.3
│   │   ├── SurveySearchService.cs                # F1 filters + English-name search
│   │   ├── CloneSurveyService.cs                 # FR-1.8 (copy-all-data, drops response history)
│   │   ├── Interfaces/                           # ISurveyStore, ISurveyLifecycleService, ...
│   │   ├── Dtos/                                 # DraftSurvey, SurveyView, StatusTransitionResult, ...
│   │   └── Exceptions/
│   ├── Sections/
│   │   ├── SectionCascadeService.cs              # FR-2.5..2.8 destructive delete + routing/translation purge
│   │   ├── SectionValidator.cs
│   │   ├── Interfaces/  Dtos/  Exceptions/
│   ├── QuestionsSets/
│   │   ├── QuestionsSetService.cs                # F10 CRUD + settings
│   │   ├── LowResponseOrderingService.cs         # FR-10.4 algorithm
│   │   ├── QuestionsSetValidator.cs
│   │   ├── Interfaces/  Dtos/  Exceptions/
│   ├── Questions/
│   │   ├── QuestionService.cs                    # F8 CRUD + move + sub-type
│   │   ├── QuestionValidator.cs                  # per-type + sub-type invariants (FR-8.8, Question Type Catalogue)
│   │   ├── KpiBindingValidator.cs                # FR-8.4, BR-8.2, BR-8.5
│   │   ├── KpiBindingChangePolicy.cs             # BR-8.5 retain-if-valid
│   │   ├── CommentFieldFlagPolicy.cs             # FR-8.9
│   │   ├── SentimentFlagPolicy.cs                # FR-8.11
│   │   ├── QuestionMoveService.cs                # FR-8.2 cross-section/set move
│   │   ├── Interfaces/  Dtos/  Exceptions/
│   ├── Routing/                                  # F9
│   │   ├── RoutingEligibilityService.cs
│   │   ├── LayoutRoutingCoupler.cs
│   │   ├── RoutingConflictDetector.cs
│   │   ├── RoutingDefaultTargeter.cs
│   │   ├── RoutingMapStore contract
│   │   ├── Interfaces/  Dtos/
│   ├── Templates/                                # F6/F7
│   │   ├── TemplateSnapshotBuilder.cs            # FR-7.4 full copy
│   │   ├── TemplateInstantiator.cs               # FR-6.3
│   │   ├── TemplateAuthorizationService.cs       # FR-7.1 built-in vs customized
│   │   ├── TemplateSearchService.cs              # FR-6.2 name + tags
│   │   ├── Interfaces/  Dtos/  Exceptions/
│   ├── Translations/                             # F11
│   │   ├── TranslationBundleService.cs
│   │   ├── TranslatableStringExtractor.cs
│   │   ├── LocaleFallbackPolicy.cs
│   │   ├── Interfaces/  Dtos/
│   ├── Appearance/                               # F4
│   │   ├── AppearanceService.cs                  # inherited vs customized
│   │   ├── Interfaces/  Dtos/
│   ├── Preview/                                  # F12
│   │   ├── PreviewPayloadBuilder.cs
│   │   ├── Interfaces/  Dtos/
│   ├── Report/                                   # F13 — reads from Elasticsearch via IReportAggregator
│   │   ├── ReportService.cs
│   │   ├── HeadlineCsatCalculator.cs
│   │   ├── PeriodResolver.cs
│   │   ├── PerQuestionViewSelector.cs
│   │   ├── ResponseWindowFilter.cs
│   │   ├── VerbatimSampler.cs
│   │   ├── Interfaces/                           # IReportAggregator → ES query port, injected concrete in Infrastructure
│   │   └── Dtos/
│   ├── Analytics/                                # F14 — reads from Elasticsearch
│   │   ├── AnalyticsService.cs
│   │   ├── FunnelCalculator.cs
│   │   ├── PeriodDeltaCalculator.cs
│   │   ├── ChannelBreakdownCalculator.cs
│   │   ├── TrendGranularityResolver.cs
│   │   ├── Interfaces/                           # IAnalyticsAggregator → ES query port
│   │   └── Dtos/
│   ├── RenderPlan/                               # Published-interface implementation for M-02/M-04
│   │   ├── SurveyRenderService.cs                # implements Domain/Interfaces/ISurveyRenderService
│   │   ├── SurveyDefinitionAssembler.cs
│   │   ├── Interfaces/
│   │   └── Dtos/
│   └── HtmlSanitisation/
│       ├── HtmlSanitiserAdapter.cs               # wraps Ganss.Xss; enforces Q3 allowlist
│       ├── SanitiserPolicyVersion.cs             # audit versioning of the allowlist
│       └── Interfaces/
├── Domain/
│   ├── Entities/
│   │   ├── Survey.cs                             # aggregate root — status, layout, active period, journey binding, etag
│   │   ├── Section.cs
│   │   ├── QuestionsSet.cs
│   │   ├── Question.cs
│   │   ├── Theme.cs
│   │   ├── Template.cs
│   │   ├── Translation.cs
│   │   └── RoutingMap.cs
│   ├── ValueObjects/
│   │   ├── SurveyStatus.cs                       # enum + transitions (Status Transition Matrix)
│   │   ├── QuestionType.cs / QuestionSubType.cs  # Question Type Catalogue
│   │   ├── ActivePeriod.cs                       # { days, hours } nullable
│   │   ├── KpiBinding.cs                         # journey/stage/touchpoint + optional touchpoint
│   │   ├── LowResponseSelection.cs
│   │   └── LayoutMode.cs
│   └── Interfaces/                               # PUBLISHED cross-module interfaces
│       ├── ISurveyRenderService.cs               # consumed by M-02 / M-04
│       ├── IActiveSurveyReader.cs                # consumed by M-04
│       └── (cross-cutting client ports M-01 depends on — NOT owned by M-01:
│                these are DEPENDENCIES, declared for wiring; concrete implementations live in the owning modules)
│           IJourneyReader.cs                     # M-16 published interface — referenced here for compile-time discovery
│           IKpiCatalogReader.cs                  # M-06 published interface
│           ITenantSettingsReader.cs              # M-11 published interface
│           ITenantDesignGuidelinesReader.cs      # M-11 published interface
│           IPermissionChecker.cs                 # M-10 published interface
│           IResponsePurgeService.cs              # M-04 published interface (BR-1.6 purge + in-flight invalidation)
│           IEventLogWriter.cs                    # M-17 published interface (survey.published, survey.archived)
│           IFileStorageService.cs                # shared file-storage — logo uploads (F4)
└── Infrastructure/
    ├── Persistence/
    │   ├── TenantDbContext.cs                    # implements ITenantDbContext for M-01 DbSets
    │   ├── Configurations/                       # one IEntityTypeConfiguration<T> per entity — explicit HasColumnName
    │   │   ├── SurveyConfiguration.cs
    │   │   ├── SectionConfiguration.cs
    │   │   ├── QuestionsSetConfiguration.cs
    │   │   ├── QuestionConfiguration.cs
    │   │   ├── ThemeConfiguration.cs
    │   │   ├── TemplateConfiguration.cs
    │   │   ├── TranslationConfiguration.cs
    │   │   ├── RoutingMapConfiguration.cs
    │   │   └── ValueConverters.cs                # ActivePeriod ↔ jsonb, RoutingMap ↔ jsonb, etc.
    │   ├── Stores/                               # per-aggregate data-access services (implement Application/*/Interfaces/*Store ports)
    │   │   ├── SurveyStore.cs
    │   │   ├── SectionStore.cs
    │   │   ├── QuestionsSetStore.cs
    │   │   ├── QuestionStore.cs
    │   │   ├── TemplateStore.cs
    │   │   ├── ThemeStore.cs
    │   │   ├── TranslationStore.cs
    │   │   └── RoutingMapStore.cs
    │   └── Repositories/
    │       └── RulesCountProjection.cs           # read-only projection of M-02 rules_count via IChannelSurveyRulesReader
    ├── Migrations/
    │   └── _Baseline.sql                         # tenant tables + indexes + partitions (no responses partition — that's M-04)
    ├── Elasticsearch/
    │   ├── ReportAggregator.cs                   # implements IReportAggregator → tenant_{tenantId}_analytics
    │   ├── AnalyticsAggregator.cs                # implements IAnalyticsAggregator
    │   ├── EsQueryBuilder.cs                     # filter clauses (period, kpi, section, question)
    │   └── EsClientFactory.cs
    ├── HtmlSanitisation/
    │   └── GannsHtmlSanitiserAdapter.cs          # concrete Ganss.Xss adapter behind Application/HtmlSanitisation/Interfaces/
    ├── FileStorage/
    │   └── LogoUploadAdapter.cs                  # wraps shared IFileStorageService (AV scan + CMK envelope encrypt)
    └── Events/
        └── EventLogWriter.cs                     # implements IEventLogWriter → M-17 event_log insert

tests/
├── Nabadat.SurveyBuilder.UnitTests/
│   ├── Nabadat.SurveyBuilder.UnitTests.csproj
│   ├── TestSupport/                              # shared fakes (InMemorySurveyStore, RecordingTenantDbContext, TestTime)
│   ├── Surveys/                                  # mirrors Application/Surveys/
│   ├── Sections/
│   ├── QuestionsSets/
│   ├── Questions/
│   ├── Routing/
│   ├── Templates/
│   ├── Translations/
│   ├── Appearance/
│   ├── Preview/
│   ├── Report/
│   ├── Analytics/
│   ├── RenderPlan/
│   └── HtmlSanitisation/
├── Nabadat.SurveyBuilder.IntegrationTests/
│   ├── Nabadat.SurveyBuilder.IntegrationTests.csproj
│   ├── Infrastructure/
│   │   ├── SurveyBuilderApplicationFactory.cs    # Testcontainers Postgres + _Baseline.sql + WebApplicationFactory
│   │   ├── SurveyBuilderTestSeed.cs              # helpers to seed drafts / active surveys / templates
│   │   └── EsTestcontainer.cs                    # Elasticsearch Testcontainer for Report/Analytics integration lane
│   ├── Endpoints/                                # one file per Controller
│   ├── Services/                                 # multi-step service-layer verifications (concurrency, ETag, ExecuteAsync)
│   └── Scenarios/                                # one file per scenario-test declared in spec.md
│       ├── SurveyLifecycleFromDraftToActiveScenarioTests.cs   # US1
│       ├── SurveyApprovalWorkflowScenarioTests.cs             # US2
│       ├── QuestionsSetLowResponseOrderingScenarioTests.cs    # US3
│       └── TemplateCreateAndInstantiateScenarioTests.cs       # US5
├── Nabadat.SurveyBuilder.ContractTests/
│   ├── Nabadat.SurveyBuilder.ContractTests.csproj
│   ├── SurveyRenderServiceContractTests.cs       # M-02 / M-04 dispatch contract
│   └── ActiveSurveyReaderContractTests.cs
└── Nabadat.E2ETests/                      # EXISTING project — extended, not recreated
    └── SurveyBuilder/                            # NEW folder
        ├── SurveyLibraryTests.cs                 # US1
        ├── SurveyBuildMethodTests.cs             # US1
        ├── SurveySettingsTests.cs                # US1
        ├── SurveyAppearanceTests.cs              # US1
        ├── SurveyBuilderTests.cs                 # US1
        ├── SurveyApprovalTests.cs                # US2
        ├── SectionsAndSetsTests.cs               # US3
        ├── RoutingTests.cs                       # US4
        ├── TemplatesTests.cs                     # US5
        ├── TranslateTests.cs                     # US6
        ├── PreviewTests.cs                       # US7
        ├── ReportTests.cs                        # US8
        └── AnalyticsTests.cs                     # US9

frontend/                                  # EXISTING SPA — extended, not recreated
├── src/
│   ├── features/
│   │   └── surveys/                              # NEW feature module
│   │       ├── api/                              # thin fetch wrappers per Controller, callJson pattern (see CLAUDE.md § Backend Integration)
│   │       │   ├── surveys-api.ts
│   │       │   ├── sections-api.ts
│   │       │   ├── questions-sets-api.ts
│   │       │   ├── questions-api.ts
│   │       │   ├── templates-api.ts
│   │       │   ├── translations-api.ts
│   │       │   ├── report-api.ts
│   │       │   ├── analytics-api.ts
│   │       │   └── etag.ts                       # If-Match header helper (Q1)
│   │       ├── pages/
│   │       │   ├── SurveyLibraryPage.tsx         # F1
│   │       │   ├── BuildMethodPage.tsx           # F5
│   │       │   ├── SurveySettingsPage.tsx        # F3
│   │       │   ├── SurveyAppearancePage.tsx      # F4
│   │       │   ├── SurveyBuilderPage.tsx         # F8
│   │       │   ├── TranslateWorkspacePage.tsx    # F11
│   │       │   ├── PreviewPage.tsx               # F12
│   │       │   ├── ReportPage.tsx                # F13 — full-page route, per CLAUDE.md "Routes vs Dialogs"
│   │       │   ├── AnalyticsPage.tsx             # F14
│   │       │   ├── TemplatePickerPage.tsx        # F6
│   │       │   └── TemplateEditorPage.tsx        # F7
│   │       ├── components/                       # cx/-tier composed components (reuse ui/ primitives)
│   │       │   ├── SurveyStatusPill.tsx
│   │       │   ├── QuestionPalette.tsx
│   │       │   ├── QuestionCard.tsx
│   │       │   ├── KpiBindingEditor.tsx
│   │       │   ├── RoutingMapEditor.tsx
│   │       │   ├── SectionColumn.tsx
│   │       │   ├── QuestionsSetCard.tsx
│   │       │   ├── AppearanceControls.tsx
│   │       │   ├── LivePreviewFrame.tsx          # Desktop | Mobile | WhatsApp | Email chrome variants
│   │       │   ├── DestructiveReturnToDraftDialog.tsx   # BR-1.6 blocking confirmation (Q6)
│   │       │   ├── PauseWithRulesDialog.tsx      # FR-1.10 blocking confirmation
│   │       │   ├── PublishGateBanner.tsx         # BR-1.7 non-modal disabled-tooltip surface
│   │       │   ├── EtagConflictDialog.tsx        # Q1 stale-etag 412 surface
│   │       │   ├── ReportMetricCard.tsx          # F13
│   │       │   ├── AnalyticsFunnel.tsx           # F14
│   │       │   ├── AnalyticsChannelBars.tsx
│   │       │   └── AnalyticsTrendChart.tsx
│   │       ├── hooks/
│   │       │   ├── useSurveyEtag.ts
│   │       │   ├── useSurveyEditLock.ts          # BR-15.1 pending-review lock + Q8 team-owned semantics
│   │       │   └── useUnsavedChangesGuard.ts     # NFR-5 (Q1)
│   │       └── i18n/
│   │           ├── ar.json                       # native فصحى authoring per NFR-2
│   │           └── en.json
│   └── routes/
│       └── surveys.tsx                           # add /surveys, /surveys/new, /surveys/:id/*, /templates, /templates/:id/edit
└── (existing files unchanged — SidebarProvider, tokens, layout)
```

**Structure Decision:** Backend module = `Nabadat.SurveyBuilder` following the reference `Nabadat.UserManagement` layout (architecture-constitution Article 1A). Owned tables per constitution Section 3 (`surveys`, `questions`, `question_bank`, `survey_versions`, `survey_templates`) are refined by the Q6 destructive-purge decision — see [data-model.md](./data-model.md) for the concrete table list (no `survey_versions` table, since Q6 removed the versioning need). The frontend feature module lives inside `frontend/src/features/surveys/`, reusing existing `ui/` and `cx/` primitives; no new SPA workspace is created. The single E2E project `tests/Nabadat.E2ETests/` is extended with a `SurveyBuilder/` folder (one class per US) per the CLAUDE.md E2E Test Policy rule 6.

---

## Team Allocation & Delivery Cadence

The user requested planning for **two backend developers (abukr, attia) + one frontend developer (marwan)**. The plan below assumes CLAUDE.md's build gate — per-task = compile + unit tests green — and the story-priority order in [spec.md § User Scenarios & Testing](./spec.md#user-scenarios--testing-mandatory).

### Lanes

- **Backend lane A (abukr)** — leads **`Surveys` + `Sections` + `QuestionsSets` + `Questions` + `Routing`** sub-domains and the `_Baseline.sql`. Owns the destructive Return-to-Draft flow (Q6) end-to-end (BR-1.6), the Publish gate (Q9 / BR-1.7), the low-response ordering algorithm (FR-10.4), and the routing eligibility + coupling rules (F9).
- **Backend lane B (attia)** — leads **`Templates` + `Translations` + `Appearance` + `Preview` + `Report` + `Analytics` + `RenderPlan` + `HtmlSanitisation`** sub-domains and the ES read adapters (`ReportAggregator`, `AnalyticsAggregator`). Owns the approval workflow (US2 / F15) since it touches template snapshots and translation propagation, and owns the Ganss.Xss sanitiser configuration (Q3).
- **Frontend lane (marwan)** — leads the entire `frontend/src/features/surveys/` tree. Consumes backend endpoints as they land; when a backend endpoint is not yet ready, uses MSW-style local mocks that match the contract in [`contracts/`](./contracts/).

### Foundational tasks (must land first, gate everything)

**F0-1 · Both backends jointly (day 1–2)** — bootstrap the module and the two test projects:
create `Nabadat.SurveyBuilder.csproj` + `Nabadat.SurveyBuilder.UnitTests.csproj` + `Nabadat.SurveyBuilder.IntegrationTests.csproj` + `Nabadat.SurveyBuilder.ContractTests.csproj`; wire `SurveyBuilderServiceCollectionExtensions.AddSurveyBuilderModule(...)` into `Nabadat.TenantAdmin`'s composition; ship `_Baseline.sql` (empty tables + partitioning where required by DB-04 — M-01 owns no partition-heavy table); author `SurveyBuilderApplicationFactory` (Testcontainers Postgres + Elasticsearch, `_Baseline.sql` applied per test class); register `ITenantDbContext` and cross-module reader ports (`IJourneyReader` / `IKpiCatalogReader` / `ITenantSettingsReader` / `ITenantDesignGuidelinesReader` / `IPermissionChecker` / `IResponsePurgeService` / `IEventLogWriter` / `IFileStorageService`) — concrete implementations come from the owning modules (already exist for M-10 / M-16 / M-06 / M-11; M-04 `IResponsePurgeService` is a **new port that M-04 must ship**, tracked as a cross-module dependency below).

**F0-2 · Frontend (marwan, day 1–2)** — bootstrap `features/surveys/`, add the routes to `AppRouter`, register sidebar entries in `app-sidebar.tsx` under an appropriate `NavGroup` per CLAUDE.md sidebar rules, seed the `en.json` / `ar.json` i18n bundles, wire the `useSurveyEtag` + `useUnsavedChangesGuard` hooks, and stand up a Storybook-style demo route so E2E can hit stub pages before backend endpoints land.

### Story priority pairing (post-foundational)

Backend lanes A and B work in parallel; frontend follows one story behind so mocks can be replaced with real endpoints as soon as they compile-green.

| US ID | Story | Backend owner | Frontend owner | Notes |
|---|---|---|---|---|
| US1 (P1) | Author / save / publish a basic survey | **abukr** (Surveys / Questions / Sections) | **marwan** | US1 is the MVP + defines the shared surface every later story reuses. Frontend can start on library + build-method + settings while abukr lands the write endpoints. |
| US2 (P1) | Approval & publishing workflow | **attia** (ApprovalWorkflowService) | **marwan** | Depends on US1's status transitions. |
| US3 (P2) | Sections + rotating Questions Sets + low-response ordering | **abukr** (QuestionsSets, LowResponseOrderingService, RenderPlan) | **marwan** | RenderPlan endpoint is the seam for M-02 / M-04. |
| US4 (P2) | Answer routing / skip logic | **abukr** (Routing) | **marwan** | Coupled with layout selection; small surface. |
| US5 (P2) | Templates (built-in library + tenant customized) | **attia** (Templates + TemplateSnapshotBuilder + TemplateInstantiator) | **marwan** | Snapshot no-link rule (Q4 / BR-7.1) is validated in the scenario test. |
| US6 (P2) | Translate workspace | **attia** (Translations) | **marwan** | Bilingual UI verified by E2E in both `ar` and `en`. |
| US7 (P2) | Multi-channel preview | **attia** (Preview) | **marwan** | Client-side render — no server logic beyond `GET /surveys/{id}` (already in US1). |
| US8 (P3) | Survey Report (ES read) | **attia** (Report + Elasticsearch/ReportAggregator) | **marwan** | Ships with a fixture-seeded ES for the integration lane. |
| US9 (P3) | Survey Analytics (ES read) | **attia** (Analytics + Elasticsearch/AnalyticsAggregator) | **marwan** | Delta calculation on prior period (FR-14.3, FR-14.5). |

### Cross-module dependencies to unblock before US1 / US2 ship

`/speckit-analyze` (2026-07-15) confirmed by repo inspection that **none** of the published interfaces below exist anywhere under `src/` yet. Only `Nabadat.CustomerJourneyManagement` (M-16), `Nabadat.KpiManagement` (M-06), `Nabadat.UserManagement` (M-10), and the `Nabadat.TenantAdmin` composition-root host currently exist as projects; **M-02, M-04, and M-09 have no module at all yet**. This table is the authoritative blocker list — resolution approach (stub-and-swap vs. wait for the owning module vs. descope) is an **open decision, deliberately not made here**; whoever picks up Foundational-phase work must decide before T020 executes.

| Port | Owning module | Module status | Gates | Impact if unresolved at Foundational-phase start |
|---|---|---|---|---|
| `IResponsePurgeService` | M-04 Response Collection | **Module does not exist** | US1 destructive Return-to-Draft (BR-1.6) | Already tracked — T021/T072; rest of US1 ships, only this path returns 501. |
| `IChannelSurveyRulesReader` | M-02 Channel Management | **Module does not exist** | US1 `RulesCountProjection` / Pause-with-rules confirmation (FR-1.10) — **MVP scenario** | Not previously called out as a blocker. Without it, `SurveyLifecycleService`'s Pause path cannot show the rule count and US1's acceptance scenario 10 cannot be verified. |
| `INotificationDispatcher` | M-09 Notifications and Alerts | **Module does not exist** | US2 `ReviewNotificationBuilder` / reviewer broadcast (FR-15.2) — **P1 story** | Not previously called out as a blocker. Without it, Submit-for-Review cannot notify P-01 reviewers and US2's Independent Test cannot be verified. |
| `ITenantSettingsReader` | M-11 Tenant Administration | **No dedicated module** (only the `Nabadat.TenantAdmin` host project exists; grep confirms this interface isn't defined there) | BR-3.1 post-expiry setting read, US1/US2 wiring | Not previously called out as a blocker. |
| `ITenantDesignGuidelinesReader` | M-11 Tenant Administration | **No dedicated module** (same as above) | US1 `AppearanceService` Inherited-mode resolution (F4) — **MVP scenario** | Not previously called out as a blocker. |
| `IJourneyReader` | M-16 Journey Management | Module exists, interface not yet exposed there | US1 KPI binding (FR-8.4) | Lower risk — same team, likely a small addition to an existing module. |
| `IKpiCatalogReader` | M-06 KPI Engine | Module exists, interface not yet exposed there | US1 KPI dropdown / catalogue read | Lower risk — same team, likely a small addition to an existing module. |
| `IPermissionChecker` | M-10 User Management | Module exists, interface not yet exposed there | US2 "Publish own surveys" grant check | Lower risk — same team, likely a small addition to an existing module. |

Coordinate with the owners of M-02, M-04, M-09, M-11 (or make the call to stub these locally per-interface) before Foundational-phase task T020 is executed; until resolved, US1's `RulesCountProjection`/`AppearanceService` and US2's `ReviewNotificationBuilder` cannot be implemented against a real backing service. See [research.md § Cross-module contracts](./research.md#cross-module-contracts) for the M-04 request/response shape (the only one currently documented in detail).

---

## Complexity Tracking

> Fill only if the Constitution Check has violations that must be justified.

**No violations.** The Constitution Check passes without exceptions. The Backend Data-Access Gate, Frontend Design Gate, and Backend Module Structure Gate all pass on the reference-module pattern (`Nabadat.UserManagement`).

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *(none)* | — | — |

---

## Post-Design Constitution Re-check

*Completed after Phase 1 artefacts (research.md, data-model.md, contracts/, quickstart.md) were drafted.*

- [x] **GP-01 — GP-05** still satisfied after data-model.md, contracts/, and quickstart.md were drafted. [data-model.md § 7](./data-model.md#7-audit-and-events) confirms every write emits an M-17 event (GP-04 audit path); [research.md § 4.5](./research.md#45-iresponsepurgeservice-m-04-new-port) shows the GP-03 right-to-erasure path via M-04's purge. No M-01 code writes ES (GP-01).
- [x] Every endpoint in [contracts/](./contracts/) declares `required_permission`, `required_scope`, `default_personas` (API-03) — verified per file.
- [x] Cursor-based pagination on every list endpoint (API-04) — `GET /surveys` and `GET /templates` explicitly use `page_size` (default 50, max 200) + `page_token`. No offset pagination anywhere.
- [x] `Idempotency-Key` on every sensitive write (APIs-constitution Article 7.1) — `POST /surveys` (create), `POST /surveys/{id}/clone`, `POST /surveys/{id}/status` when destructive (BR-1.6) or Pause-with-rules (FR-1.10), `POST /surveys/{id}/publish`, `POST /surveys/{id}/submit`, `POST /surveys/{id}/return-to-draft`, `POST /templates/{id}/instantiate`, `POST /templates`, `POST /templates/{id}/rebuild-from-survey`. Verified in [contracts/surveys.md](./contracts/surveys.md), [contracts/approval-workflow.md](./contracts/approval-workflow.md), [contracts/templates.md](./contracts/templates.md).
- [x] `ETag` + `If-Match` on every mutable resource (APIs-constitution Article 7.2 + Q1) — every write endpoint requires `If-Match: W/"<row_version>"`; read endpoints return the ETag. Scope is per aggregate root (Survey, Section, QuestionsSet, Question, Theme, Template, Translation) — see [research.md § 2](./research.md#2-etag-strategy-for-optimistic-locking-q1).
- [x] Every non-2xx response uses the API-05 envelope with `correlation_id` + `tenant_id`. Error codes are dot-namespaced per surface — enumerated in [research.md § 9](./research.md#9-idempotency-etag-scope-and-api-05-error-codes).
- [x] Frontend design system compliance — every new component listed in the plan reuses `ui/` or `cx/` primitives, applies `nb-*` + `d{n}-*` tokens, uses logical properties only, and is verified in both themes and both directions (research.md § 8 details).
- [x] Backend Module Structure Gate — canonical Article 1A layout confirmed for `Nabadat.SurveyBuilder` (four layer folders, inward-only dependency, one type per file, interface placement fixed).
- [x] Backend Data-Access Gate — EF Core only, `_Baseline.sql` owns the DDL, no EF migrations, per-aggregate stores + business services with the unit-test seam.

**Foundational blocker (recorded for `/speckit-tasks`)**: constitution AMENDMENT-012 (draft in [contracts/published-interface.md](./contracts/published-interface.md)) must be filed and ratified before US1's destructive Return-to-Draft path ships. Two changes: (a) correct M-01's owned-tables list in constitution Section 3; (b) register the new `survey.responses.purged` event in Section 4. Track as a Foundational task in `tasks.md`.

**Gate status: PASS.** Phase 2 (`/speckit-tasks`) may proceed.
