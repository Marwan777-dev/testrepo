# Feature Specification: M-13 Integration Hub

**Feature Branch**: `006-integration-hub`

**Created**: 2026-07-27

**Status**: Draft (rev 1.2 — derived from SRS v1.3, 27 Jul 2026)

**Rev 1.1 changes** (audit remediation, 27 Jul 2026): (1) permission consistency per PO ruling — Request Logs are exclusive to P-07, P-01 has no log access (BR-24 erratum applied end-to-end: narrative, acceptance scenarios, tests); (2) functional UI detail restored per the preserve/omit rule — defaults, placeholders, helper text, card/alert/dialog copy, and shipped message strings that drive behaviour, validation, guidance, and generated tasks; purely visual/design-system details remain intentionally excluded; (3) three consistency corrections from the coverage audit (integration-inactive rejection code restored to the SRS value, AC-S3-01 added, Duration/Identifier exclusion made explicit). No scope change; no new functionality.

**Input**: User description: "Build M-13 Integration Hub, a module of the multi-tenant Nabadat Voice-of-Customer SaaS platform, consisting of an admin console of eight screens plus a headless inbound API runtime, exactly as specified in SRS-M13-Integration-Hub-v1_3.md *(filename updated at rev 1.2 to the current SRS version; the original invocation referenced v1_1)*, which is the normative single source of truth: every FR, BR, VR, NFR, AC, and CMC identifier in that document must be satisfied, and the ratified prototype M13-Integration-Hub-Prototype-v0.5-Ratified.html is the binding UI baseline for layout, labels, and copy. [... full description as provided by the user in the `/speckit-specify` invocation, covering: integration points (5 scenarios), authentication (API key / OAuth 2.0), service channels, the 23-parameter built-in catalogue plus custom parameters, parameter mappings with read-time resolution, request logs with PII masking, console-wide behaviours, capacity guardrails, and cross-module ownership boundaries.]"

**Source SRS**: `SRS-M13-Integration-Hub-v1_3.md` v1.3 (27 Jul 2026, Status: Implementation-ready — single source of truth for SpecKit)

**Rev 1.2 changes** (PO update round 4 + final-audit editorial items, 27 Jul 2026): **G-25** mapping capability by data type (List — always on, not changeable; Text/Boolean/URL — optional, off by default; other types — unavailable; new BR-27) · **G-26** the *Searchable* usage flag removed (five flags remain) · **G-27** built-in parameter data type is read-only · **G-28** extra (unregistered) parameters never block processing and are reported in the request logs (reaffirms BR-14 — no behaviour change). Editorial: Input filename updated to the current SRS version; dangling section pointer corrected. UI baseline advanced to prototype v0.5. No other change.

**Referenced prototype**: `M13-Integration-Hub-Prototype-v0.5-Ratified.html` — **not present in this repository at spec-writing time**. This spec preserves inline every prototype-ratified detail that affects **functional behaviour, interaction, navigation, validation, workflow, or user guidance** (fields, defaults, placeholders, helper text, alerts, dialog copy, message strings), carried under the `[UI]` / `[Derived from UI]` tags. **Purely visual/design-system details (colours, icons, typography, spacing, styling, decorative chrome, theme toggle) are intentionally excluded**; they remain normative in the SRS's `[UI]`-tagged sections and the Nabadat design system, which implementation must consult for visual treatment. If the prototype file is later added to the repo, it confirms (never supersedes) the content captured here and in the SRS.

**Module code**: M-13 (Nabadat VOC Platform, Phase 2 — constitution Section 3 registry; owned-table placeholders `api_keys`, `webhook_configs`, `connector_configs`, `integration_log` are reservation names only, per AD-06/DB-06, and are expected to be corrected to real table names at planning time, mirroring the AMENDMENT-011/012/M-15 precedent)

**Traceability convention** (mirrors SRS "Sources & traceability tags"):

| Tag | Meaning |
|---|---|
| `[BR]` | Explicit business requirement (M-13 business brief) |
| `[PO-Gxx]` | Ratified Product Owner decision (gap register G-01…G-24 — see SRS *Decision References*) |
| `[PO]` | Other ratified PO input (permissions, contracts, NFRs, validation, security, migration) |
| `[UI]` | Behaviour explicitly present in the ratified prototype |
| `[Derived from UI]` | Behaviour inferred from the prototype, not explicitly written elsewhere |
| `[Formalized default]` | Former assumption converted into a formal requirement/rule in SRS v1.1 |

Conflict rule (preserved from the SRS): explicit business requirements and ratified PO decisions override prototype appearance.

---

## Overview

M-13 Integration Hub is the inbound edge of the Nabadat platform. It lets a tenant's **Tenant IT Administrator** (persona **P-07**) expose authenticated APIs through which backend source systems raise survey requests in one of five interaction scenarios, and lets the **CX Manager** (persona **P-01**, called "CX Program Manager" in the platform's canonical persona registry) govern the transaction data model those APIs accept: service channels, the parameter catalogue, per-channel parameter contracts, and source-value → display-value mappings. Every inbound request is logged with all parameters received and the response returned. `[BR]`

The module ships an **admin console of eight screens** (SCR-01…SCR-08) plus a **headless inbound API runtime** (Feature 0 — no screen of its own; it is the request-processing engine every integration's endpoint runs on).

**In scope** (SRS "Scope"):
1. Integration-point management: create, edit, activate/deactivate; one name, one service channel, exactly one scenario per integration. `[BR]` `[PO-G02]`
2. Five integration scenarios (SCN-01…SCN-05).
3. Caller authentication: API Key or OAuth 2.0 client-credentials, mechanism-specific configuration, show-once credential generation, API-key revocation. `[BR]` `[PO]`
4. System-built APIs where the service channel ID is the only mandatory (path) parameter; all other data travels as free key–value pairs. `[BR]`
5. Service-channel management: bilingual names, manually entered channel ID, description, status, per-channel parameter contract. `[BR]` `[PO-G03]`
6. Parameter management: 23 built-in parameters, custom parameters, enable/disable, five usage flags (Searchable removed, `[PO-G26]`), 13 data types (built-in types read-only, `[PO-G27]`), mapping capability per data type (`[PO-G25]`), validation rules, channel assignment.
7. Parameter mapping: source value → bilingual display value, manual + bulk Excel import/export, replace-all.
8. Request logs: every request logged with PII masking and 90-day retention. `[BR]` `[PO-G14]`
9. Request validation pipeline, normative result-code catalogue, per-integration rate limiting, idempotent retries, link expiry, iFrame origin whitelisting.
10. Audit events for all configuration changes.

**Descoped by ratified decision**: mapping version history (removed entirely — the platform audit trail is the sole change record, `[PO-G12]`).

**Explicitly out of scope for v1** (owned elsewhere, per the SRS's Cross-Module Contracts):
- Survey dispatch, delivery-channel selection, sending, retries, cadence — **M-02 Channels & Distribution**. M-13 hands off accepted dispatch requests and stops.
- Survey definitions and rendering payloads (JSON/iFrame) — **M-03**. M-13 retrieves and relays them, treating the schema as opaque.
- Response validation, deduplication, and storage — **M-04**. M-13 forwards ingestion payloads.
- Reporting/analytics consumption of transaction metadata — **M-06 / M-07**.
- Operational alerting — **M-09**, deferred; Phase 1 only logs failures.
- User, role, and permission administration — **M-10** (M-13 registers its permission keys and consumes authorisation).
- **Trigger rules** (rule-based eligibility/sampling of incoming transactions) — a **deferred capability**; Phase 1 has no trigger-rule engine, so every request that passes validation is processed (BR-01).
- **Mapping version history** — permanently descoped; the audit trail is the only change record (`[PO-G12]`).
- **Legacy migration** — M-13 is a greenfield implementation. No migration of legacy configurations (parameters, mappings, channels, integrations, rules), no migration utilities, no backward-compatibility requirements.

**Key terms** (imported from the SRS Glossary, for standalone readability):

| Term | Definition |
|---|---|
| Integration (point) | A named, tenant-scoped API configuration serving exactly one service channel through exactly one integration scenario, with its own credentials and request logs. |
| Integration scenario | One of the five normative interaction patterns SCN-01…SCN-05. |
| Caller / source system | The tenant backend that invokes an M-13 API (core bus, CRM, mobile backend, queue system…) — a non-human actor. |
| Service channel | The business channel a transaction came through (portal, app, counter, call center). **Not** a distribution channel (WhatsApp/SMS/email — those belong to M-02). |
| Channel ID | The manually entered, URL-safe identifier of a service channel; the only mandatory path parameter of every M-13 API; locked after the channel's first successful request. |
| Parameter | The definition of one transaction data field. Origins: **built-in** (platform-shipped, 23 total), **custom** (tenant-created), **unregistered** (received on the wire without a definition). |
| Parameter contract | The per-channel set of supported parameters and its required subset; the runtime authority on requiredness. |
| Mapping | A translation entry `source value → display value (EN, AR)` for a mapping-enabled parameter, resolved at read time. |
| Unmapped value | A received value of a mapping-enabled parameter with no mapping entry: stored raw, displayed raw, queued for P-01. |
| Unregistered parameter | A received key–value pair with no parameter definition: stored raw, visible only in request logs, excluded from analytics until registered. |
| Read-time resolution | Rendering rule: display values are looked up in the current mapping table whenever data is read, so mapping changes relabel historical data retroactively by design. |
| Show-once | Credential secrets are displayed a single time at generation and are never retrievable afterwards. |
| Idempotent retry | A repeat request with the same tenant + channel ID + `transaction_id`; safe by design. |
| Trigger rules | In Phase 1: the fixed behaviour that every validated request is processed; rule-based eligibility/sampling is deferred. |
| Tenant | An isolated customer instance; all M-13 configuration and data are tenant-scoped. |

---

## Clarifications

### Session 2026-07-27

- Q: What should happen when a customer follows an expired SCN-02 survey link, given F0.3's result-code catalogue has no code for it? → A: Out of scope for M-13 — expiry enforcement on link-click happens at the survey-serving layer (M-02/M-03), not as an M-13 API result code; M-13's obligation ends at issuing `survey_url` + `expires_at` when the link is created.
- Q: BR-26 names OAuth scopes for only 3 of the 5 scenarios (`survey-requests:write` SCN-01, `responses:write` SCN-05, `survey-links:read` SCN-02) — what scopes gate SCN-03 (JSON render) and SCN-04 (iFrame embed)? → A: Two more named scopes following the same `‹resource›:‹verb›` convention: `survey-definitions:read` (SCN-03) and `survey-embed:read` (SCN-04) — five scopes total, one per scenario.
- Q: NFR-16 sets capacity guardrails (≤200 custom parameters, ≤100 channels, ≤200 integrations per tenant) but never states what happens when a tenant tries to exceed one — what's the enforcement behavior? → A: Console-side validation error only (same pattern as every other uniqueness/limit check, e.g. VR-F01…F12): creation is blocked with an inline error naming the limit; no new inbound-API result code, since these are console-created configuration entities, not caller-facing API traffic.
- Q: Is mapping source-value uniqueness (VR-F08) case-sensitive or case-insensitive? → A: Case-insensitive, matching the VR-F01/VR-F04 pattern used elsewhere in the spec (integration name, channel ID). `S001` and `s001` are the same source value; read-time resolution (F0.5) performs the same case-insensitive lookup so an incoming value matches its mapping regardless of casing.
- Q: FR-F0-02 requires header-based authentication on every scenario, but SCN-04's embed URL is loaded directly by a browser `<iframe>`, which cannot attach a custom auth header — how do these reconcile? → A: Two-step flow. The caller's backend calls M-13's authenticated `GET .../survey-embed/{channelId}` endpoint to obtain a short-lived, pre-authorized embed URL; the browser's `<iframe src>` then loads that returned URL against a separate, origin-checked-only rendering endpoint owned by M-03 (CMC-02) — not M-13's authenticated API. The Allowed-Origins whitelist is configured per-integration in M-13 (FR-S2-10) but enforced at M-03's render endpoint, where the browser's request actually lands.
- Q: When M-04 rejects a SCN-05 payload after M-13 already returned `202`, what happens to that outcome? → A: This scenario must not occur — no limitation on responses at all; every SCN-05 payload that passes M-13's own validation pipeline (FR-F0-02) must be saved by M-04 unconditionally. M-04 has no discretionary rejection path that could silently drop an already-accepted response; "delivered-to-M-04" and "durably stored" are guaranteed equivalent for any payload M-13 forwards.
- Q: How long does the BR-18/F0.7 idempotency guarantee hold — unbounded, a fixed window (e.g. 24h), or tied to log retention? → A: No limitation — no fixed retention window is imposed or guaranteed; a retry submitted after a long-enough delay may be processed as a new request, producing a duplicate survey dispatch (SCN-01/02) or duplicate stored response (SCN-05). This is an accepted, known limitation of the idempotency guarantee, not a defect requiring a bounded-forever index.

---

## User Scenarios & Testing *(mandatory)*

> **Governance note.** Every acceptance criterion, business rule, validation rule, and result code below is quoted or closely paraphrased from the source SRS and preserves its ID (`FR-*`, `BR-*`, `VR-*`, `NFR-*`, `AC-*`, `CMC-*`, `D-*`) so it remains traceable. Priorities (P1/P2/P3) below are this spec's own ordering of the SRS's already-ratified requirements into independently-testable increments — they do not descope anything; every requirement in the SRS is in scope for v1.

### User Story 1 — Define a Service Channel and its Parameter Contract (Priority: P1) 🎯 MVP

**Persona**: CX Manager (P-01).

A CX Manager creates a service channel — the business point of contact a transaction came through (e.g. a self-service kiosk, a call center) — giving it bilingual EN/AR names, a manually typed channel ID, an optional description, and a status. They then configure which of the (already-seeded) 23 built-in parameters this channel's backend can send (**Supported**) and which of those are mandatory (**Required**). This is the foundational configuration step: nothing else in the module is testable without at least one service channel to attach an integration to.

**Why this priority**: every downstream story (creating an integration, sending a request, viewing logs) needs a channel to exist first. It is the true entry point of the module.

**Independent Test**: Open `/integration-hub/service-channels` → **New service channel** → type EN/AR names, type a channel ID (e.g. "My kiosk #1" — verify live sanitisation strips it to "Mykiosk1", capped under 20 chars) → toggle **Supported** on a few built-in parameters, tick **Required** on a subset → **Create channel** → the channel appears in the SCR-03 list with correct supported/required counts and an Active badge.

**Acceptance Scenarios**:

1. **AC-S4-01** — **Given** the channel-ID field, **When** "My kiosk #1" is typed, **Then** the field contains only letters/digits/hyphens (e.g. "Mykiosk1") and never exceeds 19 characters (`maxlength`), matching VR-F04.
2. **AC-S4-02** — **Given** a channel with one 2xx request already logged against it, **When** SCR-04 opens in edit mode, **Then** the channel-ID field is read-only with the lock explanation (BR-05, FR-S4-02).
3. **AC-S4-03** — **Given** a contract row with **Supported** on and **Required** ticked, **When** **Supported** is toggled off, **Then** **Required** clears and disables, and the live contract-summary alert's count drops accordingly (FR-S4-03/04).
4. **Duplicate name/ID blocked** — **Given** an existing channel named "Self-Service Kiosk" or ID `KIOSK-01`, **When** a second channel reuses either (case-insensitively for the ID, VR-F04), **Then** save is blocked with an inline uniqueness error (VR-F02/F04).
5. **Deactivation cascade** — **Given** an Active channel with serving integrations, **When** it is set Inactive, **Then** those integrations' endpoints reject calls with `E-1004` within 60 seconds, and the channel disappears from the SCR-02 channel-select for new integrations, while historical data and logs remain (BR-07).
6. **No delete, ever** — **Given** any service channel, **When** the row actions render, **Then** no delete control exists anywhere in the UI (BR-07, FR-S3-02); a channel that has ever received traffic can never be removed, only deactivated.
7. **AC-S3-01** — **Given** 5 channels of which 1 inactive, **When** the list renders, **Then** counts and badges match the channel records and no delete action is offered.

**Unit Test Coverage**:

- **Units under test**:
  - `ChannelIdSanitizer` — strips characters outside `[A-Za-z0-9-]` live as typed; enforces the 19-character input cap (VR-F04).
  - `ChannelIdUniquenessValidator` — case-insensitive uniqueness per tenant (VR-F04).
  - `ChannelIdLockGuard` — read-only once the channel has logged its first 2xx request (BR-05); rejects edit attempts on a locked ID server-side even if a stale client sends one.
  - `ParameterContractDependencyRule` — `Required` may only be `true` when `Supported` is `true`; clearing `Supported` force-clears `Required` (FR-S4-04).
  - `ChannelNameValidator` — EN ≤ 50 chars required + unique per tenant (VR-F02); AR required (VR-F03).
- **Required cases**:
  - `Sanitize("My kiosk #1")` → `"Mykiosk1"` (spaces and `#` stripped, case preserved) (VR-F04).
  - `Sanitize(19 valid chars + 1 more)` → truncated to 19 chars.
  - `Validate(existingIds=["KIOSK-01"], id="kiosk-01")` → `Invalid("A channel with this ID already exists")` (case-insensitive, VR-F04).
  - `IsLocked(channel, hasLoggedSuccessfulRequest=true)` → `true`; a subsequent `PUT` changing `channelId` → rejected server-side.
  - `IsLocked(channel, hasLoggedSuccessfulRequest=false)` → `false`; `channelId` editable.
  - `ApplyDependency(supported=false, required=true)` → `(supported=false, required=false)`.
  - `ApplyDependency(supported=true, required=false→true)` → `(supported=true, required=true)` (allowed only while supported).
  - `Validate(nameEn="", nameAr="جيد")` → `Invalid("Channel name · EN is required")` (VR-F02).

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/v1/integration-hub/service-channels` — create with valid EN/AR names + ID + contract rows → 201; audit event `channel.created`.
  - `POST .../service-channels` — duplicate ID case-insensitive → 409 with the uniqueness message.
  - `PUT .../service-channels/{id}` — edit channel ID before first success → 200, endpoint path changes; edit attempt after first 2xx → 409 (ID locked).
  - `PUT .../service-channels/{id}` — set `active=false` → 200; a serving integration's endpoint subsequently returns `409 E-1004` (cross-story assertion, see US4).
  - `GET /api/v1/integration-hub/service-channels` — list reflects supported/required/integration counts.
- **What's intentionally NOT covered end-to-end**: `ChannelIdSanitizer` (client-side live-typing behaviour), `ParameterContractDependencyRule` — covered by unit tests.

**Scenario Test**: `scenario-test: not-needed — channel create/edit is a single-endpoint operation per acceptance scenario; the deactivation-cascade scenario is asserted end-to-end in US4's scenario test instead (cross-story dependency), not duplicated here.`

**E2E Test Coverage** (frontend SPA):

- **User flows under test**: `/integration-hub/service-channels` (SCR-03 list), `/integration-hub/service-channels/new` and `…/:id` (SCR-04 create/edit) → `ServiceChannelTests.cs` in `tests/Nabadat.E2ETests/IntegrationHub/`.
- **Required scenarios**:
  - `ServiceChannel_sanitizes_id_live_as_typed_and_caps_at_19_chars` — AC-S4-01.
  - `ServiceChannel_locks_id_field_after_first_successful_request` — AC-S4-02.
  - `ServiceChannel_required_toggle_disables_when_supported_is_off` — AC-S4-03.
  - `ServiceChannel_blocks_save_on_duplicate_name_or_id` — VR-F02/F04.
  - `ServiceChannel_list_shows_no_delete_action_anywhere` — BR-07.
  - `ServiceChannel_it_admin_sees_read_only_view` — BR-24 (P-07 read-only visibility, cross-checked with US9).

---

### User Story 2 — Manage the Parameter Catalogue (Priority: P1)

**Persona**: CX Manager (P-01).

A CX Manager governs the tenant's parameter catalogue: the 23 built-in parameters (all enabled by default) that ship with the platform, plus any tenant-specific custom parameters they create. Each parameter carries bilingual names, a locked-after-first-use `snake_case` API field name, one of thirteen data types (with Range's min/max/unit sub-configuration), an optional validation rule, five usage flags (Searchable removed `[PO-G26]`; Mapping support per BR-27), and channel assignments.

**Why this priority**: the parameter catalogue is what a service channel's contract (US1) and an integration's accepted-parameters preview (US3) are built from; while the 23 built-ins ship pre-seeded, most tenants will need at least one custom parameter before their integration is truly useful.

**Independent Test**: Open `/integration-hub/parameters` → verify the "All · 23" tab shows every built-in enabled → **New parameter** → type an EN name "Wait Time" (verify API field auto-suggests `wait_time`) → select type **Range**, set min/max/unit → set usage flags → assign to a channel → **Create parameter** → the new Custom parameter appears in the list with its flags rendered as check/dash glyphs.

**Acceptance Scenarios**:

1. **AC-S6-01** — **Given** type is switched from Range to List, **When** changed, **Then** the Range card hides and the List panel (pointing to Parameter Mappings) shows, and vice versa.
2. **AC-S6-02** — **Given** EN name "Wait Time" is typed, **When** typing completes, **Then** the API field auto-suggests `wait_time` (lowercased, spaces → `_`, invalid chars stripped) and remains manually editable **before** first use.
3. **AC-S6-03** — **Given** an API field name that already exists (even on a disabled parameter or a built-in), **When** saving, **Then** save is blocked with an inline uniqueness error (VR-F06).
4. **AC-S5-01** — **Given** tab "Custom" + type filter "Range" are both applied, **When** the list renders, **Then** only custom Range parameters remain, while the tab counts stay global (unaffected by the type filter).
5. **AC-S5-02** — **Given** the Enabled toggle on a parameter (e.g. `service`) referenced by a channel contract, **When** switched off, **Then** the impact warning (Dialog D-6) lists that reference before anything changes.
6. **Built-ins are never deleted or renamed** — **Given** any built-in parameter, **When** its row renders, **Then** no delete action exists and the API field name is permanently read-only (BR-09, VR-F06).
7. **Range validation** — **Given** a Range parameter with Min = 100 and Max = 50, **When** saved, **Then** save is blocked with "Minimum must be less than Maximum" (VR-F07).

**Unit Test Coverage**:

- **Units under test**:
  - `ApiFieldNameSuggester` — derives `snake_case` from the EN name (lowercase, spaces → `_`, strip non-alphanumerics) (SCR-06 field rules).
  - `ApiFieldNameUniquenessValidator` — unique per tenant across built-in + custom + enabled + disabled (VR-F06).
  - `ApiFieldNameLockGuard` — locked once the first request carrying it has been received (BR-11).
  - `RangeConfigValidator` — Min required, Max required, Min < Max (VR-F07).
  - `ParameterDisableImpactScanner` — finds every M-10 data-scope filter, rule builder, and channel contract referencing a parameter (BR-10); returns the reference list for Dialog D-6.
  - `BuiltInParameterGuard` — rejects delete/rename attempts on any of the 23 built-ins (BR-09); allows enable/disable only.
- **Required cases**:
  - `Suggest("Wait Time")` → `"wait_time"`.
  - `Suggest("Été & Café!")` → non-`[a-z0-9\s]` characters are **stripped** (no transliteration — the SRS auto-suggest rule is lowercase, spaces → `_`, invalid characters stripped), yielding a valid `snake_case` candidate; the user may edit it manually before first use.
  - `Validate(existingFields=["wait_time"], field="wait_time", includeDisabled=true)` → `Invalid("This API field name is already in use")` (VR-F06).
  - `IsLocked(parameter, hasReceivedRequest=true)` → `true`.
  - `Validate(min=100, max=50)` → `Invalid("Minimum must be less than Maximum")` (VR-F07).
  - `ScanReferences(parameterId="service", scopeFilters=[…], channelContracts=[…])` → returns the non-empty reference list feeding Dialog D-6's copy.
  - `ScanReferences(parameterId="unused_custom_param")` → returns empty list → disable proceeds with no dialog.
  - `Guard(builtIn=true, action=Delete)` → `throws InvalidOperationException` (BR-09).
  - `Guard(builtIn=true, action=Disable)` → allowed.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/v1/integration-hub/parameters` — create custom Range parameter → 201; audit event `parameter.created`.
  - `POST .../parameters` — duplicate API field (incl. against a disabled parameter) → 409.
  - `PATCH .../parameters/{id}` `{ enabled: false }` on an unreferenced parameter → 200, no warning needed server-side (client shows D-6 only when references exist).
  - `PATCH .../parameters/{id}` `{ enabled: false }` on a parameter referenced by a channel contract → 200 but response includes the reference list so the client can render D-6 pre-confirmation, OR the endpoint requires an explicit `confirm=true` once references exist (design-time decision; both satisfy BR-10's "explicit impact warning" requirement — see plan.md for the resolved shape).
  - `GET /api/v1/integration-hub/parameters?origin=custom&type=range` — combined AND filter.
  - `DELETE` — no such endpoint exists (BR-09); attempting any delete-shaped call → 404/405.
- **What's intentionally NOT covered end-to-end**: `ApiFieldNameSuggester` (client-side typing behaviour), `RangeConfigValidator`, `BuiltInParameterGuard` — covered by unit tests.

**Scenario Test**: `scenario-test: not-needed — parameter create/enable/disable are single-endpoint operations; the disable-impact-warning scenario is a single PATCH whose response shape is asserted directly, no cross-endpoint state to carry.`

**E2E Test Coverage** (frontend SPA):

- **User flows under test**: `/integration-hub/parameters` (SCR-05 list) and the SCR-06 drawer (no dedicated route — opens over SCR-05) → `ParameterCatalogueTests.cs` in `tests/Nabadat.E2ETests/IntegrationHub/`.
- **Required scenarios**:
  - `Parameters_type_switch_between_range_and_list_shows_correct_panel` — AC-S6-01.
  - `Parameters_api_field_auto_suggests_from_english_name` — AC-S6-02.
  - `Parameters_blocks_save_on_duplicate_api_field_including_disabled` — AC-S6-03.
  - `Parameters_origin_and_type_filters_combine_with_AND` — AC-S5-01.
  - `Parameters_disable_shows_impact_warning_when_referenced` — AC-S5-02.
  - `Parameters_builtin_row_has_no_delete_action_and_locked_api_field` — BR-09.
  - `Parameters_range_validation_blocks_min_greater_than_max` — VR-F07.

---

### User Story 3 — Onboard an Integration via the New/Edit Wizard (Priority: P1)

**Persona**: Tenant IT Administrator (P-07).

A Tenant IT Administrator creates an integration point in a 3-step wizard: **Step 1** picks a name, an active service channel, and exactly one of the five integration scenarios; **Step 2** configures authentication (API Key **or** OAuth 2.0 client-credentials, generating show-once credentials); **Step 3** reviews the generated endpoint, the channel's accepted-parameters contract, and the result-code catalogue, then publishes. Editing reopens the same wizard pre-filled.

**Why this priority**: this is where a caller's endpoint actually gets provisioned — the wizard is the single onboarding path for every scenario, and nothing can be called (US4) or logged (US5) without an integration existing first.

**Independent Test**: `/integration-hub/integrations` → **New integration** → Step 1: name "Core Services Bus — Survey Dispatch", pick the channel from US1, select the **Dispatch via Nabadat** scenario card → Continue → Step 2: pick **API key**, type a key label, **Generate new API key** → Dialog D-1 shows the plaintext once, **Done** → Continue → Step 3: verify the endpoint preview shows the method, path, and highlighted channel-ID token, and the accepted-parameters table matches the channel's contract from US1 → **Create integration** → back on SCR-01 with the new row, zero traffic, "—" error rate.

**Acceptance Scenarios**:

1. **AC-S2-01** — **Given** step 2 with API key selected, **When** the mechanism is switched to OAuth 2.0, **Then** the API-key fields hide and the OAuth fields show, and vice versa (dynamic visibility, FR-S2-04).
2. **AC-S2-02** — **Given** Dialog D-1 (API key generated) is open, **When** **Done** is clicked, **Then** no screen, log, or API ever displays the plaintext key again (show-once, BR-16).
3. **AC-S2-03** — **Given** a revoked key, **When** the caller uses it, **Then** the request logs `401 E-1401` with the ratified message copy: *"API key was revoked on ‹date›. Generate a new key in Integrations."*
4. **AC-S2-04** — **Given** the service channel is changed on step 1, **When** step 3 renders, **Then** both the endpoint path token and the Accepted-parameters table reflect the new channel (FR-S2-07/08).
5. **Exactly one scenario** — **Given** the five scenario radio cards, **When** one is selected, **Then** it shows a highlight ring and check mark and no other card can be simultaneously selected (BR-02); a second scenario for the same caller requires a **second** integration.
6. **Only active channels selectable** — **Given** a deactivated channel, **When** the Step-1 channel select opens, **Then** it is not offered (FR-S2-02, BR-07).
7. **Credential discarded on cancel** — **Given** a credential generated mid-wizard, **When** **Cancel** is clicked before **Create integration**, **Then** the generated credential is discarded along with the draft (BR-25) and is unusable even if somehow retained client-side.
8. **Endpoint live within 60s** — **Given** a newly created integration, **When** a valid request targets its endpoint, **Then** it is accepted within 60 seconds of creation (Derived from UI).

**Unit Test Coverage**:

- **Units under test**:
  - `IntegrationNameValidator` — required, unique per tenant, ≤ 100 chars (VR-F01).
  - `ScenarioSelectionRule` — exactly one of SCN-01…05 (BR-02).
  - `ApiKeyGenerationService` — generates a key, hashes/encrypts it for storage, returns the plaintext exactly once; on regeneration, implicitly revokes the prior active key (BR-16).
  - `OAuthClientGenerationService` — generates `client_id`/`client_secret` (hashed/encrypted at rest), assigns the fixed `client_credentials` grant type and 15-minute token lifetime in code (BR-17), applies selected scopes.
  - `CredentialRevocationService` — immediate revocation; subsequent use → `401 E-1401`.
  - `WizardDraftDiscardPolicy` — on cancel, discards any credential generated mid-wizard (BR-25).
- **Required cases**:
  - `Validate(name="", channel=X, scenario=SCN-01)` → `Invalid("Integration name is required")` (VR-F01).
  - `Validate(existingNames=["Core Bus"], name="core bus")` → `Invalid` — uniqueness is **case-insensitive** per VR-F01 (`[Formalized default]`, SRS v1.2).
  - `SelectScenario(current=SCN-01, attemptSecond=SCN-03)` → rejected; only one scenario field exists per integration, not a multi-select (BR-02).
  - `Generate(keyLabel="Core Bus Key")` → returns plaintext once; stored value is not equal to the plaintext (hashed/encrypted); a second call to retrieve it returns only the masked form.
  - `Generate(existingActiveKey=K1, newLabel="K2")` → `K1` is implicitly revoked, `K2` becomes active (BR-16).
  - `Revoke(key=K1)` → subsequent auth check for `K1` → `Invalid` → maps to `401 E-1401`.
  - `GenerateOAuthClient(scopes=["survey-requests:write"])` → grant type is always `client_credentials`; token TTL is always 15 minutes, neither configurable via input.
  - `DiscardOnCancel(generatedCredential=K1, wizardCancelled=true)` → `K1` is never persisted/usable.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/v1/integration-hub/integrations` — create SCN-01 integration with API-key auth → 201, endpoint provisioned; audit events `integration.created` + `credential.generated`.
  - `POST .../integrations` — create with OAuth auth + scopes → 201; audit events as above.
  - `POST .../integrations/{id}/credentials/revoke` — revoke the active API key → 200; audit event `credential.revoked`; subsequent API call using the old key → `401 E-1401`.
  - `POST .../integrations/{id}/credentials` — generate a new key while one is active → 200, old key implicitly revoked (BR-16).
  - `POST .../integrations` — duplicate integration name → 409 (VR-F01).
  - `POST .../integrations` — deactivated channel supplied → 400/409 (channel not selectable server-side either, defense in depth).
  - `PUT .../integrations/{id}` — edit mode changes the service channel → 200; endpoint path in the response reflects the new channel.
- **What's intentionally NOT covered end-to-end**: `ScenarioSelectionRule`, `ApiKeyGenerationService`'s hashing internals, `WizardDraftDiscardPolicy` (client-side cancel behaviour) — covered by unit tests.

**Scenario Test**: `scenario-test: IntegrationOnboardingScenarioTests` — walks `POST /integrations` (create with API key) → `GET /integrations/{id}` (verify endpoint + contract shape) → a live call to the provisioned endpoint (verify `202 ACCEPTED`, per US4) → `POST /integrations/{id}/credentials/revoke` → a repeat call to the same endpoint (verify `401 E-1401`). Spans 4+ calls, carries the integration id + credential across steps, asserts the final state: exactly one `integration.created`, one `credential.generated`, and one `credential.revoked` audit event, in order.

**E2E Test Coverage** (frontend SPA):

- **User flows under test**: `/integration-hub/integrations` (SCR-01 list, create entry point), `/integration-hub/integrations/new` and `…/:id` (SCR-02 wizard) → `IntegrationWizardTests.cs` in `tests/Nabadat.E2ETests/IntegrationHub/`.
- **Required scenarios**:
  - `Wizard_switches_auth_fields_when_mechanism_changes` — AC-S2-01.
  - `Wizard_api_key_dialog_never_shows_plaintext_again_after_done` — AC-S2-02.
  - `Wizard_endpoint_and_contract_preview_update_when_channel_changes` — AC-S2-04.
  - `Wizard_only_offers_one_scenario_selection_at_a_time` — BR-02.
  - `Wizard_channel_select_excludes_inactive_channels` — FR-S2-02.
  - `Wizard_cancel_discards_generated_credential_and_draft` — BR-25.
  - `Wizard_blocks_step_advance_on_missing_required_field` — VR-F01/F10.
  - `Integrations_new_row_shows_zero_traffic_and_dash_error_rate` — AC-S1-03.

---

### User Story 4 — Process Inbound API Requests (Priority: P1)

**Persona**: Caller / source system (non-human actor).

The headless runtime every provisioned integration's endpoint runs on. A caller sends a request carrying the service channel ID as the only mandatory path parameter and any other transaction data as free key–value pairs. The request passes an ordered, atomic validation pipeline (TLS → auth → rate limit → payload size → channel resolution → channel-active check → required-parameters check → type/validation-rule checks), then is handed to the scenario's downstream owner (M-02 for dispatch, redirect resolution via M-02 rules, M-03 for JSON/iFrame, M-04 for response ingestion) or returns the requested artifact directly (link, JSON, embed URL).

**Why this priority**: this is the module's entire reason to exist — an integration and a channel with no working request pipeline deliver zero value. It is P1 alongside US1–US3 because none of those stories are demonstrably "done" until a real request succeeds against them.

**Independent Test**: Using the integration from US3 (SCN-01, API key), send a valid `POST` with all channel-required parameters and a valid API key → expect `202 ACCEPTED` with a `request_id`, and the request visible in SCR-08 within 60 seconds (per US5). Then send a request missing a required parameter → expect the whole request rejected with `400 E-1002` naming the missing field, and nothing forwarded to M-02.

**Acceptance Scenarios**:

1. **AC-F0-01** — **Given** a valid SCN-01 request with all required parameters, **When** POSTed with valid credentials, **Then** the response is `202 ACCEPTED` with a `request_id`, and the request appears in SCR-08 within 60 s.
2. **AC-F0-02** — **Given** a request missing a channel-required parameter, **When** submitted, **Then** the whole request is rejected `400 E-1002`, the missing field is named in the message (e.g. *"Required parameter 'mobile' is missing for service channel E-SERVICES-PORTAL."*), and nothing reaches M-02/M-04.
3. **AC-F0-03** — **Given** a request carrying an unknown key `loyalty_tier` with no parameter definition, **When** processed, **Then** the request succeeds, the pair is stored raw, is visible in the SCR-08 log detail, and does **not** appear in any report/dashboard/filter/rule builder (BR-14).
4. **AC-F0-04** — **Given** a retry with an identical `transaction_id` (same tenant + channel ID), **When** processed, **Then** no second survey is sent and no duplicate response is stored (BR-18).
5. **AC-F0-05** — **Given** an inactive channel, **When** any request targets it, **Then** `409 E-1004` is returned and logged.
6. **Ordered pipeline, atomic failure** — **Given** a request that would fail both authentication and payload-size checks, **When** processed, **Then** it is rejected at the **first** failing step in the normative order (TLS → auth → rate limit → payload size → channel resolution → channel-active → required-params → type/validation) with that step's error code, and the whole request is atomically rejected — no partial processing.
7. **Rate limit** — **Given** an integration receiving requests above its configured limit (default 100 req/s), **When** the limit is exceeded, **Then** `429 E-1429` is returned.
8. **Payload cap** — **Given** a request body over 2 MB, **When** submitted, **Then** `413 E-1413` is returned before any parameter parsing.
9. **All five scenarios return their normative artifact** — dispatch → `202 ACCEPTED`; redirect link → `200 OK` with `survey_url` + `expires_at` (24h default); JSON render → `200 OK` with the survey definition JSON from M-03; iFrame embed → `200 OK` with an embed URL, refused if the caller's origin is not on the integration's allowed-origins whitelist; response ingestion → `202 ACCEPTED`, payload forwarded to M-04.

**Unit Test Coverage**:

- **Units under test**:
  - `RequestValidationPipeline` — orchestrates the 8-step ordered, atomic pipeline (FR-F0-02); short-circuits on first failure, mapping each step to its result code.
  - `ResultCodeMapper` — maps each pipeline outcome to the normative catalogue (`E-1001`, `E-1002`, `E-1003`, `E-1004`, `E-1401`, `E-1413`, `E-1429`, `E-1500`, `202`, `200`) with the exact message copy patterns.
  - `ChannelContractRequiredFieldChecker` — validates presence of every parameter the channel contract marks required (BR-08 — the contract, not the parameter-level default, is authoritative).
  - `ParameterTypeValidator` — dispatches to the 13 per-type validators (VR-T01…T13: Text, Number, Boolean, Email, Phone, List [membership not enforced], Range [min/max inclusive], Date, Date & time, Currency, Percentage, URL, Geolocation).
  - `UnregisteredParameterStore` — separates key–value pairs with no parameter definition, stores them raw, flags them "unregistered" for the log detail and excludes them from reports/dashboards/filters/rule builders (BR-14).
  - `IdempotencyKeyResolver` — keys on `(tenant, channelId, transaction_id)`; a repeat is accepted (new log entry) without re-triggering downstream side effects (BR-18/F0.7).
  - `AllowedOriginsWhitelistStore` (SCN-04) — persists and exposes the per-integration Allowed-Origins whitelist (FR-S2-10) for M-03's rendering endpoint to enforce at browser-load time; M-13 itself only manages the configuration, it does not receive the browser's origin-bearing request (see Clarifications, 2026-07-27).
  - `SurveyLinkExpiryCalculator` (SCN-02) — default 24h from issue, override per FR-S2-10.
- **Required cases**:
  - `Process(request={validAllRequired})` → `202 ACCEPTED` + `request_id` (AC-F0-01).
  - `Process(request={missing:"mobile"})` → `400 E-1002` message = `"Required parameter 'mobile' is missing for service channel E-SERVICES-PORTAL."` (AC-F0-02).
  - `Process(request={extra:"loyalty_tier"})` → `202`/`200` (scenario-dependent) + `loyalty_tier` stored raw, marked unregistered (AC-F0-03, BR-14).
  - `Process(request, retry=true, sameTransactionId=true)` → no duplicate downstream dispatch/store (AC-F0-04, BR-18).
  - `Process(request, channelActive=false)` → `409 E-1004` (AC-F0-05).
  - `Process(request, authInvalid=true, payloadTooLarge=true)` → `401 E-1401` (auth fails first in pipeline order, not `413`).
  - `Process(request, rateExceeded=true)` → `429 E-1429`.
  - `Process(request, payloadBytes=2_000_001)` → `413 E-1413`.
  - `Validate(type=Phone, value="07701")` → `Invalid` → `422 E-1003` message `"Value '07701' for 'mobile' failed validation rule for type Phone."` (not E.164).
  - `Validate(type=Phone, value="+962770123456")` → `Valid` (E.164, 8–15 digits after `+`).
  - `Validate(type=Range, value=150, min=0, max=100)` → `Invalid` (out of inclusive bounds).
  - `Validate(type=List, value="anything-unmapped")` → `Valid` (membership not enforced at ingestion, VR-T06/BR-12).
  - `ComputeExpiry(issuedAt=T, override=null)` → `T + 24h` (default, F0.8).
  - `Resolve(origin="https://evil.example", whitelist=["https://trusted.example"])` → refused.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - Live calls to each of the five scenario endpoints (`POST …/survey-requests/{channelId}`, `POST …/survey-links/{channelId}`, `POST …/survey-definitions/{channelId}`, `GET …/survey-embed/{channelId}`, `POST …/responses/{channelId}` — illustrative paths per F0.1) — one integration test class per scenario, asserting the correct result code + downstream hand-off stub (M-02/M-03/M-04 as no-op published-interface calls, mirroring the M-06-score-reader stub pattern established for M-15).
  - Full pipeline-order test: a request crafted to fail two checks simultaneously asserts the **earlier** step's code wins.
  - Idempotent retry test: two identical requests (same `transaction_id`) → both logged, only one downstream dispatch/store call recorded.
  - Rate-limit test: N+1 requests within one second against a 100 req/s-limited integration → the (N+1)th is `429`.
  - Payload-cap test: a > 2 MB body → `413` before any parameter parsing (assert zero log detail for parameters).
- **What's intentionally NOT covered end-to-end**: the 13 per-type validators' full boundary matrices (`ParameterTypeValidator`) — covered by unit tests; only one representative case per type is re-asserted at the integration layer (Phone) to prove wiring.

**Scenario Test**: `scenario-test: InboundRequestLifecycleScenarioTests` — for SCN-01: send a request → assert `202` → poll `GET /request-logs` (US5) until the entry appears (≤ 60s) → retry the identical request (same `transaction_id`) → assert no duplicate downstream dispatch and exactly 2 log entries (one per attempt, per BR-18's "a new log entry is written" on retry) → deactivate the channel mid-test → repeat the request → assert `409 E-1004`. Spans 4+ calls across 2 modules' worth of behaviour (Feature 0 + SCR-08), asserts the final aggregate: 2 accepted log entries + 1 rejected log entry, one downstream dispatch call total.

**E2E Test Coverage**: `e2e-tests: skipped — Feature 0 is an explicitly headless system feature with no admin-console screen (SRS: "Feature 0 — Inbound Request Processing (headless system feature, no screen)"). Its behaviour is fully verified by the unit, integration, and scenario tests above against the live API surface; there is no browser flow to drive. Its *visibility* to a human (SCR-08 Request Logs) is covered by User Story 5's E2E suite.`

---

### User Story 5 — Monitor Integration Health and Investigate via Request Logs (Priority: P1)

**Persona**: Tenant IT Administrator (P-07).

A Tenant IT Administrator opens the Integrations list to see, at a glance, how many integrations exist, how much traffic they've handled in the last 24 hours, and their aggregate error rate — then drills into any integration's logs, or opens the dedicated Request Logs screen, to investigate a specific failure: filtering by status class, integration, and time window (including a Last-hour option), expanding a row to see every parameter received (masked for PII) and the full response returned.

**Why this priority**: without observability, P-07 cannot verify that US3's integrations and US4's runtime are actually working, nor diagnose a caller's integration problem. It closes the loop on the module's core value.

**Independent Test**: Seed 6 integrations (1 inactive) with a mix of successful and failed requests → open `/integration-hub/integrations` → verify the stat tiles read "6 / 5 active", correct 24h request count, and a correctly colour-coded error rate → open `/integration-hub/logs` → filter chips "Client errors" + a specific integration + "Last hour" → verify only matching rows remain and per-chip counts reflect the window → expand a row → verify PII fields render masked and the full response detail is shown.

**Acceptance Scenarios**:

1. **AC-S1-01** — **Given** 6 integrations of which 1 is inactive, **When** the page loads, **Then** the tile shows "6 / 5 active" and the inactive row shows the neutral badge and "suspended" sub-line.
2. **AC-S1-02** — **Given** search text "CRM" and channel filter `CALL-CENTER`, **When** both applied, **Then** only rows matching **both** remain (FR-S1-02, AND combination).
3. **AC-S1-03** — **Given** a new integration just created, **When** SCR-01 reloads, **Then** it appears with zero traffic and "—" error rate (no traffic yet, FR-S1-05).
4. **AC-S8-01** — **Given** filter chips 4xx + integration X + Last hour, **When** applied, **Then** only matching rows remain and counts reflect the window (FR-S8-01, AND combination).
5. **AC-S8-02** — **Given** a log row expanded, **When** PII fields render, **Then** mobile/email/name values are masked in exactly the masked format (e.g. `+9627•••••312`, `M••••• A•-R•••••`), including in export (BR-14/FR-S8-03).
6. **AC-S8-03** — **Given** an auth-rejected request, **When** expanded, **Then** the parameters panel shows *"— request rejected before parameter parsing"* instead of data (since auth fails before parameter-level pipeline steps).
7. **Error-rate colour thresholds** — **Given** an integration's rolling 24h error rate, **When** it is `< 1%`, **Then** it renders D2 (healthy); `1–5%` renders D3 (warning); `> 5%` renders D4 (critical) — FR-S1-06.
8. **Export of filtered view** — **Given** the log table is filtered to 4xx + one integration, **When** **Export** is clicked, **Then** exactly the filtered rows are exported, with PII masked identically to the on-screen view (FR-S8-04, BR-14).

**Unit Test Coverage**:

- **Units under test**:
  - `IntegrationHealthTileCalculator` — computes total/active count, rolling-24h request count, and error rate from request-log aggregates; renders "—" when there is zero traffic (FR-S1-05).
  - `ErrorRateColourResolver` — `< 1% → D2`, `1–5% → D3`, `> 5% → D4` (FR-S1-06).
  - `IntegrationListFilter` — AND-combines name search + channel filter (FR-S1-02).
  - `RequestLogFilterCombinator` — AND-combines status-class chips + integration select + time window incl. "Last hour" (FR-S8-01).
  - `PiiMaskingFormatter` — masks mobile/email/customer-name in a documented, deterministic pattern for list, detail, and export views alike (FR-S8-03).
  - `RejectedRequestDetailProjection` — for auth-rejected (pre-parameter-parsing) requests, renders the "rejected before parameter parsing" notice instead of parameter data.
- **Required cases**:
  - `Compute(total=6, active=5, errors24h=0, requests24h=0)` → tile = "6 / 5 active", error rate = "—" (no traffic).
  - `ColourFor(rate=0.008)` → `D2`; `ColourFor(rate=0.03)` → `D3`; `ColourFor(rate=0.08)` → `D4` (boundary cases at exactly 1% and 5% resolved per FR-S1-06's inclusive/exclusive convention, documented in the calculator).
  - `Filter(search="CRM", channel="CALL-CENTER", rows=[…])` → intersection only (AC-S1-02).
  - `Combine(statusClass="4xx", integration="X", window="LastHour")` → AND-intersected result set + counts scoped to the window (AC-S8-01).
  - `Mask(mobile="+962770123456")` → `"+9627•••••312"` (exact pattern from the SRS example).
  - `Mask(name="Mona Al-Rashid")` → `"M••••• A•-R•••••"` (exact pattern from the SRS example).
  - `Project(request, rejectedAtStage="Authentication")` → parameters panel = `"— request rejected before parameter parsing"` (AC-S8-03).

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `GET /api/v1/integration-hub/integrations` — list with computed tiles + FR-S1-02 filters.
  - `GET /api/v1/integration-hub/request-logs` — status-class + integration + time-window (incl. `last_hour`) filters, AND-combined, cursor-paginated, newest-first.
  - `GET /api/v1/integration-hub/request-logs/{id}` (or expanded inline in the list response) — full parameter + response detail, PII masked.
  - `GET /api/v1/integration-hub/request-logs/export?…` — export of the current filtered view, masked identically.
  - `GET /api/v1/integration-hub/request-logs` as a P-01 (CX Manager) → 403, since request logs are P-07-only per the Permissions Matrix (cross-checked with US9).
- **What's intentionally NOT covered end-to-end**: `PiiMaskingFormatter`'s exact masking algorithm across all input shapes, `ErrorRateColourResolver`'s boundary math — covered by unit tests.

**Scenario Test**: `scenario-test: not-needed — monitoring and log investigation are read-only, single-endpoint views per acceptance scenario; the "request appears in logs within 60s" cross-story assertion is already covered by US4's scenario test.`

**E2E Test Coverage** (frontend SPA):

- **User flows under test**: `/integration-hub/integrations` (SCR-01, stat tiles + list) and `/integration-hub/logs` (SCR-08) → `IntegrationMonitoringTests.cs` and `RequestLogsTests.cs` in `tests/Nabadat.E2ETests/IntegrationHub/`.
- **Required scenarios**:
  - `Integrations_stat_tiles_reflect_total_active_and_traffic` — AC-S1-01.
  - `Integrations_search_and_channel_filter_combine_with_AND` — AC-S1-02.
  - `Integrations_new_integration_shows_zero_traffic_and_dash_rate` — AC-S1-03.
  - `RequestLogs_filters_combine_with_AND_and_counts_reflect_window` — AC-S8-01.
  - `RequestLogs_expanded_row_masks_pii_in_exact_format` — AC-S8-02.
  - `RequestLogs_auth_rejected_row_shows_rejected_before_parsing_notice` — AC-S8-03.
  - `RequestLogs_export_masks_pii_identically_to_screen` — FR-S8-04.
  - `RequestLogs_cx_manager_role_is_denied_access` — Permissions Matrix (cross-checked with US9).

---

### User Story 6 — Manage Parameter Mappings Inline (Priority: P2)

**Persona**: CX Manager (P-01).

For any mapping-enabled parameter (typically List-typed), a CX Manager translates raw backend values (e.g. `S001`) into bilingual business-friendly display values (e.g. "Visa Request" / "طلب فيزا"). Mappings resolve at **read time**, so editing or deleting a mapping retroactively relabels historical data by design. Unmapped incoming values are never rejected — they're stored raw and surfaced in a 7-day unmapped-values queue with one-click mapping.

**Why this priority**: List-type parameters function without mappings (raw values are accepted and stored, BR-12), so mapping is a business-friendliness refinement over the P1 core loop, not a blocker to it — but it is high-value and used often enough to be P2 rather than P3.

**Independent Test**: Select a mapping-enabled parameter with no mappings yet → send a request via US4 carrying an unmapped value `S014` → open `/integration-hub/mappings` → verify the unmapped-values alert lists `S014` → click **Map now** → the value pre-fills a draft row → fill EN/AR display values → **Save** → the mapping becomes Active and any historical/future report renders the new label immediately.

**Acceptance Scenarios**:

1. **AC-S7-03** — **Given** incoming value `S014` with no mapping, **When** received, **Then** the response stores/displays the raw value and `S014` appears in the queue alert.
2. **AC-S7-02** — **Given** Replace-all confirmed (see US7), **When** a historical report renders in AR, **Then** it shows the new AR labels (read-time resolution) — asserted here for the simpler inline-edit case too: editing one mapping's AR label retroactively changes how every past response with that source value renders.
3. **Inline add** — **Given** **Add value** is clicked, **When** the draft row appears, **Then** it has a "Draft" status badge and a **Save** button; Save requires a non-empty, parameter-unique source value (VR-F08).
4. **Confirmed delete** — **Given** a mapping row's **Delete** is clicked, **When** Dialog D-7 appears and **Delete** is confirmed, **Then** the mapping is removed and takes effect at read time immediately; responses carrying that source value revert to displaying the raw value until remapped.
5. **No version history** — **Given** any mapping change (add, edit, delete), **When** inspected afterward, **Then** no version-history or restore UI exists anywhere — the platform audit trail is the only change record (BR-13, `[PO-G12]`).
6. **Parameter selector scoping** — **Given** the mapping-parameter selector, **When** it opens, **Then** only mapping-enabled parameters are offered, each rendered "Name — api_field (n values)".

**Unit Test Coverage**:

- **Units under test**:
  - `MappingSourceValueUniquenessValidator` — unique within the parameter, **case-insensitively** (VR-F08).
  - `UnmappedValueQueueService` — surfaces values with no mapping entry received in the trailing 7 days; removes a value from the queue once mapped.
  - `MappingResolver` — read-time lookup of source value → `{display_en, display_ar}`, matched **case-insensitively**; falls back to the raw value (as originally received, casing preserved) when unmapped (F0.5, BR-13).
  - `MappingEnabledParameterFilter` — the SCR-07 selector offers only parameters with the mapping-support usage flag on.
- **Required cases**:
  - `Validate(existingValues=["S001"], newValue="S001")` → `Invalid("This source value already has a mapping")` (VR-F08).
  - `Validate(existingValues=["S001"], newValue="s001")` → `Invalid` — case-insensitive match, same source value as `S001` (VR-F08).
  - `Resolve(sourceValue="s001", mappings={S001:{en:"Visa Request",ar:"طلب فيزا"}})` → `{en:"Visa Request", ar:"طلب فيزا"}` — resolves regardless of incoming casing (F0.5).
  - `Enqueue(value="S014", firstSeenAt=now)` → appears in the 7-day queue; `Enqueue(value="S014", firstSeenAt=now-8days)` → does not appear (window expired, assuming no repeat occurrence).
  - `Dequeue(value="S014", mappingCreated=true)` → removed from the queue.
  - `Resolve(sourceValue="S001", mappings={S001:{en:"Visa Request",ar:"طلب فيزا"}})` → `{en:"Visa Request", ar:"طلب فيزا"}`.
  - `Resolve(sourceValue="S014", mappings={})` → falls back to raw `"S014"` for both EN and AR (F0.5).
  - `Resolve(sourceValue="S001", mappings updated AFTER the response was originally stored)` → returns the **new** label (retroactive read-time resolution, not the label in force when the response was stored).
  - `FilterMappingEnabled(parameters=[{mappingSupport:true},{mappingSupport:false}])` → only the first is offered.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/v1/integration-hub/parameters/{id}/mappings` — add a mapping → 201; audit event `mapping.added`.
  - `POST .../mappings` — duplicate source value within the same parameter → 409 (VR-F08).
  - `PUT .../mappings/{mappingId}` — edit display values → 200; audit event `mapping.edited`; a subsequent read of historical data reflects the new label.
  - `DELETE .../mappings/{mappingId}` — remove a mapping → 200; audit event `mapping.deleted`; subsequent reads of that source value fall back to raw.
  - `GET .../parameters/{id}/mappings/unmapped-queue` — returns values received in the trailing 7 days with no mapping.
- **What's intentionally NOT covered end-to-end**: `MappingSourceValueUniquenessValidator`, `MappingEnabledParameterFilter` — covered by unit tests.

**Scenario Test**: `scenario-test: MappingReadTimeResolutionScenarioTests` — send a request (US4) carrying unmapped value `S014` → `GET` the unmapped-values queue (assert `S014` present) → `POST` a mapping for `S014` → `GET` the queue again (assert `S014` absent) → re-fetch the earlier request's log/report projection (assert it now renders the new display label, proving retroactive read-time resolution, F0.5). Spans 4 calls, carries the source value across steps, asserts the final aggregate: exactly one `mapping.added` audit event and the historical projection updated.

**E2E Test Coverage** (frontend SPA):

- **User flows under test**: `/integration-hub/mappings` (SCR-07, inline add/edit/delete portion) → `ParameterMappingsTests.cs` in `tests/Nabadat.E2ETests/IntegrationHub/`.
- **Required scenarios**:
  - `Mappings_unmapped_value_alert_shows_and_map_now_prefills_draft` — AC-S7-03.
  - `Mappings_inline_add_row_requires_unique_nonempty_source_value` — VR-F08.
  - `Mappings_delete_shows_confirmation_and_takes_effect_immediately` — Dialog D-7.
  - `Mappings_no_version_history_or_restore_control_exists_anywhere` — BR-13.
  - `Mappings_parameter_selector_only_lists_mapping_enabled_parameters` — FR-S7-01.

---

### User Story 7 — Bulk Import, Export, and Replace-All Parameter Mappings via Excel (Priority: P2)

**Persona**: CX Manager (P-01).

A CX Manager exports a parameter's current mappings to Excel (columns `source_value`, `display_en`, `display_ar`), edits them offline, and re-imports in either **Merge** (default) or **Replace-all** mode. Import is strictly all-or-nothing: a row-level validation report is produced, and the import applies only if every row is valid. Replace-all is irreversible and requires an explicit confirmation naming the consequence.

**Why this priority**: bulk operations are an efficiency layer over US6's inline editing for tenants with large List catalogues (up to the 5,000-mappings-per-parameter / 10,000-import-rows guardrails) — valuable but not required for the module's core loop to function, hence P2.

**Independent Test**: Export a parameter's mappings → edit the file offline, introducing one intentionally invalid row → **Import from Excel** → Merge mode → verify the import is rejected wholesale with a row-level report naming the bad row and reason, and none of the valid rows were applied. Fix the file, re-import successfully. Then use **Replace all mappings…** → confirm the destructive dialog → verify all prior mappings are gone and replaced with the imported set.

**Acceptance Scenarios**:

1. **AC-S7-01** — **Given** a file with 214 valid rows + 1 invalid row, **When** imported, **Then** nothing is applied and the failing row and reason are reported (all-or-nothing, VR-F09).
2. **Export shape** — **Given** **Export to Excel** is clicked, **When** the file downloads, **Then** it contains exactly the columns `source_value`, `display_en`, `display_ar` for the currently selected parameter (FR-S7-05).
3. **Merge is the default, non-destructive mode** — **Given** Dialog D-4 opens, **When** it renders, **Then** **Merge with existing** is pre-selected; duplicate source values within the imported file are rejected as part of validation, while duplicates against *existing* mappings in Merge mode update the existing entry.
4. **Replace-all confirmation names the consequence** — **Given** Replace-all is triggered (footer button or D-4's Replace-all import mode), **When** Dialog D-5 opens, **Then** its text names the exact current mapping count and the parameter, and states the action cannot be undone.
5. **Capacity guardrail** — **Given** an import file with more than 10,000 rows, **When** submitted, **Then** the import is rejected before any row processing (NFR-16).
6. **Duplicate within file** — **Given** an import file containing the same `source_value` twice, **When** validated, **Then** the whole import is rejected with that duplication named in the row-level report (VR-F09).

**Unit Test Coverage**:

- **Units under test**:
  - `ExcelMappingExporter` — serializes a parameter's mappings to the three ratified columns.
  - `ExcelMappingImportValidator` — validates every row (required columns present, `source_value` non-empty, no in-file duplicates), producing a row-level report; all-or-nothing gate (VR-F09).
  - `ExcelMappingImportModeApplier` — Merge (upsert by `source_value`) vs. Replace-all (delete existing, insert imported set) semantics.
  - `ImportRowCountGuard` — rejects files exceeding 10,000 rows before any row is parsed (NFR-16).
  - `MappingsPerParameterGuard` — rejects an operation (add or import) that would push a parameter past 5,000 mappings (NFR-16).
- **Required cases**:
  - `Export(mappings=[{S001,"Visa Request","طلب فيزا"}])` → workbook with header row `source_value, display_en, display_ar` + one data row.
  - `Validate(rows=[214 valid, 1 with empty source_value])` → `Invalid`, report = `[{row: 215, column: "source_value", reason: "required"}]`, nothing applied (AC-S7-01).
  - `Validate(rows=[{S001,...}, {S001,...}])` → `Invalid`, report names the duplicate row (VR-F09).
  - `Apply(mode=Merge, existing=[S001,S002], imported=[S001(new label),S003])` → result = `[S001(new label), S002, S003]` (upsert + preserve untouched).
  - `Apply(mode=ReplaceAll, existing=[S001,S002], imported=[S003])` → result = `[S003]` only (S001/S002 gone).
  - `GuardRowCount(rowCount=10001)` → rejected before parsing (NFR-16).
  - `GuardMappingCount(existing=4999, importing=2)` → rejected (would exceed 5,000, NFR-16).

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `GET /api/v1/integration-hub/parameters/{id}/mappings/export` — returns the 3-column file for the current mapping set.
  - `POST /api/v1/integration-hub/parameters/{id}/mappings/import` `{ mode: "merge", file }` — valid file → 200, mappings upserted; audit event `mapping.import` with row count + mode.
  - `POST .../import` — file with one invalid row → 400/422 with the row-level report; zero mappings changed (verify via a follow-up `GET`).
  - `POST .../import` `{ mode: "replace_all" }` — valid file → 200, prior mappings gone, replaced; audit event `mapping.replace_all` with rows-removed/added counts.
  - `POST .../import` — file with 10,001 rows → 400 (NFR-16), before-and-after `GET` shows zero mappings changed.
  - `POST /api/v1/integration-hub/parameters/{id}/mappings/replace-all` (direct, non-import replace, if the UI's footer button maps to a distinct endpoint vs. the import-with-replace-mode path) — irreversible, confirmed, audited.
- **What's intentionally NOT covered end-to-end**: `ExcelMappingImportValidator`'s exhaustive row-shape matrix, `MappingsPerParameterGuard`'s exact boundary — covered by unit tests.

**Scenario Test**: `scenario-test: BulkMappingReplaceScenarioTests` — `GET` export (baseline) → `POST` import with `mode=merge` and one intentionally invalid row → assert `GET` mappings unchanged (all-or-nothing held) → fix the file → `POST` import again, valid this time → assert applied → `POST` import with `mode=replace_all` and a fresh set → assert the prior merged set is entirely gone and only the new set remains. Spans 5 calls, asserts final aggregate: one `mapping.import` (merge) event and one `mapping.replace_all` event, with rows-removed/added counts matching the actual before/after diff.

**E2E Test Coverage** (frontend SPA):

- **User flows under test**: `/integration-hub/mappings` (SCR-07, Excel import/export/replace-all portion) → additional `[TestMethod]` blocks in `ParameterMappingsTests.cs` (same file as US6).
- **Required scenarios**:
  - `Mappings_export_downloads_three_ratified_columns` — FR-S7-05.
  - `Mappings_import_all_or_nothing_shows_row_level_report_on_failure` — AC-S7-01.
  - `Mappings_import_dialog_defaults_to_merge_mode` — Dialog D-4.
  - `Mappings_replace_all_confirmation_names_count_and_is_irreversible` — Dialog D-5.
  - `Mappings_import_over_10000_rows_is_rejected` — NFR-16.

---

### User Story 8 — Manage Credential Lifecycle (Priority: P2)

**Persona**: Tenant IT Administrator (P-07).

Beyond initial generation during US3's wizard, a Tenant IT Administrator manages credentials on an ongoing basis: revoking a compromised API key immediately, or generating a replacement (which implicitly revokes the prior key). There are no sandbox/test credentials, no expiry fields, and no IP allow-lists — secrets are always shown exactly once and never retrievable again.

**Why this priority**: initial credential creation is folded into US3 (P1, needed for onboarding); ongoing revoke/regenerate operations are an important but lower-frequency operational concern, justifying P2.

**Independent Test**: On an existing integration with an active API key, open Step 2 of the edit wizard → click **Revoke** → confirm Dialog D-3 (which names the masked key and states the consequence) → verify the key is revoked immediately and a subsequent caller request with that key returns `401 E-1401`. Then generate a new key and verify the old one (already revoked) still fails identically while the new one succeeds.

**Acceptance Scenarios**:

1. **Revocation is immediate and irreversible** — **Given** an active API key, **When** **Revoke** is confirmed in Dialog D-3, **Then** every subsequent request signed with that key is rejected `401 E-1401` starting immediately, and there is no "un-revoke" action anywhere (Status Lifecycle table).
2. **Regeneration implicitly revokes** — **Given** an active key K1, **When** a new key K2 is generated for the same integration, **Then** K1 is implicitly revoked without a separate confirmation step (BR-16, `[Derived from UI]`).
3. **No sandbox, no expiry, no IP allow-list fields** — **Given** the API-key or OAuth configuration forms render, **When** inspected, **Then** none of these fields exist anywhere in the console (ratified removal, SCR-02 Step 2).
4. **OAuth token lifetime and grant type are fixed, not configurable** — **Given** the OAuth configuration card, **When** rendered, **Then** no grant-type field and no access-token-lifetime field are present; both are fixed in code (`client_credentials`, 15 minutes) — only the hint text communicates the fixed lifetime (BR-17).
5. **Scopes limit callable endpoints** — **Given** an OAuth client with only `survey-links:read` selected, **When** a token issued to it calls a dispatch (SCN-01) endpoint, **Then** the call is rejected (scope-insufficient — mapped to `401 E-1401` per the pipeline's authentication step, since scope is part of the authentication/authorization check).

**Unit Test Coverage**:

- **Units under test**:
  - `CredentialRevocationService` *(also introduced under US3; extended here with the standalone revoke-without-regeneration flow and the "no un-revoke" invariant)*.
  - `OAuthScopeEnforcer` — maps each scenario's endpoint to its required scope; rejects tokens lacking it.
  - `CredentialFieldSetGuard` — a static/config-level check (or an automated UI-contract test) asserting the API-key and OAuth field sets never include expiry, sandbox, or IP-allow-list fields (guards against future accidental regressions of a ratified removal, `[PO-G13]`).
- **Required cases**:
  - `Revoke(key=K1)` → `K1.status = Revoked`; `Attempt(Unrevoke, K1)` → no such operation exists (compile-time/API-surface absence, not a runtime rejection).
  - `Generate(newKey=K2, whileActive=K1)` → `K1.status = Revoked`, `K2.status = Active`, no separate confirmation required for K1's revocation.
  - `EnforceScope(token.scopes=["survey-links:read"], calledEndpoint=SCN-01)` → rejected (insufficient scope).
  - `EnforceScope(token.scopes=["survey-requests:write"], calledEndpoint=SCN-01)` → allowed.
  - `AssertFieldSet(apiKeyFields)` → does not contain `expiry`, `sandbox`, `allowedSourceIps`.
  - `AssertFieldSet(oauthFields)` → does not contain `grantType`, `tokenLifetime`.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/v1/integration-hub/integrations/{id}/credentials/revoke` — revoke without regenerating → 200; subsequent call with that key → `401 E-1401`.
  - `POST .../credentials` (generate) while one is active → 200; old key immediately unusable, verified via a live call.
  - A live call using an OAuth token whose scopes don't include the target scenario's required scope → `401 E-1401`.
- **What's intentionally NOT covered end-to-end**: `CredentialFieldSetGuard` — a console-contract check, covered by a unit/contract test, not a live API call.

**Scenario Test**: `scenario-test: not-needed — covered by US3's IntegrationOnboardingScenarioTests, which already exercises generate → revoke → repeat-call-rejected; this story adds no new cross-endpoint sequence beyond what US3 already asserts.`

**E2E Test Coverage** (frontend SPA):

- **User flows under test**: SCR-02 Step 2 (revoke/regenerate on an existing integration) → additional `[TestMethod]` blocks in `IntegrationWizardTests.cs` (same file as US3).
- **Required scenarios**:
  - `Wizard_revoke_dialog_names_masked_key_and_consequence` — Dialog D-3.
  - `Wizard_generating_new_key_while_one_active_shows_no_extra_confirmation_for_old_key` — BR-16.
  - `Wizard_auth_forms_never_render_expiry_sandbox_or_ip_allowlist_fields` — `[PO-G13]`.
  - `Wizard_oauth_form_has_no_grant_type_or_token_lifetime_fields` — BR-17.

---

### User Story 9 — Cross-Persona Read-Only Visibility and Permission Enforcement (Priority: P2)

**Persona**: both P-07 (Tenant IT Administrator) and P-01 (CX Manager).

Each persona has full manage access to their own screens; cross-persona **read-only** visibility applies as follows: P-01 may view (never edit) Integrations; P-07 may view (never edit) Service Channels, Parameters, and Mappings. **Request Logs are exclusive to P-07 — P-01 has no log access of any kind** (PO ruling, BR-24 as corrected in SRS v1.2). All sensitive actions — credential generate/revoke, parameter disable, mapping replace/import, channel ID change, integration activate/deactivate — are permission-controlled and audited, enforced server-side regardless of what the client renders.

**Why this priority**: this is a cross-cutting correctness requirement threading through every other story; it is P2 because the base flows (US1–US5) are usable by their owning persona without it, but a real multi-role tenant deployment is incomplete without the guardrail being verifiably enforced.

**Independent Test**: As a P-01 (CX Manager) session, open `/integration-hub/integrations` — verify it renders read-only (no New/Edit/Revoke controls, or controls disabled/hidden per FR-GBL-05) — then open `/integration-hub/logs` and verify the **access-denied state** renders (P-01 has no `m13.log.view` grant). Finally, attempt the underlying write endpoints directly (e.g. `POST /integrations`) and `GET /request-logs`, and verify the server independently rejects both with 403, regardless of the client's rendering.

**Acceptance Scenarios**:

1. **Read-only rendering & log exclusivity** — **Given** a P-01 session, **When** SCR-01/SCR-02 render, **Then** all P-07-only actions (New integration, credential generate/revoke) are hidden or disabled (FR-GBL-05), and **SCR-08 does not render at all** — direct navigation to `/integration-hub/logs` shows the access-denied state (no `m13.log.view` grant, BR-24); given a P-07 session, the read-only mirror holds for SCR-03/04/05/06/07.
2. **Server-side enforcement independent of the client** — **Given** a P-01 session, **When** a raw `POST /api/v1/integration-hub/integrations` request is sent regardless of UI state, **Then** the server returns `403` and the attempt is audited.
3. **Direct-route access without view permission** — **Given** a user with neither view permission for a screen, **When** they navigate directly to its route, **Then** the standard access-denied state renders (FR-GBL-02/05).
4. **All sensitive actions are audited** — **Given** any of: credential generate/revoke, parameter disable, mapping replace/import, channel-ID change, integration activate/deactivate, **When** performed, **Then** an audit event is recorded with actor, tenant, timestamp, entity, and a before/after summary (Permissions Matrix, BR-21).

**Unit Test Coverage**:

- **Units under test**:
  - `PermissionKeyResolver` — maps each M-13 action to its permission key (`m13.integration.view/manage`, `m13.credential.manage`, `m13.log.view`, `m13.channel.view/manage`, `m13.parameter.view/manage`, `m13.mapping.manage/replace`) per the Permissions Matrix, and resolves the effective grant for a given persona/role.
  - `CrossPersonaViewGuard` — implements BR-24: P-01 gets `*.view`-only on P-07's screens and vice versa.
  - `AuditEventEmitter` — for each of the 12 audited action families in the Permissions Matrix, emits an event carrying actor, tenant, timestamp, entity, before/after summary.
- **Required cases**:
  - `Resolve(persona=P-01, action="integration.manage")` → `Denied` (P-01 has view-only on Integrations, BR-24).
  - `Resolve(persona=P-01, action="integration.view")` → `Allowed`.
  - `Resolve(persona=P-07, action="channel.manage")` → `Denied`.
  - `Resolve(persona=P-07, action="log.view")` → `Allowed` (own screen).
  - `Resolve(persona=P-01, action="log.view")` → `Denied` (per the Permissions Matrix, P-01 has no log-view grant at all — logs are P-07-exclusive, unlike channels/parameters/mappings which P-07 gets read-only).
  - `Emit(action="credential.revoked", actor=U1, before={status:Active}, after={status:Revoked})` → one audit event with all required fields.
  - `Emit(action="channel.id_changed", before={id:"OLD"}, after={id:"NEW"})` → one audit event.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/v1/integration-hub/integrations` as P-01 → 403 (ERR: permission denied), audited.
  - `POST /api/v1/integration-hub/service-channels` as P-07 → 403, audited.
  - `GET /api/v1/integration-hub/integrations` as P-01 → 200 (read-only view allowed, BR-24).
  - `GET /api/v1/integration-hub/service-channels` as P-07 → 200 (read-only view allowed, BR-24).
  - `GET /api/v1/integration-hub/request-logs` as P-01 → 403 (logs are P-07-exclusive per the Permissions Matrix, no cross-persona view grant here).
  - A direct route hit for a screen neither role can view (if such a role exists in the tenant's configuration) → access-denied state (FR-GBL-02).
  - Every sensitive action listed in the Permissions Matrix, performed successfully by its authorized persona → asserts exactly one matching audit event per action (12 families).
- **What's intentionally NOT covered end-to-end**: `PermissionKeyResolver`'s full persona × action matrix (all combinations) — covered exhaustively by unit tests; only representative allow/deny pairs are re-asserted at the integration layer.

**Scenario Test**: `scenario-test: not-needed — permission checks are single-request assertions per acceptance scenario; there is no multi-step state to carry across calls beyond what US3's and US6's scenario tests already exercise for their own audit-event sequences.`

**E2E Test Coverage** (frontend SPA):

- **User flows under test**: cross-persona rendering across all 8 screens → `CrossPersonaPermissionsTests.cs` in `tests/Nabadat.E2ETests/IntegrationHub/`.
- **Required scenarios**:
  - `Integrations_cx_manager_sees_read_only_view_with_no_manage_controls` — FR-GBL-05.
  - `ServiceChannels_it_admin_sees_read_only_view_with_no_manage_controls` — FR-GBL-05.
  - `RequestLogs_direct_route_access_without_permission_shows_access_denied` — FR-GBL-02/05.
  - `Mappings_direct_route_access_without_permission_shows_access_denied` — FR-GBL-02/05.

---

### User Story 10 — Activate and Deactivate Integrations and Service Channels (Priority: P3)

**Persona**: Tenant IT Administrator (P-07) for integrations; CX Manager (P-01) for channels.

Either persona can toggle their own entities between Active and Inactive without deleting them (deletion never exists for either entity type once created — for channels, only after traffic; for integrations, never). Deactivating an integration suspends its endpoint; deactivating a channel cascades `E-1004` rejections to every integration serving it and hides the channel from new-integration selection.

**Why this priority**: initial creation (US1, US3) already defaults entities to Active, and the deactivation *consequences* are already asserted cross-story in US1 (AC scenario 5) and US4 (AC-F0-05); this story's marginal remaining value is the toggle UX itself and the reactivation path, justifying P3.

**Independent Test**: Deactivate an Active integration → verify its SCR-01 row shows the neutral "Inactive" badge and "suspended" sub-line, and a live call to its endpoint now fails. Reactivate it → verify the badge reverts and calls succeed again.

**Acceptance Scenarios**:

1. **Integration deactivate/reactivate round-trip** — **Given** an Active integration, **When** deactivated, **Then** its SCR-01 row shows "Inactive"/"suspended" and its endpoint rejects calls; **when** reactivated, **then** the badge reverts and calls succeed again.
2. **No delete control for integrations, ever** — **Given** any integration row, **When** actions render, **Then** no delete control exists (Status Lifecycle table: "Delete (does not exist)").
3. **Channel deactivate hides it from new integrations** — **Given** an Active channel with zero or more serving integrations, **When** deactivated, **Then** it no longer appears in SCR-02's channel select for *new* integrations, while existing integrations serving it keep the (now-rejecting) reference visible with a warning (SCR-02 edge case).

**Unit Test Coverage**:

- **Units under test**:
  - `IntegrationStatusToggle` — Active ⇄ Inactive, audited, no delete transition exists.
  - `ServiceChannelStatusToggle` — Active ⇄ Inactive, audited; on deactivate, excludes the channel from the "active channels only" query used by SCR-02's selector.
- **Required cases**:
  - `Toggle(integration, from=Active, to=Inactive)` → `200`, audit event `integration.deactivated`.
  - `Toggle(integration, from=Inactive, to=Active)` → `200`, audit event `integration.activated`.
  - `Attempt(Delete, integration)` → no such state transition/endpoint exists.
  - `Toggle(channel, from=Active, to=Inactive)` → excluded from `GetActiveChannelsForSelector()`'s result set.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `PATCH /api/v1/integration-hub/integrations/{id}` `{ active: false }` → 200; a subsequent live call to the endpoint → rejected `401 E-1401` (credentials suspended while the integration is inactive, per the SRS Status Lifecycle `[Derived from UI]`).
  - `PATCH .../integrations/{id}` `{ active: true }` → 200; endpoint calls succeed again.
  - `GET /api/v1/integration-hub/service-channels?active=true` (the selector's query, used by SCR-02) — excludes a channel just deactivated.
- **What's intentionally NOT covered end-to-end**: the toggle state machines themselves — covered by unit tests.

**Scenario Test**: `scenario-test: not-needed — covered by the deactivate/reactivate round-trip integration tests above; no additional cross-endpoint state to assert beyond what US1's and US4's scenario tests already establish for the channel-deactivation cascade.`

**E2E Test Coverage** (frontend SPA):

- **User flows under test**: SCR-01 row-level status toggle, SCR-04 Active toggle → additional `[TestMethod]` blocks in `IntegrationMonitoringTests.cs` (US5) and `ServiceChannelTests.cs` (US1).
- **Required scenarios**:
  - `Integrations_deactivate_reactivate_round_trip_updates_badge_and_endpoint` — AC scenario 1.
  - `Integrations_row_never_shows_a_delete_action` — Status Lifecycle table.
  - `ServiceChannels_deactivated_channel_disappears_from_new_integration_selector` — SCR-02 edge case.

---

### Edge Cases

Comprehensive edge cases derived from the SRS, each testable and traceable:

**Validation pipeline & security**:
- **Simultaneous failures** — a request that would fail both authentication and payload size is rejected at the earliest step in the normative order (auth before payload size), never a combined/ambiguous error (FR-F0-02).
- **Auth-rejected requests carry no parameter detail** — the log's parameters panel shows the "rejected before parameter parsing" notice, never partial/garbled data (AC-S8-03).
- **iFrame origin not whitelisted** — SCN-04 embedding from a non-whitelisted origin is refused outright, not silently degraded (F0.8).
- **Survey link expired** — a SCN-02 link used after its `expires_at` (default 24h) is refused, but this happens entirely outside M-13's API surface: the end-customer's link click is served by the survey-serving layer (M-02/M-03), not by any M-13 endpoint, so no F0.3 result code applies. M-13's own responsibility is limited to issuing `survey_url` + `expires_at` at request time (see Clarifications, 2026-07-27).

**Channel & parameter lifecycle**:
- **Channel ID race** — two concurrent edits to the same not-yet-locked channel ID: last-write-wins with full audit trail (NFR-17), consistent with the module's stated concurrency model.
- **Parameter referenced by three different consumers simultaneously** (a scope filter, a rule builder, and a channel contract) — the impact warning (Dialog D-6) lists **all** references, not just the first found (BR-10).
- **Re-enabling a previously disabled built-in parameter** whose API field was already locked by a historical request — the field name stays exactly as it was; only the enabled state toggles (BR-09, BR-11 are independent axes).
- **Custom parameter disabled, never renamed to be reused for something else** — disabling never frees the API field name for a different purpose; it remains permanently associated with its original definition (VR-F06's "including disabled parameters" clause).

**Mappings**:
- **Value mapped, then the mapping deleted, then the same raw value arrives again** — it is treated as unmapped again and re-enters the 7-day queue (no memory of the deleted mapping beyond the audit trail).
- **Import file with a source value that collides with an existing Draft (unsaved) inline row** — the import (a server-side operation) and the Draft (client-only, unsaved) row do not interact; the Draft row is simply a client-side UI state that the user must resolve (save or discard) independently.
- **Replace-all triggered while another editor is mid-edit** — last-write-wins with full audit trail (NFR-17); the SRS explicitly names this as the documented failure/concurrency mode, not a locking mechanism.

**Cross-module & failure modes**:
- **Downstream module (M-02/M-03/M-04) unavailable** — M-13 never exposes the downstream error directly to the caller; it returns `500 E-1500` with the retry-idempotent message, and the caller may safely retry with the same `transaction_id` (F0.3, Error Handling).
- **Rate limit reconfiguration mid-flight** — Nabadat Operations changes a per-integration rate limit without a code deployment (NFR-4); in-flight requests already past the rate-limit check are unaffected by a concurrent limit change.
- **Very late retry of a `transaction_id`** — a retry submitted long after the original request (no fixed idempotency window, BR-18/F0.7, Clarifications 2026-07-27) may be processed as a brand-new request, producing a duplicate survey dispatch (SCN-01/02) or duplicate stored response (SCN-05); this is an accepted limitation, not a defect to remediate.
- **Retro-reportability of previously received unregistered values** (Assumption A-2, SRS) — once a key is later registered as a parameter, previously received raw values become reportable going forward; this is the SRS's sole remaining open assumption (not a [NEEDS CLARIFICATION] — the PO may confirm or reverse it without structural impact, and it does not block this spec).

**Permissions**:
- **Role with neither view permission attempts a direct deep link** — access-denied state, never a partial/broken render (FR-GBL-02/05).
- **A P-07 session attempts a P-01-only write via a crafted request** (bypassing the hidden UI controls) — the write endpoints must independently return 403 (ERR pattern consistent with M-15's precedent), never relying on client-side hiding alone.

**Bilingual / RTL**:
- **Arabic channel/parameter/mapping display names** — accepted natively; AR text fields render RTL with the Arabic font stack (NFR-10); AR mapping display values render correctly in RTL table cells (SCR-07).

---

## Requirements *(mandatory)*

> Every functional requirement below preserves its SRS ID for traceability (`FR-*`, `BR-*`, `VR-*`, `NFR-*`, `CMC-*`, `D-*`).

### Global Console Behaviours (FR-GBL — apply to all eight screens)

- **FR-GBL-01** — Tables paginate **server-side** beyond 50 rows; no user-facing column sorting in Phase 1; default orders: integrations/channels/parameters by creation, logs newest-first, mappings by entry order.
- **FR-GBL-02** — Skeleton rows while loading; empty states with guidance text and the screen's primary CTA; error state with retry; access-denied state for a missing view permission.
- **FR-GBL-03** — Unsaved-changes guard on SCR-02 (wizard), SCR-04 (channel editor), and SCR-06 (parameter drawer) — prompts before discarding unsaved edits on navigation away.
- **FR-GBL-04** — Success toasts confirm create/save/import/revoke; failed generation actions show an error toast; inline validation copy per VR-F12.
- **FR-GBL-05** — Actions the role lacks are hidden or disabled; direct-route access without the view permission renders the access-denied state (FR-GBL-02).

**Navigation (functional)** `[UI]` — persistent module navigation with two groups: **Inbound integrations** → *Integrations* (SCR-01), *Request logs* (SCR-08); **Data model** → *Service channels* (SCR-03), *Parameters* (SCR-05), *Parameter mappings* (SCR-07). The active item is highlighted and navigation occurs without a page reload. A breadcrumb `Nabadat › Integration Hub › ‹current screen›` provides context and back-navigation on every screen; every route is directly addressable (deep links). The prototype's version badge, review banner, and persona chips are review annotations **excluded from the product** `[Derived from UI]`. Visual treatment of the navigation shell (styling, theming, icons) is design-system territory and intentionally out of this spec.

### Feature 0 — Inbound Request Processing (headless, no screen)

- **FR-F0-01** — Five normative integration scenarios (SCN-01…05): Dispatch via Nabadat (transaction details → `202 ACCEPTED`, Nabadat sends via M-02), Redirect link (transaction details → one-time `survey_url` + `expires_at`, survey resolved via M-02 rules), JSON render (transaction details → survey JSON from M-03), iFrame embed (transaction details as query string → M-13-authenticated call returns a short-lived embed URL; the browser's iframe subsequently loads that URL against a separate, origin-checked-only M-03 rendering endpoint — see FR-F0-08), Response ingestion (transaction details + survey response → `202 ACCEPTED`, forwarded to M-04). **Illustrative endpoints**: base URLs/paths shown in the SRS are illustrative only; **normative regardless of final paths**: HTTP method semantics, the channel ID as the only mandatory path parameter (BR-03), request/response semantics per scenario, and the result-code catalogue.
- **FR-F0-02** — Ordered, atomic validation pipeline: (1) HTTPS/TLS 1.2+ enforced, plain HTTP refused; (2) authentication (API key header or OAuth bearer) — invalid/revoked/unknown → `401 E-1401`; (3) per-integration rate limit, default 100 req/s → `429 E-1429`; (4) payload size ≤ 2 MB → `413 E-1413`; (5) channel resolution — unknown → `404 E-1001`; (6) channel status — inactive → `409 E-1004`; (7) required parameters per the channel contract → `400 E-1002`, first missing field named; (8) type & validation-rule checks → `422 E-1003`, a validation failure rejects the whole request; (9) unregistered key–value pairs separated and stored raw, request logged, scenario processing executes. Any failure at steps 2–8 rejects the entire request atomically — nothing is forwarded downstream.
- **FR-F0-03** — Normative result-code catalogue: `202 ACCEPTED` · `200 OK` · `400 E-1002 MISSING_REQUIRED_PARAMETER` · `401 E-1401 INVALID_CREDENTIALS` · `404 E-1001 UNKNOWN_SERVICE_CHANNEL` · `409 E-1004 CHANNEL_INACTIVE` · `413 E-1413 PAYLOAD_TOO_LARGE` · `422 E-1003 INVALID_PARAMETER_VALUE` · `429 E-1429 RATE_LIMIT_EXCEEDED` · `500 E-1500 INTERNAL_ERROR` (caller may retry idempotently). **Normative message copy patterns** `[UI]`: `202` dispatch — *"Survey request accepted for channel distribution (M-02)."* · `202` ingestion — *"Response forwarded to M-04 Response Collection."* · `E-1002` — *"Required parameter 'mobile' is missing for service channel E-SERVICES-PORTAL."* · `E-1003` — *"Value '07701' for 'mobile' failed validation rule for type Phone."* · `E-1401` (revoked) — *"API key was revoked on ‹date›. Generate a new key in Integrations."* · `E-1500` — *"Unexpected error while queueing the request. The caller may retry with the same transaction_id (idempotent)."* API `message` fields are developer-oriented English; localisation of API messages is not required (SRS Error Handling).
- **FR-F0-04** — Thirteen normative data types and their validation formats (VR-T01…T13): Text (UTF-8, max length default 255, optional regex), Number (integer/decimal, optional min/max), Boolean (`true/false`/`1/0`, case-insensitive), Email (RFC 5322 basic), Phone (E.164, `+` and 8–15 digits), List (UTF-8 ≤ 100 chars, membership **not** enforced — unmapped values accepted, translated at read time), Range (numeric, must fall within configured min/max inclusive; min/max/unit configured on type selection), Date (ISO 8601 `YYYY-MM-DD`), Date & time (ISO 8601 with timezone), Currency (decimal amount + ISO-4217 code, optional min/max on amount), Percentage (decimal, default bounds 0–100, configurable), URL (RFC 3986 absolute), Geolocation (lat −90…90, long −180…180). Every validation failure rejects the request with `E-1003`. **Mapping capability is determined by the data type (BR-27, `[PO-G25]`): List — always enabled, not changeable; Text, Boolean, URL — available, disabled by default, user-changeable; all other types — unavailable (disabled, not changeable).** **The type list is closed: Duration and Identifier were evaluated and rejected (`[PO-G17]`) and MUST NOT appear as data types anywhere** — including the SCR-06 type select (guarded by a field-set/contract test mirroring `CredentialFieldSetGuard`).
- **FR-F0-05** — Mapping resolution model: mappings resolve at **read time** (reports/dashboards/exports translate stored source values through the *current* mapping table; changing a mapping retroactively relabels historical responses by design); incoming values with no mapping are never rejected (raw value stored, displayed as-is, enters the unmapped-values queue); no version history exists — the audit trail is the sole change record; Replace-all is irreversible.
- **FR-F0-06** — Unregistered parameters (key–value pairs with no parameter definition) are accepted and stored raw; visible **only** in request logs; excluded from reports, dashboards, filters, and rule builders until formally registered (a parameter is created whose API field name matches the key).
- **FR-F0-07** — Idempotency: retries carrying the same `(tenant, channelId, transaction_id)` are safe end-to-end — M-13 accepts the retry (a new log entry is written) and downstream deduplication guarantees no duplicate survey (SCN-01/02) and no duplicate stored response (SCN-05), **within no fixed, guaranteed retention window** (see Clarifications, 2026-07-27): a retry submitted after a sufficiently long delay may be treated as a new request, producing a duplicate dispatch or stored response — an accepted limitation, not a defect.
- **FR-F0-08** — Link & iFrame security: SCN-02 survey links expire **24 hours** after issue by default (`expires_at` returned to the caller, override configurable per FR-S2-10); SCN-04 is a **two-step flow** (see Clarifications, 2026-07-27): the caller's backend makes M-13's normally-authenticated `GET .../survey-embed/{channelId}` call to obtain a short-lived embed URL, and the end-customer's browser separately loads that URL from M-03's rendering endpoint (CMC-02), which is unauthenticated but enforces the per-integration **Allowed Origins whitelist** (configured in M-13, FR-S2-10) against the browser's request origin — embedding from a non-whitelisted origin is refused there; JSON/iFrame definitions retrieved from M-03 over HTTPS.
- **FR-F0-09** — Trigger rules (boundary): Phase 1 contains no trigger-rule engine — every request that passes validation is processed by its scenario (BR-01); rule-based eligibility/sampling is a deferred capability.
- **FR-F0-10** — Built-in parameter catalogue (normative, minimum set — 23 parameters): Customer ID `customer_id` (Text) · Customer Name `customer_name` (Text) · Customer Type `customer_type` (List — mapping always on) · Customer Segment `customer_segment` (List — mapping always on) · VIP `vip` (Boolean) · Gender `gender` (List — mapping always on) · Nationality `nationality` (List — mapping always on) · Mobile `mobile` (Phone) · Email `email` (Email) · Transaction ID `transaction_id` (Text) · Transaction Date `transaction_date` (Date & time) · Service `service` (List — mapping always on) · Product `product` (List — mapping always on) · Branch `branch` (List — mapping always on) · Department `department` (List — mapping always on) · Region `region` (List — mapping always on) · Journey `journey` (List — mapping always on) · Journey Stage `journey_stage` (List — mapping always on) · Touchpoint `touchpoint` (List — mapping always on) · Agent `agent` (Text) · Employee `employee` (Text) · Service Channel `service_channel` (List — mapping always on; system-populated from the path channel) · Source System `source_system` (Text). Built-ins can be enabled/disabled but never deleted or renamed at the API-field level, and their **data type is read-only** (`[PO-G27]`, BR-09); all 23 ship enabled by default (BR-23). Mapping support follows BR-27 (`[PO-G25]`): every **List** parameter is mapping-enabled (always on, not changeable); **Text/Boolean/URL** parameters may enable it (off by default); all other types cannot.

### Functional Requirements — SCR-01 Integrations (list)

- **FR-S1-01** — Render three stat tiles (Integrations, Requests · 24h, Error rate · 24h) computed over the rolling window.
- **FR-S1-02** — Live name search AND-combined with the service-channel filter.
- **FR-S1-03** — Render the integrations table: Integration (name + credential-kind/created-date or "suspended" sub-line) · Service channel (monospace chip) · Scenario (badge) · Authentication (badge) · Status (Active/Inactive) · Requests·24h · Error rate (semantic badge or "—") · Last activity (relative time) · row actions (View logs, Edit).
- **FR-S1-04** — Navigation: header **New integration** → SCR-02 create; header **View request logs** → SCR-08; row **View logs** → SCR-08 filtered to the integration; row **Edit** → SCR-02 pre-filled.
- **FR-S1-05** — Traffic figures derive from request logs; "—" error rate when there is no traffic.
- **FR-S1-06** — Error-rate colour thresholds: `< 1%` D2, `1–5%` D3, `> 5%` D4.

**SCR-01 shipped copy & guidance** `[UI]`: tile sub-texts — Integrations: *"Across n service channels"*; Requests · 24 h: *"All scenarios, all integrations"*; Error rate: *"n failed of m requests"*. Search placeholder *"Search integrations…"*; channel filter first option *"All service channels"*. Footer boundary note (user guidance): *"Delivery of dispatched surveys … is owned by M-02 … Validation, deduplication and storage … by M-04 …"* (full text per SRS §SCR-01 Layout).

### Functional Requirements — SCR-02 New/Edit Integration (3-step wizard)

- **FR-S2-01** — Three-step wizard: step indicator, Back/Continue/Create controls, cancel-discard, state reset on re-entry, edit-mode pre-fill.
- **FR-S2-02** — Step-1 fields: `name` (required, unique per tenant, ≤ 100 chars, VR-F01), `serviceChannel` (required, **active channels only**, rendered "Name — CHANNEL-ID"), `description` (optional), `scenario` (exactly one of 5 radio cards).
- **FR-S2-03** — Exactly-one scenario selection via the five radio cards (BR-02).
- **FR-S2-04** — Mechanism radio (API key / OAuth 2.0) switches the visible configuration dynamically.
- **FR-S2-05** — API-key generation (show-once, Dialog D-1) and revocation (Dialog D-3).
- **FR-S2-06** — OAuth client generation (show-once, Dialog D-2) with scopes.
- **FR-S2-07** — Step-3 endpoint preview re-renders on scenario/channel change; Copy action.
- **FR-S2-08** — Accepted-parameters table re-renders from the selected channel's contract.
- **FR-S2-09** — Result-codes card renders the FR-F0-03 catalogue.
- **FR-S2-10** — Conditional security configuration: *Allowed origins* list for SCN-04 and *Link expiry* override (default 24h) for SCN-02, shown after scenario selection.

**Step-2 field sets (ratified, exact)**:
- **API key**: `keyLabel` (required text) · `currentKey` (read-only masked + **Revoke** button, visible only when an active key exists). **Must NOT include**: expiry field, allowed-source-IPs field, environment/sandbox field.
- **OAuth 2.0**: `clientName` (required text) · `tokenEndpoint` (read-only, illustrative default) · `scopes` (multi-select pill checkboxes; default `survey-requests:write` selected; values `survey-requests:write` [SCN-01], `survey-links:read` [SCN-02], `survey-definitions:read` [SCN-03], `survey-embed:read` [SCN-04], `responses:write` [SCN-05] — one scope per scenario endpoint, BR-26). **Must NOT include**: grant-type field (fixed `client_credentials` in code), access-token-lifetime field (fixed 15 minutes in code).

**Step-1 field details (shipped copy & defaults)** `[UI]`:
- `name` — placeholder *"e.g. Core Services Bus — Survey Dispatch"*; helper *"Shown in lists, logs and alerts. Unique within the tenant."*
- `serviceChannel` — **default: the first active channel**; helper *"Only active service channels are listed. The channel defines which parameters this API accepts and requires."*
- `description` — placeholder *"What system calls this integration and why."*
- `scenario` — **no default selection in create mode**; section guidance *"One scenario per integration — create a separate integration for each additional scenario."*
- Scenario-card descriptions (normative shipped copy): **Dispatch via Nabadat** — *"Caller sends the transaction details and receives a result code. Nabadat selects the delivery channel and sends the survey through M-02 Channels & Distribution."* · **Redirect link** — *"Caller receives a one-time survey URL and redirects the customer to it."* · **JSON render** — *"Caller receives the survey definition as JSON and renders it inside its own UI."* · **iFrame embed** — *"Caller displays the survey inside an embedded iFrame. Allowed embedding origins must be whitelisted."* · **Response ingestion** — *"Caller sends the transaction details together with the completed survey response; M-13 hands the payload to M-04 Response Collection for validation and storage."*

**Step-2 helper copy** `[UI]`: mechanism-card descriptions — API key: *"Static tenant-scoped key sent in the `X-Api-Key` header. Best for server-to-server calls from a trusted backend."*; OAuth 2.0: *"Client-credentials flow. The caller exchanges a client ID and secret for a short-lived access token. Best for shared enterprise buses."* Field helpers — `keyLabel`: *"Identifies the key in logs and in the key registry."*; `currentKey`: *"Generated ‹date› by ‹user›. Revoking rejects all further requests with `E-1401` immediately."*; `tokenEndpoint`: *"Access tokens are valid for a fixed **15 minutes**."*; `scopes`: *"Scopes limit which scenario endpoints a token may call."*

**Step-3 shipped copy & interaction** `[UI]`: the endpoint preview's **Copy** button flips its label to *"Copied ✓"* on click; the sample body carries the guidance comments *"// Body — key–value pairs. The service channel ID is the only mandatory path parameter; // required body fields come from the channel contract."* (query-string sample for SCN-04; `survey_response` object added for SCN-05). Accepted-parameters card description: *"Inherited from the ‹channel name› channel contract. Other key–value pairs are accepted and stored as unregistered parameters — excluded from reports, dashboards, filters and rule builders until formally registered."* Result-codes card description: *"The caller always receives a structured result code from the normative catalogue below."*

### Functional Requirements — SCR-03 Service Channels (list)

- **FR-S3-01** — Render the service-channels table: Service channel (name + description) · Channel ID (monospace chip) · Status · Supported-params count · Required count · Integrations count · row action (Edit).
- **FR-S3-02** — No delete action exists anywhere (BR-07).
- **FR-S3-03** — Navigation to SCR-04 (create / row edit).

**SCR-03 shipped guidance** `[UI]`: footer note — *"**Not the same as distribution channels.** Service channels describe where the transaction happened; the channels used to deliver surveys (WhatsApp, SMS, email…) are configured in **M-02 Channels & Distribution**."*

### Functional Requirements — SCR-04 Service Channel Create/Edit

- **FR-S4-01** — Identity field set: EN/AR names, manually entered channel ID with live sanitisation, description, Active toggle.
- **FR-S4-02** — Channel-ID lock behaviour per BR-05: read-only with explanation after the channel's first successful (2xx) request.
- **FR-S4-03** — Live contract-summary alert with supported/required counts.
- **FR-S4-04** — Parameter-contract table with live filter and the Supported → Required dependency (Required enabled only while Supported is on).

**SCR-04 field details (shipped copy & defaults)** `[UI]`:
- `nameEn` — placeholder *"e.g. Self-Service Kiosk"*; helper *"Max 50 characters. Unique within the tenant."*
- `nameAr` — RTL input with an Arabic example placeholder.
- `channelId` — placeholder *"e.g. SELF-SERVICE-KIOSK"*; helper (normative): *"Letters, numbers and \"-\" only · under 20 characters · no spaces. Editable until the channel receives its **first successful request** — locked permanently after that, because callers hard-code it in the endpoint path."* When locked, the field renders read-only with this explanation.
- `description` — placeholder *"What this channel covers and which backend serves it."*
- `active` — **default: On**; helper *"Inactive channels stop accepting API requests (`E-1004`) and are hidden from new integrations."*
- Contract-summary info alert (live): *"**Contract summary:** ‹n supported · m required› parameters. Required parameters missing from an incoming request are rejected with `E-1002`."*
- Parameter-contract card description: *"Turn on **Supported** for every field this channel's backend can send; mark **Required** to make it mandatory. Only active parameters are listed."* Contract filter placeholder *"Filter parameters…"*.

### Functional Requirements — SCR-05 Parameters (list)

- **FR-S5-01** — Origin tabs (All/Built-in/Custom, live counts) + name/API-field search + type filter, all combined AND.
- **FR-S5-02** — Parameters table: Parameter (dimmed if disabled) · API field (chip) · Type · Origin badge · Enabled (inline toggle) · Required/Filterable/Reporting/Dashboard (check/dash) · Mapping ("Mapped" link or "—") · Channels count · row action (Edit).
- **FR-S5-03** — Inline enable/disable toggle, guarded by the impact warning (Dialog D-6, BR-10) and audited.
- **FR-S5-04** — Navigation: **New parameter** → SCR-06 drawer; **Manage mappings** and per-row "Mapped" link → SCR-07.

**SCR-05 shipped guidance** `[UI]`: search placeholder *"Search by name or API field…"*; footer note names the catalogue's consumers (M-06 KPI Engine, M-07 dashboards, M-10 data-scope filters) and states the dependency guard: disabling a parameter referenced by scope filters, rules, or channel contracts requires an impact warning (BR-10).

### Functional Requirements — SCR-06 Parameter Editor (drawer)

- **FR-S6-01** — Drawer behaviour: opens over SCR-05 with scrim; closes via ✕, scrim click, or Esc.
- **FR-S6-02** — Field set with API-field auto-suggest and lock-on-first-use (BR-11).
- **FR-S6-03** — Conditional type configuration: Range card (min/max/unit) and List panel (mapping pointer, BR-12).
- **FR-S6-04** — Five usage flags with ratified defaults (Searchable removed, `[PO-G26]`): Required by default (Off), Filterable (On), Reporting visibility (On), Dashboard visibility (Off), Mapping support (per type — BR-27: forced On for List, Off by default for Text/Boolean/URL, disabled otherwise).
- **FR-S6-05** — Channel-assignment pills add the parameter as supported with the required-default applied (BR-08).

**SCR-06 field details (shipped copy & defaults)** `[UI]`:
- `nameEn` — placeholder *"e.g. Wait Time"* (typing auto-suggests the API field); `nameAr` — RTL, Arabic example placeholder; both max 50 (VR-F05).
- `apiField` — placeholder *"wait_time"*; helper (normative): *"snake_case, unique per tenant. This is the key the caller sends. Locked once the first request using it has been received — renaming after that would break the caller (tenet T-08)."*
- `type` — helper *"Range and List types take extra configuration below."*; the select offers **exactly the 13 ratified types — never Duration or Identifier** (`[PO-G17]`, FR-F0-04); **read-only when editing a built-in parameter** (`[PO-G27]`); the selection drives the Mapping-support flag state (BR-27).
- Range card — Minimum/Maximum required, Unit optional with placeholder *"minutes"*.
- List panel — *"List values and their source-value translations are managed in **Parameter mappings**."* + **Open mappings** button.
- `validationRule` — optional; placeholder *"e.g. ^[A-Z]{2}\d{6}$"*; helper *"Requests with values failing the rule are rejected with `E-1003`."*
- Usage-flag descriptions (shipped copy): **Required by default** — *"Default when assigned to a channel; each channel can override."* · **Filterable** — *"Available as a filter facet in reports and dashboards."* · **Reporting visibility** — *"Appears as a data column in reports (M-07)."* · **Dashboard visibility** — *"Available as a breakdown dimension on dashboards (M-06/M-07)."* · **Mapping support** — *"Source values are translated through the mapping table. Always on for **List**; optional for **Text**, **Boolean** and **URL**; not available for other types."* (state driven by the selected data type, BR-27)
- Channel-assignment helper: *"Assigning here adds the parameter as **supported** on the channel; fine-tune required/optional in the channel's contract."*

### Functional Requirements — SCR-07 Parameter Mappings

- **FR-S7-01** — Parameter selector lists mapping-enabled parameters only, re-renders the table on change.
- **FR-S7-02** — Unmapped-values alert with *Map now* pre-fill; hidden when the queue is empty.
- **FR-S7-03** — Mapping table with inline draft add-row; source values unique per parameter.
- **FR-S7-04** — Row edit and delete; delete behind confirmation (Dialog D-7); effective at read time immediately.
- **FR-S7-05** — Excel export with columns `source_value`, `display_en`, `display_ar`.
- **FR-S7-06** — Excel import (Dialog D-4): Merge / Replace-all modes; all-or-nothing with a row-level report.
- **FR-S7-07** — Replace-all (Dialog D-5): irreversible, permission-controlled, audited (BR-13).

**SCR-07 shipped copy & guidance** `[UI]`: unmapped-values warning alert (pattern, shown only when the queue is non-empty): *"**‹n› unmapped values received in the last 7 days:** ‹value chips› — responses carrying them display the raw value until mapped. Map now"* — *Map now* pre-fills a draft row. Toolbar shows an informational source-system badge (*"Source system: ‹name›"*). Inline draft-row placeholders: source *"S0xx"*, *"Display value (EN)"*, Arabic display placeholder (RTL). Footer information line: *"‹n› mappings · last updated ‹when› by ‹user›"* alongside the **Replace all mappings…** action.

### Functional Requirements — SCR-08 Request Logs

- **FR-S8-01** — Filters: status-class chips (All/Success·2xx/Client errors·4xx/Server errors·5xx), integration select, time select (Last hour/24h/7d/30d, **default: Last 24 hours**) — AND combination, counts per window.
- **FR-S8-02** — Log table with expandable detail: *Parameters received* (registered + unregistered) and *Response returned*.
- **FR-S8-03** — PII masking in list, detail, and export.
- **FR-S8-04** — Export of the current filtered view.
- **FR-S8-05** — Every request logged with the full field list; auth-rejected requests carry the rejected-before-parsing notice.

**SCR-08 shipped copy & guidance** `[UI]`: screen guidance ends *"…Click a row to expand the full exchange."* Masking info alert (normative): *"Personal data in logged parameters (mobile, email, customer name) is masked in all log views. Log retention: 90 days."* Auth-rejected detail notice (normative): *"— request rejected before parameter parsing"*.

### Cross-Screen Business Rules

- **BR-01** — Phase 1 has no trigger-rule engine: every request passing validation is processed; eligibility/sampling rules are deferred.
- **BR-02** — Exactly one integration scenario per integration; an additional scenario requires an additional integration.
- **BR-03** — The service channel ID is the only mandatory parameter of any M-13 API, carried as the path parameter; all other parameters are free key–value pairs.
- **BR-04** — Channel ID format: manual entry, letters/numbers/`-` only, under 20 characters, no spaces/special characters, unique per tenant.
- **BR-05** — Channel ID lifecycle: editable until the channel's first successful (2xx) request, then locked permanently. Pre-lock edits change the endpoint path; the old ID resolves `E-1001`.
- **BR-06** — Channel display names are bilingual (EN + AR); renaming never affects the ID.
- **BR-07** — Inactive channels reject requests with `E-1004`, are hidden from new-integration selection, remain listed. Channels with traffic history cannot be deleted — deactivate only.
- **BR-08** — The channel contract (supported/required per channel) is the authority on requiredness at request time; the parameter-level "Required by default" flag is only the assignment default.
- **BR-09** — Built-in parameters: enable/disable only — never deleted, never renamed, and their data type is read-only (`[PO-G27]`). Custom parameters: disabled, never hard-deleted.
- **BR-10** — Disabling a parameter referenced by M-10 data-scope filters, rule builders, or a channel contract requires an explicit impact warning listing the references.
- **BR-11** — API field names are `snake_case`, unique per tenant, locked once the first request carrying them has been received.
- **BR-12** — The mapping table is the single source of List values; List membership is not validated at ingestion.
- **BR-13** — Mappings are bilingual, resolve at read time (retroactive relabelling by design), have no version history, and Replace-all is irreversible. Unmapped incoming values are stored raw, never rejected, queued for mapping.
- **BR-14** — Unregistered (extra) parameters never block processing — the request proceeds normally; each extra parameter is stored raw, **reported in the request logs**, and excluded from reports/dashboards/filters/rule builders until formally registered (`[PO-G28]` — reaffirms `[PO-G09]`).
- **BR-15** — Validation failures reject the whole request with the defined business error code; requests are atomic.
- **BR-16** — Credential secrets are shown exactly once at generation and stored hashed/encrypted; API-key revocation is immediate (`E-1401`); generating a new key revokes the active one.
- **BR-17** — OAuth: client-credentials grant fixed in code; access-token lifetime fixed at 15 minutes in code; scopes limit which scenario endpoints a token may call.
- **BR-18** — Retries with the same `(tenant, channelId, transaction_id)` are idempotent end-to-end, with no fixed, guaranteed retention window; a sufficiently late retry may be processed as a new request (accepted limitation, not a defect).
- **BR-19** — Survey resolution (which survey applies) is owned by M-02 rules for all scenarios; M-13 never selects surveys.
- **BR-20** — Survey links (SCN-02) expire 24 hours after issue by default; iFrame embedding (SCN-04) requires an Allowed-Origins whitelist; all communication is HTTPS.
- **BR-21** — All sensitive configuration actions are permission-controlled and audited.
- **BR-22** — No migration from the legacy system: greenfield configuration only.
- **BR-23** — All 23 built-in parameters ship enabled by default.
- **BR-24** — Cross-persona read-only visibility: P-01 may view **Integrations screens** and P-07 may view data-model screens, read-only, via the `*.view` permission keys. **Request logs (SCR-08) are exclusive to P-07 — P-01 has no log access** (PO ruling, 27 Jul 2026; SRS v1.2 erratum).
- **BR-25** — Credentials generated inside a cancelled create-wizard are discarded with the draft.
- **BR-26** — OAuth scope naming: one scope per scenario endpoint following the `‹resource›:‹verb›` convention; the full ratified set of five is `survey-requests:write` (SCN-01 dispatch), `survey-links:read` (SCN-02 redirect link), `survey-definitions:read` (SCN-03 JSON render), `survey-embed:read` (SCN-04 iFrame embed), `responses:write` (SCN-05 response ingestion).
- **BR-27** — Mapping capability is determined by the data type: **List** — always enabled, not changeable; **Text, Boolean, URL** — available, disabled by default, user-changeable; **all other types** — unavailable (disabled, not changeable). (`[PO-G25]`)

### Validation Rules (consolidated register)

Data-type validation rules carry IDs **VR-T01…VR-T13** (see FR-F0-04). Field- and entity-level rules:

- **VR-F01** — Integration name (SCR-02): required, unique per tenant, ≤ 100 characters. Violation → inline error, save blocked.
- **VR-F02** — Channel name · EN (SCR-04): required, ≤ 50 characters, unique per tenant. Violation → inline error.
- **VR-F03** — Channel name · AR (SCR-04): required. Violation → inline error.
- **VR-F04** — Service channel ID (SCR-04): required; letters/digits/`-` only; < 20 chars (`maxlength` 19); no spaces/special characters; invalid characters stripped live; unique per tenant **case-insensitively**; stored and matched in the URL exactly as entered. Violation → inline error / live strip.
- **VR-F05** — Parameter names EN/AR (SCR-06): required, ≤ 50 characters. Violation → inline error.
- **VR-F06** — API field name (SCR-06): required, `snake_case`, unique per tenant across built-in/custom/enabled/disabled. Violation → inline error, save blocked.
- **VR-F07** — Range configuration (SCR-06): Minimum and Maximum required; Minimum < Maximum. Violation → inline error.
- **VR-F08** — Mapping source value (SCR-07): required, unique within the parameter **case-insensitively** (matches VR-F01/VR-F04's convention). Violation → inline error, save blocked.
- **VR-F09** — Excel import file (D-4): columns `source_value`, `display_en`, `display_ar`; duplicates within the file rejected; import all-or-nothing with row-level report.
- **VR-F10** — Key label / Client name (SCR-02): required. Violation → inline error.
- **VR-F11** — API request payload: ≤ 2 MB. Violation → `413 E-1413`.
- **VR-F12** — Console message copy patterns: "‹Field› is required" / "‹Value› is already in use".
- **VR-F13** — Tenant capacity guardrails on create (SCR-02 integrations, SCR-04 channels, SCR-06 custom parameters): the tenant must not already be at its NFR-16 ceiling (200 integrations / 100 channels / 200 custom parameters). Violation → inline console error naming the limit reached (pattern: "You've reached the limit of ‹n› ‹entity› for this tenant."); creation blocked, no API-level result code.

### Status Lifecycle

- **Integration**: Active ⇄ Inactive (P-07, audited); Inactive → endpoint rejects calls with `401 E-1401` (credentials suspended, `[Derived from UI]`). Invalid transition: Delete (does not exist).
- **Service channel**: Active ⇄ Inactive (`E-1004` when inactive); Channel-ID sub-state Editable → **Locked** on first 2xx (one-way). Invalid transitions: Delete after traffic; unlock.
- **Parameter**: Enabled ⇄ Disabled (guarded by BR-10); API-field sub-state Renameable → **Locked** on first use (one-way; built-ins always locked). Invalid transitions: Hard delete; rename built-in.
- **Mapping entry**: Draft (unsaved inline row) → Active; Active → deleted (immediate read-time effect). Invalid transition: Restore (no history).
- **Credential**: Active → Revoked (one-way); a newly generated credential supersedes the active one. Invalid transitions: Un-revoke; plaintext retrieval.
- **Request log entry**: Immutable once written; purged at retention (90 days). Invalid transitions: Edit/delete by users.

### Cross-Module Contracts

- **CMC-01 (M-02 Channels & Distribution)** — Owns survey dispatch: survey resolution (which survey applies — for **all** scenarios), delivery-channel selection, sending, retries, cadence. M-13 hands off accepted SCN-01 requests (tenant, channel ID, transaction parameters, request id) and stops; M-13's `202` means accepted-to-queue; M-02 delivery failures never surface as M-13 API errors.
- **CMC-02 (M-03 Survey & Forms)** — Owns survey definitions and rendering. M-13 retrieves the definition JSON (SCN-03) / embed URL (SCN-04) for the resolved survey and relays it unchanged, treating the schema as opaque. For SCN-04 specifically, M-03 also owns the actual browser-facing rendering endpoint that the returned embed URL points at — an unauthenticated, origin-checked-only endpoint distinct from M-13's authenticated API surface (see FR-F0-08).
- **CMC-03 (M-04 Response Collection)** — Owns response validation, deduplication (key: tenant + channel ID + `transaction_id`), and storage. M-13 forwards SCN-05 payloads only after they pass M-13's own contract validation (FR-F0-02); **M-04 MUST save every such payload unconditionally** — no discretionary M-04-side rejection path exists that could silently drop an already-accepted response (see Clarifications, 2026-07-27). M-13's `202` means delivered-to-M-04, and "delivered" and "durably stored" are guaranteed equivalent for any payload M-13 forwards.
- **CMC-04 (M-06 KPI Engine / M-07 Dashboards & Reporting)** — Consume transaction metadata and the parameter catalogue: *Reporting visibility* → report column; *Dashboard visibility* → breakdown dimension; *Filterable* → filter facet; read-time mapping resolution applies wherever display values render.
- **CMC-05 (M-09 Notifications)** — Operational alerting on integration failures is a future phase; Phase 1 only logs failures. No M-13 requirement may assume M-09 delivery.
- **CMC-06 (M-10 User & Role Management)** — M-13 registers its permission keys in the Permissions Matrix and delegates authorisation. M-10 data-scope filters are built on M-13 parameter definitions and value sets; BR-10's impact warning protects that dependency.
- **CMC-07 (M-14 / M-15 / M-16 — rules, actions, journeys)** — May reference M-13 parameters; such references participate in the BR-10 impact warning.

### Permissions Matrix

Interim, ratified action-level split (refined later by M-10):

| Action | P-07 Tenant IT Admin | P-01 CX Manager | Audited |
|---|---|---|---|
| View integrations & wizard (`m13.integration.view`) | ✓ | Read-only (BR-24) | — |
| Create/edit integration, scenario, settings (`m13.integration.manage`) | ✓ | — | ✓ |
| Activate/deactivate integration (`m13.integration.manage`) | ✓ | — | ✓ |
| Generate credentials (`m13.credential.manage`) | ✓ | — | ✓ |
| Revoke API key (`m13.credential.manage`) | ✓ | — | ✓ |
| View/export request logs (`m13.log.view`) | ✓ | — | — |
| View channels/parameters/mappings (`m13.channel.view`, `m13.parameter.view`) | Read-only (BR-24) | ✓ | — |
| Create/edit service channel (`m13.channel.manage`) | — | ✓ | ✓ |
| Change channel ID pre-lock (`m13.channel.manage`) | — | ✓ | ✓ |
| Activate/deactivate channel (`m13.channel.manage`) | — | ✓ | ✓ |
| Create/edit parameter, flags, validation (`m13.parameter.manage`) | — | ✓ | ✓ |
| Enable/disable parameter incl. built-ins (`m13.parameter.manage`) | — | ✓ | ✓ |
| Add/edit/delete mappings (`m13.mapping.manage`) | — | ✓ | ✓ |
| Import/export mappings (`m13.mapping.manage`) | — | ✓ | ✓ (import) |
| Replace all mappings (`m13.mapping.replace`) | — | ✓ | ✓ |

Audit events (actor, tenant, timestamp, entity, before/after summary): integration created/updated · integration activated/deactivated · credential generated · credential revoked · channel created/updated · channel ID changed · channel activated/deactivated · parameter created/updated · parameter enabled/disabled · mapping added/edited/deleted · mapping import (mode, row count) · mapping replace-all (rows removed/added). With version history descoped, these events are the sole change record for mappings.

### Error Handling

**API (caller-facing)**: normative catalogue and pipeline per Feature 0; every response carries a structured result code. Duplicate `transaction_id` is not an error (BR-18). Integration/downstream failure: `500 E-1500` with the retry-idempotent message; M-13 never exposes downstream (M-02/M-03/M-04) errors directly.

**Console (user-facing)**:
- Validation errors: inline, per field, on blur/save — required, uniqueness (integration name, channel ID, API field, mapping source value), charset/length (channel ID), Range min<max, tenant capacity guardrails (VR-F01…F13, copy per VR-F12).
- Permission errors: actions the role lacks are hidden or disabled; direct-route access without view permission → access-denied state.
- System/network errors: standard error state with retry; dialogs preserve entered data on failure.
- Missing data: unmapped values → the SCR-07 queue alert (never an error).
- Duplicate records: blocked at save with uniqueness messages.
- Concurrency: last-write-wins with full audit trail.
- Import errors: row-level validation report; all-or-nothing application.

### Notifications & Dialogs

- **D-1 — API key generated**. Title "API key generated"; text "Copy the key now — for security it is shown **only once**."; code panel + Copy; warning: "**Store it in your secrets manager.** If lost, revoke it and generate a new key — revocation takes effect immediately."; button **Done** (primary). Esc/outside-click closes; closing without copying is allowed.
- **D-2 — Client credentials generated**. Same shape as D-1 with `client_id`/`client_secret`.
- **D-3 — Revoke this API key?**. "All requests signed with ‹masked key› are rejected with `E-1401` the moment you confirm. The caller must switch to a newly generated key. This cannot be undone." Buttons: **Cancel** (outline) / **Revoke key** (destructive filled) — revokes immediately, audited.
- **D-4 — Import mappings from Excel**. "Template columns: `source_value` `display_en` `display_ar`. Duplicate source values within the file are rejected; existing values are updated." Import-mode radio: *Merge with existing* (default) / *Replace all*. Import is all-or-nothing with a row-level report. Buttons: **Cancel** / **Import**.
- **D-5 — Replace all mappings?**. "This removes all **‹n› current mappings** for **‹parameter›** and replaces them with the imported set. This action cannot be undone." Buttons: **Cancel** / **Replace all** (destructive filled) — audited.
- **D-6 — Disable parameter: impact warning**. Lists each referencing scope filter, rule, and channel contract by name. Buttons: **Cancel** (no change) / **Disable anyway** (destructive) — audited.
- **D-7 — Delete mapping confirmation**. "Delete mapping ‹source value› → ‹display EN›? Responses carrying this value will display the raw value until remapped." Buttons: **Cancel** / **Delete** (destructive) — audited.
- **Inline alerts** `[UI]`: SCR-04 contract-summary (info), SCR-07 unmapped-values (warning), SCR-08 masking/retention (info) — copy per the per-screen shipped-copy blocks above.
- **Inline feedback** `[UI]`: Copy buttons flip to *"Copied ✓"*.
- Success toasts on create/save/import/revoke (FR-GBL-04). No email/system notifications in Phase 1 — operational alerting deferred to M-09.

### Non-Functional Requirements

- **NFR-1** — API availability 99.9% monthly.
- **NFR-2** — 95% of API requests complete within 500ms, excluding downstream systems.
- **NFR-3** — Maximum request payload 2 MB.
- **NFR-4** — Default rate limit 100 requests/sec per integration, configurable by Nabadat Operations with no code changes.
- **NFR-5** — HTTPS with TLS 1.2+ everywhere (API and console).
- **NFR-6** — Secrets encrypted/hashed at rest; show-once at generation; never logged.
- **NFR-7** — All configuration changes audited.
- **NFR-8** — Request-log retention 90 days; tenant-specific retention by subscription plan is future scope.
- **NFR-9** — Multi-tenant isolation: integrations, credentials, channels, parameters, mappings, and logs are tenant-scoped; no cross-tenant access.
- **NFR-10** — Localization: console fully bilingual EN/AR with RTL layout; AR inputs render RTL with the Arabic font stack; light and dark themes per the Nabadat design system.
- **NFR-11** — Accessibility: keyboard operability of dialogs/drawer (Esc closes), focus-visible rings, reduced-motion support.
- **NFR-12** — Responsive behaviour: desktop-first; tiles collapse to two/one columns and the sidebar hides below tablet width; tables scroll horizontally.
- **NFR-13** — Usability: destructive actions always behind explicit confirmation naming the consequence.
- **NFR-14** — Browser support: current evergreen Chrome/Edge/Firefox/Safari.
- **NFR-15** — Session handling: platform-standard console session; API has no session (per-request auth).
- **NFR-16** — Scalability guardrails: ≤ 200 custom parameters, ≤ 100 channels, ≤ 200 integrations per tenant; ≤ 5,000 mappings per parameter; Excel import ≤ 10,000 rows. Enforcement: the mapping-related guardrails have dedicated inbound-API-adjacent guards (`ImportRowCountGuard`, `MappingsPerParameterGuard`, User Story 7); the per-tenant creation guardrails (custom parameters, channels, integrations) are enforced as a **console-side validation error** on the create action (VR-F13) — no inbound-API result code, since these entities are created only through the console, never via the caller-facing API.
- **NFR-17** — Concurrency: last-write-wins with full audit records; no pessimistic locking in Phase 1.

### Key Entities

- **Integration** — Name (unique, VR-F01) · description · service channel · scenario (one of five) · authentication type · status (Active/Inactive) · SCN-04 allowed-origins list and SCN-02 link-expiry override · creation metadata.
- **Credential set** — Belongs to one integration. API key (label, show-once secret, Active/Revoked) **or** OAuth client (client name, scopes, show-once secret).
- **Service channel** — Name EN + AR · channel ID (VR-F04, lock state) · description · status · parameter contract.
- **Parameter** — Name EN + AR · API field name (VR-F06, lock-on-first-use) · data type + type configuration (Range min/max/unit; List via mappings; type read-only for built-ins, `[PO-G27]`) · validation rule · origin (built-in/custom) · enabled state · five usage flags (Searchable removed `[PO-G26]`; Mapping support per BR-27) · channel assignments.
- **Channel-parameter assignment** — Channel × parameter with `supported` and `required` flags — the contract row.
- **Mapping entry** — Parameter × source value (unique per parameter, VR-F08) with display EN + AR and status (Draft/Active).
- **Unmapped-value queue item** — Parameter × raw source value with a 7-day occurrence window.
- **Request log entry** — Timestamp · integration · method + path · scenario · all parameters received (registered + unregistered, PII-masked at display) · full response returned · HTTP status · result code · latency · credential label · rejection stage where applicable. Immutable; retained 90 days.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001 — Onboarding speed** — A Tenant IT Administrator can onboard a new integration (name + channel + scenario + credential generation) and have its endpoint callable in under 5 minutes from opening the wizard, verified on the golden path of User Story 3.
- **SC-002 — Health-at-a-glance** — A Tenant IT Administrator viewing the Integrations list can identify an unhealthy integration (error rate above 5%) within 10 seconds of page load, verified on a seeded tenant of 20 integrations.
- **SC-003 — Request-processing reliability** — 100% of requests that fail the validation pipeline are rejected atomically with the correct normative result code and zero partial downstream side effects, verified via User Story 4's integration and scenario tests.
- **SC-004 — Idempotency correctness** — 100% of retried requests carrying an identical `(tenant, channelId, transaction_id)` produce exactly one downstream dispatch/store action regardless of retry count, verified via User Story 4's scenario test.
- **SC-005 — Zero data loss on unmapped/unregistered values** — 100% of unmapped List values and unregistered key–value pairs are preserved raw (never rejected, never silently dropped), verified via User Stories 4 and 6.
- **SC-006 — Retroactive mapping correctness** — 100% of historical data re-renders with an updated display label immediately after a mapping edit or replace-all, with zero stale-label reads, verified via User Stories 6 and 7's scenario tests.
- **SC-007 — Bulk import safety** — 0 partial imports occur across any invalid Excel file — every import is fully applied or fully rejected, verified via User Story 7's `AC-S7-01` test.
- **SC-008 — Credential security** — 0 instances of a plaintext credential secret appearing anywhere after its show-once dialog closes (screen, log, API, export), verified via User Story 3's `AC-S2-02` test and a repo-wide log/response-schema audit.
- **SC-009 — PII protection** — 100% of mobile/email/customer-name values render masked in every list, detail, and export view, with zero unmasked-access code paths in Phase 1, verified via User Story 5's `AC-S8-02` test.
- **SC-010 — Cross-persona isolation** — 0 successful writes occur via the API surface when attempted by a persona lacking the `*.manage` permission for that entity, verified via User Story 9's integration tests.
- **SC-011 — Audit completeness** — Every one of the 12 audited action families in the Permissions Matrix emits an audit event with actor, tenant, timestamp, entity, and before/after summary, verified via audit-log assertions across every story's integration/scenario tests.
- **SC-012 — Bilingual parity** — All eight screens render with full parity in Arabic (RTL) and English (LTR), with 0 physical direction properties in the M-13 codebase (verified per the CLAUDE.md self-review regex and RTL logical-property scan).
- **SC-013 — Performance targets** — 95% of API requests complete within 500ms excluding downstream systems (NFR-2); console screens meet the platform's standard load-time expectations, verified via performance regression tests on a seeded tenant.
- **SC-014 — Accessibility** — 0 WCAG 2.1 AA violations in an automated axe scan of all eight screens in both LTR and RTL; keyboard-only operation of every dialog/drawer (Esc closes) is possible.
- **SC-015 — Capacity guardrails held** — 0 tenant configurations exceed 200 custom parameters, 100 channels, 200 integrations, 5,000 mappings per parameter, or a 10,000-row import, verified via NFR-16 guard tests.
- **SC-016 — Guaranteed response durability** — 0 SCN-05 responses that pass M-13's own validation pipeline are ever lost or unstored downstream — every payload M-13 forwards to M-04 is durably saved (CMC-03), verified via a scenario test spanning M-13's `202` response through to a confirmed M-04-side stored record.

---

## Assumptions

- **A-2 (from the SRS, carried forward as the sole remaining business-behaviour assumption)** — Retro-reportability: when an unregistered key is later registered as a parameter, previously received raw values become reportable going forward (a consequence of raw storage + read-time resolution). The PO may confirm or reverse this without structural impact; it does not block this spec.
- **Prototype file absence** — `M13-Integration-Hub-Prototype-v0.5-Ratified.html` is referenced throughout the SRS via `[UI]` tags but is not present in this repository at spec-writing time. Every `[UI]`-tagged detail that affects functional behaviour, interaction, navigation, validation, workflow, or user guidance is preserved inline in this spec; **purely visual/design-system details are intentionally excluded** and remain normative in the SRS and the Nabadat design system, so the absence of the raw HTML file creates no functional information gap for planning or implementation.
- **Persona naming reconciliation** — the SRS's "P-01 CX Manager" and the platform constitution's canonical "P-01 CX Program Manager" (Section 8 registry) refer to the same persona ID; this spec uses "CX Manager" (the SRS's term) when discussing M-13-specific behaviour and notes the canonical registry name here for cross-module consistency.
- **Owned-table naming** — the constitution's Section 3 registry currently lists M-13's owned tables as the Phase-1 reservation placeholders `api_keys`, `webhook_configs`, `connector_configs`, `integration_log` (AD-06/DB-06). This spec's Key Entities (Integration, Credential set, Service channel, Parameter, Channel-parameter assignment, Mapping entry, Unmapped-value queue item, Request log entry) do not map 1:1 onto those four placeholder names. Reconciling the real schema against the registry (and filing the resulting constitution correction, mirroring the AMENDMENT-011/012 and M-15 precedents) is a planning-phase concern, not a spec-level blocker.
- **Illustrative endpoint paths** — the specific URL paths shown throughout the SRS (`https://api.nabadat.cx/v1/…`, `https://auth.nabadat.cx/oauth2/token`) are illustrative only; final API paths are fixed at implementation time per the platform's `/api/v1/` versioning convention (constitution API-01), not frozen by this spec.
- **Existing M-10 forward-reference** — `src/Nabadat.UserManagement/Application/Permissions/M13ParameterContractAdapter.cs` and related types already exist in the codebase, anticipating M-13's parameter-contract shape for M-10's data-scope filters (CMC-06). This spec's Parameter/Channel-contract entities should be reconciled against that existing adapter at planning time so the two modules' contracts agree.

### Dependencies

- **M-02 (Channels & Distribution)** — owns survey resolution (all scenarios), delivery-channel selection, sending, retries. M-13 hands off and stops (CMC-01).
- **M-03 (Survey & Forms)** — owns survey definitions and rendering; M-13 retrieves and relays unchanged (CMC-02).
- **M-04 (Response Collection)** — owns response validation, deduplication, and storage; M-13 forwards ingestion payloads, and M-04 must save every accepted payload unconditionally, with no silent-drop path (CMC-03).
- **M-06 / M-07 (KPI Engine / Dashboards & Reporting)** — consume transaction metadata and the parameter catalogue via Reporting/Dashboard/Filterable visibility flags (CMC-04).
- **M-09 (Notifications)** — operational alerting deferred in full; Phase 1 only logs failures (CMC-05).
- **M-10 (User & Role Management)** — M-13 registers permission keys and delegates authorisation; M-10 data-scope filters are built on M-13 parameter definitions (CMC-06) — a forward reference to this dependency already exists in `Nabadat.UserManagement` (see Assumptions above).
- **M-14 / M-15 / M-16 (rules, actions, journeys)** — may reference M-13 parameters; such references participate in the BR-10 impact warning (CMC-07).
- **Platform audit service** — M-13 emits audit events per the Permissions Matrix; M-17 owns `audit_log`/`event_log` per the platform constitution.
- **Platform Settings / Nabadat Operations tooling** — per-integration rate-limit configuration (NFR-4) is an operator-facing capability outside the M-13 tenant console itself.

---

## SRS Coverage Checklist

*Verification that every SRS section has been processed and represented in this specification.*

| SRS Section | Represented in this spec? | Where |
|---|---|---|
| Purpose, Scope (in/out/descoped/deferred), Actors, User Roles | ✔ | Overview |
| Navigation Overview (screen hierarchy, sidebar, top bar, entry/exit points) | ✔ | Requirements → "Navigation (functional)" (groups, breadcrumb, deep links, artifact exclusions) + per-screen FR blocks; visual shell treatment intentionally excluded per the preserve/omit rule |
| FR-GBL-01…05 | ✔ | Requirements → Global Console Behaviours |
| Feature 0 (FR-F0-01…10, AC-F0-01…05) | ✔ | User Story 4 + Requirements → Feature 0 |
| SCR-01 (FR-S1-01…06, AC-S1-01…03) | ✔ | User Story 5 + Requirements → SCR-01 |
| SCR-02 (FR-S2-01…10, AC-S2-01…04, D-1/D-2/D-3) | ✔ | User Stories 3 & 8 + Requirements → SCR-02 |
| SCR-03 (FR-S3-01…03, AC-S3-01) | ✔ | User Story 1 + Requirements → SCR-03 |
| SCR-04 (FR-S4-01…04, AC-S4-01…03) | ✔ | User Story 1 + Requirements → SCR-04 |
| SCR-05 (FR-S5-01…04, AC-S5-01…02, D-6) | ✔ | User Story 2 + Requirements → SCR-05 |
| SCR-06 (FR-S6-01…05, AC-S6-01…03) | ✔ | User Story 2 + Requirements → SCR-06 |
| SCR-07 (FR-S7-01…07, AC-S7-01…03, D-4/D-5/D-7) | ✔ | User Stories 6 & 7 + Requirements → SCR-07 |
| SCR-08 (FR-S8-01…05, AC-S8-01…03) | ✔ | User Story 5 + Requirements → SCR-08 |
| Cross-screen Business Rules BR-01…26 | ✔ | Requirements → Cross-Screen Business Rules (verbatim) |
| Validation Rules VR-T01…13, VR-F01…12 | ✔ | Requirements → Feature 0 (types) + Validation Rules register |
| Status Lifecycle | ✔ | Requirements → Status Lifecycle |
| Cross-Module Contracts CMC-01…07 | ✔ | Requirements → Cross-Module Contracts + Dependencies |
| Permissions Matrix | ✔ | Requirements → Permissions Matrix + User Story 9 |
| Error Handling | ✔ | Requirements → Error Handling |
| Notifications (D-1…D-7, toasts) | ✔ | Requirements → Notifications & Dialogs |
| Non-functional Requirements NFR-1…17 | ✔ | Requirements → Non-Functional Requirements |
| Glossary & Data Dictionary | ✔ | Overview → Key terms + Requirements → Key Entities |
| Assumptions (A-2 + formalized-defaults conversion map) | ✔ | Assumptions (A-2 retained; formalized defaults already folded into their FR-GBL/BR/VR-F/NFR homes throughout) |
| Open Questions | ✔ | "None" — matches SRS; no `[NEEDS CLARIFICATION]` markers in this spec |
| Decision References (G-01…G-24) | ✔ | Referenced inline via `[PO-Gxx]`-style traceability throughout the Overview and User Stories |

**Coverage summary**: 100% of SRS sections are represented in this specification. **Zero `[NEEDS CLARIFICATION]` markers** — the source SRS is explicitly "Implementation-ready" with "Open Questions: None," and every ambiguity was already ratified by the Product Owner per the SRS's own Decision References register. The only carried-forward assumption (A-2) is explicitly non-blocking per the SRS itself.

---

## Traceability Notes

- Every functional requirement in this spec cites its originating SRS ID (`FR-*`, `BR-*`, `VR-*`, `NFR-*`, `CMC-*`, `D-*`, `AC-*`).
- Every cross-screen business rule (BR-01…BR-26) is quoted or closely paraphrased from the SRS to preserve intent.
- Every validation rule (VR-F01…VR-F13, VR-T01…VR-T13) preserves the exact SRS message/format wording where the SRS specifies shipped copy; VR-F13 (capacity guardrails) is new in this spec's Clarifications session (2026-07-27), filling a gap the SRS's NFR-16 left unspecified.
- Every dialog (D-1…D-7) and result-code message pattern preserves the SRS's normative copy.
- The result-code catalogue (`E-1001`, `E-1002`, `E-1003`, `E-1004`, `E-1401`, `E-1413`, `E-1429`, `E-1500`) is the canonical, HTTP-status-paired form mandated by the user's feature description and the SRS alike.
- Removed/descoped features (mapping version history, sandbox/test credentials, expiry fields, IP allow-lists, trigger-rule engine) are recorded only under their governing BR/Scope entries, never restated as active requirements.

---

**End of specification.**
