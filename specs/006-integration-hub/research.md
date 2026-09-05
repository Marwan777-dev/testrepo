# Research: M-13 Integration Hub

**Feature**: `006-integration-hub` | **Date**: 2026-07-27

**Source spec**: `specs/006-integration-hub/spec.md` (rev 1.2 + two clarification rounds, 2026-07-27) —
derived from `SRS-M13-Integration-Hub-v1_1.md`, zero `[NEEDS CLARIFICATION]` markers remaining
after 8 clarification questions across two sessions (7 resolved, 1 declined — SRS/prototype
version-provenance, low urgency).

Because the spec itself carries no open clarification markers, this research phase focuses on
**codebase-grounded technical decisions**: which of M-13's six cross-module dependencies
(M-01/M-02/M-04/M-09/M-10, plus the M-14/M-15/M-16 forward consumers) already exist under `src/`
and can be wired for real today, versus which need the now-standard stub/dependency-inversion
treatment established by M-15 and M-01 — and how the constitution's own module registry
reconciles with a naming error inherited from the source SRS.

**Team**: 2 resources this time (down from M-15's 3) — **AbuKr** (backend, solo — covers all 10
user stories' backend work) and **Marwan** (frontend, solo — covers all 8 screens). No second
backend engineer means the backend work is strictly sequential, not split into parallel tracks;
see §1 below for the resulting sequencing.

---

## 1. Team & sequencing context (2 resources, not 3)

- **Decision**: With one backend engineer, sequence strictly by dependency, not by parallel
  vertical slices (unlike M-15's AbuKr/Atia split): **Foundational → US1 (channels) → US2
  (parameters) → US3 (integration wizard) → US4 (inbound processing) → US5 (monitoring/logs) →
  US9 (permissions, cross-cutting, verify early) → US6 (mappings inline) → US7 (bulk
  mappings) → US8 (credential lifecycle) → US10 (activate/deactivate)**. This is close to the
  spec's own priority order (P1: US1-5, P2: US6-9, P3: US10) with one adjustment: US9
  (cross-persona permissions) is pulled forward from P2 into the tail of the P1 phase because
  it's cheap to verify as soon as US1/US3's screens exist (a read-only-rendering check), and
  catching a permission-enforcement gap early is cheaper than finding it after 6 more stories
  are built on top.
- **Rationale**: US1 and US2 are mutually near-independent (parameters are pre-seeded, so a
  channel's contract can reference built-ins immediately; custom parameters are additive) but
  US1 is listed first because it is the feature's true entry point (nothing is testable without
  a channel). US3 needs an Active channel (US1). US4 (the headless runtime) needs a real,
  callable integration (US3) to test against. US5 needs US4 to have generated real traffic to
  monitor. This mirrors the spec's own "Independent Test" narratives almost exactly.
- **Marawan (frontend, solo)**: builds pages in the same order as the backend stories complete
  their contracts — SCR-03/04 (US1) → SCR-05/06 (US2) → SCR-01/02 (US3) → SCR-01 stat tiles +
  SCR-08 (US5) → SCR-07 (US6/US7) → SCR-02 Step 2 credential ops (US8) → cross-persona
  read-only rendering (US9) → status toggles (US10). Unlike M-15, this feature needs **zero
  custom-SVG primitives** (no gauges/zone-sliders/timer-rings) — every SCR-01…08 screen is
  built from existing shadcn primitives (`Table`, `Dialog`, `Sheet`/drawer, `Tabs`, `Badge`,
  `Select`, stepper for the wizard) already available per the repo's Component Sourcing Rule,
  which meaningfully de-risks the frontend estimate relative to M-15.
- **Alternatives considered**: splitting backend by "console CRUD" vs. "headless runtime"
  (mirroring the write-path/read-path split M-15 used) — rejected with only one backend engineer,
  since there's no second person to hand the second track to; the split concept only pays off
  with 2+ backend engineers.

## 2. Domain name & module registration (AMENDMENT-008)

- **Decision**: `Nabadat.IntegrationHub` — derived from the Section-3 registry name
  "Integration Hub" (M-13), following the `Nabadat.UserManagement`/`Nabadat.KpiManagement`/
  `Nabadat.CustomerJourneyManagement`/`Nabadat.SurveyBuilder` naming pattern. No `M13`/`M-13`
  token in any project, namespace, or type name (AMENDMENT-008 rule 1).
- **Project family** (mirrors `Nabadat.SurveyBuilder`, the most structurally similar existing
  module — it also has a public-facing API surface plus an authenticated admin surface):
  - `src/Nabadat.IntegrationHub/Nabadat.IntegrationHub.csproj`
  - `tests/Nabadat.IntegrationHub.UnitTests/`
  - `tests/Nabadat.IntegrationHub.IntegrationTests/`
  - E2E tests append to the existing shared `tests/Nabadat.E2ETests/` project, in a new
    `IntegrationHub/` module folder (mirroring `KpiManagement/`, `CustomerJourneyManagement/`,
    `UserManagement/`, `OrganizationSettings/`) — spec.md's own E2E Test Coverage blocks already
    name this exact path (`tests/Nabadat.E2ETests/IntegrationHub/*.cs`).
  - Register all three new projects in `Nabadat.TenantAdmin.sln` alongside the existing module
    entries.

## 3. Owned-table registry correction (parallels AMENDMENT-011/012 and the M-15 precedent)

- **Finding**: `constitution.md` Section 3 lists M-13's owned tables as the placeholder
  reservation set `api_keys`, `webhook_configs`, `connector_configs`, `integration_log`
  (AD-06/DB-06). None of these four have actually been created in any baseline SQL yet — same
  situation as M-15's `action_plans`/`action_assignments`/`action_progress` before that
  feature's baseline shipped.
- **Decision**: Ship the real schema under names matching spec.md's Key Entities:
  `integrations`, `credentials` (single table, discriminated by `mechanism` — `api_key` |
  `oauth_client` — rather than two tables, since exactly one credential set exists per
  integration at a time and the fields barely diverge), `service_channels`, `parameters`,
  `channel_parameter_assignments`, `parameter_mappings`, `unmapped_value_occurrences` (backs the
  7-day queue, FR-S7-02), and `integration_request_logs` (the high-volume, append-only,
  **DB-04 monthly-partitioned** log — joining the existing partitioned-table list `responses`,
  `delivery_log`, `audit_log`, `notification_log`, `event_log`).
- **Constitution follow-up required**: file an amendment (same shape as AMENDMENT-011/012)
  correcting Section 3's M-13 row and adding `integration_request_logs` to DB-04's partitioned-
  table list — tracked in coordination-log.md, not a plan blocker.
- **Rationale**: identical reasoning to M-15's AD-06 reconciliation — the placeholder names
  don't map onto any real entity in the ratified spec.

## 4. Cross-module dependencies — three real integrations exist today, three need stubs

This is the single most consequential research finding for this feature: **unlike M-15's
all-stub situation, half of M-13's cross-module dependencies already have real, working
counterparts in the codebase.**

### 4.1 M-10 (`Nabadat.UserManagement`) — REAL integration available today (CMC-06)

`src/Nabadat.UserManagement/Application/Permissions/M13ParameterContractAdapter.cs` already
exists, exposing `POST /api/v1/authorization/scope/parameters` — an endpoint that ingests a
batch of `{ name, label, allowedValues }` parameter definitions from an external scope provider,
explicitly named for M-13 in its own doc comment ("a batch of scope parameter definitions pushed
by an external scope provider (M-13)"). This is **not a stub** — M-10 already built its side of
BR-10's "M-10 data-scope filters are built on M-13 parameter definitions and value sets"
contract, waiting for M-13 to be the caller.
- **Decision**: M-13 implements a real outbound call (`IDataScopeContractPublisher` or similar,
  in `Application/Parameters/`) that pushes filterable/mapping-enabled parameters' name, label,
  and known value set (List-type parameters: the mapping table's distinct source values; other
  filterable types: no enumerable value set, so likely excluded from this push — confirmed at
  implementation time against `M13ParameterPayload`'s shape) to M-10's real endpoint whenever a
  parameter or its mappings change. This directly implements BR-10 without any stub.
- **Reconciliation task**: `M13ParameterPayload`'s `SourceModule` field and reserved-name list
  (`ReservedNames`) should be cross-checked against M-13's actual field-naming conventions
  (`snake_case` API field names, BR-11) during implementation to confirm no collision.

### 4.2 M-01 (`Nabadat.SurveyBuilder`) — REAL integration available today, but the SRS mis-named it

- **Finding (naming defect)**: the SRS's CMC-02 calls the survey-definitions/rendering owner
  "M-03 Survey & Forms." Per the constitution's own Module Registry (Section 3), **M-03 is
  "Audience and Contact Management"** — an unrelated module. The actual owner of survey
  definitions and rendering is **M-01 "Survey and Form Builder"** (`Nabadat.SurveyBuilder`,
  which already exists and is fully built). This is a genuine reference defect inherited from
  the source SRS, not a new one introduced by this plan — flagged per constitution §12.2 ("a
  question not answered here is flagged for amendment, not silently resolved") rather than
  silently perpetuated into the architecture.
- **What M-01 actually publishes** (`Domain/Interfaces/ISurveyRenderService.cs`, `AD-01`
  published contract): `GetActiveSurveyDefinitionAsync(SurveyId, LocaleCode, ct) →
  SurveyDefinition?` — the exact shape SCN-03 (JSON render) needs to relay to a caller, already
  built and already consumed by two other modules (M-02, M-04) for the same purpose. M-13
  becomes a third legitimate consumer.
- **Decision**: M-13 takes a direct project reference to `Nabadat.SurveyBuilder` (mirroring how
  M-01 itself already depends on other modules' published interfaces) and calls
  `ISurveyRenderService.GetActiveSurveyDefinitionAsync` for real, for SCN-03, once a `SurveyId`
  has been resolved (see §4.3 — resolution itself is still a stub, since it's M-02's job).
  Document this in `contracts/published-interfaces.md` as a **consumed** interface, with the
  spec's "M-03" language annotated as a corrected reference to M-01.

### 4.3 M-02 (Channels & Distribution) — does not exist; M-13 needs two stub ports

`Nabadat.SurveyBuilder`'s own `IChannelSurveyRulesReader` doc comment independently confirms:
"the concrete implementation is supplied by M-02 (which does not exist under `src/` yet)."
M-13's CMC-01 depends on M-02 for two distinct things:
1. **Survey resolution** — "which survey applies" for a given channel + transaction parameters
   (BR-19, all five scenarios need this before they can call M-01's `ISurveyRenderService` or
   dispatch anything). M-13 owns this port (`ISurveyResolutionReader` or similar,
   `Domain/Interfaces/`), ships a deterministic stub (`NullSurveyResolutionReader` — always
   returns "no survey resolved," surfaced as a clear internal-error/not-configured state rather
   than a silent wrong-survey dispatch) until M-02 ships.
2. **Dispatch hand-off** — SCN-01's actual "send the survey through the suitable channel" step.
   M-13 owns this port too (`ISurveyDispatchGateway` or similar), stubbed the same way.
- **Decision**: both ports live in `Nabadat.IntegrationHub`'s own `Domain/Interfaces/`, following
  the exact dependency-inversion shape already established twice in this codebase (M-15's
  `IKpiScoreReader`/`NullKpiScoreReader`, M-01's `IChannelSurveyRulesReader`/no-op-stub-returning-0).
  Every SCN-01/02/03/04 test in User Story 4 that needs a resolved survey runs against the stub's
  deterministic "not resolved" response until C-XX (tracked in coordination-log.md) ships.

### 4.4 M-04 (Response Collection) — does not exist; confirms the "must save unconditionally" clarification's cost

- **Finding**: no `Nabadat.ResponseCollection`-style project exists; M-01's `ISurveyRenderService`
  doc comment ("Consumed by... M-04 (Response Collection) at response-start time") confirms M-04
  is anticipated but unbuilt, same as M-02.
- **Decision**: M-13 owns an outbound port (`IResponseIngestionGateway` or similar,
  `Domain/Interfaces/`) for SCN-05's hand-off, stubbed until M-04 ships. Today's clarification
  (M-04 must save every forwarded payload unconditionally, no silent-drop path, new **SC-016**)
  is recorded as a contract requirement on this port's real future implementation — the stub
  itself trivially satisfies it (it either always succeeds deterministically in tests, or the
  test harness asserts the call was made with the exact payload, since there is no real M-04 to
  verify durability against yet).

### 4.5 SCN-04's public, unauthenticated rendering endpoint — owned by neither M-13 nor M-01's authenticated API

- **Finding**: today's clarification session established a two-step flow for SCN-04 (M-13's
  authenticated call returns a short-lived embed URL; the browser separately loads that URL from
  an unauthenticated, origin-checked-only rendering endpoint). `Nabadat.SurveyBuilder`'s only
  public-facing render surface found (`SurveyRenderPlanController`) is `[Authorize]`-protected —
  it is an authenticated **admin diagnostics** endpoint, not the actual respondent-facing survey
  renderer. Per the platform constitution's Section 1 stack table, the actual respondent-facing
  surface is a **separate "Survey renderer" React/Preact frontend** (Zone 1, no business logic),
  fed by M-04's (not-yet-built) public response-collection API — consistent with M-04 owning
  "response collection" broadly, including serving the respondent UI, not just ingesting answers.
- **Decision**: M-13's SCN-04 implementation is limited to (a) resolving the survey (§4.3's
  stub), (b) constructing a correctly-shaped, short-lived, signed embed URL that points at the
  platform's existing (or future) public survey-renderer surface, and (c) exposing the
  Allowed-Origins whitelist for that public surface to enforce. **M-13 does not build or own the
  actual rendering page** — that page's existence and origin-enforcement are a dependency on the
  public survey-renderer frontend / M-04, tracked in coordination-log.md, not built by this
  feature. This narrows M-13's SCN-04 scope considerably versus a naive reading of the SRS.

### 4.6 M-09 (Notifications) — does not exist; exact established stub pattern reused

- **Finding**: `Nabadat.UserManagement` already ships the exact pattern needed —
  `Domain/Interfaces/IM09NotificationService.cs` (a minimal consumer-side port) +
  `Infrastructure/Notifications/UnavailableM09NotificationService.cs` (throws, causing the
  caller's own operation to fail closed rather than silently swallowing the notification).
- **Decision**: M-13 needs no notification port at all for Phase 1 — CMC-05/INT-03-equivalent
  scope explicitly limits M-13 to "only logs failures" (SCR-08), with zero email/push/in-app
  alerting. If a future phase adds M-09 integration, it would mirror this exact
  `IM09NotificationService`/`UnavailableM09NotificationService` shape — noted here only so a
  future developer doesn't reinvent it.

### 4.7 M-14 / M-15 / M-16 (CMC-07) — M-13 is the published side, no consumer exists yet

- **Decision**: mirrors M-15's own `IActionOverlayReader` forward-contract pattern exactly: M-13
  publishes `IParameterCatalogReader` (or similar) in `Domain/Interfaces/` now, so a future
  M-14/M-15/M-16 rule/action/journey builder has something to consume without an M-13 code
  change later. No real consumer exists yet (M-15 exists but its rule/data-scope needs are
  currently served by M-10 directly per §4.1, not M-13 — M-13's parameters feed M-10's
  data-scope system, which M-15/M-14/M-16 already consume via M-10, so this port may end up
  unused directly; documented as a skeleton only, same as M-15's `IActionOverlayReader`).

## 5. API surface: reconciling spec's proposed shapes with binding constitution rules

Identical set of corrections as M-15's plan required (research.md precedent), reapplied here:

- **Versioning (API-01)**: `/api/v1/integration-hub/...` for the console CRUD surface. The five
  inbound-scenario endpoints (SCN-01…05) are **illustrative only** per the spec's own FR-F0-01
  note — final paths fixed at implementation, still under `/api/v1/`.
- **Pagination (API-04)**: every SCR-01/03/05/07/08 list uses cursor pagination (`page_size`
  default 50/cap 200, `page_token`), never the bare "paginate beyond 50 rows" phrasing FR-GBL-01
  implies.
- **Error envelope (API-05)**: `{ error: { code, message, correlation_id, tenant_id } }` for the
  **console** API. The **inbound scenario API's** result-code catalogue (F0.3) is a distinct,
  narrower, caller-facing envelope by explicit design (`{ result_code, message, request_id }`-
  shaped, per the spec's own message-copy examples) — these are two different envelopes for two
  different audiences (tenant console vs. external caller systems), not a contradiction to
  resolve.
- **Permission declaration (API-03)**: every console endpoint declares `required_permission`
  from the Permissions Matrix's `m13.*` keys (already canonical, spec-defined) plus
  `required_scope: organisation` and `default_personas` mapped to constitution's **P-07**
  (Tenant IT Administrator — exact match) and **P-01** (CX Program Manager — spec calls it "CX
  Manager," same ID per spec's own Assumptions reconciliation note).
- **Concurrency (NFR-17)**: last-write-wins, no `ETag`/`If-Match` — same documented Article-7.2
  exception shape as M-15's Complexity Tracking entry (this spec's NFR-17 explicitly ratifies it,
  so it's not a fresh violation, just a repeat of an already-accepted pattern).

## 6. Cross-module coordination log

Filed as `specs/006-integration-hub/coordination-log.md`:

- **C-01 — M-02 (Channels & Distribution)**: needed for survey resolution (all scenarios, BR-19)
  and SCN-01 dispatch hand-off. PENDING — module does not exist under `src/`. M-13 ships both
  stub ports described in §4.3.
- **C-02 — M-04 (Response Collection)**: needed for SCN-05 hand-off and the "must save
  unconditionally" guarantee (SC-016), plus the actual respondent-facing SCN-04 public render
  surface. PENDING — module does not exist. M-13 ships the stub port in §4.4; the SCN-04 public
  render page itself is entirely out of this feature's build scope (§4.5).
- **C-03 — M-13 owned-tables registry correction**: file an amendment once
  `IntegrationHub_Baseline.sql` ships, correcting Section 3 and adding `integration_request_logs`
  to DB-04's partitioned-table list.
- **C-04 — SRS naming defect (M-03 → M-01)**: the source SRS's CMC-02 names "M-03 Survey &
  Forms" when the real owner is M-01 (`Nabadat.SurveyBuilder`). No constitution change needed
  (the constitution is already correct); flagging so nobody builds a phantom "M-03" dependency
  for this feature, and so a future SRS revision corrects the label.
- **C-05 — M-09 (Notifications)**: no action needed for Phase 1 (explicitly zero-scope);
  documented only so a future phase reuses the established stub pattern rather than reinventing
  it.
- **C-06 — M-14/M-15/M-16 forward contract**: M-13 ships `IParameterCatalogReader` as a
  forward-only published skeleton (§4.7); no consumer to wire yet.

## 7. Frontend integration points

- **Route registration**: `frontend/src/features/integration-hub/` (new feature folder,
  mirroring `kpi-management`/`journeys`/`settings`/`actions` — `components/`, `hooks/`, `pages/`).
  Routes in `frontend/src/App.tsx`: `/integration-hub/integrations`,
  `/integration-hub/integrations/new`, `/integration-hub/integrations/:id` (SCR-01/02),
  `/integration-hub/service-channels`, `.../new`, `.../:id` (SCR-03/04),
  `/integration-hub/parameters` (SCR-05, SCR-06 opens as a drawer over it, no separate route),
  `/integration-hub/mappings` (SCR-07), `/integration-hub/logs` (SCR-08).
- **No custom-SVG primitives needed** (see §1) — every widget (stat tiles, status badges, the
  3-step wizard stepper, the parameter-contract table with Supported→Required dependency, the
  Excel import dialog) is buildable from existing shadcn primitives already in
  `frontend/src/components/ui/`, per the repo's Component Sourcing Rule. This is the single
  biggest frontend-effort difference versus M-15.
- **Sidebar**: per CLAUDE.md's "categorize, don't append" rule, add an "Integration Hub" nav
  group (or fold into an existing "Platform"/"Integrations" category if one exists) with its two
  sub-groups mirrored from the spec's own Navigation Overview (Inbound integrations: Integrations,
  Request logs; Data model: Service channels, Parameters, Parameter mappings) — update
  `ROLE_NAV_KEYS` for P-01 and P-07's differing visibility per BR-24.
- **Testing**: `tests/Nabadat.E2ETests/IntegrationHub/` — `ServiceChannelTests.cs`,
  `ParameterCatalogueTests.cs`, `IntegrationWizardTests.cs`, `IntegrationMonitoringTests.cs`,
  `RequestLogsTests.cs`, `ParameterMappingsTests.cs`, `CrossPersonaPermissionsTests.cs` (exact
  file names already specified in spec.md's E2E Test Coverage blocks per story).

## 8. Technical Context resolution (feeds `plan.md`)

| Field | Resolution |
|---|---|
| Language/Version | C# / .NET 10 (ASP.NET Core) — backend; TypeScript / React 19 — frontend (constitution Section 1 stack table) |
| Primary Dependencies | EF Core (Npgsql provider, DB-08); direct project reference to `Nabadat.SurveyBuilder` for `ISurveyRenderService` (real, §4.2); direct HTTP call to `Nabadat.UserManagement`'s real `POST /api/v1/authorization/scope/parameters` (§4.1); 4 new M-13-owned stub ports for M-02/M-04 (§4.3/4.4); Vite + React 19 + Tailwind 4 + `@base-ui/react` + shadcn (frontend, per repo CLAUDE.md); `ClosedXML` or equivalent for SCR-07 Excel import/export (VR-F09, FR-S7-05/06) |
| Storage | PostgreSQL 16+, tenant schema — new tables `integrations`, `credentials`, `service_channels`, `parameters`, `channel_parameter_assignments`, `parameter_mappings`, `unmapped_value_occurrences`, `integration_request_logs` (§3); `integration_request_logs` is DB-04 monthly-partitioned (high-volume, like `responses`/`event_log`); no Elasticsearch (operational CRUD + request logging, not analytics — AD-04 scope) |
| Testing | xUnit v3 + FluentAssertions 6.12.\* + NSubstitute 5.\* (unit, `Nabadat.IntegrationHub.UnitTests`); Testcontainers Postgres + `WebApplicationFactory` (integration, `Nabadat.IntegrationHub.IntegrationTests`); MSTest + `Microsoft.Playwright.MSTest` (E2E, appended to `tests/Nabadat.E2ETests/IntegrationHub/`) |
| Target Platform | Kubernetes (SaaS) / Docker Compose (on-prem) — same codebase, AD-05; browsers per NFR-14 |
| Project Type | Web application — backend module (`Nabadat.IntegrationHub`, hosted inside `Nabadat.TenantAdmin`) + a **headless inbound API surface** (no admin-console screen, Feature 0) + frontend SPA feature (`frontend/src/features/integration-hub/`) |
| Performance Goals | NFR-1 (99.9% monthly API availability), NFR-2 (95% of API requests < 500ms excl. downstream), NFR-4 (100 req/s default per-integration rate limit, Operations-configurable) |
| Constraints | TLS 1.2+ everywhere (NFR-5); 2MB payload cap (NFR-3); no fixed idempotency retention window (BR-18, today's clarification — accepted limitation, not engineered); last-write-wins concurrency (NFR-17, same Article-7.2 exception shape as M-15); PII masking in all log views/exports with zero unmasked-access code paths (NFR-9/FR-S8-03); cursor-only pagination (API-04) |
| Scale/Scope | 8 screens (SCR-01…08) + 1 headless feature (Feature 0, 5 scenarios); ~20+ REST endpoints across console CRUD + 5 inbound scenario endpoints; 8 new tenant tables; 10 user stories, 27 cross-screen business rules (BR-01…27), 25 validation rules (13 data-type + 13 field-level, one added this session — VR-F13), 17 NFRs, 7 cross-module contracts |

---

**Output**: all Technical Context fields resolved; zero `NEEDS CLARIFICATION` markers remain (the
spec itself carried none after its two clarification rounds; this phase resolved the
implementation-level unknowns the spec deliberately left to architecture review, and — unlike
M-15 — found that 2 of 6 cross-module dependencies (M-01, M-10) are real, working integrations
available today, not stubs).
