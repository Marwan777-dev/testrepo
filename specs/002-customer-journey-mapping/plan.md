# Implementation Plan: Customer Journey Mapping Module (M-16)

**Branch**: `M-16-customer-journey-mapping` | **Date**: 2026-06-08 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-customer-journey-mapping/spec.md`

---

## Summary

M-16 delivers the customer journey configuration engine for Nabadat. It enables tenant users to define journeys with stages and touchpoints, bind KPIs with weight-validated scoring parameters, manage reusable personas, publish immutable journey version snapshots, configure threshold-based pain/happy detection, and expose report contract metadata to M-07. The module exposes three published interfaces (`IJourneyConfigReader`, `IReportContractReader`, `IJourneyScoreProvider`) consumed in-process by M-06 and M-07. All configuration events are published transactionally to M-17. The full frontend ships in Phase 1: Journey Builder, KPI & Scoring, Persona Management, Version History, and Detection Rules pages.

---

## Technical Context

**Language/Version**: C# 13 / .NET 10, ASP.NET Core 10 (backend); TypeScript 5, React 19, Vite 6, Tailwind CSS v4, `@base-ui/react` (frontend)

**Primary Dependencies**:
- Backend: `Npgsql.EntityFrameworkCore.PostgreSQL` (EF Core driver), `FluentValidation`, `NSubstitute` + `FluentAssertions 6.12.*` (unit tests), `Testcontainers.PostgreSql 4.*` (integration tests), `Microsoft.Playwright.MSTest` (E2E)
- Frontend: `react-router`, `i18next`, `@base-ui/react`, `lucide-react`, `sonner` (toasts), `recharts` (score trend visualization)

**Storage**:
- Tenant schema (per-tenant PostgreSQL): `journeys`, `stages`, `touchpoints`, `kpi_bindings`, `scoring_configs` (**tenant-level singleton — one row per tenant**, SRS §4.2.9 Q11), `personas`, `journey_persona_bindings`, `journey_versions`, `detection_configs`, `detection_threshold_overrides`, `report_contracts`, `kpi_type_definitions`, `journey_scores`
- No control-plane tables — all M-16 data is tenant-scoped
- `scoring_configs` holds the tenant strategic scoring parameters (`alpha`, `mot_multiplier`, `n_floor`, `flag_percentile`, `rolling_window_days`), read by M-06 via `IScoringConfigStore` and edited from the Platform Settings → Customer Journey page (feature 003). It is NOT per-journey; the redundant `tenant_scoring_config` table from feature 003's first cut is retired in favour of this one.

**Testing**:
- Unit: `dotnet test tests/Nabadat.Platform.M16.UnitTests` (xUnit v3, NSubstitute, FluentAssertions 6.12.*)
- Integration: `dotnet test tests/Nabadat.Platform.M16.IntegrationTests` (Testcontainers PostgreSQL, WebApplicationFactory)
- E2E: `dotnet test tests/Nabadat.TenantApp.E2ETests --filter "FullyQualifiedName~JourneyTests|FullyQualifiedName~PersonaTests|FullyQualifiedName~DetectionTests"` (MSTest + Playwright against `http://localhost:5173`)

**Target Platform**: Linux container (SaaS, Kubernetes) + Docker Compose (on-premises)

**Project Type**: Modular monolith module (ASP.NET Core) + SPA frontend feature set

**Performance Goals**:
- Journey CRUD operations: < 200 ms p95 (standard PostgreSQL writes)
- `IJourneyScoreProvider` call (compute + persist + event): < 500 ms p95 (M-06 delegation + `journey_scores` upsert + M-17 write)
- `IJourneyConfigReader` / `IReportContractReader` in-process calls: < 20 ms p95 (direct PostgreSQL read)
- Stage/touchpoint limit enforcement check: < 10 ms p95 (single row count query)

**Constraints**:
- No `tenant_id` column in any tenant-schema table (DB-02, AD-02)
- All M-17 event writes MUST be in the same DB transaction as the triggering action (FR-015)
- `IJourneyConfigReader` and `IReportContractReader` MUST be in-process interfaces; M-06 and M-07 MUST NOT read M-16 tables directly (AD-01)
- KPI weight sum = 100% enforced at service layer (cross-row constraint not enforceable by DB CHECK)
- Journey name uniqueness is case-insensitive per tenant, enforced via PostgreSQL functional unique index `LOWER(name)` with a partial index excluding `Archived` journeys
- `Archived` journey/persona status is terminal — any transition attempt rejected at data layer
- Max stages (20 default) and max touchpoints per stage (30 default) are per-tenant limits read from M-11 via `IM11TenantService.GetJourneyLimits()` per request

**Scale/Scope**: ~50 active journeys per tenant at launch; version snapshot blobs < 500 KB each; `journey_scores` holds one row per journey, updated on each score request

---

## Constitution Check

*GATE: Must pass before implementation begins.*

| Principle | Requirement | M-16 Design |
|-----------|-------------|-------------|
| **GP-01** — PostgreSQL is authoritative | All journey config, personas, versions, scores committed to PostgreSQL first | ✅ Every entity write targets PostgreSQL; `journey_scores` is updated in the same transaction as the M-17 `journey.score.updated` event write |
| **GP-02** — Customer-Controlled Encryption | High-sensitivity fields under CMK | ✅ Not applicable — M-16 stores journey configuration data (names, descriptions, thresholds, KPI types). No PII or high-sensitivity personal data is stored. `created_by`/`updated_by` hold M-10 user UUIDs (operational references, not PII content). |
| **GP-03** — Right to Erasure | Personal data cleared within SLA | ✅ M-16 holds no personal data. Journey text content and KPI configuration are operational tenant data, not personal data under PDPL. No erasure logic required in M-16. |
| **GP-04** — Tenant / Scope Isolation | No cross-tenant data access; denied attempts audited | ✅ All tables in `tenant_{slug}` schema (AD-02); no `tenant_id` columns; schema-level isolation is the sole boundary. Out-of-scope → 404; in-scope authorization failure → 403 + M-17 audit event. |
| **GP-05** — Constitution Compliance Gate | Plan passes Constitution Check before implementation | ✅ This check |

**Architecture Decisions verified:**

| Decision | Requirement | Status |
|----------|-------------|--------|
| AD-01 — Modular Monolith | M-16 exposes published interfaces; no concrete-type cross-module references | ✅ `IJourneyConfigReader`, `IReportContractReader`, `IJourneyScoreProvider` are M-16's published interfaces. M-16 consumes `IM10PermissionService`, `IM11TenantService`, M-06's scoring interface, and `M17EventPublisher` through their respective published interfaces only |
| AD-02 — Schema-Per-Tenant | Tenant tables have no `tenant_id` column | ✅ All 13 M-16 tables in `tenant_{slug}` schema, no `tenant_id` columns |
| AD-03 — No Caching Layer | No Redis; no in-memory analytics cache | ✅ `journey_scores` is a PostgreSQL table (durable, not a cache layer). Per-tenant limits are request-scoped only — no cross-request in-memory cache |
| AD-04 — Elasticsearch for Analytics | Journey score analytics projected to `tenant_{tenantId}_analytics` by M-06 | ✅ M-16 does not query or write Elasticsearch directly. M-06 handles ES indexing. |
| AD-05 — Two Deployment Modes | All code works in SaaS and on-prem modes | ✅ No SaaS-specific branching in M-16. KMS path not applicable (no high-sensitivity fields). |
| DB-02 — No `tenant_id` in tenant tables | Tenant schema tables must not carry `tenant_id` | ✅ All 13 M-16 tables confirmed |
| API-01 — Versioned endpoints | All endpoints at `/api/v1/` | ✅ |
| API-03 — Permission declaration | Every endpoint declares `required_permission`, `required_scope`, `default_personas` | ✅ Documented in contracts/ |
| API-04 — Cursor-based pagination | All list endpoints use cursor pagination | ✅ Journey, persona, stage, touchpoint, and version lists all cursor-paginated |
| T-01 — Multi-Language | Frontend supports Arabic (RTL) and English | ✅ Persona entities carry `name_ar`/`name_en` and `description_ar`/`description_en`; all frontend pages use `i18next`; RTL-first layout with logical CSS properties |
| T-03 — Configuration over Code | Journey KPIs, thresholds, scoring models are data, not code | ✅ All configured via API; no code release required for tenant configuration changes |
| T-05 — Tenant Isolation Without Exception | No cross-tenant data path | ✅ Tenant context resolved once at request entry (AD-07); all queries are schema-scoped |

**No constitution violations found.**

---

## Project Structure

### Documentation (this feature)

```text
specs/002-customer-journey-mapping/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── journeys-api.md          # Journey CRUD, lifecycle, versioning endpoints
│   ├── configuration-api.md     # KPI bindings, scoring config, detection config endpoints
│   ├── personas-api.md          # Persona CRUD and lifecycle endpoints
│   └── published-interfaces.md  # IJourneyConfigReader, IReportContractReader, IJourneyScoreProvider
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
└── Nabadat.Platform.M16/
    ├── Nabadat.Platform.M16.csproj
    ├── Api/
    │   ├── JourneysController.cs           # GET/POST /journeys, GET/PUT/PATCH /journeys/{id}
    │   ├── StagesController.cs             # POST /journeys/{id}/stages, PUT/DELETE /stages/{id}, reorder
    │   ├── TouchpointsController.cs        # POST /stages/{id}/touchpoints, PUT/DELETE /touchpoints/{id}
    │   ├── PersonasController.cs           # GET/POST /personas, GET/PUT/PATCH /personas/{id}
    │   ├── JourneyVersionsController.cs    # POST /journeys/{id}/publish, GET /journeys/{id}/versions
    │   └── DetectionController.cs          # PUT /journeys/{id}/detection, GET /journeys/{id}/reports
    ├── Application/
    │   ├── Journeys/
    │   │   ├── JourneyService.cs
    │   │   ├── JourneyStatusTransitionService.cs
    │   │   └── JourneyNameUniquenessValidator.cs
    │   ├── Stages/
    │   │   ├── StageService.cs
    │   │   └── StageReorderService.cs
    │   ├── Touchpoints/
    │   │   └── TouchpointService.cs
    │   ├── KpiBindings/
    │   │   ├── KpiBindingService.cs
    │   │   └── KpiWeightValidator.cs
    │   ├── Scoring/
    │   │   └── ScoringConfigService.cs
    │   ├── Personas/
    │   │   ├── PersonaService.cs
    │   │   └── PersonaStatusTransitionService.cs
    │   ├── Versioning/
    │   │   ├── JourneyVersionService.cs
    │   │   └── JourneySnapshotSerializer.cs
    │   ├── Detection/
    │   │   ├── DetectionConfigService.cs
    │   │   └── DetectionOverrideResolver.cs
    │   ├── Reports/
    │   │   └── ReportContractService.cs
    │   ├── Scores/
    │   │   └── JourneyScoreProviderService.cs
    │   ├── Limits/
    │   │   └── JourneyLimitEnforcer.cs
    │   └── Events/
    │       └── M17EventPublisher.cs
    ├── Domain/
    │   ├── Entities/
    │   │   ├── Journey.cs
    │   │   ├── Stage.cs
    │   │   ├── Touchpoint.cs
    │   │   ├── KpiBinding.cs
    │   │   ├── ScoringConfig.cs
    │   │   ├── Persona.cs
    │   │   ├── JourneyPersonaBinding.cs
    │   │   ├── JourneyVersion.cs
    │   │   ├── DetectionConfig.cs
    │   │   ├── DetectionThresholdOverride.cs
    │   │   ├── ReportContract.cs
    │   │   ├── KpiTypeDefinition.cs
    │   │   └── JourneyScore.cs
    │   ├── ValueObjects/
    │   │   ├── JourneyStatus.cs
    │   │   ├── PersonaStatus.cs
    │   │   ├── PlatformKpiType.cs
    │   │   └── ScoringDirection.cs
    │   └── Interfaces/
    │       ├── IJourneyRepository.cs
    │       ├── IStageRepository.cs
    │       ├── ITouchpointRepository.cs
    │       ├── IPersonaRepository.cs
    │       ├── IVersionRepository.cs
    │       ├── IDetectionRepository.cs
    │       ├── IReportContractRepository.cs
    │       ├── IKpiTypeRepository.cs
    │       ├── IJourneyConfigReader.cs         # Published interface → consumed by M-06
    │       ├── IReportContractReader.cs        # Published interface → consumed by M-07
    │       └── IJourneyScoreProvider.cs        # Published interface → consumed by callers
    └── Infrastructure/
        └── Persistence/
            ├── JourneyRepository.cs
            ├── StageRepository.cs
            ├── TouchpointRepository.cs
            ├── PersonaRepository.cs
            ├── VersionRepository.cs
            ├── DetectionRepository.cs
            ├── ReportContractRepository.cs
            ├── KpiTypeRepository.cs
            └── JourneyScoreRepository.cs

tests/
├── Nabadat.Platform.M16.UnitTests/
│   ├── Nabadat.Platform.M16.UnitTests.csproj
│   ├── Journeys/
│   │   ├── JourneyServiceTests.cs
│   │   ├── JourneyStatusTransitionServiceTests.cs
│   │   └── JourneyNameUniquenessValidatorTests.cs
│   ├── Stages/
│   │   └── StageServiceTests.cs
│   ├── Touchpoints/
│   │   └── TouchpointServiceTests.cs
│   ├── KpiBindings/
│   │   ├── KpiBindingServiceTests.cs
│   │   └── KpiWeightValidatorTests.cs
│   ├── Personas/
│   │   ├── PersonaServiceTests.cs
│   │   └── PersonaStatusTransitionServiceTests.cs
│   ├── Versioning/
│   │   ├── JourneyVersionServiceTests.cs
│   │   └── JourneySnapshotSerializerTests.cs
│   ├── Detection/
│   │   ├── DetectionConfigServiceTests.cs
│   │   └── DetectionOverrideResolverTests.cs
│   ├── Scores/
│   │   └── JourneyScoreProviderServiceTests.cs
│   └── Events/
│       └── M17EventPublisherTests.cs
├── Nabadat.Platform.M16.IntegrationTests/
│   ├── Nabadat.Platform.M16.IntegrationTests.csproj
│   ├── Infrastructure/
│   │   └── M16ApplicationFactory.cs
│   ├── Endpoints/
│   │   ├── JourneysEndpointTests.cs
│   │   ├── StagesEndpointTests.cs
│   │   ├── TouchpointsEndpointTests.cs
│   │   ├── PersonasEndpointTests.cs
│   │   ├── JourneyVersionsEndpointTests.cs
│   │   └── DetectionEndpointTests.cs
│   ├── Services/
│   │   ├── JourneyScoreProviderTransactionTests.cs
│   │   └── KpiWeightEnforcementTests.cs
│   └── Scenarios/
│       ├── JourneyDefinitionFlowTests.cs
│       ├── KpiAndScoringConfigurationTests.cs
│       ├── PersonaAndVersionManagementTests.cs
│       └── DetectionAndReportContractTests.cs
└── Nabadat.TenantApp.E2ETests/              # Shared project (already exists from M-10)
    ├── COVERAGE.md                          # Updated with M-16 coverage rows
    ├── JourneyBuilderTests.cs               # US-1 E2E flows
    ├── KpiScoringTests.cs                   # US-2 E2E flows
    ├── PersonaVersionTests.cs               # US-3 E2E flows
    └── DetectionRulesTests.cs               # US-4 E2E flows

frontend/src/
└── features/
    └── journeys/
        ├── pages/
        │   ├── JourneyListPage.tsx
        │   ├── JourneyBuilderPage.tsx        # /journeys/:id/builder
        │   ├── KpiScoringPage.tsx            # /journeys/:id/kpi-scoring
        │   ├── PersonaManagementPage.tsx     # /journeys/personas
        │   ├── VersionHistoryPage.tsx        # /journeys/:id/versions
        │   └── DetectionRulesPage.tsx        # /journeys/:id/detection
        ├── components/
        │   ├── JourneyStatusBadge.tsx
        │   ├── StageCard.tsx
        │   ├── TouchpointCard.tsx
        │   ├── KpiWeightEditor.tsx
        │   ├── PersonaStatusBadge.tsx
        │   ├── VersionSnapshotViewer.tsx
        │   └── DetectionThresholdEditor.tsx
        ├── hooks/
        │   └── useJourneyUpdated.ts
        └── api.ts
```

**Structure Decision**: Web application pattern with a new `src/Nabadat.Platform.M16/` C# module alongside existing `M10`, `M11`, and `M18` modules. Frontend features added under `frontend/src/features/journeys/`. E2E tests added to the existing shared `tests/Nabadat.TenantApp.E2ETests/` project.

---

## Phases

### Phase 0: Research (Complete — see research.md)

All design unknowns resolved in `research.md`:
- JourneyVersion snapshot: `jsonb` column `snapshot_payload` in `journey_versions` ✅
- Concurrent edit notification: polling `GET /api/v1/journeys/{id}/updated-at` hook ✅
- KPI weight validation: service layer + per-row DB CHECK (sum enforced in service) ✅
- Detection threshold override resolution: touchpoint > stage > journey specificity ✅
- `IJourneyScoreProvider` delegation: synchronous in-process call to M-06's published interface ✅
- `IJourneyConfigReader`: direct PostgreSQL read by M-16 implementation, returned as DTO ✅
- Journey name uniqueness: functional unique index `LOWER(name)` + partial `WHERE status != 'Archived'` ✅
- ReportContract structure: `jsonb` payload with stage/touchpoint/KPI dimensions ✅
- Per-tenant limits: read from M-11 via `IM11TenantService.GetJourneyLimits()` per request ✅

---

### Phase 1: Design & Contracts (This document + artifacts)

Outputs:
- `data-model.md` — PostgreSQL schema for all 13 M-16 tenant-schema tables
- `contracts/journeys-api.md` — Journey CRUD, lifecycle, publishing, detection, report contracts
- `contracts/configuration-api.md` — KPI bindings, scoring config
- `contracts/personas-api.md` — Persona CRUD and lifecycle
- `contracts/published-interfaces.md` — C# interface contracts for M-06/M-07 consumers
- `quickstart.md` — Runnable validation guide

---

### Phase 2: Tasks (Output of `/speckit-tasks`)

**Phase A — Journey Definition (US-1)**
Journey/stage/touchpoint CRUD, KPI binding, lifecycle state machine. Backend → frontend Journey Builder + List → E2E.

**Phase B — KPI & Scoring Configuration (US-2)**
KPI weight validator, **tenant-level** scoring config persistence (one `scoring_configs` row per tenant via `IScoringConfigStore`; tenant `GET/PUT /api/v1/tenant/scoring-config` — no journey-level scoring endpoint), `IJourneyConfigReader` (KPI bindings + structure; scoring parameters are read separately at tenant level). Backend → frontend KPI editor + tenant Settings → Customer Journey page (feature 003 hosts the scoring editor) → E2E. See `tasks.md` US-2 Amendment for the per-journey → per-tenant reshape.

**Phase C — Personas & Versioning (US-3)**
Persona CRUD + lifecycle, `JourneyVersionService`, snapshot serializer, publish action. Backend → frontend Persona + Version pages → E2E.

**Phase D — Detection & Report Contracts (US-4)**
`DetectionConfigService`, override resolver, `ReportContractService`, `IReportContractReader`. Backend → frontend Detection Rules page → E2E.

**Foundational (before Phase A)**:
- Scaffold `Nabadat.Platform.M16.csproj` + test projects + DI registration
- Database migration: all 13 tenant-schema tables
- Published interface definitions skeleton
- `M17EventPublisher` wrapper

---

## Key Design Decisions

### 1. Published Interface Boundary

M-16 exposes three published interfaces consumed in-process:
- `IJourneyConfigReader.GetJourneyConfig(journeyId)` — M-06 calls this to retrieve KPI bindings and stage/touchpoint structure. The **tenant** scoring parameters are NOT journey-scoped and are read separately via `IScoringConfigStore` (one row per tenant). M-16's implementation queries its own tables; no direct M-16 table access by M-06.
- `IScoringConfigStore.GetAsync()` / `UpdateAsync(...)` — exposes the tenant-level `scoring_configs` singleton (α, MOT multiplier, n_floor, flag percentile, rolling-window days). M-06 reads it once per computation cycle per tenant; feature 003's `ScoringConfigController` (`GET/PUT /api/v1/tenant/scoring-config`) writes through it.
- `IReportContractReader.GetReportContract(journeyId)` — M-07 calls this to retrieve report metadata. Returns `ReportContractDto`.
- `IJourneyScoreProvider.GetScoresAsync(journeyId)` — Any consumer calls this for on-demand score computation. M-16 delegates to M-06's scoring interface, persists result to `journey_scores`, publishes `journey.score.updated` to M-17 — all in one transaction.

### 2. JourneyVersion Snapshot: JSONB Self-Contained Copy

Stored as a single `jsonb` blob (`snapshot_payload`) in `journey_versions`. The blob captures the entire journey tree at publish time: all stages (sequence + metadata), touchpoints (channels, flags, KPI bindings), the **tenant `ScoringConfig` values active at publish** (copied into the snapshot so historical recomputation uses the parameters that were live for that version — SRS §4.2.9 note), and `DetectionConfig`. Written by `JourneySnapshotSerializer.Serialize(journey)`. Historical retrieval is a single row read; immune to future schema migrations.

### 3. KPI Weight Validation

KPI weight sum = 100% is enforced at the service layer (`KpiWeightValidator`) before any DB write. Each `kpi_bindings` row carries a per-row CHECK (`weight > 0 AND weight <= 100`). The save operation for a touchpoint's KPI bindings is always a full replace (delete + insert) in one transaction — no partial updates that could temporarily violate the invariant.

### 4. Concurrent Edit Notification (FR-018)

Last-write-wins at the database layer. UI detection via a polling hook (`useJourneyUpdated`): on page load the client captures `journey.updatedAt`; every 15 seconds it calls `GET /api/v1/journeys/{id}/updated-at`; if the timestamp has changed it fires a non-blocking toast. Saving proceeds regardless. No server-side locking.

### 5. Detection Threshold Override Resolution

`DetectionOverrideResolver.GetEffectiveThresholds(entityType, entityId, journeyId)` checks:
1. `detection_threshold_overrides` for `scope_type = 'touchpoint'` + `scope_id = touchpointId` → return if found.
2. `detection_threshold_overrides` for `scope_type = 'stage'` + `scope_id = parentStageId` → return if found.
3. Fall back to `detection_configs` journey-level thresholds.

### 6. Journey Name Case-Insensitive Uniqueness

Functional partial unique index: `CREATE UNIQUE INDEX idx_journeys_name_lower ON journeys (LOWER(name)) WHERE status <> 'Archived'`. Archived journeys release their name for reuse. Service layer pre-checks with a clear error before any DB write.

### 7. Per-Tenant Limits from M-11

`JourneyLimitEnforcer` calls `IM11TenantService.GetJourneyLimits()` per request. On M-11 circuit-breaker open, falls back to platform defaults (20 stages, 30 touchpoints) with a warning log — it does not block the journey operation.

### 8. ScoringConfig is Tenant-Scoped (SRS §4.2.9 / §11.7, Q11 RESOLVED)

`scoring_configs` is a **per-tenant singleton** — exactly one row per tenant schema, enforced by a unique index on a constant expression (`((true))`), with no `journey_id` and no `tenant_id`. All journeys in a tenant share the same five strategic parameters (`alpha`, `mot_multiplier`, `n_floor`, `flag_percentile`, `rolling_window_days`), keeping scoring methodology consistent and cross-journey comparable; there are no per-journey overrides. M-16 owns the table and exposes it through `IScoringConfigStore`; M-06 reads it once per computation cycle; the editing UI lives on the Platform Settings → Customer Journey page (feature 003). This supersedes the per-journey `scoring_configs` shape (`journey_id` FK + `model_type`/`stage_weight_mode`/`normalization_params`) that feature 002 originally shipped — the per-journey scoring tasks are superseded by the US-2 Amendment in `tasks.md`, and feature 003's redundant `tenant_scoring_config` table is retired in favour of this one. The per-journey scoring model/normalization fields are dropped: the scoring formulas (SRS §11) are owned and implemented by M-06 and are not tenant-tunable per journey.
