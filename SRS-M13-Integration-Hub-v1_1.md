# M-13 — Integration Hub

Software Requirements Specification · Nabadat VOC Platform
Version 1.1 · 26 Jul 2026 · Status: Implementation-ready (single source of truth for SpecKit)
Revision v1.1 — structural review for SpecKit: stable IDs (FR/BR/VR/NFR/AC/CMC), assumptions formalized, glossary & data dictionary added, illustrative endpoints marked. **No business behaviour or UI requirement changed.**

**Sources & traceability tags used throughout**

| Tag | Meaning |
|---|---|
| `[BR]` | Explicit business requirement (M-13 business brief) |
| `[PO-Gxx]` | Ratified Product Owner decision from the requirement-gap register (G-01…G-24, listed in *Decision References*) |
| `[PO]` | Other ratified PO input (Bucket-2 decisions: permissions, contracts, NFRs, validation, security, migration) |
| `[UI]` | Behaviour explicitly present in the ratified prototype `M13-Integration-Hub-Prototype-v0.4-Ratified.html` |
| `[Derived from UI]` | Behaviour inferred from the prototype, not explicitly written elsewhere |
| `[Assumption]` | Genuinely missing information; default stated in *Assumptions* |
| `[Formalized default]` | Former assumption converted into a formal requirement/rule in v1.1; default value in force until the PO revises it — conversion map in *Assumptions* |

Conflict rule applied: explicit business requirements and ratified PO decisions override UI appearance; where the PO superseded the original brief (e.g. channel-ID entry, version history) the PO decision governs and the change is recorded in *Decision References*.

---

## Purpose

M-13 is the inbound edge of the Nabadat platform. It lets a tenant's Technical Administrator expose authenticated APIs through which backend source systems raise survey requests in five interaction scenarios, and lets the CX Manager govern the transaction data model those APIs accept: service channels, the parameter catalogue, per-channel parameter contracts, and source-value → display-value mappings. Every inbound request is logged with all parameters received and the response returned. `[BR]`

## Scope

**In scope**

1. Integration-point management: create, edit, activate/deactivate; each integration has a name, serves exactly one service channel, and follows exactly one integration scenario. `[BR]` `[PO-G02]`
2. Five integration scenarios (SCN-01…SCN-05, see *Inbound Request Processing*). `[BR]`
3. Caller authentication: API Key or OAuth 2.0, with mechanism-specific configuration shown on selection, credential generation (show-once) and API-key revocation. `[BR]` `[PO]`
4. System-built APIs that receive survey requests; the service channel ID is the only mandatory parameter (path parameter); all other parameters are sent freely as key–value pairs. `[BR]`
5. Service-channel management: bilingual names, manually entered channel ID, description, status, and per-channel parameter contract (supported + required sets; different parameter sets per channel). `[BR]` `[PO-G03]`
6. Parameter management: 23 built-in parameters, custom parameters, enable/disable built-ins, required, searchable, filterable, reporting visibility, dashboard visibility, API field name, validation rules, mapping support, service-channel assignment. `[BR]`
7. Data types: Text, Number, Boolean, Email, Phone, List, Range (with range configuration on selection) plus the ratified additions Date, Date & time, Currency, Percentage, URL, Geolocation. Duration and Identifier were evaluated and rejected. `[BR]` `[PO-G17]`
8. Parameter mapping: backend value → business display value (bilingual EN/AR), manual editing, bulk Excel import, bulk Excel export, replace mappings. `[BR]` `[PO-G10]`
9. Request logs for every request, including all parameters received and the response returned, with PII masking and 90-day retention. `[BR]` `[PO-G14]`
10. Request validation pipeline, normative result-code catalogue, per-integration rate limiting, idempotent retry behaviour, link expiry and iFrame origin whitelisting. `[PO-G07]` `[PO-G15]` `[PO-G22]` `[PO]`
11. Audit events for all configuration changes. `[PO]`

**Descoped by ratified decision**

- **Mapping version history** — listed in the original business requirements, removed entirely by the PO. The platform audit trail is the only change record. `[PO-G12]`

**Out of scope (owned by other modules)** `[PO]`

- Survey dispatch, delivery-channel selection, sending, retries: **M-02 Channels & Distribution**. M-13 hands off accepted dispatch requests and stops.
- Survey definitions and rendering payloads (JSON/iFrame): **M-03**. M-13 retrieves and relays them.
- Response validation, deduplication and storage: **M-04**. M-13 forwards ingestion payloads.
- Reporting/analytics consumption of transaction metadata: **M-06 / M-07**.
- Operational alerting: **M-09**, future phase — Phase 1 only logs failures.
- User, role and permission administration: **M-10** (M-13 registers permission keys and consumes authorisation).
- Boundary statement (ratified): *M-13 only handles integration, request validation, parameter processing, and trigger rules.* Phase-1 interpretation of "trigger rules" is fixed by BR-01 (every valid request is processed).

**Deferred capabilities** *(moved from Open Questions — decision recorded)*
- **Trigger rules** — rule-based eligibility/sampling of incoming transactions is deferred beyond Phase 1. Phase-1 behaviour: BR-01. Any future trigger-rule capability requires a new scoped requirement and prototype. `[PO]`

**Legacy migration — explicitly out of scope (ratified)** `[PO]`
Legacy configurations (parameters, mappings, service channels, integrations, rules) are **not** migrated. M-13 is a greenfield implementation; all configuration is created from scratch. No migration utilities, no import from the legacy system, no backward-compatibility requirements.

## Actors

| Actor | Description |
|---|---|
| **P-07 Tenant IT Administrator** | Tenant-side technical administrator. Manages integrations, authentication, credentials, API scenarios and integration settings; inspects request logs. `[PO-G01]` `[PO]` |
| **P-01 CX Manager** | Business owner of the CX configuration. Manages service channels, parameters, mappings and related business configuration. `[BR]` `[PO]` |
| **Caller / source system** | Non-human actor: the tenant backend invoking M-13 APIs (core bus, CRM, mobile backend, queue system). `[BR]` |
| **Nabadat Operations** | Internal operator; configures per-integration rate limits without code changes. Not a console user of M-13 screens. `[PO-G15]` |

Note `[PO-G01]`: the original brief assigned integration management to persona P-08; the PO ratified **P-07** because P-08 is an internal Nabadat persona in the Platform Definition.

## User Roles

| Capability | P-07 Tenant IT Admin | P-01 CX Manager |
|---|---|---|
| Integrations (SCR-01/02): view | Yes | Read-only (BR-24) |
| Integrations: create / edit / activate / deactivate | Yes | No |
| Credentials: generate / revoke | Yes | No |
| Request logs (SCR-08): view / export | Yes | No |
| Service channels (SCR-03/04): view | Read-only (BR-24) | Yes |
| Service channels: create / edit / status / ID change (pre-lock) | No | Yes |
| Parameters (SCR-05/06): view | Read-only (BR-24) | Yes |
| Parameters: create / edit / enable / disable | No | Yes |
| Mappings (SCR-07): view | Read-only (BR-24) | Yes |
| Mappings: add / edit / delete / import / export / replace-all | No | Yes |

All sensitive actions — credential generation/revocation, parameter disable, mapping replace/import, channel ID change, integration activation/deactivation — are permission-controlled and audited. `[PO]` Full matrix in *Permissions Matrix*.

## Navigation Overview

**Screen hierarchy & routes** `[UI]` `[Derived from UI]` (route paths derived)

| ID | Screen | Route | Primary persona |
|---|---|---|---|
| SCR-01 | Integrations (list) | `/integration-hub/integrations` | P-07 |
| SCR-02 | New / edit integration (3-step wizard) | `/integration-hub/integrations/new`, `…/:id` | P-07 |
| SCR-03 | Service channels (list) | `/integration-hub/service-channels` | P-01 |
| SCR-04 | Service channel create / edit | `/integration-hub/service-channels/new`, `…/:id` | P-01 |
| SCR-05 | Parameters (list) | `/integration-hub/parameters` | P-01 |
| SCR-06 | Parameter editor (right-side drawer over SCR-05) | — | P-01 |
| SCR-07 | Parameter mappings | `/integration-hub/mappings` | P-01 |
| SCR-08 | Request logs | `/integration-hub/logs` | P-07 |

**Sidebar** `[UI]`: fixed left, dark navy. Contents top-to-bottom: Nabadat logo mark + wordmark with "Voice of Customer" caption; module tag "M-13 · Integration Hub"; nav group **Inbound integrations** → *Integrations*, *Request logs*; nav group **Data model** → *Service channels*, *Parameters*, *Parameter mappings*; footer with signed-in user, role and persona code. Each nav item shows an owning-persona chip (P-07 / P-01) — this chip is a **prototype review annotation, excluded from the product UI** `[Derived from UI]`. Active nav item is highlighted; clicking navigates without page reload. `[UI]`

**Top bar** `[UI]`: sticky; breadcrumb `Nabadat › Integration Hub › ‹current screen›`; right side holds a theme toggle (light/dark). The "Prototype v0.4 · ratified baseline" badge and the blue review banner beneath the top bar are **prototype-only artifacts, excluded from the product** `[Derived from UI]`.

**Entry / exit points & cross-links** `[UI]`
- SCR-01 → SCR-02 via *New integration* and per-row edit; SCR-01 → SCR-08 via *View request logs* (header) and per-row log action.
- SCR-02 → SCR-01 via *Cancel* (no save) or *Create integration* (save).
- SCR-03 ⇄ SCR-04 via *New service channel* / row edit / *Cancel* / *Create channel*.
- SCR-05 → SCR-06 (drawer) via *New parameter* and row edit; SCR-05 → SCR-07 via *Manage mappings* and per-row "Mapped" link.
- SCR-06 (List type) → SCR-07 via *Open mappings* (closes the drawer, navigates). `[UI]`
- Back navigation: browser back / breadcrumb; unsaved-changes guard on SCR-02, SCR-04, SCR-06 (FR-GBL-03).
- Deep links: each route above is directly addressable `[Derived from UI]`.

---

## Functional Requirements

### Global Console Behaviours (FR-GBL — apply to all M-13 screens)

- **FR-GBL-01 — Pagination & ordering** `[Formalized default — was A-8]`: tables paginate server-side beyond 50 rows; no user-facing column sorting in Phase 1; default orders — integrations/channels/parameters by creation, logs newest-first, mappings by entry order.
- **FR-GBL-02 — Empty / loading / error / access-denied states** `[Formalized default — was A-9]`: skeleton rows while loading; empty states with guidance text and the screen's primary CTA; error state with retry; access-denied state for missing view permission.
- **FR-GBL-03 — Unsaved-changes guard** `[Formalized default — was A-5]`: SCR-02, SCR-04 and SCR-06 prompt before discarding unsaved edits on navigation away.
- **FR-GBL-04 — Feedback** `[Formalized default — was A-12]`: success toasts confirm create/save/import/revoke; failed generation actions show an error toast; inline validation copy per VR-F12.
- **FR-GBL-05 — Permission gating** `[Derived from UI]`: actions the role lacks are hidden or disabled; direct-route access without the view permission renders the access-denied state (FR-GBL-02).

### Feature 0 — Inbound Request Processing (headless system feature, no screen)

**Purpose.** The runtime behaviour of the APIs that M-13 builds per integration. `[BR]`
**Actors.** Caller (source system).
**Business objective.** Receive survey requests from backend systems with the service channel ID as the only mandatory (path) parameter and all other data as free key–value pairs; validate against the channel contract; return a structured result code; hand off to the owning module per scenario. `[BR]`

**F0.1 · FR-F0-01 — Integration scenarios (normative)** `[BR]` — endpoint patterns as displayed in the SCR-02 endpoint preview `[UI]`:

> **Illustrative endpoints.** The base URL and path segments shown in this document (`https://api.nabadat.cx/v1/…`, `https://auth.nabadat.cx/oauth2/token`) are **illustrative only**, mirroring the ratified prototype's endpoint preview; final API paths are fixed at implementation and are not frozen by this SRS. **Normative** regardless of final paths: the HTTP method semantics, the channel ID as the only mandatory path parameter (BR-03), request/response semantics per scenario, and the result-code catalogue (F0.3).

| ID | Scenario | Pattern shown in UI | Caller sends | Caller receives | Downstream owner |
|---|---|---|---|---|---|
| SCN-01 | Dispatch via Nabadat | `POST …/v1/survey-requests/{channelId}` | Transaction details | Result code (`202 ACCEPTED`) | Nabadat sends the survey through the suitable channel via M-02 `[BR]` |
| SCN-02 | Redirect link | `POST …/v1/survey-links/{channelId}` | Transaction details | Survey link to redirect to (`survey_url`, `expires_at`) | Survey resolved via M-02 rules `[PO-G21]` |
| SCN-03 | JSON render | `POST …/v1/survey-definitions/{channelId}` | Transaction details | Survey JSON to render | Definition retrieved from M-03 `[PO]` |
| SCN-04 | iFrame embed | `GET …/v1/survey-embed/{channelId}?…` | Transaction details as query string `[UI]` | Embed URL viewed in an iFrame | Rendered by M-03; Allowed-Origins enforced `[PO]` |
| SCN-05 | Response ingestion | `POST …/v1/responses/{channelId}` | Transaction details **plus** the survey response | Result code (`202 ACCEPTED`) | Forwarded to M-04 for validation, dedup, storage `[BR]` `[PO]` |

**F0.2 · FR-F0-02 — Request validation pipeline (normative order)** `[PO-G07]` `[PO]`
1. HTTPS/TLS 1.2+ enforced; plain HTTP refused.
2. Authentication (API key header or OAuth bearer token). Invalid, revoked or unknown → `401 E-1401`.
3. Per-integration rate limit (default 100 req/s) → `429 E-1429`. `[PO-G15]`
4. Payload size ≤ 2 MB → `413 E-1413`. `[PO]` (code `[Derived]` from the NFR)
5. Channel resolution: unknown `{channelId}` → `404 E-1001`.
6. Channel status: inactive → `409 E-1004`. `[PO-G06]`
7. Required parameters per the channel contract → `400 E-1002`, first missing field named in the message. `[BR]` `[UI]`
8. Type & validation-rule checks per F0.4 → `422 E-1003`; **a validation failure rejects the whole request** (no accept-and-flag). `[PO-G07]` `[PO]`
9. Unregistered key–value pairs are separated and stored raw (F0.6); the request is logged (SCR-08); scenario processing executes.

Requests are atomic: any failure at steps 2–8 rejects the entire request and nothing is forwarded downstream. `[PO-G07]`

**F0.3 · FR-F0-03 — Result-code catalogue (normative, ratified as proposed)** `[PO-G07]` `[UI]`

| HTTP | Code | Meaning |
|---|---|---|
| 202 | `ACCEPTED` | Request accepted; survey handed to M-02 for distribution (SCN-01) / response forwarded to M-04 (SCN-05) |
| 200 | `OK` | Link / JSON / embed returned in the response body |
| 400 | `E-1002 MISSING_REQUIRED_PARAMETER` | Missing required parameter for this service channel |
| 401 | `E-1401 INVALID_CREDENTIALS` | Invalid or revoked credentials |
| 404 | `E-1001 UNKNOWN_SERVICE_CHANNEL` | Unknown service channel ID in the path |
| 409 | `E-1004 CHANNEL_INACTIVE` | Service channel is inactive |
| 413 | `E-1413 PAYLOAD_TOO_LARGE` | Body exceeds 2 MB `[Derived]` |
| 422 | `E-1003 INVALID_PARAMETER_VALUE` | Parameter value failed its validation rule |
| 429 | `E-1429 RATE_LIMIT_EXCEEDED` | Rate limit exceeded — default 100 req/s per integration, configurable by Nabadat Operations |
| 500 | `E-1500 INTERNAL_ERROR` | Unexpected failure; caller may retry idempotently (F0.7) `[UI]` |

Message copy patterns (normative examples, from logged responses `[UI]`): `Required parameter 'mobile' is missing for service channel E-SERVICES-PORTAL.` · `Value '07701' for 'mobile' failed validation rule for type Phone.` · `API key was revoked on 2026-07-20. Generate a new key in Integrations.` · `Survey request accepted for channel distribution (M-02).` · `Response forwarded to M-04 Response Collection.` · `Unexpected error while queueing the request. The caller may retry with the same transaction_id (idempotent).`

**F0.4 · FR-F0-04 — Data types & validation rules (normative)** `[PO]` `[PO-G17]`

| VR | Type | Accepted format | Per-parameter rule options |
|---|---|---|---|
| VR-T01 | Text | UTF-8 string | Max length (default 255), optional regex |
| VR-T02  Number | Integer or decimal | Optional min / max |
| VR-T03  Boolean | `true/false`, `1/0`, case-insensitive | — |
| VR-T04  Email | RFC 5322 basic mailbox format | — |
| VR-T05  Phone | E.164: `+` and 8–15 digits | — |
| VR-T06  List | UTF-8 string ≤ 100 chars | **Membership is not enforced** — unmapped values are accepted `[PO-G08]`; translation happens at read time (F0.5) |
| VR-T07  Range | Numeric | Must fall within the configured min/max (inclusive); min, max and unit are configured when the type is selected `[BR]` `[UI]` |
| VR-T08  Date | ISO 8601 date `YYYY-MM-DD` | — |
| VR-T09  Date & time | ISO 8601 with timezone (e.g. `2026-07-22T14:31:56Z`) | — |
| VR-T10  Currency | Amount (decimal) + ISO-4217 currency code | Optional min / max on amount |
| VR-T11  Percentage | Decimal | Bounds default 0–100, configurable |
| VR-T12  URL | RFC 3986 absolute URL | — |
| VR-T13  Geolocation | Latitude −90…90, longitude −180…180 | — |

Every validation failure rejects the request with the defined business error code (`E-1003`). `[PO]`

**F0.5 · FR-F0-05 — Mapping resolution model** `[PO-G24]` `[PO-G08]` `[PO-G12]`
- Mappings resolve **at read time**: reports, dashboards and exports translate stored source values through the *current* mapping table; changing a mapping retroactively relabels historical responses by design.
- Incoming values with no mapping are never rejected: the raw value is stored, displayed as-is, and enters the per-parameter unmapped-values queue surfaced on SCR-07.
- No version history exists; the audit trail is the only change record; *Replace all* is irreversible.

**F0.6 · FR-F0-06 — Unregistered parameters** `[PO-G09]`
Key–value pairs received without a parameter definition are accepted and stored raw. They are visible **only** in request logs and **do not appear in reports, dashboards, filters, or rule builders until formally registered as system parameters** (i.e., a parameter is created whose API field name matches the key). Retro-reportability of previously received values: Assumption A-2.

**F0.7 · FR-F0-07 — Idempotency** `[PO-G22]`
Retries carrying the same `(tenant, channelId, transaction_id)` are safe end-to-end: M-13 accepts the retry (a new log entry is written) and downstream deduplication guarantees no duplicate survey (SCN-01/02) and no duplicate stored response (SCN-05).

**F0.8 · FR-F0-08 — Link & iFrame security** `[PO]`
- SCN-02 survey links expire **24 hours** after issue by default; `expires_at` is returned to the caller. (override configured per FR-S2-10)
- SCN-04 integrations must use an **Allowed Origins whitelist**; embedding from non-whitelisted origins is refused. (whitelist configured per FR-S2-10)
- JSON/iFrame survey definitions are retrieved from M-03; all communication uses HTTPS.

**F0.9 · FR-F0-09 — Trigger rules (boundary)** `[PO]`
Phase 1 contains no trigger-rule engine: every request that passes validation is processed by its scenario (BR-01). Rule-based eligibility/sampling is a **deferred capability** — see *Scope › Deferred capabilities*.

**F0.10 · FR-F0-10 — Built-in parameter catalogue (minimum set, normative)** `[BR]`; types & mapping capability per the ratified prototype `[UI]`

| Parameter | API field | Type | Mapping-capable |
|---|---|---|---|
| Customer ID | `customer_id` | Text | — |
| Customer Name | `customer_name` | Text | — |
| Customer Type | `customer_type` | List | Yes |
| Customer Segment | `customer_segment` | List | Yes |
| VIP | `vip` | Boolean | — |
| Gender | `gender` | List | — |
| Nationality | `nationality` | List | Yes |
| Mobile | `mobile` | Phone | — |
| Email | `email` | Email | — |
| Transaction ID | `transaction_id` | Text | — |
| Transaction Date | `transaction_date` | Date & time | — |
| Service | `service` | List | Yes |
| Product | `product` | List | Yes |
| Branch | `branch` | List | Yes |
| Department | `department` | List | Yes |
| Region | `region` | List | Yes |
| Journey | `journey` | List | — |
| Journey Stage | `journey_stage` | List | — |
| Touchpoint | `touchpoint` | List | — |
| Agent | `agent` | Text | — |
| Employee | `employee` | Text | — |
| Service Channel | `service_channel` | List — system-populated from the path channel `[Derived from UI]` | — |
| Source System | `source_system` | Text | — |

Built-ins can be enabled/disabled `[BR]` but never deleted or renamed at the API-field level `[PO-G16]` `[Derived from UI]`. Initial state: all enabled (BR-23 `[Formalized default]`; the prototype shows Gender, Department and Employee disabled only to demonstrate the disabled state).

**Acceptance criteria (Feature 0)**
- **AC-F0-01** Given a valid SCN-01 request with all required parameters, when POSTed with valid credentials, then the response is `202 ACCEPTED` with a `request_id`, and the request appears in SCR-08 within 60 s.
- **AC-F0-02** Given a request missing a channel-required parameter, when submitted, then the whole request is rejected `400 E-1002`, the missing field is named, and nothing reaches M-02/M-04.
- **AC-F0-03** Given a request carrying an unknown key `loyalty_tier`, when processed, then the request succeeds, the pair is stored raw, is visible in the log detail, and does not appear in any report/dashboard/filter/rule builder.
- **AC-F0-04** Given a retry with an identical `transaction_id`, when processed, then no second survey is sent and no duplicate response is stored.
- **AC-F0-05** Given an inactive channel, when any request targets it, then `409 E-1004` is returned and logged.

---

### SCR-01 — Integrations (list)

**Purpose.** Give P-07 a health-aware inventory of every integration point of the tenant and the entry point to create or edit one. `[BR]` `[UI]`
**Actors.** P-07 (manage); P-01 read-only (BR-24).
**Business objective.** One place to see what is exposed to source systems, how it authenticates, and whether it is healthy.

**Functional requirements (register)**
- **FR-S1-01** Render the three stat tiles (Integrations, Requests · 24 h, Error rate · 24 h) computed over the rolling window. `[UI]`
- **FR-S1-02** Live name search combined (AND) with the service-channel filter. `[UI]`
- **FR-S1-03** Render the integrations table with the specified columns, badges, sub-lines and per-row actions. `[UI]`
- **FR-S1-04** Navigation per the Buttons table (SCR-02 create/edit, SCR-08 logs). `[UI]`
- **FR-S1-05** Derive traffic figures from request logs; show "—" error rate when there is no traffic. `[Derived from UI]`
- **FR-S1-06** Error-rate colour thresholds: < 1 % D2, 1–5 % D3, > 5 % D4. `[Formalized default — was A-7]`

**Layout** `[UI]`. Page header (title "Integrations" + description) with actions right-aligned; a row of three stat tiles; a toolbar (search + channel filter); the integrations table; a footer boundary note ("Delivery of dispatched surveys … is owned by M-02 … Validation, deduplication and storage … by M-04 …").

**Sections & widgets**

*Stat tiles* `[UI]` (three; the median-latency tile was removed by ratified decision):

| Tile | Value | Sub-text |
|---|---|---|
| Integrations | Total count + "n active" | "Across n service channels" |
| Requests · 24 h | Rolling 24-hour request count across all integrations | "All scenarios, all integrations" |
| Error rate · 24 h | Percentage, coloured semantically, with a status badge (e.g. "Healthy") | "n failed of m requests" |

Error-rate thresholds: < 1 % healthy (D2), 1–5 % warning (D3), > 5 % critical (D4) — FR-S1-06 `[Formalized default]`.

**Fields (toolbar)**

| Name | Label/placeholder | Type | Required | Editable | Default | Behaviour |
|---|---|---|---|---|---|---|
| `search` | "Search integrations…" | Text (search box with icon) | No | Yes | empty | Live, case-insensitive substring filter on integration name; combines AND with the channel filter `[UI]` |
| `channelFilter` | "All service channels" | Select | No | Yes | All | Options: All + every service channel ID; filters the table `[UI]` |

**Table — Integrations** `[UI]`

| Column | Content & formatting |
|---|---|
| Integration | Name (semibold) + sub-line: credential kind ("Key" / "Client") · created date, or "suspended" for inactive `[UI]` |
| Service channel | Channel ID in a monospace code chip |
| Scenario | Brand-cyan badge with scenario icon + label (one of the five SCN labels) |
| Authentication | Outline badge with key icon: "API Key" or "OAuth 2.0" |
| Status | "Active" (green D2 dot badge) / "Inactive" (neutral dot badge) |
| Requests · 24 h | Right-aligned, tabular numerals |
| Error rate | Semantic badge (D2/D3/D4 per threshold) or "—" when no traffic `[UI]` |
| Last activity | Relative time ("2 min ago", "41 days ago") |
| (actions) | Icon buttons: *View logs* (→ SCR-08), *Edit* (→ SCR-02 pre-filled) `[UI]` |

Sorting: none exposed in UI; default order = creation order `[Derived from UI]` (FR-GBL-01). Pagination: FR-GBL-01. Bulk actions: none `[UI]`. Empty/loading states: FR-GBL-02. Export: none on this screen `[UI]`. Row selection: none `[UI]`.

**Buttons**

| Label | Location | Style | Permission | Action / navigation | Notes |
|---|---|---|---|---|---|
| View request logs | Header | Secondary, small | `m13.log.view` | Navigate to SCR-08 | `[UI]` |
| New integration | Header | Primary, plus icon | `m13.integration.manage` | Navigate to SCR-02 (create mode, step 1) | `[UI]` |
| View logs (row) | Row actions | Icon | `m13.log.view` | SCR-08 (filtered to the integration — `[Derived from UI]`) | |
| Edit (row) | Row actions | Icon (pencil) | `m13.integration.manage` | SCR-02 pre-filled (edit mode) | `[UI]` |

**Business rules**
- Requests/error/last-activity figures derive from request logs (SCR-08 data). `[Derived from UI]`
- An inactive integration remains listed with the Inactive badge and "suspended" sub-line; its endpoint rejects calls. `[UI]` (rejection code — see Cross-screen BR-14)

**Workflow — monitor & drill down.** P-07 opens SCR-01 → scans tiles → filters by channel or searches → opens a row's logs or edits it. Failure flow: a data-load failure shows the standard error state (FR-GBL-02).

**Acceptance criteria**
- **AC-S1-01** Given 6 integrations of which 1 is inactive, when the page loads, then the tile shows "6 / 5 active" and the inactive row shows the neutral badge and "suspended".
- **AC-S1-02** Given search text "CRM" and channel filter `CALL-CENTER`, when both applied, then only rows matching **both** remain.
- **AC-S1-03** Given a new integration created, when SCR-01 reloads, then it appears with zero traffic and "—" error rate.

---

### SCR-02 — New / Edit Integration (3-step wizard)

**Purpose.** Define an integration point: basics + scenario, authentication, then review the generated endpoint and channel contract before publishing. `[BR]` `[UI]`
**Actors.** P-07.
**Business objective.** A caller can be onboarded end-to-end (endpoint + credentials + contract) in one flow.

**Functional requirements (register)**
- **FR-S2-01** Three-step wizard: step indicator, Back/Continue/Create controls, cancel-discard, state reset on re-entry, edit-mode pre-fill. `[UI]`
- **FR-S2-02** Step-1 field set (name, service channel — active channels only, description) per the Fields table. `[BR]` `[UI]`
- **FR-S2-03** Exactly-one scenario selection via the five radio cards (BR-02). `[BR]` `[PO-G02]` `[UI]`
- **FR-S2-04** Mechanism radio switches the visible configuration (API key vs OAuth 2.0) per the ratified field sets. `[BR]` `[PO]` `[UI]`
- **FR-S2-05** API-key generation (show-once, Dialog D-1) and revocation (Dialog D-3). `[PO]` `[UI]`
- **FR-S2-06** OAuth client generation (show-once, Dialog D-2) with scopes. `[PO]` `[UI]`
- **FR-S2-07** Step-3 endpoint preview re-renders on scenario/channel change; copy action. `[UI]`
- **FR-S2-08** Accepted-parameters table re-renders from the selected channel's contract. `[PO]` `[UI]`
- **FR-S2-09** Result-codes card renders the F0.3 catalogue. `[UI]`
- **FR-S2-10** Conditional security configuration: *Allowed origins* list for SCN-04 and *Link expiry* override (default 24 h) for SCN-02, shown after scenario selection. `[Formalized — was A-6; behaviour ratified BR-20; input fields absent from prototype v0.4]`

**Layout** `[UI]`. Page header ("New integration" + description; *Cancel* right-aligned); step indicator (1 *Basics & scenario* → 2 *Authentication* → 3 *Endpoint & parameters*; states: current, done with check, upcoming); one step visible at a time; footer with *Back* / *Continue* (renamed *Create integration* on step 3). Edit mode opens the same wizard pre-filled `[UI]`; wizard state resets when re-entered `[UI]`.

#### Step 1 — Basics & scenario

**Fields**

| Name | Label | Type | Required | Editable | Default | Placeholder | Validation & rules |
|---|---|---|---|---|---|---|---|
| `name` | Integration name | Text | Yes | Yes | empty | "e.g. Core Services Bus — Survey Dispatch" | Unique per tenant; ≤ 100 chars (VR-F01); hint: "Shown in lists, logs and alerts. Unique within the tenant." `[UI]` |
| `serviceChannel` | Service channel | Select | Yes | Yes | first active channel | — | Options: **active channels only**, rendered "Name — CHANNEL-ID" `[UI]`; hint: "Only active service channels are listed. The channel defines which parameters this API accepts and requires." `[UI]`; changing it updates the step-3 endpoint and contract `[UI]` |
| `description` | Description | Textarea | No | Yes | empty | "What system calls this integration and why." | `[UI]` |
| `scenario` | Integration scenario | Radio cards (5) | Yes | Yes | none in create mode `[Derived from UI]` | — | Exactly one of SCN-01…05; selected card shows highlight ring + check `[UI]` |

*Scenario cards* `[UI]` — icon, title, description (normative copy): **Dispatch via Nabadat** "Caller sends the transaction details and receives a result code. Nabadat selects the delivery channel and sends the survey through M-02 Channels & Distribution." · **Redirect link** "Caller receives a one-time survey URL and redirects the customer to it." · **JSON render** "Caller receives the survey definition as JSON and renders it inside its own UI." · **iFrame embed** "Caller displays the survey inside an embedded iFrame. Allowed embedding origins must be whitelisted." · **Response ingestion** "Caller sends the transaction details together with the completed survey response; M-13 hands the payload to M-04 Response Collection for validation and storage."
Section hint: "One scenario per integration — create a separate integration for each additional scenario." `[PO-G02]` `[UI]`

#### Step 2 — Authentication

**Mechanism selector** `[BR]` `[UI]`: two radio cards — **API key** ("Static tenant-scoped key sent in the `X-Api-Key` header. Best for server-to-server calls from a trusted backend.") and **OAuth 2.0** ("Client-credentials flow. The caller exchanges a client ID and secret for a short-lived access token. Best for shared enterprise buses."). Selecting a card shows only that mechanism's configuration (dynamic visibility). `[BR]` `[UI]`

**API-key configuration (ratified field set)** `[PO]`

| Name | Label | Type | Required | Editable | Default | Rules |
|---|---|---|---|---|---|---|
| `keyLabel` | Key label | Text | Yes | Yes | — | Hint: "Identifies the key in logs and in the key registry." `[UI]` |
| `currentKey` | Current key | Read-only text (masked, monospace) + **Revoke** button | — | No | masked value | Visible only when an active key exists `[Derived from UI]`; hint: "Generated ‹date› by ‹user›. Revoking rejects all further requests with E-1401 immediately." `[UI]` |

Removed by ratified decision (must NOT appear): expiry field, allowed-source-IPs field, environment/sandbox field. `[PO]` `[PO-G13]`

**OAuth 2.0 configuration (ratified field set)** `[PO]`

| Name | Label | Type | Required | Editable | Default | Rules |
|---|---|---|---|---|---|---|
| `clientName` | Client name | Text | Yes | Yes | — | `[UI]` |
| `tokenEndpoint` | Token endpoint | Read-only text | — | No | `https://auth.nabadat.cx/oauth2/token` (illustrative — F0.1 note) | Hint: "Access tokens are valid for a fixed **15 minutes**." Lifetime is fixed at code level; no field. `[PO]` `[UI]` |
| `scopes` | Scopes | Multi-select pill checkboxes | No | Yes | `survey-requests:write` selected | Shown values `[UI]`: `survey-requests:write`, `responses:write`, `survey-links:read`; hint: "Scopes limit which scenario endpoints a token may call." Scope naming convention: BR-26 |

Removed by ratified decision (must NOT appear): grant-type field (fixed `client_credentials` in code), access-token lifetime field. `[PO]`

**Buttons (step 2)**

| Label | Style | Action | Success | Failure |
|---|---|---|---|---|
| Generate new API key | Primary, key icon | Creates a key for the entered label → opens **Dialog D-1** | Key shown once | Error toast (FR-GBL-04) |
| Revoke | Destructive-outline, small (in Current key row) | Opens **Dialog D-3** | Key revoked immediately on confirm | — |
| Generate client credentials | Primary, key icon | Creates client → opens **Dialog D-2** | Credentials shown once | Error toast (FR-GBL-04) |

#### Step 3 — Endpoint & parameters

**Endpoint preview panel** `[UI]`: dark code panel with gradient top border; method chip (`POST`, or `GET` for SCN-04); full URL (illustrative — see F0.1 note) with dimmed base, scenario path, and the channel ID rendered as a highlighted path token; **Copy** button whose label becomes "Copied ✓" on click `[UI]`. Below, a sample body: JSON key–value sample with comment lines "// Body — key–value pairs. The service channel ID is the only mandatory *path* parameter; // required *body* fields come from the channel contract." `[UI]`; SCN-04 shows a query-string sample instead; SCN-05 adds a `survey_response` object `[UI]`. The panel re-renders when the scenario or channel changes. `[UI]`

**Card — Accepted parameters** `[UI]`: description "Inherited from the ‹channel name› channel contract. Other key–value pairs are accepted and stored as unregistered parameters — excluded from reports, dashboards, filters and rule builders until formally registered." `[PO-G09]`. Table (re-rendered from the selected channel's contract `[UI]` — ratified): Parameter · API field (chip) · Type · Rule ("Required" D4 badge / "Optional" neutral badge).

**Card — Result codes returned to the caller** `[UI]`: description "The caller always receives a structured result code from the normative catalogue below." Table = F0.3 catalogue rows 202–429 `[UI]`.

**Wizard buttons**

| Label | Location | State rules | Action |
|---|---|---|---|
| Cancel | Header | Always enabled | Discard, return to SCR-01 `[UI]` |
| Back | Footer | Disabled on step 1 `[UI]` | Previous step |
| Continue / Create integration | Footer | "Continue" on steps 1–2; "Create integration" on step 3 `[UI]` | Advance; on step 3 persists the integration, provisions the endpoint, returns to SCR-01 `[UI]` |

**Validation (wizard).** Step advance requires the step's required fields (name, channel, scenario on step 1; mechanism config on step 2) — inline messages "‹Field› is required", uniqueness message on name (copy: VR-F12).

**Business rules**
- Exactly one scenario per integration. `[PO-G02]`
- Only active channels are selectable. `[UI]`
- Credentials are show-once; the plaintext is never retrievable after the dialog closes; stored hashed/encrypted. `[PO]` `[UI]`
- Revocation is immediate; generating a new key while one is active revokes the old key implicitly. `[UI]` `[Derived from UI]`
- A token/key lacking access (revoked, wrong scope) is rejected `401 E-1401`. `[UI]`
- Creating the integration makes the endpoint callable within 60 s. `[Derived from UI]`

**Workflow — create (success flow).** P-07: *New integration* → fills step 1, picks scenario → Continue → picks mechanism, fills config, generates credentials, copies the secret from the show-once dialog → Continue → reviews endpoint, contract, result codes → *Create integration* → SCR-01 shows the new row. **Failure flows:** missing required field blocks the step with inline errors; generation failure shows an error and leaves the wizard state intact; *Cancel* at any point discards everything.

**Edge cases**
- Editing an integration whose channel was deactivated afterwards: the channel remains displayed; a warning indicates calls are rejected `E-1004` `[Derived from UI]`.
- Changing the channel in edit mode changes the endpoint path; the step-3 preview makes this visible before saving. `[UI]`
- Generating credentials then cancelling the wizard: the generated credential is discarded with the draft (BR-25).

**Dialogs**

**D-1 — API key generated** `[UI]`. Trigger: *Generate new API key*. Content: title "API key generated"; text "Copy the key now — for security it is shown **only once**."; dark code panel with the key + **Copy** button; warning alert "**Store it in your secrets manager.** If lost, revoke it and generate a new key — revocation takes effect immediately."; button **Done** (primary) closes. Esc / outside-click closes `[UI]`; closing without copying is allowed (the key is not shown again).

**D-2 — Client credentials generated** `[UI]`. Trigger: *Generate client credentials*. Content: title; show-once text; code panel with `client_id` and `client_secret`; **Done**. Same close behaviour as D-1.

**D-3 — Revoke this API key?** `[UI]`. Trigger: *Revoke*. Content: "All requests signed with ‹masked key› are rejected with `E-1401` the moment you confirm. The caller must switch to a newly generated key. This cannot be undone." Buttons: **Cancel** (outline) — closes with no change; **Revoke key** (destructive filled) — revokes immediately, closes, audited. Esc / outside-click = cancel `[UI]`.

**Acceptance criteria**
- **AC-S2-01** Given step 2 with API key selected, when the mechanism is switched to OAuth 2.0, then the API-key fields hide and the OAuth fields show, and vice versa.
- **AC-S2-02** Given D-1 open, when Done is clicked, then no screen, log or API ever displays the plaintext key again.
- **AC-S2-03** Given a revoked key, when the caller uses it, then the request logs `401 E-1401` with the ratified message copy.
- **AC-S2-04** Given the channel changed on step 1, when step 3 renders, then the endpoint path token and the Accepted-parameters table both reflect the new channel.

---

### SCR-03 — Service Channels (list)

**Purpose.** P-01's inventory of service channels and entry point to create or edit one. `[BR]` `[UI]`
**Actors.** P-01 (manage); P-07 read-only (BR-24).
**Business objective.** Govern the business channels transactions come through, each with its own parameter contract, because each backend exposes different transaction data. `[BR]`

**Functional requirements (register)**
- **FR-S3-01** Render the service-channels table with the specified columns and counts. `[UI]`
- **FR-S3-02** No delete action exists anywhere (BR-07). `[PO-G06]` `[UI]`
- **FR-S3-03** Navigation to SCR-04 (create / row edit). `[UI]`

**Layout** `[UI]`. Page header (title "Service channels" + description) with *New service channel* right-aligned; the channels table; footer note: "**Not the same as distribution channels.** Service channels describe where the transaction happened; the channels used to deliver surveys (WhatsApp, SMS, email…) are configured in **M-02 Channels & Distribution**."

**Table — Service channels** `[UI]`

| Column | Content |
|---|---|
| Service channel | Name (semibold) + description sub-line |
| Channel ID | Monospace code chip |
| Status | Active (D2 dot badge) / Inactive (neutral) |
| Supported params | Count, right-aligned |
| Required | Count, right-aligned |
| Integrations | Count of integrations serving the channel, right-aligned |
| (actions) | *Edit* icon → SCR-04 pre-filled `[UI]` |

Sorting/pagination/bulk/empty/loading: FR-GBL-01 / FR-GBL-02; no delete action exists anywhere `[UI]` (see BR below).

**Buttons**

| Label | Location | Permission | Action |
|---|---|---|---|
| New service channel | Header, primary, plus icon | `m13.channel.manage` | SCR-04 create mode `[UI]` |
| Edit (row) | Row actions | `m13.channel.manage` (view for read-only roles) | SCR-04 edit mode `[UI]` |

**Business rules**
- A channel that has ever received traffic cannot be deleted — deactivate only. `[PO-G06]` (No delete control exists in the UI. `[UI]`)

**Acceptance criteria**
- **AC-S3-01** Given 5 channels of which 1 inactive, when the list renders, then counts and badges match the channel records and no delete action is offered.

---

### SCR-04 — Service Channel Create / Edit

**Purpose.** Define a channel's identity (bilingual name, manually entered ID, description, status) and its parameter contract. `[BR]` `[PO-G03]` `[UI]`
**Actors.** P-01.

**Functional requirements (register)**
- **FR-S4-01** Identity field set: EN/AR names, manually entered channel ID with live sanitisation, description, Active toggle. `[BR]` `[PO-G03]` `[UI]`
- **FR-S4-02** Channel-ID lock behaviour per BR-05: read-only with explanation after the channel's first successful (2xx) request. `[PO-G23]`
- **FR-S4-03** Live contract-summary alert with supported/required counts. `[UI]`
- **FR-S4-04** Parameter-contract table with live filter and the Supported → Required dependency. `[BR]` `[UI]`

**Layout** `[UI]`. Page header ("New service channel" + description; *Cancel* + *Create channel* right-aligned). Two columns: left — identity card followed by a contract-summary info alert; right — *Parameter contract* card with its own filter and scrollable table.

**Fields — identity card**

| Name | Label | Type | Required | Editable | Default | Placeholder | Validation & rules |
|---|---|---|---|---|---|---|---|
| `nameEn` | Channel name · EN | Text | Yes | Yes | empty | "e.g. Self-Service Kiosk" | Max 50 chars; unique within the tenant; hint shown `[UI]` `[PO-G03]` |
| `nameAr` | Channel name · AR | Text (RTL, `lang=ar`) | Yes | Yes | empty | Arabic example placeholder | `[PO-G03]` `[UI]` |
| `channelId` | Service channel ID | Text (monospace) | Yes | Conditionally — see BR | empty | "e.g. SELF-SERVICE-KIOSK" | **Entered manually.** Allowed characters: letters, numbers and `-` only; **< 20 characters** (input `maxlength` 19); no spaces or other special characters; invalid characters are stripped live as typed `[PO-G03]` `[UI]`. Unique per tenant (case rules: VR-F04). Hint (normative copy): "Letters, numbers and \"-\" only · under 20 characters · no spaces. Editable until the channel receives its **first successful request** — locked permanently after that, because callers hard-code it in the endpoint path." `[PO-G23]` `[UI]` |
| `description` | Description | Textarea | No | Yes | empty | "What this channel covers and which backend serves it." | `[UI]` |
| `active` | Active | Toggle | — | Yes | On | Hint: "Inactive channels stop accepting API requests (`E-1004`) and are hidden from new integrations." `[PO-G06]` `[UI]` |

**Contract-summary alert** `[UI]`: info style, live text "**Contract summary:** ‹n supported · m required› parameters. Required parameters missing from an incoming request are rejected with `E-1002`." Counts update as the contract table is toggled `[UI]`.

**Card — Parameter contract** `[UI]`
- Description: "Turn on **Supported** for every field this channel's backend can send; mark **Required** to make it mandatory. Only active parameters are listed."
- Filter field: search box, placeholder "Filter parameters…", live name filter `[UI]`.
- Scrollable table (sticky header, ~430 px viewport `[UI]`): columns Parameter (name + API field sub-line) · Type · **Supported** (toggle) · **Required** (checkbox).
- Field dependency `[UI]`: *Required* is enabled only while *Supported* is on; switching Supported off clears **and** disables Required. Toggling either updates the summary alert live.

**Buttons**

| Label | Location | Permission | Action | Failure |
|---|---|---|---|---|
| Cancel | Header, ghost | — | Discard, return to SCR-03 `[UI]` | — |
| Create channel / Save changes | Header, primary | `m13.channel.manage` | Validate, persist, return to SCR-03 `[UI]` | Inline field errors; stays on page |

**Business rules**
- Channel ID lifecycle `[PO-G23]`: editable in create mode and in edit mode **until the channel receives its first successful (2xx) request**; locked permanently afterwards (field renders read-only with the lock explanation). Editing the ID pre-lock changes the endpoint path of every integration serving the channel — the hint warns that callers must update their configuration; the old ID resolves `404 E-1001` immediately after save `[Derived from UI]`.
- The channel contract is the authority on requiredness at request time; the parameter-level "Required by default" flag is only the default applied on assignment. `[PO-G19]`
- Deactivating: serving integrations return `409 E-1004` within 60 s; the channel disappears from the SCR-02 channel select; historical data and logs remain. `[PO-G06]` `[UI]`

**Workflow — create.** P-01: *New service channel* → enters EN/AR names, types the ID (sanitised live) → sets description/status → switches Supported on for the backend's fields and ticks Required for the mandatory subset (summary updates live) → *Create channel* → SCR-03. **Failure flow:** duplicate ID or name blocks save with an inline error (copy: VR-F12). **Edge cases:** renaming EN/AR names never affects the ID `[PO-G03]`; attempting to edit a locked ID is impossible (read-only field).

**Acceptance criteria**
- **AC-S4-01** Given the ID field, when "My kiosk #1" is typed, then the field contains only the allowed characters (e.g. "Mykiosk1") and never exceeds 19 characters.
- **AC-S4-02** Given a channel with one 2xx request logged, when SCR-04 opens in edit mode, then the ID field is read-only with the lock explanation.
- **AC-S4-03** Given Supported switched off on a row with Required ticked, when toggled, then Required clears and disables, and the summary count drops accordingly.

---

### SCR-05 — Parameters (list)

**Purpose.** Govern the parameter catalogue: built-ins and custom, their flags, and entry to the editor. `[BR]` `[UI]`
**Actors.** P-01 (manage); P-07 read-only (BR-24).

**Functional requirements (register)**
- **FR-S5-01** Origin tabs with live counts, name/API-field search and type filter, combined AND. `[UI]`
- **FR-S5-02** Render the parameters table per the specified columns; disabled rows dimmed. `[UI]`
- **FR-S5-03** Inline enable/disable toggle, guarded by the impact warning (Dialog D-6, BR-10) and audited. `[BR]` `[PO-G11]`
- **FR-S5-04** Navigation: New-parameter drawer (SCR-06), Manage mappings and per-row "Mapped" link (SCR-07). `[UI]`

**Layout** `[UI]`. Page header (title "Parameters" + description) with *Manage mappings* and *New parameter* right-aligned; toolbar (origin tabs, search, type filter); parameters table; footer note naming the consumers (M-06 KPI Engine, M-07 dashboards, M-10 data-scope filters) and the dependency guard: disabling a parameter referenced by scope filters, rules, or channel contracts requires an impact warning. `[PO-G11]` `[UI]`

**Toolbar fields**

| Name | Type | Default | Behaviour |
|---|---|---|---|
| Origin tabs | Segmented control: **All · n / Built-in · n / Custom · n** (live counts) | All | Filters rows by origin `[UI]` |
| Search | Search box, placeholder "Search by name or API field…" | empty | Live filter on name OR API field `[UI]` |
| Type filter | Select "All types" + the 13 ratified types | All | Filters by type `[UI]` |

All three combine with AND. `[UI]`

**Table — Parameters** `[UI]`

| Column | Content |
|---|---|
| Parameter | Name (semibold); disabled parameters render the whole row dimmed `[UI]` |
| API field name | Monospace code chip |
| Type | Type label |
| Origin | Badge: "Built-in" (navy) / "Custom" (cyan) |
| Enabled | Inline toggle — enables/disables the parameter directly from the list `[BR]` `[UI]` |
| Required | Check / dash glyph (required-by-default flag) |
| Filterable | Check / dash glyph |
| Reporting | Check / dash glyph |
| Dashboard | Check / dash glyph |
| Mapping | "Mapped" link → SCR-07, or "—" `[UI]` |
| Channels | Count of channel assignments, right-aligned |
| (actions) | *Edit* icon → SCR-06 drawer `[UI]` |

Sorting/pagination/empty/loading: FR-GBL-01 / FR-GBL-02. No delete action exists `[UI]` (BR below).

**Buttons**

| Label | Location | Permission | Action |
|---|---|---|---|
| Manage mappings | Header, secondary small | `m13.mapping.manage` (view) | Navigate SCR-07 `[UI]` |
| New parameter | Header, primary, plus icon | `m13.parameter.manage` | Open SCR-06 drawer (create) `[UI]` |
| Enabled toggle (row) | Table | `m13.parameter.manage` | Enable/disable; disable is guarded (BR) and audited |
| Edit (row) | Table | `m13.parameter.manage` | Open SCR-06 pre-filled `[UI]` |

**Business rules**
- Built-in parameters can be enabled/disabled but never deleted or renamed at the API-field level. `[BR]` `[PO-G16]` `[Derived from UI]`
- Disabling any parameter referenced by M-10 data-scope filters, rule builders, or a channel contract triggers an **impact warning** listing the references; confirm proceeds, cancel aborts. `[PO-G11]` (Dialog D-6.)
- Parameters are never hard-deleted; custom parameters are disabled instead. `[Derived from UI]`

**Dialogs**

**D-6 — Disable parameter: impact warning** `[Formalized — was A-15]` `[PO-G11]`. Trigger: switching the Enabled toggle off on a parameter referenced by M-10 data-scope filters, rule builders or a channel contract. Content: lists each referencing scope filter, rule and channel contract by name. Buttons: **Cancel** (no change) / **Disable anyway** (destructive) — proceeds and is audited. Esc / outside-click = cancel.

**Acceptance criteria**
- **AC-S5-01** Given tab "Custom" + type "Range", when applied, then only custom Range parameters remain and the tab counts stay global.
- **AC-S5-02** Given the Enabled toggle on `service` which is referenced by a channel contract, when switched off, then the impact warning lists the reference before anything changes.

---

### SCR-06 — Parameter Editor (drawer)

**Purpose.** Create or edit a parameter with its type configuration, validation rule, usage flags and channel assignment. `[BR]` `[UI]`
**Actors.** P-01.

**Functional requirements (register)**
- **FR-S6-01** Drawer behaviour: opens over SCR-05 with scrim; closes via ✕, scrim click or Esc. `[UI]`
- **FR-S6-02** Field set with API-field auto-suggest and lock-on-first-use (BR-11). `[BR]` `[PO-G16]` `[UI]`
- **FR-S6-03** Conditional type configuration: Range card (min/max/unit) and List panel (mapping pointer, BR-12). `[BR]` `[PO-G18]` `[UI]`
- **FR-S6-04** Six usage flags with ratified defaults and normative descriptions. `[BR]` `[PO-G19]` `[PO-G20]` `[UI]`
- **FR-S6-05** Channel-assignment pills add the parameter as supported with the required-default applied (BR-08). `[BR]` `[UI]`

**Layout** `[UI]`. Right-side drawer (~520 px) over SCR-05 with a scrim; header (title "New parameter" + sub-text + close ✕); scrollable body; footer (*Cancel*, *Create parameter*). Close paths: ✕, scrim click, Esc `[UI]`.

**Fields**

| Name | Label | Type | Required | Editable | Default | Placeholder | Validation & rules |
|---|---|---|---|---|---|---|---|
| `nameEn` | Parameter name · EN | Text | Yes | Yes | empty | "e.g. Wait Time" | Max 50 chars `[UI]`; typing auto-suggests the API field name `[UI]` |
| `nameAr` | Parameter name · AR | Text (RTL) | Yes | Yes | empty | Arabic example | `[UI]` |
| `apiField` | API field name | Text (monospace) | Yes | Conditionally — see BR | auto-suggested snake_case from `nameEn` (lowercased, invalid chars stripped, spaces → `_`) `[UI]` | "wait_time" | Unique per tenant (across built-ins, custom, enabled and disabled); snake_case. Hint (normative): "snake_case, unique per tenant. This is the key the caller sends. Locked once the first request using it has been received — renaming after that would break the caller (tenet T-08)." `[PO-G16]` `[UI]` |
| `type` | Data type | Select | Yes | Yes | — | — | The 13 ratified types (F0.4). Hint: "Range and List types take extra configuration below." Duration and Identifier must not appear. `[PO-G17]` `[UI]` |
| `rangeMin` / `rangeMax` / `rangeUnit` | Minimum / Maximum / Unit | Number / Number / Text | Min & Max Yes; Unit No | Yes | — / — / "minutes" placeholder | **Visible only when type = Range** `[BR]` `[UI]`; Min < Max `[Derived from UI]` |
| (List panel) | List values | Info card | — | — | — | — | **Visible only when type = List** `[UI]`: "List values and their source-value translations are managed in **Parameter mappings**." + *Open mappings* button (closes drawer → SCR-07). `[PO-G18]` `[UI]` |
| `validationRule` | Validation rule | Text (monospace) | No | Yes | empty | "e.g. ^[A-Z]{2}\d{6}$" | Optional; per-type options in F0.4; hint: failures rejected with `E-1003` `[PO-G07]` `[UI]` |

**Usage flags** (toggle switches with descriptions, normative copy) `[BR]` `[PO-G19]` `[PO-G20]` `[UI]`

| Flag | Default `[UI]` | Description |
|---|---|---|
| Required by default | Off | "Default when assigned to a channel; each channel can override." |
| Searchable | On | "Values indexed for response search." |
| Filterable | On | "Available as a filter facet in reports and dashboards." |
| Reporting visibility | On | "Appears as a data column in reports (M-07)." |
| Dashboard visibility | Off | "Available as a breakdown dimension on dashboards (M-06/M-07)." |
| Mapping support | Off | "Source values are translated through the mapping table." |

**Service channel assignment** `[BR]` `[UI]`: pill checkboxes, one per channel; selecting adds the parameter as *supported* on that channel with the required-default applied; hint: "fine-tune required/optional in the channel's contract." `[UI]` `[PO-G19]`

**Buttons**

| Label | Location | Action | Failure |
|---|---|---|---|
| ✕ / Cancel | Header / footer | Close drawer, discard | — |
| Open mappings | List panel | Close drawer → SCR-07 `[UI]` | — |
| Create parameter / Save | Footer, primary | Validate, persist, close, refresh SCR-05 | Inline errors; drawer stays open |

**Business rules**
- API field collision (including with disabled and built-in parameters) blocks save with an inline error. `[Derived from UI]`
- API field is locked (read-only) once the first inbound request carrying it has been received. `[PO-G16]`
- The mapping table is the single source of List values — no inline value entry exists. `[PO-G18]` `[UI]`

**Workflow — create custom Range parameter (success).** P-01: *New parameter* → EN/AR names (API field auto-fills) → type Range → min/max/unit → optional rule → flags → assigns channels → *Create parameter* → drawer closes, row appears under Custom. **Failure:** duplicate API field blocks; missing Range min/max blocks.

**Acceptance criteria**
- **AC-S6-01** Given type switched from Range to List, when changed, then the Range card hides and the List panel shows (and vice versa).
- **AC-S6-02** Given EN name "Wait Time", when typed, then the API field auto-suggests `wait_time` and remains manually editable before first use.
- **AC-S6-03** Given an API field that already exists (even disabled), when saving, then save is blocked with an inline uniqueness error.

---

### SCR-07 — Parameter Mappings

**Purpose.** Translate raw backend values into business display values (e.g. `S001` → "Visa Request"), bilingually. `[BR]` `[UI]`
**Actors.** P-01.

**Functional requirements (register)**
- **FR-S7-01** Parameter selector lists mapping-enabled parameters only and re-renders the table. `[UI]`
- **FR-S7-02** Unmapped-values alert with *Map now* pre-fill; hidden when the queue is empty. `[PO-G08]` `[UI]`
- **FR-S7-03** Mapping table with inline draft add-row; source values unique per parameter. `[BR]` `[UI]`
- **FR-S7-04** Row edit and delete; delete behind confirmation (Dialog D-7); effective at read time immediately. `[PO-G24]` `[UI]`
- **FR-S7-05** Excel export with columns `source_value`, `display_en`, `display_ar`. `[BR]` `[UI]`
- **FR-S7-06** Excel import (Dialog D-4): Merge / Replace-all modes; all-or-nothing with row-level report. `[BR]` `[PO]` `[UI]`
- **FR-S7-07** Replace-all (Dialog D-5): irreversible, permission-controlled, audited (BR-13). `[BR]` `[PO-G12]` `[UI]`

**Layout** `[UI]`. Page header (title "Parameter mappings" + description) with header actions *Export to Excel*, *Import from Excel*, *Add value*; toolbar (parameter selector + source-system badge); unmapped-values warning alert; full-width mapping table with a footer bar (count/last-updated text + *Replace all mappings…*).

**Toolbar fields**

| Name | Type | Default | Behaviour |
|---|---|---|---|
| `mappingParameter` | Select | First mapping-enabled parameter | Options: **mapping-enabled parameters only**, rendered "Name — api_field (n values)" `[UI]`; switching re-renders the table `[UI]` |
| Source-system badge | Read-only badge ("Source system: ‹name›") | — | Informational context `[UI]` |

**Unmapped-values alert** `[PO-G08]` `[UI]`: warning style; normative copy pattern: "**‹n› unmapped values received in the last 7 days:** ‹value chips› — responses carrying them display the raw value until mapped. *Map now*". *Map now* pre-fills a draft row with the value `[Derived from UI]`. Hidden when the queue is empty `[Derived from UI]`.

**Table — Mappings** `[UI]`

| Column | Content |
|---|---|
| Source value | Monospace code chip |
| Display value · EN | Text |
| Display value · AR | Text, RTL cell, Arabic font `[PO-G10]` `[UI]` |
| Status | "Active" (D2 badge); draft rows show "Draft" (neutral) `[UI]` |
| (actions) | *Edit* and *Delete* icon buttons `[UI]` |

*Inline add row* `[UI]`: *Add value* inserts an editable first row with three inputs — source value (monospace, placeholder "S0xx"), Display EN, Display AR (RTL) — status "Draft" and a **Save** button. Save requires a non-empty source value, unique within the parameter. `[Derived from UI]`

Footer bar `[UI]`: "‹n› mappings · last updated ‹when› by ‹user›" + **Replace all mappings…** (destructive-outline) → Dialog D-5.

**Buttons**

| Label | Location | Permission | Action | Confirmation |
|---|---|---|---|---|
| Export to Excel | Header, secondary small, download icon | `m13.mapping.manage` | Downloads the current parameter's mappings with columns `source_value`, `display_en`, `display_ar` `[BR]` `[UI]` | None |
| Import from Excel | Header, secondary small, upload icon | `m13.mapping.manage` | Opens Dialog D-4 `[BR]` `[UI]` | In-dialog |
| Add value | Header, primary, plus icon | `m13.mapping.manage` | Inserts the inline draft row `[UI]` | None |
| Edit (row) | Row | `m13.mapping.manage` | Makes the row editable `[UI]` | None |
| Delete (row) | Row | `m13.mapping.manage` | Removes the mapping; effective at read time immediately | Dialog D-7 |
| Replace all mappings… | Footer bar, destructive-outline | `m13.mapping.replace` | Opens Dialog D-5 | Yes |

**Dialogs**

**D-4 — Import mappings from Excel** `[UI]`. Trigger: *Import from Excel*. Content: title; text (normative): "Template columns: `source_value` `display_en` `display_ar`. Duplicate source values within the file are rejected; existing values are updated." The validation report lists row number, column and reason per failure `[Formalized default]`; drop zone "Drop the .xlsx file here or *browse*"; **Import mode** radio: *Merge with existing* (default) / *Replace all* `[UI]`. Buttons: **Cancel** (closes, no change) / **Import** (primary). Import is **all-or-nothing**: a row-level validation report is shown and the import applies only if 100 % of rows are valid `[PO]` (report contents specified in D-4). Esc / outside-click = cancel.

**D-5 — Replace all mappings?** `[UI]`. Trigger: *Replace all mappings…* or D-4 in Replace mode `[Derived from UI]`. Content (normative): "This removes all **‹n› current mappings** for **‹parameter›** and replaces them with the imported set. This action cannot be undone." Buttons: **Cancel** / **Replace all** (destructive filled). Confirm executes, closes, audited. `[PO-G12]` `[UI]`

**D-7 — Delete mapping confirmation** `[Formalized — was A-16]`. Trigger: row *Delete*. Content: "Delete mapping ‹source value› → ‹display EN›? Responses carrying this value will display the raw value until remapped." Buttons: **Cancel** / **Delete** (destructive). Confirm deletes (read-time effect immediate), audited under the mapping-change event.

**Business rules**
- Display values are bilingual: EN + AR on every mapping; reports render the viewer's language. `[PO-G10]`
- Source values are unique per parameter. `[Derived from UI]`
- **No version history and no restore** — the audit trail is the only change record; Replace-all is irreversible. `[PO-G12]`
- Deleting or changing a mapping takes effect immediately everywhere (read-time resolution, F0.5). `[PO-G24]`
- Unmapped incoming values are never rejected; they are stored raw and queued. `[PO-G08]`

**Workflow — bulk refresh (success).** P-01 exports current mappings → edits offline → *Import from Excel* → Merge mode → validation report all-valid → Import → table and read-time labels update. **Failure:** one invalid row → nothing applied, report names the row and reason. **Edge case:** importing while another editor changed data — last-write-wins with audit records (NFR-17).

**Acceptance criteria**
- **AC-S7-01** Given a file with 214 valid + 1 invalid row, when imported, then nothing is applied and the failing row and reason are reported.
- **AC-S7-02** Given Replace-all confirmed, when a historical report renders in AR, then it shows the new AR labels (read-time resolution).
- **AC-S7-03** Given incoming value `S014` with no mapping, when received, then the response stores/display the raw value and `S014` appears in the queue alert.

---

### SCR-08 — Request Logs

**Purpose.** Full traceability of every inbound API request: all parameters received plus the response returned. `[BR]` `[UI]`
**Actors.** P-07.

**Functional requirements (register)**
- **FR-S8-01** Filters: status-class chips, integration select (filters the list) and time select including *Last hour*; AND combination; counts per window. `[UI]`
- **FR-S8-02** Log table with expandable detail: *Parameters received* (registered + unregistered) and *Response returned*. `[BR]` `[UI]`
- **FR-S8-03** PII masking in list, detail and export (BR: G-14). `[PO-G14]` `[UI]`
- **FR-S8-04** Export of the current filtered view. `[Derived from UI]`
- **FR-S8-05** Log every request with the full field list; auth-rejected requests carry the rejected-before-parsing notice. `[BR]` `[UI]`

**Layout** `[UI]`. Page header (title "Request logs" + description "…Click a row to expand the full exchange.") with an *Export* action; toolbar (status filter chips, integration select, time select); PII-masking info alert; the logs table with expandable detail rows.

**Toolbar fields**

| Name | Type | Default | Behaviour |
|---|---|---|---|
| Status chips | Chip group: **All · n / Success · n (2xx) / Client errors · n (4xx) / Server errors · n (5xx)**, each with a semantic dot | All | Filters by status class `[UI]` |
| `logIntegration` | Select "All integrations" + integration names | All | **Filters the list** (ratified fix) `[UI]` |
| `logTime` | Select | Last 24 hours | Options: **Last hour** (ratified addition), Last 24 hours, Last 7 days, Last 30 days `[UI]`; chip counts reflect the window `[Derived from UI]` |

All filters combine with AND. `[UI]`

**Masking alert** `[PO-G14]` `[UI]` (normative copy): "Personal data in logged parameters (mobile, email, customer name) is masked in all log views. Log retention: 90 days."

**Table — Request logs** `[UI]`

| Column | Content |
|---|---|
| (expand) | Caret icon; rotates 90° when the row is expanded `[UI]` |
| Time | Monospace, "Today 14:32:08" style |
| Integration | Integration name, semibold |
| Endpoint | Code chip: method + full path (query string included for GET) `[UI]` |
| Scenario | Scenario badge |
| Result | HTTP status badge (2xx D2 / 4xx D4 / 5xx D5) + result-code prefix in monospace `[UI]` |
| Latency | Right-aligned monospace ("84 ms") |

Row click toggles an expandable detail row `[UI]` containing two panels:
- **Parameters received** — key–value grid of *all* parameters (registered + unregistered), PII masked (e.g. `+9627•••••312`, `M••••• A•-R•••••`) `[UI]`; auth-rejected requests show "— request rejected before parameter parsing" `[UI]`.
- **Response returned** — key–value grid of the full response (http, code, request_id, message / survey/embed URL) `[UI]`.

Default sort: newest first `[Derived from UI]` (FR-GBL-01). Pagination: FR-GBL-01. Empty/loading: FR-GBL-02. Selection/bulk: none `[UI]`.

**Buttons**

| Label | Location | Permission | Action |
|---|---|---|---|
| Export | Header, secondary small, download icon | `m13.log.view` | Exports the current filtered view; masked values export masked `[Derived from UI]` |

**Business rules**
- Every request is logged with: timestamp, integration, method + path, scenario, all parameters received, the complete response returned, HTTP status, result code, latency, credential label used. `[BR]` `[UI]` `[Derived from UI]`
- PII masking applies in list, detail and export; no unmasked-access permission exists in Phase 1. `[PO-G14]`
- Retention 90 days; tenant-specific retention by subscription plan is a future platform capability (recorded, not built). `[PO]`
- Rejected requests are logged including the rejection stage. `[UI]`

**Workflow — investigate a failure.** P-07 opens SCR-08 → chips "Client errors" → picks the integration → expands the `E-1002` row → reads the missing-field message → fixes the caller or the channel contract. **Failure flow:** load failure → standard error state (FR-GBL-02).

**Acceptance criteria**
- **AC-S8-01** Given filter chips 4xx + integration X + Last hour, when applied, then only matching rows remain and counts reflect the window.
- **AC-S8-02** Given a log row expanded, when PII fields render, then mobile/email/name values are masked in exactly the masked format, including in export.
- **AC-S8-03** Given an auth-rejected request, when expanded, then the parameters panel shows the rejected-before-parsing notice instead of data.

---

## Cross-screen Business Rules

- **BR-01** Phase 1 has no trigger-rule engine in M-13: every request passing validation is processed by its scenario; eligibility/sampling rules are deferred. `[PO]` (see *Scope › Deferred capabilities*)
- **BR-02** Exactly one integration scenario per integration; an additional scenario requires an additional integration. `[PO-G02]`
- **BR-03** The service channel ID is the only mandatory parameter of any M-13 API, carried as the path parameter; all other parameters are free key–value pairs. `[BR]`
- **BR-04** Channel ID format: manual entry, letters/numbers/`-` only, under 20 characters, no spaces or special characters, unique per tenant. `[PO-G03]`
- **BR-05** Channel ID lifecycle: editable until the channel's first successful (2xx) request, then locked permanently. Pre-lock edits change the endpoint path; the old ID resolves `E-1001`. `[PO-G23]`
- **BR-06** Channel display names are bilingual (EN + AR); renaming never affects the ID. `[PO-G03]`
- **BR-07** Inactive channels reject requests with `E-1004`, are hidden from new-integration selection, and remain listed. Channels with traffic history cannot be deleted — deactivate only. `[PO-G06]`
- **BR-08** The channel contract (supported/required per channel) is the authority on requiredness at request time; the parameter-level "Required by default" flag is only the assignment default. Different channels carry different parameter sets. `[BR]` `[PO-G19]`
- **BR-09** Built-in parameters: enable/disable only — never deleted, never renamed. Custom parameters: disabled, never hard-deleted. `[BR]` `[PO-G16]` `[Derived from UI]`
- **BR-10** Disabling a parameter referenced by M-10 data-scope filters, rule builders or a channel contract requires an explicit impact warning listing the references. `[PO-G11]`
- **BR-11** API field names are snake_case, unique per tenant, and locked once the first request carrying them has been received. `[PO-G16]`
- **BR-12** The mapping table is the single source of List values; List membership is not validated at ingestion. `[PO-G18]` `[PO-G08]`
- **BR-13** Mappings are bilingual, resolve at read time (retroactive relabelling by design), have no version history, and Replace-all is irreversible. Unmapped incoming values are stored raw, never rejected, and queued for mapping. `[PO-G10]` `[PO-G24]` `[PO-G12]` `[PO-G08]`
- **BR-14** Unregistered parameters are stored raw, visible only in request logs, and excluded from reports, dashboards, filters and rule builders until formally registered. `[PO-G09]`
- **BR-15** Validation failures reject the whole request with the defined business error code; requests are atomic. `[PO-G07]` `[PO]`
- **BR-16** Credential secrets are shown exactly once at generation and stored hashed/encrypted; API-key revocation takes effect immediately (`E-1401`); generating a new key revokes the active one. `[PO]` `[UI]` `[Derived from UI]`
- **BR-17** OAuth: client-credentials grant fixed in code; access-token lifetime fixed at 15 minutes in code; scopes limit which scenario endpoints a token may call. `[PO]` `[UI]`
- **BR-18** Retries with the same `(tenant, channelId, transaction_id)` are idempotent end-to-end. `[PO-G22]`
- **BR-19** Survey resolution (which survey applies) is owned by M-02 rules for all scenarios; M-13 never selects surveys. `[PO-G21]`
- **BR-20** Survey links (SCN-02) expire 24 hours after issue by default; iFrame embedding (SCN-04) requires an Allowed-Origins whitelist; all communication is HTTPS. `[PO]`
- **BR-21** All sensitive configuration actions are permission-controlled and audited (see Permissions Matrix). `[PO]`
- **BR-22** No migration from the legacy system: greenfield configuration only. `[PO]`
- **BR-23** All 23 built-in parameters ship enabled by default. `[Formalized default — was A-3]`
- **BR-24** Cross-persona read-only visibility: P-01 may view Integrations/Logs screens and P-07 may view data-model screens, read-only, via the `*.view` permission keys. `[Formalized default — was A-4]`
- **BR-25** Credentials generated inside a cancelled create-wizard are discarded with the draft. `[Formalized default — was A-13]`
- **BR-26** OAuth scope naming: one scope per scenario endpoint following the `‹resource›:‹verb›` convention; the prototype shows the representative subset `survey-requests:write`, `responses:write`, `survey-links:read`. `[Formalized default — was A-11]` `[UI]`

## Validation Rules (consolidated register)

Data-type validation rules carry IDs **VR-T01 … VR-T13** in table F0.4. Field- and entity-level rules:

| ID | Applies to | Rule | On violation | Source |
|---|---|---|---|---|
| VR-F01 | Integration name (SCR-02) | Required; unique per tenant; ≤ 100 characters | Inline error; save blocked | `[UI]` · length `[Formalized default — was A-10]` |
| VR-F02 | Channel name · EN (SCR-04) | Required; ≤ 50 characters; unique per tenant | Inline error | `[UI]` `[PO-G03]` |
| VR-F03 | Channel name · AR (SCR-04) | Required | Inline error | `[PO-G03]` `[UI]` |
| VR-F04 | Service channel ID (SCR-04) | Required; letters/digits/`-` only; < 20 chars (`maxlength` 19); no spaces/special characters; invalid characters stripped live; unique per tenant **case-insensitively**; stored and matched in the URL exactly as entered | Inline error / live strip | `[PO-G03]` `[UI]` · case rules `[Formalized default — was A-14]` |
| VR-F05 | Parameter names EN/AR (SCR-06) | Required; ≤ 50 characters | Inline error | `[UI]` |
| VR-F06 | API field name (SCR-06) | Required; snake_case; unique per tenant across built-in, custom, enabled and disabled parameters | Inline error; save blocked | `[PO-G16]` `[Derived from UI]` |
| VR-F07 | Range configuration (SCR-06) | Minimum and Maximum required; Minimum < Maximum | Inline error | `[BR]` `[UI]` · min<max `[Derived from UI]` |
| VR-F08 | Mapping source value (SCR-07) | Required; unique within the parameter | Inline error; save blocked | `[Derived from UI]` |
| VR-F09 | Excel import file (D-4) | Columns `source_value`, `display_en`, `display_ar`; duplicates within the file rejected; import all-or-nothing with row-level report | Validation report; nothing applied | `[UI]` `[PO]` |
| VR-F10 | Key label / Client name (SCR-02) | Required | Inline error | `[UI]` |
| VR-F11 | API request payload | ≤ 2 MB | `413 E-1413` | `[PO]` |
| VR-F12 | Console message copy | Patterns: "‹Field› is required" / "‹Value› is already in use" | — | `[Formalized default — was A-12]` |

## Status Lifecycle

| Entity | Statuses & transitions | Visual indicator `[UI]` | Invalid transitions |
|---|---|---|---|
| Integration | Active ⇄ Inactive (P-07, audited). Inactive → endpoint rejects calls (`E-1401` — credentials suspended `[Derived from UI]`) | Active: D2 dot badge; Inactive: neutral badge + "suspended" sub-line | Delete (does not exist) |
| Service channel | Active ⇄ Inactive (`E-1004` when inactive). Channel ID sub-state: Editable → **Locked** on first 2xx (one-way) | Status badges; locked ID renders read-only with explanation | Delete after traffic; unlock |
| Parameter | Enabled ⇄ Disabled (guarded by BR-10). API field sub-state: Renameable → **Locked** on first use (one-way; built-ins always locked) | Enabled toggle; disabled rows dimmed | Hard delete; rename built-in |
| Mapping entry | Draft (unsaved inline row) → Active; Active → deleted (immediate read-time effect) | "Draft" / "Active" badges | Restore (no history) |
| Credential | Active → Revoked (one-way); a newly generated credential supersedes the active one | Masked current-key row; revoked keys produce `E-1401` | Un-revoke; plaintext retrieval |
| Request log entry | Immutable once written; purged at retention (90 days) | — | Edit/delete by users |

## Cross-Module Contracts

Ratified ownership boundaries; each contract restates a ratified decision — no new behaviour. `[PO]`

| ID | Module | Contract |
|---|---|---|
| CMC-01 | M-02 Channels & Distribution | Owns survey dispatch: survey resolution (which survey applies — for **all** scenarios `[PO-G21]`), delivery-channel selection, sending, retries, cadence. M-13 hands off accepted SCN-01 requests (tenant, channel ID, transaction parameters, request id) and stops; M-13's `202` means accepted-to-queue, and M-02 delivery failures never surface as M-13 API errors. |
| CMC-02 | M-03 Survey & Forms | Owns survey definitions and rendering. M-13 retrieves the definition JSON (SCN-03) / embed URL (SCN-04) for the resolved survey and relays it unchanged, treating the schema as opaque. |
| CMC-03 | M-04 Response Collection | Owns response validation, deduplication (key: tenant + channel ID + `transaction_id`, per F0.7) and storage. M-13 forwards SCN-05 payloads; its `202` means delivered-to-M-04, not stored. |
| CMC-04 | M-06 KPI Engine / M-07 Dashboards & Reporting | Consume transaction metadata and the parameter catalogue: *Reporting visibility* → report column; *Dashboard visibility* → breakdown dimension; *Filterable* → filter facet; read-time mapping resolution (F0.5) applies wherever display values render. |
| CMC-05 | M-09 Notifications | Operational alerting on integration failures is a future phase; **Phase 1 only logs failures** (SCR-08, error-rate tiles). No M-13 requirement may assume M-09 delivery. |
| CMC-06 | M-10 User & Role Management | M-13 registers the permission keys in the Permissions Matrix and delegates authorisation. M-10 data-scope filters are built on M-13 parameter definitions and value sets; BR-10's impact warning protects that dependency. |
| CMC-07 | M-14 / M-15 / M-16 (rules, actions, journeys) | May reference M-13 parameters; such references participate in the BR-10 impact warning. |

## Permissions Matrix

Ratified action-level split `[PO]`; interim until refined by M-10/DOC-02. Permission keys registered with M-10 in parentheses.

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

Audit events emitted (actor, tenant, timestamp, entity, before/after summary): integration created/updated · integration activated/deactivated · credential generated · credential revoked · channel created/updated · channel ID changed · channel activated/deactivated · parameter created/updated · parameter enabled/disabled · mapping added/edited/deleted · mapping import (mode, row count) · mapping replace-all (rows removed/added). With version history descoped, these events are the sole change record for mappings. `[PO]` `[PO-G12]`

## Error Handling

**API (caller-facing).** Normative catalogue and pipeline in F0.2/F0.3; message copy patterns in F0.3; every response carries a structured result code `[BR]`. Duplicate records: duplicate `transaction_id` is not an error (BR-18). Integration/downstream failure: `500 E-1500` with the retry-idempotent message; M-13 never exposes downstream (M-02/M-03/M-04) errors directly `[Derived from UI]`.

**Console (user-facing).**
- Validation errors: inline, per field, on blur/save — required, uniqueness (integration name, channel ID, API field, mapping source value), charset/length (channel ID), Range min<max — see the Validation Rules register (VR-F01…F12). Copy: VR-F12.
- Permission errors: actions the role lacks are hidden or disabled `[Derived from UI]`; direct-route access without view permission shows an access-denied state (FR-GBL-05 / FR-GBL-02).
- System/network errors: standard error state with retry (FR-GBL-02); dialogs preserve entered data on failure.
- Missing data: unmapped values → the SCR-07 queue alert (never an error) `[PO-G08]`.
- Duplicate records: blocked at save with uniqueness messages.
- Concurrency: last-write-wins with full audit trail (NFR-17).
- Import errors: row-level validation report; all-or-nothing application `[PO]`.

## Notifications

- **Confirmation dialogs** (blocking): D-3 Revoke key, D-5 Replace all mappings, D-6 Disable-parameter impact warning, D-7 Delete mapping — destructive styling, explicit consequence text, Cancel default. `[UI]` `[Formalized — D-6/D-7]`
- **Show-once dialogs**: D-1 / D-2 credential reveal with copy actions and secrets-manager warning. `[UI]`
- **Inline alerts**: SCR-04 contract summary (info), SCR-07 unmapped-values (warning), SCR-08 masking/retention (info). `[UI]`
- **Inline feedback**: Copy buttons flip to "Copied ✓". `[UI]`
- **Success toasts** on create/save/import/revoke: FR-GBL-04 `[Formalized default]` (not shown in prototype).
- **Email/system notifications**: none in Phase 1 — operational alerting deferred to M-09; Phase 1 only logs failures. `[PO]`

## Non-functional Requirements

| # | Requirement | Source |
|---|---|---|
| NFR-1 | API availability 99.9 % monthly | `[PO]` |
| NFR-2 | 95 % of API requests complete within 500 ms, excluding downstream systems | `[PO]` |
| NFR-3 | Maximum request payload 2 MB | `[PO]` |
| NFR-4 | Default rate limit 100 requests/sec per integration, configurable by Nabadat Operations with no code changes | `[PO-G15]` |
| NFR-5 | HTTPS with TLS 1.2+ everywhere (API and console) | `[PO]` |
| NFR-6 | Secrets encrypted/hashed at rest; show-once at generation; never logged | `[PO]` `[UI]` |
| NFR-7 | All configuration changes audited | `[PO]` |
| NFR-8 | Request-log retention 90 days; tenant-specific retention by subscription plan is future scope | `[PO]` |
| NFR-9 | Multi-tenant isolation: integrations, credentials, channels, parameters, mappings and logs are tenant-scoped; no cross-tenant access | `[Derived from UI]` (platform tenet) |
| NFR-10 | Localization: console fully bilingual EN/AR with RTL layout; AR inputs render RTL with the Arabic font stack; light and dark themes per the Nabadat design system | `[UI]` |
| NFR-11 | Accessibility: keyboard operability of dialogs/drawer (Esc closes), focus-visible rings, reduced-motion support | `[UI]` |
| NFR-12 | Responsive behaviour: desktop-first; tiles collapse to two/one columns and the sidebar hides below tablet width; tables scroll horizontally | `[UI]` |
| NFR-13 | Usability: destructive actions always behind explicit confirmation naming the consequence | `[UI]` |
| NFR-14 | Browser support: current evergreen Chrome/Edge/Firefox/Safari | `[Formalized default — was A-18]` |
| NFR-15 | Session handling & timeouts: platform-standard console session; API has no session (per-request auth) | `[Formalized default — was A-18]` |
| NFR-16 | Scalability guardrails: ≤ 200 custom parameters, ≤ 100 channels, ≤ 200 integrations per tenant; ≤ 5,000 mappings per parameter; Excel import ≤ 10,000 rows | `[Formalized default — was A-1]` |
| NFR-17 | Concurrency: last-write-wins with full audit records; no pessimistic locking in Phase 1 | `[Formalized default — was A-17]` |

## Glossary & Data Dictionary

### Glossary — core business terms

| Term | Definition |
|---|---|
| Integration (point) | A named, tenant-scoped API configuration serving exactly one service channel through exactly one integration scenario, with its own credentials and request logs. |
| Integration scenario | One of the five normative interaction patterns SCN-01…SCN-05 (F0.1). |
| Caller / source system | The tenant backend that invokes an M-13 API (core bus, CRM, mobile backend, queue system…). |
| Service channel | The business channel a transaction came through (portal, app, counter, call center). **Not** a distribution channel (WhatsApp/SMS/email — those belong to M-02). |
| Channel ID | The manually entered, URL-safe identifier of a service channel (VR-F04); the only mandatory path parameter of every M-13 API (BR-03); locked after the channel's first successful request (BR-05). |
| Parameter | The definition of one transaction data field. Origins: **built-in** (platform-shipped, F0.10), **custom** (tenant-created), **unregistered** (received on the wire without a definition, BR-14). |
| Parameter contract | The per-channel set of supported parameters and its required subset (BR-08); the runtime authority on requiredness. |
| Mapping | A translation entry `source value → display value (EN, AR)` for a mapping-enabled parameter, resolved at read time (F0.5). |
| Unmapped value | A received value of a mapping-enabled parameter with no mapping entry: stored raw, displayed raw, queued for P-01 (BR-13). |
| Unregistered parameter | A received key–value pair with no parameter definition: stored raw, visible only in request logs, excluded from analytics until registered (BR-14). |
| Read-time resolution | Rendering rule: display values are looked up in the current mapping table whenever data is read, so mapping changes relabel historical data (F0.5). |
| Show-once | Credential secrets are displayed a single time at generation and are never retrievable afterwards (BR-16). |
| Dispatch | SCN-01: the caller sends transaction details; Nabadat (M-02) selects the channel and delivers the survey. |
| Idempotent retry | A repeat request with the same tenant + channel ID + `transaction_id`; safe by design (F0.7, BR-18). |
| Trigger rules | In Phase 1: the fixed behaviour that every validated request is processed (BR-01); rule-based eligibility/sampling is a deferred capability (Scope). |
| Request log | The immutable record of one inbound request: all parameters received plus the response returned (SCR-08). |
| Tenant | An isolated customer instance; all M-13 configuration and data are tenant-scoped (NFR-9). |

### Data Dictionary — business entities

Business-level entity definitions (no storage schema implied). The **built-in parameter dictionary** is the normative catalogue in **F0.10**.

| Entity | Definition & key attributes (business level) |
|---|---|
| Integration | Name (unique, VR-F01) · description · service channel · scenario (one of five) · authentication type · status (Active/Inactive) · SCN-04 allowed-origins list and SCN-02 link-expiry override (FR-S2-10) · creation metadata. |
| Credential set | Belongs to one integration. API key (label, show-once secret, Active/Revoked) **or** OAuth client (client name, scopes, show-once secret). |
| Service channel | Name EN + AR · channel ID (VR-F04, lock state) · description · status · parameter contract. |
| Parameter | Name EN + AR · API field name (VR-F06, lock-on-first-use) · data type + type configuration (Range min/max/unit; List via mappings) · validation rule · origin (built-in/custom) · enabled state · six usage flags · channel assignments. |
| Channel-parameter assignment | Channel × parameter with `supported` and `required` flags — the contract row. |
| Mapping entry | Parameter × source value (unique per parameter, VR-F08) with display EN + AR and status (Draft/Active). |
| Unmapped-value queue item | Parameter × raw source value with 7-day occurrence window (SCR-07 alert). |
| Request log entry | Timestamp · integration · method + path · scenario · all parameters received (registered + unregistered, PII-masked at display) · full response returned · HTTP status · result code · latency · credential label · rejection stage where applicable. Immutable; retained 90 days (NFR-8). |

## Assumptions

- **A-2 Retro-reportability** — when an unregistered key is later registered as a parameter, previously received raw values become reportable (consequence of raw storage + read-time resolution, F0.5/F0.6). This is the only remaining business-behaviour assumption; the PO may confirm or reverse it without structural impact.

### Formalized assumptions — conversion map (v1.1)

Former assumptions converted into formal requirements/rules, values unchanged and marked `[Formalized default]` at their new home:

| Was | Now |
|---|---|
| A-1 capacity guardrails | NFR-16 |
| A-3 built-in initial state | BR-23 |
| A-4 cross-persona read-only visibility | BR-24 (+ Permissions Matrix) |
| A-5 unsaved-changes guard | FR-GBL-03 |
| A-6 SCN-04 allowed-origins / SCN-02 link-expiry fields | FR-S2-10 |
| A-7 error-rate thresholds | FR-S1-06 |
| A-8 pagination & ordering | FR-GBL-01 |
| A-9 empty/loading/error/access-denied states | FR-GBL-02 |
| A-10 integration-name length | VR-F01 |
| A-11 OAuth scope naming | BR-26 |
| A-12 message copy & toasts | VR-F12 + FR-GBL-04 |
| A-13 discarded credential drafts | BR-25 |
| A-14 channel-ID case rules | VR-F04 |
| A-15 impact-warning dialog | Dialog D-6 (SCR-05) |
| A-16 delete confirmation & import report | Dialog D-7 + D-4 spec |
| A-17 concurrency | NFR-17 |
| A-18 browser support & sessions | NFR-14 / NFR-15 |

## Open Questions

None. The former OQ-1 (trigger rules) is resolved for Phase 1 by BR-01 and recorded as a **deferred capability** under *Scope*; ownership boundaries are in *Cross-Module Contracts*.

## Decision References (for the `[PO-Gxx]` tags)

G-01 tenant integration persona = P-07 · G-02 one scenario per integration · G-03 bilingual channel names + manual channel ID (letters/numbers/`-`, < 20 chars, no spaces) · G-04/G-05 ratified credential field set (API key: label + revoke; OAuth: client name, token endpoint, scopes; grant type & lifetime fixed in code, 15-min tokens) · G-06 inactive channels reject `E-1004`, never deleted after traffic · G-07 error-code catalogue approved; validation failures reject · G-08 unmapped values accepted, stored raw, queued · G-09 unregistered parameters excluded from reports/dashboards/filters/rules until registered · G-10 bilingual mapping display values · G-11 impact-warning guard on disabling referenced parameters · G-12 mapping version history removed entirely · G-13 no sandbox/test credentials in Phase 1 · G-14 log PII masked for all; 90-day retention · G-15 rate limiting 100 req/s default, ops-configurable · G-16 API field immutable after first use · G-17 types added: Date, Date & time, Currency, Percentage, URL, Geolocation; rejected: Duration, Identifier · G-18 mapping table is the single source of List values · G-19 channel contract overrides parameter required-default · G-20 searchable = response-search index; filterable = report/dashboard facet · G-21 survey resolution owned by M-02 rules for all scenarios · G-22 idempotent retries on duplicate `transaction_id` · G-23 channel ID uneditable after first successful request · G-24 mapping resolution at read time. Plus ratified UI updates (three tiles, "Service channel" label, dynamic step-3 contract, Last-hour log filter, working integration filter) and the greenfield no-migration decision.

*End of SRS — M-13 Integration Hub v1.0.*
