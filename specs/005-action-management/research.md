# Research: M-15 Action Management

**Feature**: `005-action-management` | **Date**: 2026-07-23

**Source spec**: `specs/005-action-management/spec.md` (rev 1.1) — 100% SRS coverage, zero
`[NEEDS CLARIFICATION]` markers (all ratified 21–22 Jul 2026, see spec §"Assumptions" R-1..R-17).

Because the spec itself carries no open clarification markers, this research phase focuses on
**codebase-grounded technical decisions**: how M-15 fits the existing `Nabadat.KpiManagement`
(M-06), `Nabadat.CustomerJourneyManagement` (M-16), and `Nabadat.UserManagement` (M-10, the
DB-08 reference) modules that already exist under `src/`, and where the spec's "proposed
technical design" (its own rev-1.1 governance note) needs to be reconciled with constitution
rules that postdate the SRS (cursor pagination, `/api/v1`, `correlation_id` envelope, etc.).

---

## 1. Team & sequencing context

**Input**: 3 resources — 2 backend (AbuKr, Atia), 1 frontend (Marawan).

- **Decision**: Sequence backend work story-by-story in spec priority order (US1 → US2 → US3 →
  US5 → US4 → US6 → US7 → US8 → US9 → US10, P1s first) and split the two backend engineers by
  **vertical slice, not by layer** — e.g. AbuKr owns the Action/KPI-Target write path (US1, US4,
  US7, US9 — create/edit/settings/target-lifecycle) while Atia owns the read/status/lifecycle
  path (US2, US3, US5, US6, US10 — list, detail, automatic transitions, archive, retro-dating).
  Both converge on the shared `Domain/Entities` and `Application/Interfaces` on day one so
  neither blocks on the other's DTOs. Marawan starts SCR-01 (All Actions) as soon as US2's
  `GET /api/v1/actions` contract is stable (end of the Foundational + US1/US2 backend phase),
  then SCR-02 and SCR-03 in parallel with backend US4/US7 since the UI shapes are fully
  specified (FR-112..FR-116, FR-201..FR-210, FR-301..FR-309) and can be built against a mocked
  contract before the real endpoints land.
- **Rationale**: the write-path/read-path split keeps each backend engineer's units under test
  independent (`ThresholdValidator`/`BaselineCaptureService` vs. `ActionStatusCalculator`/
  `LowestPerformingTargetSelector`) and avoids both engineers touching `ActionsController` at
  once. One frontend developer means the 3 screens must be built sequentially — SCR-01 first
  (it is the landing page and the dependency for every drill-down), matching the spec's own P1
  ordering (US1/US2/US3 before US4+).
- **Alternatives considered**: split backend by screen (one owns SCR-02's endpoints, one owns
  SCR-01/03's) — rejected because SCR-02 (create/edit) and SCR-01/03 (read) share almost all
  Application-layer services (`ActionStatusCalculator`, `ScoreProgressCalculator`, etc.); a
  screen-based split would force both engineers into the same files daily.

---

## 2. Domain name & module registration (AMENDMENT-008)

- **Decision**: `Nabadat.ActionManagement` — derived from the Section-3 registry name
  "Action Management" (M-15), following the exact M-10→`Nabadat.UserManagement`,
  M-16→`Nabadat.CustomerJourneyManagement` pattern. No `M15`/`M-15` token in any project,
  namespace, or type name (AMENDMENT-008 rule 1); the `M-15` ID is used only in spec/plan/tasks
  prose and the constitution registry.
- **Project family** (AMENDMENT-008 rule 3, mirrored from `Nabadat.KpiManagement`):
  - `src/Nabadat.ActionManagement/Nabadat.ActionManagement.csproj`
  - `tests/Nabadat.ActionManagement.UnitTests/`
  - `tests/Nabadat.ActionManagement.IntegrationTests/`
  - E2E tests append to the existing shared `tests/Nabadat.E2ETests/` project, in a new
    `ActionManagement/` module folder (mirroring `KpiManagement/`, `CustomerJourneyManagement/`)
    — **no new E2E project** (CLAUDE.md E2E Test Policy rule 1: one project, module-named
    folders).
  - Register all three new projects in `Nabadat.TenantAdmin.sln` alongside the existing module
    entries (verified pattern: `src/Nabadat.KpiManagement`, `tests/Nabadat.KpiManagement.*Tests`
    lines in the `.sln`).

## 3. Owned-table registry correction (parallels AMENDMENT-011/012)

- **Finding**: `constitution.md` Section 3 currently lists M-15's owned tables as the
  **placeholder** reservation set `action_plans`, `action_assignments`, `action_progress`
  (AD-06/DB-06 — Phase-2 tables provisioned empty at Phase 1). A repo-wide search confirms
  **none of these three tables have actually been created yet** in any baseline SQL — the
  Phase-1 reservation migration for M-15 was never written, so there is no pre-existing empty
  schema to reconcile against (unlike a table that already exists and must not require a
  migration at activation).
- **Decision**: Ship M-15's real schema under its own names — **`actions`**, **`kpi_targets`**,
  **`action_settings`** (a single-row-per-tenant settings table, mirroring
  `Nabadat.KpiManagement`'s `OrganizationSettingsStore` single-row pattern for SET-1/SET-2) —
  defined in `src/Nabadat.ActionManagement/Infrastructure/Migrations/ActionManagement_Baseline.sql`
  per DB-08/Article 7.6 (SQL baseline owns the schema; EF only maps). No `event_log`/`audit_log`
  table is created — M-15 maps its own `Domain/Entities/EventLog.cs` onto the **shared**
  per-tenant `event_log` table (the established write-side pattern already used identically by
  `Nabadat.KpiManagement`, `Nabadat.UserManagement`, and `Nabadat.CustomerJourneyManagement` —
  each module owns an `EventLog` entity + `EventLogConfiguration` mapped onto the same physical
  table; M-17 is the conceptual/read-side owner).
- **Constitution follow-up required**: file an amendment (same shape as AMENDMENT-011/012)
  correcting Section 3's M-15 owned-tables entry from the placeholder list to
  `actions, kpi_targets, action_settings` once the baseline ships — tracked as a coordination
  item (§6 below), not a plan blocker (the M-01 precedent: AMENDMENT-012 was filed by a task,
  not by `/speckit-plan`).
- **Rationale**: `action_plans`/`action_assignments`/`action_progress` do not match the spec's
  Key Entities (Action, KPI Target, Settings) — same situation AMENDMENT-012 resolved for M-01
  (`question_bank` never materialized; the real M-01 tables replaced the placeholder list).
- **Alternatives considered**: shoehorning the spec's entities into 3 tables named
  `action_plans`/`action_assignments`/`action_progress` — rejected; the names don't map to any
  real relationship in the spec (there is no "assignment" or generic "progress" entity; Score/
  Time Progress are computed, not stored rows) and would confuse every future reader.

## 4. Cross-module dependency on M-06 (`Nabadat.KpiManagement`) — the critical gap

INT-01 (spec, hard dependency) requires 5 data flows from M-06: (a) active KPI registry,
(b) live current score, (c) normalised 0–100 index, (d) historical daily score by date,
(e) KPI-deactivation/reactivation events. Inspecting `src/Nabadat.KpiManagement` directly:

- **(a) exists today**: `IKpiConfigReader.GetActiveAsync()` / `GetByIdAsync()` /
  `GetByShortNameAsync()` (`Application/Kpis/Interfaces/IKpiConfigReader.cs`) is the published
  read contract M-06 already exposes to M-01/M-07/M-09. M-15 can consume it directly today for
  the KPI select (BR-002) and KPI names.
- **(b), (c), (d) do NOT exist today.** There is no `metric_values`/score-computation engine in
  `Nabadat.KpiManagement` — confirmed by AMENDMENT-011 (`metric_configs`/`metric_values` were
  explicitly removed from M-06's owned-tables entry "pending the M-06 score-computation engine
  release, which is out of scope of Feature 003") and by Feature 004's own note
  (`spec.md` line 32: "per-perspective score computation and storage are explicitly deferred to
  a later M-06 release"). **M-06 has no live score, no historical score, and no normalised
  index anywhere in the codebase today.**
- **(e) does NOT exist as a dedicated event.** M-06 emits only `settings.changed`
  (`KpiEventPublisher.PublishKpiSettingsChangedAsync`, `Application/Events/KpiEventPublisher.cs`)
  with `entity_type: "kpi"` and a per-field diff — confirmed by AMENDMENT-011 §1. Activation/
  deactivation surfaces as a `{ field: "active", from, to }` entry inside that generic event,
  not as a dedicated `kpi.deactivated`/`kpi.reactivated` event type. The constitution's Event
  Catalogue (Section 4) has no `kpi.deactivated` row and adding one requires an amendment
  (router §12.2 — "a question not answered here is flagged for amendment, not silently resolved
  in the spec").

- **Decision**: Follow the exact dependency-inversion precedent M-16 already established for
  this same class of problem (`Application/KpiTypes/Interfaces/IActiveKpiCatalogReader.cs`,
  documented inline: *"M-06 references M-16 (for `IJourneyBindingQuery`), so M-16 cannot
  reference M-06. M-16 owns this port; the host wires an adapter backed by M-06's
  `IKpiConfigReader`... When M-16 runs standalone, the module's own default reader supplies a
  reference catalogue"*):
  1. M-15 **owns its own inbound ports** in `Domain/Interfaces/` (published-interface location
     per Article 1A rule 3): `IKpiScoreReader` (`GetCurrentScoreAsync`, `GetHistoricalScoreAsync`,
     `GetNormalisedIndexAsync`) and reuses M-06's real `IKpiConfigReader` directly for (a) since
     that one already exists and is stable.
  2. For (b)/(c)/(d), since no real implementation can exist until M-06 ships its
     score-computation engine, M-15 ships a **stub/default adapter** (`NullKpiScoreReader` or
     similar, returning "no score" / raising `NoBaselineScoreException` deterministically) so
     the module is fully testable standalone (unit + integration) today, and the host
     (`Nabadat.TenantAdmin`) swaps in the real M-06-backed adapter **the day M-06 ships it** —
     zero M-15 code change at that point, only a DI registration swap. This is not a design
     compromise; it is the same shape M-16 already ships for exactly this situation.
  3. For (e), M-15's `KpiForceDeactivationCascade` **reads the shared `event_log` table lazily**
     (via its own `EventLog` read-mapping, watermarked by last-processed `event_log.id` stored on
     `action_settings`) filtering `event_type = 'settings.changed' AND entity_type = 'kpi'` and
     inspecting the diff for an `active: true → false` / `false → true` transition, rather than
     standing up new background-worker infrastructure (no `IHostedService`/`BackgroundService`
     precedent exists anywhere in the repo today — confirmed by search). The watermark check
     runs inline on the request paths that need it (`GET /api/v1/actions*`, target
     activate/reactivate) — "next event processing cycle" (SC-010) is satisfied by "next time
     anything reads or writes this tenant's Actions," which is materially identical to how
     Planned→Active→Completed status is itself computed lazily from dates on every render
     (FR-102) rather than stored. This avoids introducing new infrastructure (AD-03: no Redis/
     caching layer) purely to solve a polling problem the read-path already solves for status.
  4. Register a **cross-module coordination log entry** (§6 below, mirroring Feature 004's
     `coordination-log.md` C-01..C-06) tracking the M-06 score-engine and dedicated-event gaps as
     **PENDING**, owned by the M-06 team, not by M-15.
- **Rationale**: identical shape to the already-accepted M-16↔M-06 precedent; avoids inventing a
  webhook/internal-endpoint contract for (e) that the spec itself flags as "proposed technical
  design, subject to architecture review" (spec rev-1.1 governance note) when a zero-new-infra
  lazy-read alternative satisfies the same acceptance criteria (SC-010).
- **Alternatives considered**:
  - *Block M-15 until M-06 ships the score engine* — rejected; the spec's own scope is explicit
    that M-15 v1 must ship now, and 8 of 10 user stories (all but the live-pace-colour parts of
    US2/US3) do not require live/historical scores to be functionally complete against the
    stub. Blocking the whole module on an unscheduled M-06 release is disproportionate.
  - *`POST /api/internal/kpi-deactivation-events` webhook* (as sketched in the spec's Story 7
    Integration Test Coverage) — kept as a documented alternative in the coordination log, not
    the primary design, because it requires M-06 to know M-15 exists and call it (an explicit
    coupling the architecture constitution's event-log pattern is designed to avoid); the lazy
    read-path consumer achieves the same audit/cascade outcome without that coupling.

## 5. API surface: reconciling spec's proposed shapes with binding constitution rules

The spec explicitly declares (rev-1.1 governance note) that all endpoint paths, HTTP verbs,
and header names in its Integration Test Coverage blocks are "proposed technical design,
subject to architecture review — SRS §17 declares API shapes out of scope." Constitution
articles that postdate/bind independently of the SRS:

- **Versioning (API-01 / Article 2.3)**: every route gets the `/api/v1/` prefix
  (`/api/v1/actions`, not the spec's bare `/api/actions`).
- **Pagination (API-04 / Article 6.1)**: `GET /api/v1/actions` uses cursor pagination
  (`page_size` default 50/cap 200, `page_token`, response `{ items, next_page_token,
  total_count }`) — never the offset/page-number style implied by "paginated" in FR-110.
- **Error envelope (API-05 / Article 5.2)**: `{ error: { code, message, correlation_id,
  tenant_id } }` — the spec's shorthand `{ error: { code, message } }` in its test sketches is
  missing the two mandatory fields; every contract in this feature includes them.
- **Tenant resolution (API-02)**: tenant from JWT/subdomain only — never a body/query field,
  consistent with spec (no tenant_id anywhere in its request shapes — correct already).
- **Permission declaration (API-03)**: every endpoint declares `required_permission`,
  `required_scope`, `default_personas` from the canonical Section-8 registry. The spec's interim
  roles ("CX Program Manager" / "CX Analyst" / "Executive/Viewer") map to constitution personas
  **P-01** (CX Program Manager), **P-02** (CX Analyst), and **P-06** (Executive Sponsor)
  respectively — "Viewer" is not a distinct canonical persona yet; P-06 is the closest existing
  read-only persona and PERM-02 already anticipates M-10 refining this later.
- **Concurrency (ERR-8, spec)**: the spec calls for last-write-wins + a stale-save header, which
  is a **weaker** guarantee than Article 7.2's default `ETag`/`If-Match` optimistic concurrency.
  Since the spec explicitly ratified last-write-wins (R-4, stakeholder-ratified), this feature
  is a documented, spec-ratified exception to Article 7.2's default — recorded in Complexity
  Tracking, not silently overridden.
- **Decision**: adopt all of the above; document each contract in `contracts/api-endpoints.md`
  with the corrected path/pagination/envelope shape, annotated against the spec's original
  (proposed) sketch so the mapping is traceable.

## 6. Cross-module coordination log

Filed as `specs/005-action-management/coordination-log.md`, mirroring Feature 004's
`coordination-log.md` shape (status values PENDING → IN PROGRESS → SHIPPED / RATIFIED):

- **C-01 — M-06 score-computation engine** (`IKpiScoreReader` real implementation) — PENDING,
  owned by the M-06 team; M-15 ships a stub adapter meanwhile (§4 above).
- **C-02 — Dedicated `kpi.deactivated`/`kpi.reactivated` events** (optional hardening; not
  required if the lazy `settings.changed` consumer is accepted) — PENDING, requires a
  constitution Event Catalogue amendment if pursued.
- **C-03 — M-15 owned-tables registry correction** (§3 above) — to be filed as an amendment once
  the baseline ships (not blocking; same shape as AMENDMENT-012).
- **C-04 — M-07 overlay contract (INT-02)** — forward contract only; M-15 exposes the metadata
  (name, status, dates, archived flag) via its own published read interface
  (`Domain/Interfaces/IActionOverlayReader` or similar) for M-07 to consume once M-07's trend
  chart ships the overlay feature (M-07 does not exist under `src/` yet either — same "PENDING,
  no owning module yet" shape as Feature 004's C-03/C-04).
- **C-05 — M-09 notification subscription (INT-03)** — explicitly postponed in full per spec;
  no action needed until M-09 exists.
- **C-06 — M-10 permission refinement (PERM-02)** — interim roles hardcoded per §5 above; no
  action needed until M-10 ships a richer persona/permission model.

## 7. Frontend integration points

- **Route registration**: `frontend/src/features/actions/` (new feature folder, mirroring
  `kpi-management`/`journeys`/`settings` — `components/`, `hooks/`, `pages/`). Routes added to
  `frontend/src/App.tsx`: `/actions`, `/actions/new`, `/actions/:id/edit`, `/actions/:id` (FR-401).
  Settings subsection: a new `/settings/actions` page following the existing
  `frontend/src/features/settings/pages/` pattern (parallel to the persona-baselines settings
  page already registered at `/settings/persona-baselines`).
- **Design system**: per repo-root `CLAUDE.md` (binding, Frontend Design Gate) — reuse
  `ChartContainer`/Recharts wrapper is not applicable here (no standard x/y chart in this
  feature); the Stepped Zone Slider, Threshold Slider, and Timer Ring are all **custom SVG**
  per CLAUDE.md's explicit rule ("KPI Gauges... needle/marker positioning → custom SVG") — these
  three components map directly onto that rule (gradients/zones/needles are exactly what FR-501/
  502/503 specify). Build them as new `frontend/src/components/cx/` primitives so future modules
  (e.g. M-07's overlay) can reuse the Timer Ring / zone-slider visual language.
  All neutral SVG chrome (tracks, tick marks, grid) must be theme-aware per CLAUDE.md's dark-mode
  SVG rule; only the D1–D5 zone fills and `--nb-stone` grey are fixed status hex/tokens.
- **Testing**: `tests/Nabadat.E2ETests/ActionManagement/` — `ActionAddEditTests.cs`,
  `AllActionsTests.cs`, `ActionDetailsTests.cs`, `ActionsSettingsTests.cs` (exact files/methods
  the spec already enumerates per user story's E2E Test Coverage blocks).

## 8. Technical Context resolution (feeds `plan.md`)

| Field | Resolution |
|---|---|
| Language/Version | C# / .NET 10 (ASP.NET Core) — backend; TypeScript / React 19 — frontend (constitution Section 1 stack table) |
| Primary Dependencies | EF Core (Npgsql provider, DB-08); `Nabadat.KpiManagement.Application.Kpis.Interfaces.IKpiConfigReader` (direct project reference, existing); M-15-owned `IKpiScoreReader`/`IKpiLifecycleEventReader` ports with a stub default adapter (§4); Vite + React 19 + Tailwind 4 + `@base-ui/react` + shadcn-style components (frontend, per repo CLAUDE.md) |
| Storage | PostgreSQL 16+, tenant schema (`tenant_{slug}`) — new tables `actions`, `kpi_targets`, `action_settings` (§3); no Elasticsearch (this feature's data is not analytics/dashboard-class per AD-04 — it is operational CRUD + computed pace values) |
| Testing | xUnit v3 + FluentAssertions 6.12.\* + NSubstitute 5.\* (unit, `Nabadat.ActionManagement.UnitTests`); Testcontainers Postgres + `WebApplicationFactory` (integration, `Nabadat.ActionManagement.IntegrationTests`); MSTest + `Microsoft.Playwright.MSTest` (E2E, appended to `tests/Nabadat.E2ETests/ActionManagement/`) |
| Target Platform | Kubernetes (SaaS) / Docker Compose (on-prem) — same single codebase, AD-05; browser targets per NFR-9 (last 2 evergreen Chrome/Edge/Firefox/Safari) |
| Project Type | Web application — backend module (`Nabadat.ActionManagement`, hosted inside `Nabadat.TenantAdmin`) + frontend SPA feature (`frontend/src/features/actions/`) |
| Performance Goals | NFR-5: SCR-01 interactive < 2s @ 200 Actions; search/filter feedback < 100ms; slider drag 60fps; server authoritative for evaluation-time facts only, live pace values computed client-side from delivered raw inputs |
| Constraints | Day-granularity, tenant-timezone date comparisons everywhere (BR-022/NFR-8, via injected `TimeProvider` per DB-08 rule 7); cursor-only pagination (API-04); last-write-wins concurrency (spec-ratified exception to Article 7.2, recorded in Complexity Tracking); no new background-worker infrastructure (§4) |
| Scale/Scope | 3 screens (SCR-01/02/03) + 1 Settings subsection; ~10 REST endpoints; 2 new tenant tables + shared `event_log` mapping; 10 user stories, 23 cross-screen business rules, 11 validation rules, 17 measurement-model formulas |

---

**Output**: all Technical Context fields resolved; zero `NEEDS CLARIFICATION` markers remain
(the spec itself carried none; this phase resolved the implementation-level unknowns the spec
deliberately left to architecture review).
