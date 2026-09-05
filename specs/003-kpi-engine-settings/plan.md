# Implementation Plan: CX Metrics & KPI Engine (M-06) + Platform Settings

**Branch**: `003-kpi-engine-settings` | **Date**: 2026-06-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-kpi-engine-settings/spec.md`

---

## Summary

M-06 ships the **CX Metrics & KPI Engine's definition surfaces** (KPI Management catalogue + KPI Configuration form with live preview, including the CXI composite variant) and the **Platform Settings page** (Organization section + Customer Journey ScoringConfig section, the latter surfacing M-16's tenant `scoring_configs` entity).

The feature spans:

- **Backend**: a new `Nabadat.KpiManagement` modular-monolith module (M-06; domain name per AMENDMENT-008 — the `M06` token never appears in any project/assembly/namespace/type name) owning `kpi_definitions`, `kpi_thresholds`, `kpi_perspectives`, `cxi_weights` **and `organization_settings`** in `tenant_{slug}` schemas; and a service that edits the tenant ScoringConfig via M-16's published interface (`IScoringConfigStore`). **`organization_settings` is owned by M-06 itself and lives in the tenant DB (`tenant_{slug}` schema) — re-homed from the never-built M-11 `Nabadat.TenantAdministration` per the 2026-06-24 decision. Because M-06 owns the table AND the editing surface (controller, validators, SVG sanitiser, save-service, event publisher), the Organization store / logo store / industry provider are M-06-INTERNAL services, not cross-module published interfaces — there is no M-06↔M-11/M-16 hop for Organization.** M-06 also exposes its own **published interface** `IKpiConfigReader` (for downstream M-01 / M-07 / M-09 consumers) and `IKpiDefinitionDeactivationConsumer` to receive M-16 deactivation events through M-17 read paths.
- **Frontend**: three new feature folders under `frontend/src/features/`: `kpi-management/` (catalogue list + KPI Configuration form, including the CXI composite variant), `settings/` (Settings landing + Organization section + Customer Journey section), with shared primitives (gauge, threshold-band editor, weights table) under `frontend/src/components/cx/kpi/`.
- **Cross-module integration**: M-16 publishes `IJourneyBindingQuery.GetKpiBindingUsageAsync(kpiId)` (touchpoint count + journey count for the FR-026 deactivation confirmation) and `IScoringConfigStore.{Get,Update}Async(tenantId)` (read/write the canonical `scoring_configs` row); these are NEW additions to the M-16 published-interface surface introduced by feature 002 and are documented in `contracts/published-interfaces.md`. **The Organization surface is NOT cross-module** — `organization_settings`, its store, the logo store, and the industry provider are all **M-06-internal** (re-homed from M-11 to M-06, 2026-06-24): M-06 owns the tenant table and is the single source of truth for the canonical industry list. When the M-11 `Nabadat.TenantAdministration` module is eventually built, tenant-provisioning would consume an M-06-published industry list (a small future addition), but for this feature nothing crosses a module boundary for Organization.
- **Audit**: every save emits a single registered event per the constitution Section 4 catalogue — `settings.changed` for KPI catalogue + KPI configuration + KPI activation + Organization edits; `journey.scoring_config.updated` (already registered in AMENDMENT-007) for ScoringConfig edits. No new event types are introduced; no constitution amendment is needed.

The score-computation engine, M-01 question→KPI binding, M-07 dashboard rendering, and M-09 alerting are explicitly out of scope (per spec); per-perspective score storage is also out of scope (Clarifications session 2026-06-21).

---

## Technical Context

**Language/Version**: C# 13 / .NET 10, ASP.NET Core 10 (backend); TypeScript 5, React 19, Vite 6, Tailwind CSS v4, `@base-ui/react` (frontend).

**Primary Dependencies**:

- **Backend**: `Npgsql.EntityFrameworkCore.PostgreSQL` (EF Core driver), `FluentValidation`, `Microsoft.Extensions.TimeProvider.Testing` (test-time injection), and a vetted SVG sanitiser library — **`Ganss.Xss.HtmlSanitizer`** configured for SVG (whitelist-based; strips `<script>`, `<foreignObject>`, `<iframe>`, `<use>` with external `href`, every `on*` event-handler attribute, per FR-050).
- **Backend tests**: `xunit.v3` 1.*, `xunit.runner.visualstudio` 3.*, `FluentAssertions` **6.12.*** (pinned MIT), `NSubstitute` 5.*, `Testcontainers.PostgreSql` 4.*, `Microsoft.AspNetCore.Mvc.Testing` 10.*.
- **Frontend**: `react-router` (already in repo), `i18next` + `react-i18next` (already in repo), `@base-ui/react` (already in repo), `lucide-react`, `sonner` (toast notifications for "Logo sanitised" + "CXI member auto-removed" non-blocking notices per spec Edge Cases), and a custom universal-gauge SVG component (not Recharts — per CLAUDE.md "When to use Recharts vs Custom SVG", gauges with zone coloring + needle dots + target markers MUST be custom SVG).
- **E2E**: `Microsoft.Playwright.MSTest` (scaffolds a new `tests/Nabadat.Portal.E2ETests/` project — first feature in this workspace; project is reused by future portal features).

**Storage**:

- Tenant schema (PostgreSQL per-tenant, `nabadat_tenant`): **five M-06-owned tables — `kpi_definitions`, `kpi_thresholds`, `kpi_perspectives`, `cxi_weights`, and `organization_settings`** (the last re-homed from M-11 to M-06, tenant DB, this feature); **the M-16-owned `scoring_configs` table** (the tenant-level ScoringConfig singleton — owned and provisioned by feature 002; this feature surfaces the editor, it does NOT create a new table). All live in the tenant DB; none in the control-plane DB. ⚠️ **Retired**: the separate `tenant_scoring_config` table introduced by this feature's first US-4 cut is dropped — `scoring_configs` (now a per-tenant singleton, feature 002 data-model §3.1) is the single canonical store consumed by both M-16 and M-06.
- No control-plane tables — all data is tenant-scoped.
- Logo blobs: per-tenant object storage region (T-04) under a documented prefix (`tenants/{tenantId}/branding/logo.{ext}`); SVG payloads stored as the **sanitised** byte stream (FR-050), never the upload bytes; PNG / JPEG stored unmodified. No CMK envelope encryption (per spec Constitution Check GP-02).

**Testing**:

- Unit: `dotnet test tests/Nabadat.KpiManagement.UnitTests` (xUnit v3, NSubstitute, FluentAssertions 6.12.*).
- Integration: `dotnet test tests/Nabadat.KpiManagement.IntegrationTests` (Testcontainers PostgreSQL, `WebApplicationFactory<Program>` via a `KpiManagementApplicationFactory` fixture; reuses migration runner from feature 002).
- E2E: `dotnet test tests/Nabadat.Portal.E2ETests` (first project in workspace — includes its own `E2ETestBase` with `SignInAsync` for portal `localStorage`-token + MFA flow per CLAUDE.md E2E Test Policy). Drives a running `frontend/` Vite dev server at `E2E_BASE_URL=http://localhost:5173` (the portal SPA in this repo).

**Target Platform**: Linux container (SaaS, Kubernetes) + Docker Compose (on-premises).

**Project Type**: Modular monolith module (ASP.NET Core) + SPA frontend feature set (Vite + React 19).

**Performance Goals**:

- KPI catalogue list (`GET /api/v1/kpis`) — < 200 ms p95 for a tenant with ≤ 50 custom KPIs (PostgreSQL read + ordering composition).
- KPI Configuration page load (`GET /api/v1/kpis/{id}`) — < 250 ms p95 (KPIDefinition + Thresholds + Perspectives + CXIWeights single round-trip via JOIN).
- KPI save (`POST` / `PUT`) — < 300 ms p95 (validate + transactional write of 4 tables + M-17 event publish in one transaction).
- KPI deactivation cascade (`PATCH /api/v1/kpis/{id}/activation` with `confirm=true`) — < 400 ms p95 even when the deactivated KPI is a CXI member (KPI update + CXI weights recompute + M-17 event with nested `cxi_side_effect` payload).
- Live preview re-render — < 100 ms from the React state update that triggers it (per FR-039 / FR-068). The gauge is a pure-SVG render with no network calls; threshold band boundaries reposition via inline `style="--x: …; --y: …"` and a memoized `useMemo` over the segment paths.
- ScoringConfig `GET` / `PUT` — < 150 ms p95 (single row read/write + one event).

**Constraints**:

- No `tenant_id` column in any tenant-schema table (DB-02, AD-02).
- All M-17 event writes MUST be in the **same DB transaction** as the triggering action (constitution Section 4 + AMENDMENT-007 + GP-01).
- M-06 calls M-16 **only through M-16's published interface** (AD-01) — never queries `kpi_bindings`, `touchpoints`, or `scoring_configs` directly.
- M-06's `IKpiConfigReader` must return a stable DTO shape; M-01 / M-07 / M-09 consume DTOs only.
- Short Name uniqueness is case-insensitive per tenant, enforced via a PostgreSQL **functional unique index** `UNIQUE (LOWER(short_name))` on `kpi_definitions`.
- Per-row CHECK constraints: `kpi_thresholds.lower_bound < kpi_thresholds.x`, `kpi_thresholds.x < kpi_thresholds.y`, `kpi_thresholds.y < kpi_thresholds.upper_bound`; `cxi_weights.weight > 0`; `kpi_definitions.kpi_type IN ('Standard', 'Custom')`; `kpi_definitions.calculation_method IN ('WeightedAverage', 'TopNBox', 'NPSStandard', 'WeightedComposite')`.
- Eight seeded standard KPIs are written by the M-06 baseline migration (`KpiManagement_Baseline.sql`) during tenant provisioning — NOT during the M-06 module start-up. Seeding lives in the migration runner, not the application.
- SVG logo upload is gated behind a server-side sanitiser; if the sanitiser cannot make the payload safe (parse failure, content the sanitiser must reject rather than strip), the upload is rejected with `LOGO_SVG_UNSAFE_CONTENT` (FR-050).
- α slider precision: 3 decimal places (FR-054). β is **never stored** — always derived as `1 − α` at read time.
- CXI cannot include itself (FR-045) — enforced at the API layer AND with a CHECK constraint on `cxi_weights`: `member_kpi_id != cxi_kpi_id`.
- KPI deletion is unsupported (FR-002) — there is no `DELETE` route, and no soft-delete column; deactivation (FR-026) is the only lifecycle off-switch.

**Scale/Scope**: A tenant carries 8 seeded standard KPIs + up to ~50 custom KPIs at launch; 0..10 perspectives per KPI; ≤ 7 CXI members (today's catalogue size); 1 ScoringConfig row per tenant; 1 OrganizationSettings row per tenant.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-evaluated after Phase 1 design (Constitution Check #2 at the bottom of this file).*

### Frontend Design Gate

This feature ships UI under `frontend/`. The agent has read the repo-root `CLAUDE.md`'s frontend design system before authoring this plan and confirms each of the following:

- [x] Read the repo-root `CLAUDE.md` end to end (design system, RTL rules, brand palette and D1–D5 scale, Component Sourcing Rule, DO / DO NOT lists, brand voice).
- [x] Reuse existing components from `frontend/src/components/` (`ui/` primitives, `cx/` feature components) per the Component Sourcing Rule — never recreate what exists. The Phase 1 design surveys `frontend/src/components/` for `KpiFlipCard`, gauge primitives, `BadgeOutline`, and form scaffolding before deciding what is genuinely new.
- [x] Brand palette (`nb-*`) is for chrome only; `D1`–`D5` is for KPI status only (Two-Palette Rule). Only logical direction utilities (`ps-*`, `ms-*`, `text-start`). Confirmed in the gauge zone-coloring spec (D1 → D5 across the three threshold bands) and the action-button hierarchy (one filled primary per page on KPI Configuration + Settings pages, secondary via `variant="secondary"`).
- [x] Both light AND dark themes verified; both RTL AND LTR verified. E2E `[TestMethod]` matrix exercises the page in each combination via Playwright's `page.emulateMedia({ colorScheme })` + `page.context().addInitScript(() => document.documentElement.dir = 'rtl')`.

### Core Governing Principles (GP-01 – GP-05)

| Principle | Requirement | M-06 + Settings Design |
|-----------|-------------|------------------------|
| **GP-01** — PostgreSQL is authoritative | All KPI definitions, thresholds, perspectives, CXI weights, ScoringConfig, Organization settings committed to PostgreSQL first | ✅ Every entity write targets PostgreSQL; M-17 event row is written in the **same** transaction as the entity write. Elasticsearch is not touched by this feature (no read-side analytics — FR-068's "page loads within 1.5s" is a PostgreSQL-only read budget). |
| **GP-02** — Customer-Controlled Encryption | High-sensitivity fields under CMK | ✅ Not applicable — KPI configuration, ScoringConfig, and Organization-name/industry are configuration metadata, not high-sensitivity personal data. Logo blobs are operational branding assets; standard storage-layer encryption applies, no envelope. (Confirmed by spec Constitution Check and Assumptions.) |
| **GP-03** — Right to Erasure | Personal data cleared within SLA | ✅ Not applicable — this feature stores no per-subject personal data. Audit `actor_id` references M-10 user UUIDs (operational refs, not PII content). |
| **GP-04** — Tenant / Scope Isolation | No cross-tenant data access; denied attempts audited | ✅ All M-06 + Organization + ScoringConfig tables in `tenant_{slug}` schema (AD-02). Spec SC-005 ("Tenant isolation: a randomised cross-tenant probe returns 404/403 in 100% of attempts and writes 100% of denied attempts to `audit_log`") is verified by `KpiCrossTenantIsolationScenarioTests` (Tenant A queries Tenant B's KPI id → 404 + `audit_log` entry). |
| **GP-05** — Constitution Compliance Gate | Plan passes Constitution Check before implementation | ✅ This check. Re-evaluated at end of plan (Constitution Check #2). |

### Architecture Decisions

| Decision | Requirement | Status |
|----------|-------------|--------|
| **AD-01** — Modular Monolith | M-06 exposes published interfaces; no concrete-type cross-module references | ✅ M-06 publishes `IKpiConfigReader` (consumed by M-01, M-07, M-09). M-06 consumes M-16's `IJourneyBindingQuery` + `IScoringConfigStore`, M-10's `IPermissionService`, and M-17's `IEventPublisher` only through their published interfaces. The Organization store / logo store / industry provider are **M-06-internal** (M-06 owns the `organization_settings` table + the editing surface, re-homed from the never-built M-11) — no cross-module hop. |
| **AD-02** — Schema-Per-Tenant | Tenant tables have no `tenant_id` column | ✅ All five M-06 tables in `tenant_{slug}` (incl. `organization_settings`); `scoring_config` (M-16) likewise — all in the tenant DB. No `tenant_id` columns anywhere. |
| **AD-03** — No Caching Layer | No Redis; no in-memory analytics cache | ✅ Per-request only. The KPI catalogue list is queried fresh on each `GET /api/v1/kpis`. The "[X] Active KPIs" subtitle (FR-008) updates from local React state on same-session mutations only — no polling, no SSE, no in-memory cache server-side. |
| **AD-04** — Elasticsearch for Read-Side Analytics | M-06 owns analytics index writes | ✅ Not applicable to THIS feature. Analytics ingestion ships with the M-06 score-computation engine release (out of scope here per spec Section 1.2 + Clarifications session 2026-06-21). Configuration metadata (KPI definitions etc.) lives only in PostgreSQL. |
| **AD-05** — Two Deployment Modes | All code works in SaaS and on-prem modes | ✅ No `ENABLE_MULTI_TENANT` / `ENABLE_BILLING` / `ENABLE_TENANT_MGMT` branches in feature code. KPI Configuration + Settings are available in both modes. Logo upload backend is provider-agnostic (S3-compatible API in SaaS; file system in on-prem — both behind `ILogoStore`). |
| **AD-06** — Phase 2 Tables Provisioned Empty | No M-06 dependency on Phase 2 modules | ✅ Not applicable — M-06 does not touch any Phase 2 tables. |
| **AD-07** — Tenant Context Immutable Per Request | Tenant resolved once at the gateway | ✅ All endpoints read tenant context from the request-scoped `ITenantContext`; no endpoint mutates it. |

### Database Spec Rules

| Rule | Requirement | Status |
|------|-------------|--------|
| **DB-01** — Schema naming | `tenant_{slug}` | ✅ |
| **DB-02** — No `tenant_id` on tenant tables | Forbidden on per-tenant tables | ✅ All four new M-06 tables + `organization_settings` confirmed; `scoring_configs` is unchanged (already feature-002-compliant) |
| **DB-03** — Primary keys | UUID or integer; no composite tenant keys | ✅ All new tables use `uuid` PK |
| **DB-04** — Date partitioning | High-volume tables MUST partition by date | ✅ Not applicable — `kpi_definitions`/`kpi_thresholds`/`kpi_perspectives`/`cxi_weights`/`organization_settings` are low-volume configuration tables (a tenant has ≤ ~60 rows total across all four). |
| **DB-05** — Migration atomicity | All-tenants atomic; rollback all on any tenant failure | ✅ Single `KpiManagement_Baseline.sql` migration creates all four M-06 tables + seeds the eight standard KPIs in one transaction; rollback file `KpiManagement_Baseline_Rollback.sql` drops them in reverse FK order. Idempotent re-run is safe via `CREATE TABLE IF NOT EXISTS` + `ON CONFLICT DO NOTHING` on the seed `INSERT`. |
| **DB-08** — Data-Access Implementation (EF Core) (AMENDMENT-007) | EF Core only; per-aggregate service over `ITenantDbContext`; `ExecuteAsync` the sole tx boundary; SQL-baseline-owned schema (no EF migrations); no raw ADO.NET / `FromSql*` / `ExecuteSql*` in feature code | ✅ Each table is fronted by exactly ONE `<Aggregate>Service` (`KpiDefinitionService`, `KpiThresholdService`, `KpiPerspectiveService`, `CxiWeightService`) in `Application/<SubDomain>/` holding CRUD + business logic — no separate `*DataService` (DB-08 / the M-10 reference name it `<Aggregate>Service`). Port `I<Aggregate>Service` in `Application/<SubDomain>/Interfaces/`; context port `ITenantDbContext` in `Application/Interfaces/`. `KpiSaveService` composes the entity services' multi-table write inside `ITenantDbContext.ExecuteAsync`; no unit-of-work type. EF maps the SQL-baseline schema via one `IEntityTypeConfiguration<T>` per entity (explicit `HasColumnName`) in `Infrastructure/Persistence/Configurations/`. No repositories, no EF migrations. Reference: M-10 `Nabadat.UserManagement`. |

### Backend Module Gates (AMENDMENT-007 / AMENDMENT-008 / AMENDMENT-009)

| Gate | Requirement | Status |
|------|-------------|--------|
| **Backend Project Naming Gate** (AMENDMENT-008) | Projects/assemblies/namespaces/types use `Nabadat.<DomainName>`; the `M{NN}` token never appears in a code-artifact name | ✅ M-06 → `Nabadat.KpiManagement`, M-11 → `Nabadat.TenantAdministration`, M-16 → `Nabadat.CustomerJourneyManagement`; tests `Nabadat.KpiManagement.UnitTests` / `.IntegrationTests`; fixture `KpiManagementApplicationFactory`. Hyphenated IDs (`M-06`/`M-11`/`M-16`) retained only as identifiers in prose/registry/events/API-03. Mapping recorded in the Structure Decision (AMENDMENT-008 §2). |
| **Backend Module Structure Gate** (AMENDMENT-009) | Four canonical layers `Api/`/`Application/`/`Domain/`/`Infrastructure/`, inward-only deps; feature adds into existing layer folders, invents no new top-level folder kind | ✅ M-06 follows the M-10 `Nabadat.UserManagement` reference: `Application/Interfaces/` (context ports), `Application/<SubDomain>/` (one service per entity) + `Application/<SubDomain>/Interfaces/` (service ports), `Domain/Interfaces/` (published interfaces only), `Infrastructure/Persistence/Configurations/`. M-11/M-16 additions land in their existing layer folders. |
| **Backend Data-Access Gate** (AMENDMENT-007 / DB-08) | EF Core; ONE per-aggregate service over `ITenantDbContext`; `ExecuteAsync` sole tx boundary; SQL-baseline schema, no EF migrations; no repositories / raw SQL in feature code | ✅ See DB-08 row above. `<Aggregate>Service` (CRUD + business, in `Application/<SubDomain>/`) + `I<Aggregate>Service` (`Application/<SubDomain>/Interfaces/`) + `ITenantDbContext` (`Application/Interfaces/`) + `IEntityTypeConfiguration<T>` (`Infrastructure/Persistence/Configurations/`). No `*DataService`, no `I<X>Repository`. |

### API Spec Rules

| Rule | Requirement | Status |
|------|-------------|--------|
| **API-01** — Versioning | `/api/v1/...` | ✅ All endpoints versioned |
| **API-02** — Tenant resolution | From JWT claim or subdomain | ✅ Resolved by the existing tenant-context middleware |
| **API-03** — Permission declaration | Every endpoint declares `required_permission` + `required_scope` + `default_personas` | ✅ Encoded in `contracts/kpi-api.md` and `contracts/settings-api.md`; permission attributes carried on controller actions |
| **API-04** — Cursor-based pagination | All list endpoints cursor-paginated | ✅ `GET /api/v1/kpis` cursor-paginated (`cursor` + `limit` query params); for a tenant with ≤ 50 custom KPIs the cursor is almost always empty on the first page, but the contract still complies. |
| **API-05** — Error response envelope | `{error: {code, message, correlation_id, tenant_id}}` | ✅ All error paths return the API-05 envelope; user-facing `message` is bilingual EN+AR via the per-tenant i18n resolver (T-01) |
| **API-06** — Authentication headers | `Authorization: Bearer <token>` etc. | ✅ All endpoints behind JWT bearer auth |

### Event Catalogue (Section 4)

This feature emits **only** registered events:

- `settings.changed` — KPI catalogue + KPI configuration + KPI activation **and Organization edits, all emitted M-06-side** (M-06 owns all five tables incl. `organization_settings`; the org event uses M-06's existing `KpiEventPublisher.PublishOrganizationSettingsChangedAsync`). Registered to M-11 in the constitution catalogue; the emit site is M-06 since M-06 owns the underlying tables.
- `journey.scoring_config.updated` (M-16, registered in AMENDMENT-007) — ScoringConfig edits.

**No new event types are needed.** No constitution amendment is required.

### Persona Registry (Section 8)

This feature uses **only** P-01 (CX Program Manager), P-02 (CX Analyst), P-06 (Executive Sponsor), and P-07 (Tenant IT Administrator) — all already registered. No new personas.

### Platform Tenets

| Tenet | Applies | Status |
|-------|---------|--------|
| **T-01** — Multi-Language by Design | Yes — feature has UI | ✅ KPI Configuration + Settings pages render in both EN and AR with RTL parity; FR-066 + SC-008 cover the requirement; gauge labels and emoji ordering follow the reading direction. |
| **T-02** — Channel Agnostic | No — no survey-channel code in this feature | ✅ |
| **T-03** — Configuration over Code | Yes — KPI definitions are config | ✅ KPI definitions, thresholds, targets, and CXI weights are tenant-data, not code; ScoringConfig is tenant-data; no code release required to change tenant KPIs. |
| **T-04** — Data Residency by Architecture | Yes — logo blobs stored per-tenant | ✅ `ILogoStore.PutAsync` resolves the storage region from the tenant's provisioning-time `DEPLOYMENT_REGION` flag; no runtime jurisdiction routing logic. |
| **T-05** — Tenant Isolation Without Exception | Yes — every endpoint | ✅ Schema-per-tenant enforced; SC-005 verified by integration tests |
| **T-06** — AI Assists, Humans Decide | Not applicable — no AI outputs in this feature | ✅ |
| **T-07** — Industry Flexibility Without Custom Code | Yes — Industry enum drives downstream templates | ✅ Industry choice is data on `organization_settings.industry`; downstream template rendering (out of scope here) reads it as config. |
| **T-08** — Forward-Compatible Foundation | Yes — stable IDs | ✅ KPI `id` is a UUID (stable across M-16 bindings); perspective `id` is UUID (stable for the deferred per-perspective scoring release). |

**No constitution violations found. Proceeding to Phase 0.**

---

## Project Structure

### Documentation (this feature)

```text
specs/003-kpi-engine-settings/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── kpi-api.md                # KPI catalogue, configuration, activation, CXI weights endpoints
│   ├── settings-api.md           # Organization + ScoringConfig endpoints
│   └── published-interfaces.md   # IKpiConfigReader (M-06 publishes); M-16 IJourneyBindingQuery + IScoringConfigStore deltas. (Organization store/logo/industry are M-06-INTERNAL — re-homed from M-11 to M-06 — so NOT in the published-interface surface.)
├── checklists/
│   └── requirements.md           # Spec quality checklist (already exists; carried forward)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── Nabadat.KpiManagement/                       # NEW — this feature
│   ├── Nabadat.KpiManagement.csproj
│   ├── Api/
│   │   ├── KpisController.cs                   # GET/POST/PUT /kpis, GET/PUT /kpis/{id}, PATCH /kpis/{id}/activation, GET /kpis/{id}/binding-usage, PUT /kpis/{cxi_id}/weights
│   │   ├── ScoringConfigController.cs          # GET/PUT /tenant/scoring-config (delegates to M-16's IScoringConfigStore)
│   │   └── OrganizationController.cs           # GET/PUT /tenant/organization, POST /tenant/organization/logo (delegates to M-06-internal IOrganizationSettingsStore + ILogoStore)
│   ├── Application/
│   │   ├── Interfaces/                          # DB-08: context ports live in Application/Interfaces
│   │   │   └── ITenantDbContext.cs             # Exposes DbSet<>s + SaveChangesAsync + ExecuteAsync (the only multi-write tx boundary; NO unit-of-work type)
│   │   ├── Kpis/
│   │   │   ├── Interfaces/                      # DB-08 unit-test seam: per-entity service ports (M-10 reference)
│   │   │   │   ├── IKpiDefinitionService.cs
│   │   │   │   └── IKpiThresholdService.cs
│   │   │   ├── KpiDefinitionService.cs         # ONE service per entity — CRUD + business logic over ITenantDbContext (NOT a repository, NO separate *DataService)
│   │   │   ├── KpiThresholdService.cs          # ONE service per entity — CRUD + threshold business rules over ITenantDbContext
│   │   │   ├── KpiDefinitionValidator.cs
│   │   │   ├── KpiThresholdValidator.cs
│   │   │   ├── KpiNormalisationCalculator.cs   # NPS passthrough, CES inversion, FCR binary, scale-based normalisation
│   │   │   ├── TopNBoxWarningRule.cs
│   │   │   ├── KpiSaveService.cs               # Cross-aggregate orchestrator — composes the entity services' writes in ITenantDbContext.ExecuteAsync + M-17 event publish
│   │   │   ├── KpiActivationCommandHandler.cs  # Confirm flow + cascade side-effects (incl. cxi_side_effect payload)
│   │   │   ├── KpiCatalogueQuery.cs            # Filter + search + canonical ordering
│   │   │   └── KpiBindingUsageProbe.cs         # M-16 published-interface adapter
│   │   ├── Cxi/
│   │   │   ├── Interfaces/
│   │   │   │   └── ICxiWeightService.cs
│   │   │   ├── CxiWeightService.cs             # ONE service per entity — CRUD + CXI weight business logic over ITenantDbContext
│   │   │   ├── CxiWeightNormaliser.cs          # Relative integers → effective %
│   │   │   ├── CxiActivationRule.cs            # ≥ 2 non-zero weights to activate
│   │   │   ├── CxiMemberMembershipRule.cs      # Auto-remove on deactivation; forbid self
│   │   │   └── CxiSnapshotComposer.cs          # Composes member_breakdown payload for IKpiConfigReader
│   │   ├── Perspectives/
│   │   │   ├── Interfaces/
│   │   │   │   └── IKpiPerspectiveService.cs
│   │   │   └── KpiPerspectiveService.cs        # ONE service per entity — CRUD + business logic for KPIPerspective definitions (per FR-019) over ITenantDbContext
│   │   ├── ScoringConfig/
│   │   │   ├── ScoringConfigValidator.cs       # Per-field rules
│   │   │   ├── AlphaBetaDeriver.cs             # β = 1 − α
│   │   │   └── ScoringConfigUpdateService.cs   # Atomic persist via M-16 IScoringConfigStore + audit emission
│   │   ├── Organization/                       # US-6 — ALL M-06-internal (table + surface owned by M-06; no cross-module interface)
│   │   │   ├── Interfaces/                      # M-06-internal ports (unit-test seams), NOT published cross-module
│   │   │   │   ├── IOrganizationSettingsStore.cs
│   │   │   │   ├── ILogoStore.cs
│   │   │   │   └── IIndustryEnumProvider.cs
│   │   │   ├── OrganizationSettingsValidator.cs
│   │   │   ├── LogoUploadValidator.cs          # Content-type + size checks
│   │   │   ├── SvgSanitiser.cs                 # Wraps Ganss.Xss + custom SVG ruleset (strips script/foreignObject/iframe/use[href]/on*)
│   │   │   ├── OrganizationSettingsStore.cs    # Implements IOrganizationSettingsStore over M-06's ITenantDbContext (tenant DB); writes the row + emits `settings.changed` (entity `organization`) via KpiEventPublisher in one tx
│   │   │   ├── LogoStore.cs                    # Implements ILogoStore (object-storage abstraction, region-routed per T-04; bytes in blob storage, not the tenant DB)
│   │   │   ├── IndustryEnumProvider.cs         # Implements IIndustryEnumProvider — the six canonical values; M-06 is the single source of truth
│   │   │   └── OrganizationSaveService.cs      # Orchestrates validate → store/logo; atomic persist via M-06-internal IOrganizationSettingsStore + audit emission
│   │   ├── Catalogue/
│   │   │   ├── KpiListItemMapper.cs            # KPIDefinition → list-row DTO
│   │   │   └── KpiSeedDataProvider.cs          # The eight canonical seeds (used by tests; the actual seed runs in the migration)
│   │   └── Events/
│   │       └── M17EventPublisher.cs            # Wraps M-17 IEventPublisher; emits `settings.changed`
│   ├── Migrations/                              # M-06-owned SQL baselines (csproj <Content> + DevTenantSchemaBootstrapper)
│   │   ├── KpiManagement_Baseline.sql          # four KPI tables + seeds (existing)
│   │   └── KpiManagement_OrganizationSettings.sql  # NEW (US-6) — organization_settings, tenant DB, gated by the `organization_settings` sentinel
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── KpiDefinition.cs
│   │   │   ├── KpiThreshold.cs
│   │   │   ├── KpiPerspective.cs
│   │   │   ├── CxiWeight.cs
│   │   │   └── OrganizationSettings.cs         # NEW (US-6)
│   │   ├── ValueObjects/
│   │   │   ├── KpiType.cs                      # Standard | Custom
│   │   │   ├── CalculationMethod.cs            # WeightedAverage | TopNBox | NPSStandard | WeightedComposite
│   │   │   ├── Scale.cs                        # Scale0_10 | Scale1_3 | Scale1_5 | Scale1_7 | Scale1_10 | Scale1_100 | Nps
│   │   │   ├── RepresentationStyle.cs          # Number | Stars | Emoji | Slider
│   │   │   ├── EmojiSet.cs                     # FaceClassic | HandThumbs
│   │   │   └── Industry.cs                     # NEW (US-6) — Banking | Telecommunications | Government | Automotive | Entertainment | Services
│   │   └── Interfaces/                          # Domain/Interfaces = PUBLISHED cross-module interfaces only (per-entity service ports live in Application/<SubDomain>/Interfaces)
│   │       └── IKpiConfigReader.cs             # Published interface → consumed by M-01 / M-07 / M-09
│   └── Infrastructure/
│       └── Persistence/                         # DB-08: EF only maps; SQL baseline owns the schema (no EF migrations)
│           ├── TenantDbContext.cs              # Concrete ITenantDbContext (per-tenant schema); ExecuteAsync transaction boundary
│           └── Configurations/                 # One IEntityTypeConfiguration<T> per entity, explicit HasColumnName
│               ├── KpiDefinitionConfiguration.cs
│               ├── KpiThresholdConfiguration.cs
│               ├── KpiPerspectiveConfiguration.cs
│               ├── CxiWeightConfiguration.cs
│               └── OrganizationSettingsConfiguration.cs  # NEW (US-6)
│
└── Nabadat.CustomerJourneyManagement/                       # MODIFIED — published-interface addition (M-16)
    ├── (existing M-16 surface — feature 002, incl. the `scoring_configs` tenant-singleton table + `ScoringConfig` entity)
    ├── Application/
    │   └── Scoring/                             # US-4 — IScoringConfigStore + store over the EXISTING scoring_configs singleton (no new table/entity)
    ├── Domain/
    │   └── Interfaces/
    │       ├── IJourneyBindingQuery.cs          # GetKpiBindingUsageAsync(kpiId) → (touchpoint_count, journey_count)
    │       └── IScoringConfigStore.cs           # GetAsync()/UpdateAsync(). M-06 + feature 003 call this instead of touching the tenant ScoringConfig directly.
    └── (no new entity/config — reuses feature 002's `ScoringConfig` + `scoring_configs` table, reshaped to a per-tenant singleton there)

tests/
├── Nabadat.KpiManagement.UnitTests/             # NEW
│   ├── Nabadat.KpiManagement.UnitTests.csproj
│   ├── Kpis/
│   │   ├── KpiDefinitionValidatorTests.cs
│   │   ├── KpiThresholdValidatorTests.cs
│   │   ├── KpiNormalisationCalculatorTests.cs
│   │   ├── TopNBoxWarningRuleTests.cs
│   │   ├── KpiCatalogueQueryTests.cs
│   │   ├── KpiListItemMapperTests.cs
│   │   ├── KpiSeedDataProviderTests.cs
│   │   ├── KpiSaveServiceTests.cs
│   │   ├── KpiActivationCommandHandlerTests.cs
│   │   └── KpiDeactivationSideEffectsTests.cs
│   ├── Cxi/
│   │   ├── CxiWeightNormaliserTests.cs
│   │   ├── CxiActivationRuleTests.cs
│   │   ├── CxiMemberMembershipRuleTests.cs
│   │   └── CxiSnapshotComposerTests.cs
│   ├── ScoringConfig/
│   │   ├── ScoringConfigValidatorTests.cs
│   │   ├── AlphaBetaDeriverTests.cs
│   │   └── ScoringConfigUpdateServiceTests.cs
│   └── Organization/
│       ├── OrganizationSettingsValidatorTests.cs
│       ├── LogoUploadValidatorTests.cs
│       ├── SvgSanitiserTests.cs                # benign passthrough, <script> stripping, on* stripping, <foreignObject> removal, external-href <use> removal, unparseable → throws
│       └── IndustryEnumProviderTests.cs
│
├── Nabadat.KpiManagement.IntegrationTests/      # NEW — first M-06 integration suite
│   ├── Nabadat.KpiManagement.IntegrationTests.csproj
│   ├── Infrastructure/
│   │   ├── KpiManagementApplicationFactory.cs            # WebApplicationFactory + Testcontainers Postgres + migration runner
│   │   ├── KpiSeedHelper.cs
│   │   └── PersonaContextHelper.cs             # JWT issuance for P-01/P-02/P-04/P-07 personas
│   ├── Endpoints/
│   │   ├── GetKpisEndpointTests.cs
│   │   ├── CreateKpiEndpointTests.cs
│   │   ├── UpdateKpiEndpointTests.cs
│   │   ├── ActivateKpiEndpointTests.cs         # incl. binding-usage 409 + cxi_side_effect cascade
│   │   ├── BindingUsageEndpointTests.cs
│   │   ├── UpdateCxiWeightsEndpointTests.cs
│   │   ├── ScoringConfigEndpointTests.cs
│   │   ├── OrganizationEndpointTests.cs
│   │   └── LogoUploadEndpointTests.cs          # incl. SVG sanitised-persistence + LOGO_SVG_UNSAFE_CONTENT rejection
│   ├── Services/
│   │   ├── KpiSaveAtomicityTests.cs            # Force perspective insert failure → no KPI, no event
│   │   ├── ScoringConfigIdempotencyTests.cs    # No-op save emits 0 events
│   │   └── CxiCascadeAtomicityTests.cs         # Deactivate CSAT → exactly 1 event with nested cxi_side_effect
│   └── Scenarios/
│       ├── KpiCreateThenEditScenarioTests.cs
│       ├── CxiConfiguresAndRebalancesScenarioTests.cs
│       ├── ScoringConfigEditAndPersistScenarioTests.cs
│       ├── KpiDeactivationCascadeScenarioTests.cs
│       └── KpiCrossTenantIsolationScenarioTests.cs  # SC-005 / GP-04 pass condition
│
└── Nabadat.Portal.E2ETests/                    # NEW — first E2E project for frontend/ (the portal SPA)
    ├── Nabadat.Portal.E2ETests.csproj
    ├── COVERAGE.md
    ├── Infrastructure/
    │   ├── E2ETestBase.cs                      # Playwright + portal MFA SignInAsync
    │   ├── appsettings.local.json.template
    │   └── playwright.config.json
    ├── KpiManagementTests.cs                   # US-1 E2E
    ├── KpiConfigTests.cs                       # US-2 E2E (incl. analyst read-only assertions for US-7)
    ├── CxiConfigTests.cs                       # US-3 E2E
    ├── CustomerJourneySettingsTests.cs         # US-4 E2E
    └── OrganizationSettingsTests.cs            # US-6 E2E

frontend/src/
├── features/
│   ├── kpi-management/
│   │   ├── pages/
│   │   │   ├── KpiManagementPage.tsx           # /kpi-management
│   │   │   └── KpiConfigPage.tsx               # /kpi-management/new, /kpi-management/:id (CXI variant inline-toggled by KPI shape)
│   │   ├── components/
│   │   │   ├── KpiTable.tsx
│   │   │   ├── KpiTypeBadge.tsx
│   │   │   ├── ActiveKpiCountSubtitle.tsx
│   │   │   ├── KpiConfigForm.tsx
│   │   │   ├── ThresholdBandEditor.tsx
│   │   │   ├── ScaleEndpointDescriptionFields.tsx
│   │   │   ├── PerspectiveChipInput.tsx
│   │   │   ├── EmojiSetPreview.tsx
│   │   │   ├── CxiWeightsTable.tsx
│   │   │   └── BindingUsageConfirmDialog.tsx
│   │   ├── hooks/
│   │   │   ├── useKpiList.ts
│   │   │   ├── useKpiDetail.ts
│   │   │   └── useKpiSave.ts
│   │   └── api.ts
│   └── settings/
│       ├── pages/
│       │   ├── SettingsLandingPage.tsx         # /settings
│       │   ├── OrganizationSettingsPage.tsx    # /settings/organization
│       │   └── CustomerJourneySettingsPage.tsx # /settings/customer-journey
│       ├── components/
│       │   ├── SettingsSectionLink.tsx
│       │   ├── LogoUploadField.tsx
│       │   ├── IndustryDropdown.tsx
│       │   ├── AlphaBetaSlider.tsx             # Linked α / read-only β display
│       │   ├── MotMultiplierSlider.tsx
│       │   ├── ScoringConfigInfoIcon.tsx       # ? icon + bilingual tooltip
│       │   └── UnsavedChangesGuard.tsx
│       ├── hooks/
│       │   ├── useOrganizationSettings.ts
│       │   └── useScoringConfig.ts
│       └── api.ts
└── components/cx/kpi/                          # Shared, reusable across features
    ├── UniversalArcGauge.tsx                   # Custom SVG (NOT Recharts) — dual-ring + needle dot + target marker + zone coloring
    ├── KpiQuestionPreview.tsx                  # Renders Number / Stars / Emoji / Slider styles
    └── perfBands.ts                            # Tiny helper deriving the band colour at a value (no Tailwind decoration)
```

**Structure Decision**: Web-application pattern. Backend adds a new C# module `src/Nabadat.KpiManagement/` which owns the four KPI tables **plus `organization_settings`** (the Organization surface — table, store, logo store, industry provider — re-homed from the never-built M-11 to M-06 per the 2026-06-24 decision, all M-06-internal in the tenant DB); M-16 (`Nabadat.CustomerJourneyManagement`) extends only to publish the binding-usage probe + ScoringConfig store (consumed by M-06). Frontend adds two feature folders under `frontend/src/features/` (`kpi-management/` + `settings/`) plus shared SVG primitives under `frontend/src/components/cx/kpi/`. Tests follow the strict per-kind split (CLAUDE.md Unit Test Policy rule 5): three new projects (`Nabadat.KpiManagement.UnitTests`, `Nabadat.KpiManagement.IntegrationTests`, `Nabadat.Portal.E2ETests`). The Portal E2E project is the first E2E project for the `frontend/` workspace in this repo and is reused by future portal features.

**Backend naming, structure & data-access (AMENDMENT-008 / AMENDMENT-009 / AMENDMENT-007 — binding).**

- **Domain-name ↔ module-ID mapping (AMENDMENT-008 §2, recorded here and stable):** **M-06 → `Nabadat.KpiManagement`**, **M-11 → `Nabadat.TenantAdministration`** (distinct from the `Nabadat.TenantAdmin` composition-root host), **M-16 → `Nabadat.CustomerJourneyManagement`** (existing). The `M{NN}` token (e.g. `M06`, `M11`) MUST NOT appear in any project, assembly, namespace, or type name; the hyphenated IDs `M-06`/`M-11`/`M-16` remain the canonical identifiers in prose, the Section 3 registry, the Section 4 event catalogue, and API-03 declarations. Test projects follow the family `Nabadat.<DomainName>.UnitTests` / `.IntegrationTests`; the integration fixture is `KpiManagementApplicationFactory` (the `<DomainName>ApplicationFactory` form). Reference module: M-10 `Nabadat.UserManagement`.
- **Canonical four-layer folder structure (AMENDMENT-009 / architecture-constitution Article 1A):** every module is organised into `Api/`, `Application/`, `Domain/`, `Infrastructure/` with inward-only dependencies; the **M-10 `Nabadat.UserManagement` reference** is followed (M-16 `Nabadat.CustomerJourneyManagement` is the other in-repo example). New features add files into these existing layer folders — they never invent a new top-level folder kind.
- **EF Core data-access (AMENDMENT-007 / DB-08): no repositories; ONE service per entity.** Each table/aggregate is fronted by a single **`<Aggregate>Service`** in `Application/<SubDomain>/` that holds **both its CRUD (EF Core over `ITenantDbContext`) and its business logic** — there is NO separate `<Aggregate>DataService` (that suffix is an M-16-local variation; the M-10 reference and DB-08's literal naming both use `<Aggregate>Service` — e.g. `TenantUserService`, `PersonaBaselineService`). Its port `I<Aggregate>Service` lives in `Application/<SubDomain>/Interfaces/` (the unit-test mock seam, per CLAUDE.md + M-10); the `ITenantDbContext` context port lives in `Application/Interfaces/`; PUBLISHED cross-module interfaces (`IKpiConfigReader`) live in `Domain/Interfaces/`. The concrete `TenantDbContext` + one `IEntityTypeConfiguration<T>` per entity (explicit `HasColumnName`) live in `Infrastructure/Persistence/` + `Configurations/`. `ITenantDbContext.ExecuteAsync` is the **only** multi-write transaction boundary (no unit-of-work type) — `KpiSaveService` composes the entity services' writes inside it. The SQL baseline (`KpiManagement_Baseline.sql`) owns the schema; **EF generates/applies no migrations**. This replaces the `I<X>Repository` / `<X>Repository` pattern an earlier draft of this plan used.

---

## Phases

### Phase 0: Research (Output: `research.md`)

Resolves the technical unknowns the spec deferred to planning. Topics: SVG sanitiser library choice (vetted candidates + rejected alternatives), Emoji set per-K glyph slot assignment, logo storage abstraction shape, M-16 published-interface contract decision (binding-usage shape, ScoringConfig store shape), CXI cascade transaction model, and α / β floating-point precision strategy.

### Phase 1: Design & Contracts (Outputs: `data-model.md`, `contracts/*.md`, `quickstart.md`)

- `data-model.md` — PostgreSQL schemas for the four M-06 tables + `organization_settings`; references M-16's `scoring_configs` without redefining it; documents seed-data row contents for the eight standard KPIs (NPS field locks, default thresholds incl. NPS-specific `x=0, y=30`).
- `contracts/kpi-api.md` — KPI catalogue + KPI configuration + activation + CXI weights endpoints with full request/response shapes, permission attributes, and the API-05 envelope error matrix.
- `contracts/settings-api.md` — Organization + ScoringConfig endpoints, logo upload, error codes (`ORGANIZATION_NAME_REQUIRED`, `ORGANIZATION_INDUSTRY_UNKNOWN`, `LOGO_CONTENT_TYPE_UNSUPPORTED`, `LOGO_SVG_UNSAFE_CONTENT`, `INVALID_ALPHA_BETA_SUM`, `MOT_MULTIPLIER_OUT_OF_RANGE`).
- `contracts/published-interfaces.md` — `IKpiConfigReader` (M-06 publishes to M-01/M-07/M-09) + M-16 deltas (`IJourneyBindingQuery`, `IScoringConfigStore`). The Organization store/logo/industry interfaces are M-06-internal (re-homed from M-11 to M-06) and are NOT published cross-module. C# interfaces + DTOs.
- `quickstart.md` — Runnable validation guide (boot stack, sign in as P-01, create a custom KPI, configure CXI, edit ScoringConfig, upload an SVG with a `<script>` and verify it's sanitised).

### Phase 2: Tasks (Output of `/speckit-tasks`)

**Foundational (before any user story)**:

- Scaffold `Nabadat.KpiManagement.csproj` + unit + integration test projects + DI registration; register the module in `Nabadat.TenantAdmin.sln` (the host).
- Scaffold `Nabadat.Portal.E2ETests` project + `E2ETestBase.cs` + `appsettings.local.json.template` (first E2E project for `frontend/` workspace).
- Database migration `KpiManagement_Baseline.sql` (4 tables + 1 functional unique index + 8 seed rows in canonical order); rollback file.
- M-06 Organization additions (re-homed from M-11): `organization_settings` tenant-DB table migration (`KpiManagement_OrganizationSettings.sql`) + M-06-internal `IOrganizationSettingsStore` / `ILogoStore` / `IIndustryEnumProvider` + implementations (not published cross-module).
- M-16 additions: `IJourneyBindingQuery` and `IScoringConfigStore` published interfaces + implementations.
- Skeleton `IKpiConfigReader` interface (no implementation yet — fleshed out by US-2 / US-3).

**Phase A — US-1 (P1) — KPI catalogue**
Unit tests (Red Checkpoint) → `KpiCatalogueQuery`, `KpiListItemMapper`, `KpiSeedDataProvider` → Endpoint `GET /api/v1/kpis` → Frontend `KpiManagementPage` + `KpiTable` + `ActiveKpiCountSubtitle` → Per-story checkpoint with integration tests + E2E `KpiManagementTests`.

**Phase B — US-2 (P1) — Non-CXI KPI configuration**
Unit tests (Red Checkpoint) → `KpiDefinitionValidator`, `KpiThresholdValidator`, `KpiNormalisationCalculator`, `TopNBoxWarningRule`, `KpiBindingUsageProbe`, `KpiSaveService` → Endpoints `POST/PUT /api/v1/kpis`, `GET /api/v1/kpis/{id}`, `GET /api/v1/kpis/{id}/binding-usage` → Frontend `KpiConfigPage` + `KpiConfigForm` + `ThresholdBandEditor` + `UniversalArcGauge` + `KpiQuestionPreview` → Per-story checkpoint + E2E `KpiConfigTests` (covers US-7 analyst read-only assertions).

**Phase C — US-3 (P1) — CXI composite KPI**
Unit tests (Red Checkpoint) → `CxiWeightNormaliser`, `CxiActivationRule`, `CxiMemberMembershipRule`, `CxiSnapshotComposer` → Endpoint `PUT /api/v1/kpis/{cxi_id}/weights` → Frontend CXI variant of `KpiConfigPage` + `CxiWeightsTable` → Per-story checkpoint + E2E `CxiConfigTests`.

**Phase D — US-4 (P1) — Customer Journey ScoringConfig**
Unit tests (Red Checkpoint) → `ScoringConfigValidator`, `AlphaBetaDeriver`, `ScoringConfigUpdateService` → Endpoints `GET/PUT /api/v1/tenant/scoring-config` (calling M-16's `IScoringConfigStore`) → Frontend `SettingsLandingPage` + `CustomerJourneySettingsPage` + `AlphaBetaSlider` + `ScoringConfigInfoIcon` → Per-story checkpoint + E2E `CustomerJourneySettingsTests`.

**Phase E — US-5 (P2) — Activation cascade**
Unit tests (Red Checkpoint) → `KpiActivationCommandHandler`, `KpiDeactivationSideEffects` (CXI cascade with nested `cxi_side_effect` payload) → Endpoint `PATCH /api/v1/kpis/{id}/activation` → Frontend `BindingUsageConfirmDialog` integrated into US-2's `KpiConfigForm` Active toggle → Per-story checkpoint + scenario test `KpiDeactivationCascadeScenarioTests`.

**Phase F — US-6 (P2) — Organization settings**
Unit tests (Red Checkpoint) → `OrganizationSettingsValidator`, `LogoUploadValidator`, `SvgSanitiser`, `IndustryEnumProvider` → Endpoints `GET/PUT /api/v1/tenant/organization`, `POST /api/v1/tenant/organization/logo` → Frontend `OrganizationSettingsPage` + `LogoUploadField` + `IndustryDropdown` → Per-story checkpoint + E2E `OrganizationSettingsTests`.

**Phase G — Feature-end audit**
Run the full solution (`dotnet test Nabadat.TenantAdmin.sln`) + the full E2E filter on the portal workspace; produce final `COVERAGE.md` rows for every US.

---

## Key Design Decisions (record of rationale; full detail in `research.md`)

### 1. Published Interface Boundary

Two new M-16 published interfaces: `IJourneyBindingQuery` (FR-026's binding-usage probe) and `IScoringConfigStore` (US-4 read/write of the tenant ScoringConfig M-16 owns) — both added without disturbing the journey-snapshot or score-provider interfaces from feature 002. The Organization surface (`IOrganizationSettingsStore`, `ILogoStore`, `IIndustryEnumProvider`) is re-homed from M-11 to **M-06 as internal services** — M-06 owns the `organization_settings` tenant-DB table and is the single source of truth for the industry list, so these do NOT cross a module boundary. One new M-06 published interface (`IKpiConfigReader`) for downstream M-01 / M-07 / M-09 consumers.

### 2. SVG Sanitisation

Server-side sanitiser runs **before** the persistence call (FR-050). The persisted byte stream is the sanitiser's output, never the upload bytes. Library choice (vetted in `research.md`) is `Ganss.Xss.HtmlSanitizer` configured for SVG with a custom allow-list (`<svg>`, `<g>`, `<path>`, `<rect>`, `<circle>`, `<ellipse>`, `<line>`, `<polyline>`, `<polygon>`, `<text>`, `<tspan>`, `<defs>`, `<linearGradient>`, `<radialGradient>`, `<stop>`, `<symbol>` only — and a strict attribute allow-list excluding every `on*` and excluding `<use>` with external `href`). Non-parseable payloads throw `SvgUnsafeContentException` → API code `LOGO_SVG_UNSAFE_CONTENT`.

### 3. Atomicity of KPI Save + M-17 Event

`KpiSaveService.Save` composes the whole write inside **`ITenantDbContext.ExecuteAsync`** — the single multi-write transaction boundary mandated by DB-08 (no unit-of-work type): validate → upsert `kpi_definitions` → upsert `kpi_thresholds` → full-replace `kpi_perspectives` (per FR-028) → full-replace `cxi_weights` → write the M-17 `event_log` row → commit. Each write goes through its `<Aggregate>Service` (which calls `SaveChangesAsync`); `ExecuteAsync` wraps them so any step failure rolls the entire transaction back and emits NO event. Integration tests (`KpiSaveAtomicityTests`) inject a perspective-insert failure mid-transaction and assert zero rows + zero events.

### 4. CXI Cascade Single-Event Payload

`KpiActivationCommandHandler.Handle({active:false, confirm:true})` for a KPI that is a CXI member runs in one transaction: update `kpi_definitions.is_active=false` AND `show_on_dashboard=false`; delete the row in `cxi_weights` for the member; emit exactly ONE `settings.changed` row whose JSON diff carries `{ is_active: false, show_on_dashboard: false, cxi_side_effect: { cxi_kpi_id, removed_member_kpi_id, recomputed_effective_percentages: {...} } }` (per Clarifications session 2026-06-21). The `CxiWeightNormaliser` computes the post-removal proportions before the event is emitted.

### 5. α Slider + β Derivation

The slider emits α to 3 decimal places (e.g., 0.567). The backend persists α at `numeric(4,3)`. β is computed on read by `AlphaBetaDeriver.Beta(α) = 1.000 - α` rounded to 3 dp. β is never stored. The frontend `AlphaBetaSlider` shows both α and β read-only-derived in the UI in real time.

### 6. Same-Session Live Update

The "[X] Active KPIs" subtitle (FR-008) subscribes to a local React state slice (`useKpiList()` hook) that is mutated by the activation endpoint's `onSuccess` callback. No polling, no SSE. Per Clarifications session 2026-06-21 (Q3). Cross-session updates appear on the next route navigation or explicit reload only.

### 7. NPS-Specific Default Thresholds

`KpiSeedDataProvider` and the create-form initial-values resolver branch on `isNpsKpi`: x=0 / y=30 for NPS; x=20 / y=70 for every other KPI (per Clarifications session 2026-06-21, Q1 of round 2). The validator does not enforce NPS-specific defaults — the tenant is free to override them (only the `lower < x < y < upper` invariant is enforced).

### 8. Logo Storage Abstraction

`ILogoStore.PutAsync(tenantId, contentType, byteStream): Task<LogoBlobRef>` and `GetAsync(LogoBlobRef): Task<Stream>`. Implementation routes the call to the tenant's configured object storage region (T-04). The interface is **M-06-internal** — M-06 owns the tenant `organization_settings` table and its editing surface (re-homed from M-11 to M-06, 2026-06-24); M-06's Organization controller/save-service call it directly. No code outside M-06 touches storage.

---

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|

(No violations to justify — Constitution Check passes with no exceptions.)

---

## Constitution Check #2 (Post-Design Re-Evaluation)

*Performed after Phase 1 artifacts are written. This block records the second-pass evaluation.*

### Re-evaluated principles

| Principle | First-Pass Status | Post-Design Status | Notes |
|-----------|-------------------|--------------------|-------|
| GP-01 | ✅ | ✅ | `data-model.md` and `contracts/` confirm PostgreSQL as the sole authoritative store for all six entities; no ES writes, no other-store reads. |
| GP-02 | ✅ (N/A) | ✅ (N/A) | No high-sensitivity field surface emerged in the data model. |
| GP-03 | ✅ (N/A) | ✅ (N/A) | No per-subject personal data surfaces in the design. |
| GP-04 | ✅ | ✅ | Every entity in `data-model.md` lives in `tenant_{slug}`; no `tenant_id` columns; scenario test `KpiCrossTenantIsolationScenarioTests` covers the SC-005 pass condition. |
| GP-05 | ✅ | ✅ | This second check. |
| AD-01 | ✅ | ✅ | `contracts/published-interfaces.md` lists every cross-module call: M-06 → M-16 (`IJourneyBindingQuery`, `IScoringConfigStore`), M-06 → M-10 (`IPermissionService`), M-06 → M-17 (`IEventPublisher`). The Organization store/logo/industry are M-06-internal (re-homed from M-11 to M-06) — no cross-module call. No M-06 concrete type or table is referenced from outside M-06. |
| AD-02 | ✅ | ✅ | `data-model.md` confirms no `tenant_id` columns on any new tenant table. |
| AD-03 | ✅ | ✅ | No caching layer introduced. |
| AD-04 | ✅ (N/A) | ✅ (N/A) | No ES interaction in this feature. |
| AD-05 | ✅ | ✅ | Confirmed in code paths — no `ENABLE_*` branching anywhere. |
| AD-07 | ✅ | ✅ | All endpoints derive tenant context from the request-scoped `ITenantContext` and never mutate it. |
| Event catalogue | ✅ | ✅ | Only `settings.changed` and `journey.scoring_config.updated` (both pre-registered) are emitted. |
| Persona registry | ✅ | ✅ | Only P-01 / P-02 / P-06 / P-07 are referenced. |
| T-01 | ✅ | ✅ | EN/AR + RTL parity confirmed in the component design (logical CSS properties; gauge labels follow reading direction). |
| T-03 | ✅ | ✅ | All KPI / Settings configuration is data; no code release required for tenant configuration changes. |
| T-04 | ✅ | ✅ | `ILogoStore` resolves the storage region from tenant provisioning state; no runtime jurisdiction routing. |
| T-05 | ✅ | ✅ | Tenant isolation enforced by schema. |
| T-08 | ✅ | ✅ | UUID PKs throughout; stable across M-16 bindings and the future per-perspective scoring release. |

**Constitution Check #2 passes. No new violations introduced by Phase 1 design. Ready for `/speckit-tasks`.**
