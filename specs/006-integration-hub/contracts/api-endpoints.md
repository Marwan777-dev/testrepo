# API Contracts: M-13 Integration Hub

**Status**: proposed technical design — the source SRS explicitly declares illustrative endpoint
paths ("the base URL and path segments shown in this document... are illustrative only"). The
*behaviours* below are normative (traced to FR-IDs); paths/shapes are this plan's proposal,
corrected against binding constitution articles: `/api/v1/` prefix (API-01), cursor pagination
(API-04), the full `{code, message, correlation_id, tenant_id}` console error envelope (API-05),
and RBAC declarations (API-03).

Two distinct API surfaces exist, with two distinct audiences and two distinct envelopes:

1. **Console API** (`/api/v1/integration-hub/...`) — authenticated by the platform's own JWT
   session (P-01/P-07 personas), used by the SPA. Standard platform error envelope.
2. **Inbound Scenario API** (illustrative paths per F0.1, e.g. `/v1/survey-requests/{channelId}`)
   — authenticated by the tenant's own generated API key or OAuth token, called by external
   caller/source systems. Its own normative result-code envelope (F0.3), not the console's.

---

## Console API — Service Channels (SCR-03/04)

### `GET /api/v1/integration-hub/service-channels`

- **required_permission**: `m13.channel.view` · **required_scope**: `organisation` ·
  **default_personas**: P-01 (manage), P-07 (read-only, BR-24)
- Cursor-paginated (`page_size`/`page_token`), returns name/description/channel_id/status/
  supported_count/required_count/integrations_count per row (FR-S3-01).

### `POST /api/v1/integration-hub/service-channels`

- **required_permission**: `m13.channel.manage` · **default_personas**: P-01 only
- **201** → channel + contract rows; audit `channel.created`.
- **400** `validation.*` (VR-F02/F03/F04) · **409** `validation.duplicate_name` /
  `validation.duplicate_channel_id` (case-insensitive) · **400**
  `validation.capacity_exceeded` (VR-F13, ≤100 channels) · **403** non-P-01.

### `PUT /api/v1/integration-hub/service-channels/{id}`

- **required_permission**: `m13.channel.manage` · **default_personas**: P-01 only
- **200** → updated channel; audit `channel.updated` (+ `channel.id_changed` if `channel_id`
  changed, + `channel.activated`/`channel.deactivated` if `active` changed).
- **409** `channel.id_locked` — attempted `channel_id` edit after the channel's first 2xx
  request (BR-05, enforced server-side regardless of client state).
- No `DELETE` endpoint exists at all (BR-07, FR-S3-02).

---

## Console API — Parameters (SCR-05/06)

### `GET /api/v1/integration-hub/parameters`

- **required_permission**: `m13.parameter.view` · **default_personas**: P-01 (manage), P-07
  (read-only)
- Query: `origin` (`built_in`|`custom`), `type` (one of 13), `q` (name/api_field search) — AND
  combined (FR-S5-01). Cursor-paginated.

### `POST /api/v1/integration-hub/parameters`

- **required_permission**: `m13.parameter.manage` · **default_personas**: P-01 only
- **201** → parameter; audit `parameter.created`.
- **409** `validation.duplicate_api_field` (VR-F06, incl. disabled/built-in) · **400**
  `validation.range_min_max` (VR-F07) · **400** `validation.capacity_exceeded` (VR-F13, ≤200
  **custom** parameters — built-ins excluded from the count).
- No `DELETE` endpoint exists at all (BR-09).

### `PATCH /api/v1/integration-hub/parameters/{id}`

- **required_permission**: `m13.parameter.manage` · **default_personas**: P-01 only
- `{ enabled: false }` on a referenced parameter (BR-10) → **200**, response includes the
  reference list (scope filters, rules, channel contracts by name) so the client renders Dialog
  D-6 pre-confirmation *(exact confirm-flow wire shape — response-includes-list vs. a required
  `confirm=true` re-call — is a pure protocol choice with no behavioral difference; resolved at
  implementation time)*. Audit `parameter.enabled`/`parameter.disabled`.
- Attempting to change `data_type` on a built-in → **409** `parameter.type_locked` (`[PO-G27]`).

---

## Console API — Integrations (SCR-01/02)

### `GET /api/v1/integration-hub/integrations`

- **required_permission**: `m13.integration.view` · **default_personas**: P-07 (manage), P-01
  (read-only, BR-24)
- Returns the SCR-01 stat tiles (total/active count, 24h request count, error rate) + the table
  rows (FR-S1-01…06). Query: `q` (name search) AND `channel` filter (FR-S1-02). Cursor-paginated.

### `POST /api/v1/integration-hub/integrations`

- **required_permission**: `m13.integration.manage` · **default_personas**: P-07 only
- **Request**: `{ name, description?, service_channel_id, scenario, allowed_origins? (SCN-04
  only), link_expiry_override_hours? (SCN-02 only), credential: { mechanism: "api_key",
  key_label } | { mechanism: "oauth_client", client_name, scopes[] } }`
- **201** → integration + provisioned endpoint + show-once plaintext secret; audit
  `integration.created` + `credential.generated`.
- **400/409** `validation.*` (VR-F01/F10) · **400** `validation.capacity_exceeded` (VR-F13,
  ≤200 integrations) · **400** `channel.not_active` (only active channels selectable, FR-S2-02).

### `PUT /api/v1/integration-hub/integrations/{id}`

- **required_permission**: `m13.integration.manage` · **default_personas**: P-07 only
- **200** → updated integration (name, description, channel, security config); `scenario` is
  immutable after create (BR-02) — a request attempting to change it → **409**
  `integration.scenario_immutable`.

### `PATCH /api/v1/integration-hub/integrations/{id}`

- `{ active: bool }` — Active ⇄ Inactive toggle (US10). **200**; audit
  `integration.activated`/`integration.deactivated`. No `DELETE` exists (Status Lifecycle table).

### `POST /api/v1/integration-hub/integrations/{id}/credentials`

- Generate a new credential (implicitly revokes any current Active one, BR-16). **200** →
  show-once plaintext; audit `credential.generated` (+ `credential.revoked` for the superseded
  row, same request).

### `POST /api/v1/integration-hub/integrations/{id}/credentials/revoke`

- Revoke the current Active credential without regenerating. **200**; audit
  `credential.revoked`. Idempotency: revoking an already-revoked credential → **409**
  `credential.already_revoked`.

---

## Console API — Parameter Mappings (SCR-07)

### `GET /api/v1/integration-hub/parameters/{id}/mappings`

- **required_permission**: `m13.mapping.manage` (view) · **default_personas**: P-01 (manage),
  P-07 (read-only)
- Cursor-paginated; `{id}` must be a mapping-enabled parameter (else **400**).

### `GET /api/v1/integration-hub/parameters/{id}/mappings/unmapped-queue`

- Returns `UnmappedValueOccurrence` rows within the trailing 7 days (FR-S7-02).

### `POST` / `PUT` / `DELETE /api/v1/integration-hub/parameters/{id}/mappings[/{mappingId}]`

- **required_permission**: `m13.mapping.manage` · **default_personas**: P-01 only
- Add/edit/delete a mapping. **409** `validation.duplicate_source_value` (VR-F08,
  case-insensitive) on add/edit. Delete is immediate, read-time-effective (BR-13); audit
  `mapping.added`/`mapping.edited`/`mapping.deleted`.

### `GET /api/v1/integration-hub/parameters/{id}/mappings/export`

- Returns the 3-column (`source_value`, `display_en`, `display_ar`) file for the parameter
  (FR-S7-05).

### `POST /api/v1/integration-hub/parameters/{id}/mappings/import`

- **Request**: `{ mode: "merge" | "replace_all", file }` (multipart)
- All-or-nothing (VR-F09): **200** only if every row validates; **400/422** with a row-level
  report otherwise, zero mappings changed. **400** `validation.import_row_limit_exceeded`
  (NFR-16, ≤10,000 rows) rejected before any row parsing. **400**
  `validation.capacity_exceeded` (NFR-16, ≤5,000 mappings/parameter) if the merge/import would
  exceed it. Audit `mapping.import` (mode, row count) or `mapping.replace_all` (rows
  removed/added) — permission-controlled at `m13.mapping.replace` specifically for replace-all
  mode (Permissions Matrix).

---

## Console API — Request Logs (SCR-08)

### `GET /api/v1/integration-hub/request-logs`

- **required_permission**: `m13.log.view` · **default_personas**: P-07 only (no cross-persona
  read-only grant for P-01 — logs are P-07-exclusive per the Permissions Matrix, distinct from
  every other screen's BR-24 read-only-for-the-other-persona pattern)
- Query: `status_class` (`2xx`|`4xx`|`5xx`), `integration_id`, `window`
  (`last_hour`|`24h`|`7d`|`30d`, default `24h`) — AND combined (FR-S8-01). PII masked in every
  row (FR-S8-03). Cursor-paginated, newest-first.

### `GET /api/v1/integration-hub/request-logs/export`

- Same filters as the list; exports the current filtered view, PII masked identically
  (FR-S8-04).

---

## Inbound Scenario API (illustrative paths, F0.1 — normative behaviour only)

All five endpoints share the ordered validation pipeline (FR-F0-02) and result-code catalogue
(FR-F0-03). The service channel ID is the **only** mandatory path parameter (BR-03); all other
data is free key–value pairs (body or query string, per scenario).

| Scenario | Method + illustrative path | Auth scope required (BR-26) | Success response |
|---|---|---|---|
| SCN-01 Dispatch | `POST /v1/survey-requests/{channelId}` | `survey-requests:write` | `202 ACCEPTED` + `request_id` |
| SCN-02 Redirect link | `POST /v1/survey-links/{channelId}` | `survey-links:read` | `200 OK` + `{ survey_url, expires_at }` |
| SCN-03 JSON render | `POST /v1/survey-definitions/{channelId}` | `survey-definitions:read` | `200 OK` + survey definition JSON (relayed from M-01's `ISurveyRenderService`, research.md §4.2) |
| SCN-04 iFrame embed | `GET /v1/survey-embed/{channelId}?…` | `survey-embed:read` | `200 OK` + short-lived embed URL (two-step flow, Clarifications 2026-07-27 — the browser subsequently loads this URL from a *separate*, unauthenticated, origin-checked public rendering surface not owned by this endpoint) |
| SCN-05 Response ingestion | `POST /v1/responses/{channelId}` | `responses:write` | `202 ACCEPTED` (M-04 must save unconditionally, Clarifications 2026-07-27, SC-016) |

**Error envelope** (all five, F0.3): every non-2xx response carries `{ result_code, message,
request_id }` with `result_code ∈ {E-1001, E-1002, E-1003, E-1004, E-1401, E-1413, E-1429,
E-1500}`, exact message copy per spec.md's normative patterns.

**Idempotency** (BR-18/F0.7, Clarifications 2026-07-27): retries with the same `(tenant,
channelId, transaction_id)` are deduplicated **with no fixed, guaranteed retention window** — an
accepted limitation, not an engineered SLA.
