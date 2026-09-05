# Implementation Plan: M-13 Integration Hub

**Branch**: `006-integration-hub` | **Date**: 2026-07-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/006-integration-hub/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

**Team**: 2 resources — **AbuKr** (backend, solo) and **Marwan** (frontend, solo). See
[research.md §1](./research.md#1-team--sequencing-context) for the resulting strictly-sequential
backend ordering (no second backend engineer to split write-path/read-path across, unlike
M-15's 3-person team).

## Summary

M-13 Integration Hub is the inbound edge of the Nabadat platform: it lets a Tenant IT
Administrator (P-07) expose authenticated APIs for backend source systems to raise survey
requests in one of five scenarios (dispatch, redirect link, JSON render, iFrame embed, response
ingestion), and lets a CX Manager (P-01) govern the transaction data model those APIs accept —
service channels, a 23-parameter built-in catalogue plus custom parameters, per-channel
parameter contracts, and source-value → display-value mappings. Every inbound request passes an
ordered, atomic validation pipeline and is logged with full traceability (PII-masked) for 90
days. The module ships an **admin console of eight screens** plus a **headless inbound API
runtime** (no screen of its own).

**Technical approach**: a new backend module `Nabadat.IntegrationHub` (DB-08/AMENDMENT-009
reference layout) owning eight new tables, with the inbound request-validation pipeline as the
module's core `Application/Requests/` concern. Of M-13's six cross-module dependencies, this
plan found **two are real, already-shipped integrations** — a first for this repo's Phase-2
modules (M-15's plan found zero): `Nabadat.UserManagement` (M-10) already exposes a working
`POST /api/v1/authorization/scope/parameters` endpoint built specifically to receive M-13's
parameter definitions (BR-10/CMC-06), and `Nabadat.SurveyBuilder` (M-01) already publishes
`ISurveyRenderService` for retrieving survey definition JSON (SCN-03) — the source SRS
mis-labeled this dependency "M-03," a naming defect corrected in research.md §4.2 and
coordination-log.md C-04 without touching the constitution (which is already correct). The
remaining three dependencies (M-02 survey resolution/dispatch, M-04 response-ingestion hand-off,
and M-04/M-09's respective render-surface and alerting concerns) follow the now-standard
dependency-inversion stub pattern this repo already uses twice (M-15's `IKpiScoreReader`, M-01's
`IChannelSurveyRulesReader`). A companion `frontend/src/features/integration-hub/` SPA feature
ships all eight screens from **existing shadcn primitives only** — this feature needs zero new
custom-SVG components, unlike M-15.

## Technical Context

**Language/Version**: C# / .NET 10 (ASP.NET Core) — backend; TypeScript + React 19 — frontend
(constitution Section 1 stack table)

**Primary Dependencies**: EF Core (Npgsql provider, DB-08) · direct project reference to
`Nabadat.SurveyBuilder` for the real `ISurveyRenderService` (research.md §4.2) · a real outbound
HTTP call to `Nabadat.UserManagement`'s existing `POST /api/v1/authorization/scope/parameters`
(research.md §4.1) · three new M-13-owned stub ports for M-02/M-04 (research.md §4.3/4.4) ·
Vite + React 19 + Tailwind 4 + `@base-ui/react` + shadcn (repo CLAUDE.md, binding for all
`frontend/` work) · an Excel library (e.g. `ClosedXML`) for SCR-07's import/export (VR-F09)

**Storage**: PostgreSQL 16+, tenant schema `tenant_{slug}` (AD-02) — new tables `integrations`,
`credentials`, `service_channels`, `parameters`, `channel_parameter_assignments`,
`parameter_mappings`, `unmapped_value_occurrences`, `integration_request_logs` (data-model.md);
the last is **DB-04 monthly-partitioned** (high-volume, joins `responses`/`event_log`/
`audit_log` on that list). No Elasticsearch — operational CRUD + request logging, not
analytics/dashboard data (AD-04 scope).

**Testing**: xUnit v3 + FluentAssertions 6.12.\* + NSubstitute 5.\* —
`tests/Nabadat.IntegrationHub.UnitTests` · Testcontainers Postgres + `WebApplicationFactory` —
`tests/Nabadat.IntegrationHub.IntegrationTests` · MSTest + `Microsoft.Playwright.MSTest`,
appended to the existing shared `tests/Nabadat.E2ETests/IntegrationHub/` (no new E2E project,
CLAUDE.md E2E Test Policy rule 1; spec.md's own E2E Test Coverage blocks already name every test
file).

**Target Platform**: Kubernetes (SaaS) / Docker Compose (on-prem), single codebase (AD-05);
browsers per NFR-14 (last 2 evergreen Chrome/Edge/Firefox/Safari)

**Project Type**: Web application — backend module (`Nabadat.IntegrationHub`, hosted inside
`Nabadat.TenantAdmin`) + a **headless inbound API surface** (Feature 0, no admin-console screen)
+ frontend SPA feature (`frontend/src/features/integration-hub/`)

**Performance Goals**: NFR-1 (99.9% monthly API availability), NFR-2 (95% of API requests
complete within 500ms excluding downstream systems), NFR-4 (100 req/s default per-integration
rate limit, Operations-configurable without code changes)

**Constraints**: TLS 1.2+ everywhere (NFR-5); 2MB payload cap (NFR-3); the ordered, atomic
8-step validation pipeline (FR-F0-02) must short-circuit on the first failing step, never a
combined/ambiguous error; no fixed idempotency retention window (BR-18, 2026-07-27
clarification — an accepted limitation, not an engineered SLA, so no bounded-forever
deduplication index is required); last-write-wins concurrency (NFR-17, the same documented
Article-7.2 exception shape as M-15's plan); PII masking in every log view/export with zero
unmasked-access code paths in Phase 1 (NFR-9/FR-S8-03); cursor-only pagination (API-04)

**Scale/Scope**: 8 screens (SCR-01…08) + 1 headless feature (Feature 0, 5 scenarios); ~20+ REST
endpoints (console CRUD + 5 inbound scenario endpoints); 8 new tenant tables; 10 user stories,
27 cross-screen business rules (BR-01…27), 25 validation rules (13 data-type + 12 field-level +
VR-F13 added this session), 17 NFRs, 7 cross-module contracts, 2 of which are real integrations
today (M-01, M-10)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Core Governing Principles (GP-01–GP-05)

- **GP-01 (Single Source of Truth)** — PASS. PostgreSQL is authoritative for all 8 new tables;
  no Elasticsearch projection exists for this feature (no analytics/dashboard use case — AD-04),
  so there's nothing to rebuild.
- **GP-02 (Customer-Controlled Encryption)** — applies narrowly: credential secrets
  (`Credential.secret_hash`) are hashed/encrypted at rest (NFR-6, BR-16) and never retrievable
  after show-once generation — this is the platform's standard secrets-handling posture, not
  full customer-CMK envelope encryption (that regime is for high-sensitivity personal data;
  integration credentials are operational secrets, matching the precedent set for KPI/Action
  configuration metadata in prior features' Constitution Checks).
- **GP-03 (Right to Erasure)** — N/A for most of this feature's data (operational
  configuration); `IntegrationRequestLog.parameters_received` may carry customer PII (mobile,
  email, name) supplied by callers — this is masked at display/export (NFR-9/FR-S8-03) but
  **not erased at the source on an erasure request** by anything in this spec. Flagged as a
  gap to confirm during implementation: does a GP-03 erasure request need to redact historical
  `IntegrationRequestLog` rows carrying a given contact's PII? The spec is silent on this —
  tracked as a plan-time question for `/speckit-tasks` or a follow-up `/speckit-clarify`, not
  resolved here since it wasn't raised in either clarification round.
- **GP-04 (Tenant/Scope Isolation)** — PASS. Schema-per-tenant (AD-02) is the only boundary;
  the inbound scenario API resolves `{channelId}` within the authenticated tenant's schema only
  (the credential/API-key itself is tenant-scoped at generation, so cross-tenant channel-ID
  collision is architecturally impossible, not merely checked).
- **GP-05 (Constitution Compliance Gate)** — this section is the pass condition; re-checked
  post-design below.

### Frontend Design Gate *(feature ships UI under `frontend/` — gate applies)*

- [x] Repo-root `CLAUDE.md` read in full — see research.md §1/§7.
- [x] Component Sourcing Rule confirmed: all 8 screens build from existing shadcn primitives
      (`Table`, `Dialog`, `Sheet`, `Tabs`, `Badge`, `Select`, a stepper for the 3-step wizard) —
      **zero new custom-SVG components needed** (unlike M-15's gauges/sliders/timer-rings),
      confirmed by re-reading every screen's field/table spec in spec.md's Requirements section.
- [x] Two-Palette Rule: this feature has almost no D1–D5 semantic-color surface (no KPI-style
      scoring) — the closest analogue is SCR-01's error-rate badges (`< 1% D2, 1–5% D3, > 5% D4`,
      FR-S1-06) and SCR-08's HTTP-status badges (2xx D2 / 4xx D4 / 5xx D5), both legitimate D-scale
      status uses, never decorative. `nb-*`/`bg-primary` for all chrome/CTAs/active-nav. Logical
      properties only (`ps-*`, `ms-*`, `text-start`) per NFR-10/RTL-first architecture.
- [x] Both themes + both directions are an explicit NFR (NFR-10, SC-012) — verified per story at
      implementation time.

**No violation found** — spec.md's FR-S1-06/FR-S8-* status-badge specs already comply with the
D-scale-for-status-only rule verbatim.

### Backend Data-Access Gate *(feature reads/writes PostgreSQL — gate applies)*

- [x] **EF Core only** — no raw ADO.NET/Dapper/`FromSql*`/`ExecuteSql*`; all reads/writes go
      through `ITenantDbContext` (data-model.md entities map 1:1 to `IEntityTypeConfiguration<T>`
      classes).
- [x] Tables in `Infrastructure/Migrations/IntegrationHub_Baseline.sql` — **no EF migrations**.
      One config class per entity (`Integration`, `Credential`, `ServiceChannel`, `Parameter`,
      `ChannelParameterAssignment`, `ParameterMapping`, `UnmappedValueOccurrence`,
      `IntegrationRequestLog`, `EventLog`), explicit `HasColumnName` + the intra-module FKs
      (Article 4.1 — `Parameter.api_field` is an identifier reference pushed to M-10, never a
      cross-module FK).
- [x] `ITenantDbContext` in `Application/Interfaces/`, concrete `TenantDbContext` in
      `Infrastructure/Persistence/`. Multi-write atomicity (e.g. create Integration + Credential
      in one transaction; revoke-old + generate-new credential atomically, BR-16) via
      `ExecuteAsync` — no unit-of-work type; this module has no control-plane tables, so no
      cross-database transaction risk.
- [x] Per-aggregate services in `Application/<SubDomain>/` (`IntegrationService`,
      `ServiceChannelService`, `ParameterService`, `ParameterMappingService`,
      `RequestLogService`), each behind a port in the same folder's `Interfaces/`. All
      day-boundary-free logic here (unlike M-15, this module has no date-computed status —
      Integration/ServiceChannel status is a direct `active` toggle) — but the inbound pipeline's
      audit timestamps and log entries still take `TimeProvider` (DB-08 rule 7) for determinism
      in tests.

### Backend Module Structure Gate *(feature adds a new backend module — gate applies)*

- [x] Single `Nabadat.IntegrationHub` library (AMENDMENT-008 — research.md §2), four layer
      folders, no new top-level folder kind.
- [x] Inward-only dependencies: `Api → Application → Domain`,
      `Infrastructure → Application → Domain`; `Domain` (entities + the published
      `IParameterCatalogReader` + the M-13-owned inbound ports `ISurveyResolutionReader`/
      `ISurveyDispatchGateway`/`IResponseIngestionGateway`, contracts/published-interfaces.md)
      references nothing; `Api` and `Infrastructure` never reference each other; wiring in
      `IntegrationHubServiceCollectionExtensions`.
- [x] Interface placement: `ITenantDbContext` → `Application/Interfaces/`; per-sub-domain
      service ports → `Application/<SubDomain>/Interfaces/`; published/consumed cross-module
      ports → `Domain/Interfaces/`; `ICurrentTenant`-style accessors → `Api/Interfaces/`.
- [x] Sub-domain folders: `Application/Channels/` (US1), `Application/Parameters/` (US2, incl.
      the real `DataScopeContractPublisher` → M-10 call), `Application/Integrations/` (US3, US8,
      US10), `Application/Requests/` (US4 — the validation pipeline, the module's core), `
      Application/Monitoring/` (US5), `Application/Mappings/` (US6, US7), `Application/
      Permissions/` (US9) — each with its own unit-test mirror folder.

## Cross-Module Coordination Gate *(project-specific addition — see coordination-log.md)*

This feature has three genuine external dependencies that don't exist in the codebase today
(M-02, M-04's ingestion/render-surface concerns), and — unusually — **two dependencies that
already exist and can be wired for real** (M-01, M-10):

- [x] `coordination-log.md` filed, listing 6 items (C-01…C-06); C-01/C-02 (M-02/M-04) are
      PENDING and stubbed; C-04 documents the SRS's M-03→M-01 naming defect (no code impact,
      already resolved by targeting the correct module); C-03/C-05/C-06 are non-blocking
      registry/governance notes.
- [x] `ISurveyResolutionReader` / `ISurveyDispatchGateway` / `IResponseIngestionGateway`
      (Domain/Interfaces, M-13-owned) + their `Null*` default adapters make every user story
      buildable and testable standalone today, with a documented, zero-code-change swap path
      once M-02/M-04 ship.
- [x] Two dependencies are **not** stubbed because they don't need to be: `Nabadat.SurveyBuilder`
      (M-01)'s `ISurveyRenderService` and `Nabadat.UserManagement` (M-10)'s
      `POST /api/v1/authorization/scope/parameters` are real, already-shipped integrations this
      plan wires directly — a first among this repo's Phase-2 modules planned so far.

## Project Structure

### Documentation (this feature)

```text
specs/006-integration-hub/
├── plan.md                     # This file (/speckit-plan command output)
├── research.md                 # Phase 0 output
├── data-model.md                # Phase 1 output
├── quickstart.md                 # Phase 1 output
├── coordination-log.md           # Cross-module dependency tracker
├── contracts/
│   ├── api-endpoints.md          # Console API + inbound scenario API contracts
│   └── published-interfaces.md   # M-01/M-10-consumed (real) + M-02/M-04-consumed (stub) + M-14/15/16-published
└── tasks.md                      # Phase 2 output (/speckit-tasks command — NOT created here)
```

### Source Code (repository root)

```text
# Backend module — constitution AMENDMENT-009 / architecture Article 1A (M-10 reference layout)
src/Nabadat.IntegrationHub/
├── Nabadat.IntegrationHub.csproj   # project reference to Nabadat.SurveyBuilder (real M-01 call, §4.2)
├── IntegrationHubServiceCollectionExtensions.cs   # composition root: AddIntegrationHubModule(...)
├── Api/
│   ├── Controllers/
│   │   ├── ServiceChannelsController.cs, ParametersController.cs, IntegrationsController.cs
│   │   ├── ParameterMappingsController.cs, RequestLogsController.cs   # console CRUD (US1-3,5-10)
│   │   └── InboundScenarioController.cs   # the 5 SCN-01..05 endpoints (US4, Feature 0 — headless)
│   ├── Contracts/            # request/response DTOs — one type per file
│   └── Interfaces/            # ICurrentTenant-style accessors
├── Application/
│   ├── Interfaces/            # ITenantDbContext
│   ├── Channels/               # US1 — ServiceChannelService, ChannelIdSanitizer, ChannelIdUniquenessValidator,
│   │   │                       #   ChannelIdLockGuard, ParameterContractDependencyRule, ChannelNameValidator
│   │   └── Interfaces/
│   ├── Parameters/             # US2 — ParameterService, ApiFieldNameSuggester/UniquenessValidator/LockGuard,
│   │   │                       #   RangeConfigValidator, ParameterDisableImpactScanner, BuiltInParameterGuard,
│   │   │                       #   DataScopeContractPublisher (REAL call → M-10, research.md §4.1)
│   │   └── Interfaces/
│   ├── Integrations/           # US3, US8, US10 — IntegrationService, IntegrationNameValidator,
│   │   │                       #   ScenarioSelectionRule, ApiKeyGenerationService, OAuthClientGenerationService,
│   │   │                       #   CredentialRevocationService, WizardDraftDiscardPolicy, OAuthScopeEnforcer,
│   │   │                       #   IntegrationStatusToggle, ServiceChannelStatusToggle
│   │   └── Interfaces/
│   ├── Requests/                # US4 — the headless validation pipeline (module's core concern)
│   │   ├── RequestValidationPipeline.cs, ResultCodeMapper.cs, ChannelContractRequiredFieldChecker.cs
│   │   ├── ParameterTypeValidator.cs (+ 13 per-type validators), UnregisteredParameterStore.cs
│   │   ├── IdempotencyKeyResolver.cs, AllowedOriginsWhitelistStore.cs, SurveyLinkExpiryCalculator.cs
│   │   └── Interfaces/
│   ├── Monitoring/              # US5 — IntegrationHealthTileCalculator, ErrorRateColourResolver,
│   │   │                        #   IntegrationListFilter, RequestLogFilterCombinator, PiiMaskingFormatter,
│   │   │                        #   RejectedRequestDetailProjection, RequestLogService
│   │   └── Interfaces/
│   ├── Mappings/                # US6, US7 — ParameterMappingService, MappingSourceValueUniquenessValidator,
│   │   │                        #   UnmappedValueQueueService, MappingResolver, MappingEnabledParameterFilter,
│   │   │                        #   ExcelMappingExporter/ImportValidator/ImportModeApplier,
│   │   │                        #   ImportRowCountGuard, MappingsPerParameterGuard
│   │   └── Interfaces/
│   ├── Permissions/              # US9 — PermissionKeyResolver, CrossPersonaViewGuard
│   │   └── Interfaces/
│   └── Events/                   # M-17 event writes (ActionManagement/KpiManagement-style pattern)
├── Domain/
│   ├── Entities/                 # Integration, Credential, ServiceChannel, Parameter,
│   │   │                         #   ChannelParameterAssignment, ParameterMapping,
│   │   │                         #   UnmappedValueOccurrence, IntegrationRequestLog, EventLog
│   ├── ValueObjects/              # Scenario, CredentialMechanism, CredentialStatus, DataType,
│   │   │                          #   ParameterOrigin, ResultCode enums
│   └── Interfaces/                 # PUBLISHED: IParameterCatalogReader (forward, §4.7)
│                                    # CONSUMED (stub): ISurveyResolutionReader, ISurveyDispatchGateway,
│                                    #   IResponseIngestionGateway (§4.3/4.4)
└── Infrastructure/
    ├── Persistence/
    │   ├── TenantDbContext.cs
    │   └── Configurations/         # one IEntityTypeConfiguration<T> per entity
    ├── Migrations/
    │   └── IntegrationHub_Baseline.sql   # incl. integration_request_logs' DB-04 monthly partitioning
    ├── SurveyBuilderIntegration/    # module-specific adapter folder (Article 1A rule 4)
    │   └── RealSurveyRenderServiceAdapter.cs   # wraps Nabadat.SurveyBuilder.ISurveyRenderService (REAL, §4.2)
    ├── UserManagementIntegration/
    │   └── DataScopeHttpClient.cs   # calls M-10's real POST /api/v1/authorization/scope/parameters (§4.1)
    └── ChannelDispatch/             # module-specific adapter folder for the M-02/M-04 stubs
        ├── NullSurveyResolutionReader.cs, NullSurveyDispatchGateway.cs   # M-02 stubs, §4.3
        └── NullResponseIngestionGateway.cs                                # M-04 stub, §4.4

tests/
├── Nabadat.IntegrationHub.UnitTests/
│   ├── Channels/ Parameters/ Integrations/ Requests/ Monitoring/ Mappings/ Permissions/
│   └── TestSupport/
└── Nabadat.IntegrationHub.IntegrationTests/
    ├── Infrastructure/            # IntegrationHubApplicationFactory.cs (Testcontainers Postgres)
    ├── Endpoints/                 # one test class per controller (console + inbound scenario)
    ├── Services/                  # concurrency (NFR-17), transaction-atomicity checks
    └── Scenarios/                 # IntegrationOnboardingScenarioTests, InboundRequestLifecycleScenarioTests,
                                    # MappingReadTimeResolutionScenarioTests, BulkMappingReplaceScenarioTests
                                    # (per spec's Scenario Test blocks)

tests/Nabadat.E2ETests/IntegrationHub/    # appended to the existing shared E2E project
├── ServiceChannelTests.cs, ParameterCatalogueTests.cs, IntegrationWizardTests.cs
├── IntegrationMonitoringTests.cs, RequestLogsTests.cs, ParameterMappingsTests.cs
└── CrossPersonaPermissionsTests.cs

# Frontend SPA feature — mirrors frontend/src/features/{kpi-management,journeys,settings,actions}/
frontend/src/features/integration-hub/
├── components/       # ServiceChannelForm, ParameterDrawer, IntegrationWizard (3 steps),
│                      # ParameterMappingTable, RequestLogTable, credential dialogs (D-1..D-3)
├── hooks/             # useServiceChannels, useParameters, useIntegrations, useMappings, useRequestLogs
└── pages/             # AllServiceChannelsPage, ServiceChannelFormPage, AllParametersPage,
                        # AllIntegrationsPage, IntegrationWizardPage, ParameterMappingsPage, RequestLogsPage

frontend/src/App.tsx           # + routes per contracts/api-endpoints.md's route list (research.md §7)
```

**Structure Decision**: Web application (Option 2 shape), realized as one new .NET backend
module (`Nabadat.IntegrationHub`, hosted inside the existing `Nabadat.TenantAdmin` process per
AD-01's single-runtime rule) plus one new frontend feature folder inside the existing
`frontend/` SPA — no new frontend app, no new backend host, no new E2E project. This mirrors the
four already-shipped modules (`Nabadat.KpiManagement`, `Nabadat.UserManagement`,
`Nabadat.CustomerJourneyManagement`, `Nabadat.SurveyBuilder`) and their `frontend/src/features/*`
counterparts. The one structural addition beyond the established pattern is the **headless
inbound scenario API** (`InboundScenarioController`) living alongside the console controllers in
the same `Api/Controllers/` folder — it's still a normal ASP.NET controller, just one with no
corresponding frontend page (Feature 0 has explicitly `e2e-tests: skipped` for this reason).

## Complexity Tracking

> Two items require justification, both directly inherited from spec.md's own ratified
> decisions (not design choices this plan introduced).

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Last-write-wins concurrency (NFR-17) instead of Article 7.2's default `ETag`/`If-Match` optimistic concurrency | spec.md's NFR-17 explicitly ratifies "last-write-wins with full audit records; no pessimistic locking in Phase 1" — a product decision, not an implementation shortcut, and the identical shape already accepted for M-15's plan. | `ETag`/`If-Match` would reject a second concurrent editor's save outright, contradicting NFR-17's explicit no-locking mandate. |
| No fixed idempotency retention window (BR-18) instead of a bounded, engineered retention SLA | The 2026-07-27 clarification session explicitly ratified "no limitation, it can be duplicated" — a very-late retry may be processed as a new request, an accepted business risk, not a defect to engineer around. | Building a bounded idempotency-key index (mirroring the platform's `Idempotency-Key` 24h convention, APIs-constitution Article 7.1) was considered and explicitly rejected by the stakeholder in favor of simplicity — no index-expiry job, no storage-growth mitigation needed for this key. |
