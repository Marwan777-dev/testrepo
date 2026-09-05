# Data Model: M-13 Integration Hub

**Feature**: `006-integration-hub` | Derived from spec.md "Key Entities" + Requirements (FR-S*,
BR-*, VR-*) + the two 2026-07-27 clarification rounds (BR-27 mapping-by-type, VR-F13 capacity
guardrails, VR-F08 case-insensitivity, SCN-04 two-step flow, CMC-03 unconditional-save, BR-18
no-fixed-idempotency-window).

All tables live in the tenant schema (`tenant_{slug}`), owned exclusively by
`Nabadat.IntegrationHub` (DB-02/AD-02 — no `tenant_id` column). Primary keys are UUID (DB-03).
`created_at`/`updated_at` in UTC; time-dependent fields computed via injected `TimeProvider`
(DB-08 rule 7).

---

## 1. `Integration`

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `name` | text | required, ≤120 chars, **unique per tenant** (VR-F01; case-sensitivity not specified by the SRS for this field — implemented case-insensitively for consistency with VR-F04/F06/F08's established convention, confirmed at implementation time) | |
| `description` | text, nullable | optional | |
| `service_channel_id` | UUID | FK → `ServiceChannel.id`, required | intra-module FK (Article 4.1) |
| `scenario` | text | required; one of `dispatch` \| `redirect_link` \| `json_render` \| `iframe_embed` \| `response_ingestion` (SCN-01…05); **immutable after create** (BR-02 — exactly one scenario per integration; changing scenario requires a new integration) | |
| `active` | boolean | default `true` | Active ⇄ Inactive (Status Lifecycle) |
| `allowed_origins` | text[], nullable | populated only when `scenario = iframe_embed` (FR-S2-10) | SCN-04 whitelist |
| `link_expiry_override_hours` | integer, nullable | populated only when `scenario = redirect_link`; default 24 if null (FR-S2-10, FR-F0-08) | SCN-02 override |
| `created_by` | UUID (user id) | required | audit attribution only |
| `created_at` / `updated_at` | timestamptz | UTC | |

**Derived (never stored)**: `status` — `Active`/`Inactive` directly from the `active` column
(no date-computation, unlike M-15's Actions — Status Lifecycle here is a direct toggle, BR-21).

**Validation**: VR-F01 (name), BR-02 (exactly one scenario, enforced by the column being a
single field, not a set), **VR-F13** (tenant guardrail: ≤ 200 integrations, NFR-16 — checked at
create time, blocked with an inline console error naming the limit).

**Indexes**: unique index on `lower(name)` (assuming case-insensitive per the note above);
index on `service_channel_id`; index on `active`.

---

## 2. `Credential`

One row per `Integration` (BR-16 — a new key implicitly revokes the old one, so there is at
most one *Active* credential per integration at a time; **revoked** rows are retained for audit,
never deleted).

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `integration_id` | UUID | FK → `Integration.id`, required | |
| `mechanism` | text | required; `api_key` \| `oauth_client` (discriminator) | fixed at first generation; matches the integration's chosen mechanism |
| `label_or_client_name` | text | required (VR-F10) | `keyLabel` for API key, `clientName` for OAuth |
| `secret_hash` | text | required, **never the plaintext** (BR-16, NFR-6) | hashed/encrypted at rest; plaintext returned exactly once at generation, never persisted or logged |
| `scopes` | text[], nullable | populated only when `mechanism = oauth_client`; subset of the 5 ratified scopes (BR-26) | `survey-requests:write`, `survey-links:read`, `survey-definitions:read`, `survey-embed:read`, `responses:write` |
| `status` | text | `active` \| `revoked` (one-way, Status Lifecycle) | |
| `generated_at` | timestamptz | UTC | |
| `generated_by` | UUID (user id) | | audit attribution |
| `revoked_at` | timestamptz, nullable | | set on revoke |

**Fixed in code, never columns** (ratified removals — `[PO-G13]`, BR-17): grant type
(always `client_credentials`), access-token lifetime (always 15 minutes), expiry, sandbox flag,
allowed-source-IPs.

**Validation**: VR-F10 (label/client name required); BR-16 (generating a new credential while
one is `active` implicitly sets the prior row to `revoked` — a single atomic write, not two
separate user actions).

**Indexes**: index on `(integration_id, status)` for the "current active credential" lookup.

---

## 3. `ServiceChannel`

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `name_en` | text | required, ≤50 chars, unique per tenant (VR-F02) | |
| `name_ar` | text | required (VR-F03) | RTL |
| `channel_id` | text | required; `[A-Za-z0-9-]+`, ≤19 chars; **unique per tenant case-insensitively** (VR-F04); stored and matched in the URL **exactly as entered** (case-preserved for path matching, even though uniqueness is case-insensitive) | the only mandatory API path parameter (BR-03) |
| `description` | text, nullable | optional | |
| `active` | boolean | default `true` | Active ⇄ Inactive; inactive → `E-1004` (BR-07) |
| `channel_id_locked` | boolean | default `false` | one-way: set `true` on the channel's first 2xx request (BR-05); once `true`, `channel_id` is server-side immutable regardless of client state |
| `created_at` / `updated_at` | timestamptz | UTC | |

**Derived (never stored)**: `supported_count`, `required_count`, `integrations_count` —
computed from `ChannelParameterAssignment` and `Integration` at read time for SCR-03's list.

**Validation**: VR-F02/F03/F04; **VR-F13** (tenant guardrail: ≤ 100 channels, NFR-16).

**Business rules**: BR-04 (format), BR-05 (lock lifecycle — enforced server-side even if a
stale client attempts a post-lock edit), BR-06 (renaming EN/AR never touches `channel_id`),
BR-07 (no delete ever — deactivate only; a channel with `channel_id_locked = true` has, by
definition, received traffic, so BR-07's "channels with traffic history cannot be deleted"
is equivalent to "a locked channel can never be deleted" — the lock flag is the enforcement
mechanism for both rules at once).

**Indexes**: unique index on `lower(name_en)`; unique index on `lower(channel_id)`; the
literal-cased `channel_id` also needs a plain (non-lowered) index for the hot path — resolving
`{channelId}` from an inbound request's URL, which must match exactly as entered per VR-F04.

---

## 4. `Parameter`

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `name_en` | text | required, ≤50 chars (VR-F05) | |
| `name_ar` | text | required, ≤50 chars (VR-F05) | RTL |
| `api_field` | text | required, `snake_case`, **unique per tenant across built-in + custom + enabled + disabled** (VR-F06) | the wire key; locked after first use (BR-11) |
| `api_field_locked` | boolean | default `false` | one-way: `true` once the first request carrying this field has been received; built-ins are **always** `true` (BR-09/BR-11) |
| `data_type` | text | required; one of the 13 ratified types (FR-F0-04); **Duration and Identifier must never appear** (`[PO-G17]`) | |
| `data_type_locked` | boolean | computed = `origin = 'built_in'` | built-in parameter types are read-only (`[PO-G27]`, BR-09) — not a separately stored flag, derived from origin |
| `range_min` / `range_max` / `range_unit` | numeric / numeric / text, nullable | populated only when `data_type = range`; `range_min < range_max` (VR-F07) | |
| `validation_rule` | text, nullable | optional; regex or per-type rule reference | violations → `E-1003` |
| `origin` | text | required; `built_in` \| `custom` | built-ins seeded at tenant creation (BR-23, all 23 enabled) |
| `enabled` | boolean | default `true` | Enabled ⇄ Disabled (guarded by BR-10's impact-warning check, not a hard block) |
| `required_by_default` | boolean | default `false` | assignment default only — the channel contract overrides at request time (BR-08) |
| `filterable` | boolean | default `true` | |
| `reporting_visibility` | boolean | default `true` | |
| `dashboard_visibility` | boolean | default `false` | |
| `mapping_support` | boolean | **derived default by `data_type`, per BR-27**: `true` and locked when `data_type = list`; user-changeable, default `false`, when `data_type ∈ {text, boolean, url}`; `false` and locked (unavailable) for all other types | *Note: "Searchable" flag from the original SRS is removed per `[PO-G26]` — only 5 usage flags remain, matching spec.md's FR-S6-04.* |
| `created_at` / `updated_at` | timestamptz | UTC | |

**Validation**: VR-F05/F06/F07; **VR-F13** (tenant guardrail: ≤ 200 **custom** parameters —
built-ins don't count toward this ceiling, NFR-16); BR-27 (mapping-support state machine tied to
`data_type`, enforced server-side even if a client sends a contradicting value).

**Business rules**: BR-09 (built-ins: enable/disable + data-type-locked, never deleted/renamed;
customs: disabled, never hard-deleted), BR-10 (disabling a referenced parameter requires the
impact-warning flow — the reference scan itself, `ParameterDisableImpactScanner`, is a read-time
query against `ChannelParameterAssignment` + external M-10/M-14/M-15/M-16 reference reporting,
not a stored flag), BR-11 (API field lock), BR-23 (23 built-ins seeded enabled).

**Indexes**: unique index on `api_field` (tenant-scoped, includes disabled per VR-F06); index on
`(origin, data_type)` for SCR-05's origin-tab + type-filter combination.

---

## 5. `ChannelParameterAssignment`

The channel contract row (BR-08 — authoritative on requiredness at request time).

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `service_channel_id` | UUID | FK → `ServiceChannel.id`, part of composite PK | |
| `parameter_id` | UUID | FK → `Parameter.id`, part of composite PK | |
| `supported` | boolean | default `false` | |
| `required` | boolean | default `false`; **may only be `true` when `supported = true`** (FR-S4-04) — enforced server-side: setting `supported = false` force-clears `required` in the same write | applied as the assignment default (`Parameter.required_by_default`) when a parameter is first assigned via SCR-06's channel-pills (FR-S6-05), then independently editable per-channel thereafter |

**Composite PK**: `(service_channel_id, parameter_id)` — intra-module composite key, permitted
since neither half is a tenant identifier (DB-03 only forbids tenant-identifier composites).

---

## 6. `ParameterMapping`

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `parameter_id` | UUID | FK → `Parameter.id`, required | must be a mapping-enabled parameter (BR-27) at write time — not a stored constraint, validated at the service layer |
| `source_value` | text | required, **unique within the parameter, case-insensitively** (VR-F08, Clarifications 2026-07-27) | the raw backend value, e.g. `S001` |
| `display_en` | text | required | |
| `display_ar` | text | required | RTL |
| `status` | text | `draft` \| `active` (Status Lifecycle) | `draft` exists only client-side for the inline add-row UX; a `POST` always creates `active` rows — **no `draft` rows are ever persisted** (draft is a pre-save client state, not a database state) |
| `created_at` / `updated_at` | timestamptz | UTC | |

**Validation**: VR-F08 (case-insensitive uniqueness within the parameter); **VR-F13**-adjacent
guardrail: ≤ 5,000 mappings per parameter (NFR-16, enforced by `MappingsPerParameterGuard` on
both inline-add and bulk-import).

**Business rules**: BR-13 (bilingual, **read-time resolution** — F0.5: display values are looked
up in the *current* mapping table whenever data is read, so an edit or delete retroactively
relabels historical data; no version history; Replace-all is irreversible), BR-12 (this table is
the single source of List values; membership is never validated at ingestion — an unmapped
value is accepted and queued, never rejected).

**Indexes**: unique index on `(parameter_id, lower(source_value))`.

---

## 7. `UnmappedValueOccurrence`

Backs the SCR-07 "7-day unmapped values" queue (FR-S7-02). A lightweight tracking table rather
than computing the queue live from `IntegrationRequestLog` on every page load (that table is
high-volume and partitioned; this table is small and purpose-built for the queue).

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `parameter_id` | UUID | FK → `Parameter.id`, required | |
| `raw_value` | text | required (case-preserved, matched case-insensitively against `ParameterMapping.source_value` to decide "is this now mapped") | |
| `first_seen_at` | timestamptz | UTC | drives the 7-day window |
| `last_seen_at` | timestamptz | UTC | updated on repeat occurrence |
| `occurrence_count` | integer | default 1, incremented on repeat | informational only |

**Lifecycle**: a row is created (or its `last_seen_at`/`occurrence_count` updated) whenever an
inbound request carries a mapping-enabled parameter's value with no matching `ParameterMapping`
row. A row is deleted once a `ParameterMapping` is created for that `(parameter_id,
lower(raw_value))` pair (FR-S7-02's "removes a value from the queue once mapped"). Rows older
than 7 days (by `first_seen_at`, with no repeat occurrence resetting the window) are excluded
from the queue view but not necessarily hard-deleted immediately (a background sweep or a
read-time date filter both satisfy the spec; implementation detail for `/speckit-tasks`).

**Indexes**: index on `(parameter_id, first_seen_at)`.

---

## 8. `IntegrationRequestLog`

Immutable, append-only, **DB-04 monthly-partitioned** (high-volume, joining `responses`/
`delivery_log`/`audit_log`/`notification_log`/`event_log` on that list — coordination-log.md
C-03 tracks the constitution amendment adding it).

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `integration_id` | UUID, nullable | FK → `Integration.id`; **nullable** because an auth-rejected request may fail before the integration/credential is even resolved | |
| `timestamp` | timestamptz | UTC, required | |
| `method` | text | required | |
| `path` | text | required (illustrative per FR-F0-01, but logged as actually received) | |
| `scenario` | text, nullable | one of the 5 SCN values; null if rejected before scenario resolution | |
| `parameters_received` | jsonb | required | **all** parameters (registered + unregistered), stored raw; PII fields (mobile, email, customer name) masked **only at display/export time**, never at write time (the raw value must be usable for reprocessing/audit; masking is a read-side concern, FR-S8-03) |
| `response_returned` | jsonb | required | full response body (status, result code, `request_id`, message, or the scenario's artifact — link/JSON/embed URL) |
| `http_status` | integer | required | |
| `result_code` | text | required | `E-1001`…`E-1500`, `202`, `200` |
| `latency_ms` | integer | required | |
| `credential_label` | text, nullable | the credential's `label_or_client_name` at the time of the request (denormalized — a later credential revocation/regeneration must not change historical log rows) | |
| `rejection_stage` | text, nullable | e.g. `"authentication"` — populated only when the request was rejected before parameter parsing (AC-S8-03) | drives the "— request rejected before parameter parsing" detail notice |

**Retention**: 90 days (NFR-8) — partition-drop based, per DB-04's "retention enforced by
detaching old partitions" convention, not a row-level `DELETE`.

**Indexes**: index on `(integration_id, timestamp)`; index on `(timestamp)` for the global
newest-first default order; a status-class-derivable index (`http_status`) for the SCR-08 chip
filters — computed range queries (`http_status BETWEEN 200 AND 299`, etc.), not a stored
`status_class` column, per DB Article 4.6 ("columns by default, `jsonb` where justified" — the
status class here is cheaply derivable, not worth a redundant column).

---

## 9. Entity relationship summary

```
ServiceChannel (1) ──< (many) ChannelParameterAssignment >── (many) Parameter (1)
       │                                                              │
       │                                                              └──< (many) ParameterMapping
       │                                                              └──< (many) UnmappedValueOccurrence
       │
       └──< (many) Integration ──(1)── Credential (current + revoked history)
                       │
                       └──< (many) IntegrationRequestLog (nullable FK — auth-rejected rows may have none)

Parameter.api_field — the only field M-13 pushes cross-module, to M-10's REAL
  POST /api/v1/authorization/scope/parameters (research.md §4.1) — no FK, identifier-only (Article 4.1)
```

## 10. State transitions

### Integration (direct toggle, no date computation — unlike M-15's Actions)

```
Active ⇄ Inactive (either direction, audited, no confirmation dialog specified)
Inactive → endpoint rejects calls (401 E-1401, credentials-suspended framing, Status Lifecycle)
No delete transition exists, ever.
```

### Credential

```
Active → Revoked (one-way; a new credential's generation atomically revokes the prior Active one)
No un-revoke; plaintext is never retrievable after the show-once dialog closes.
```

### ServiceChannel

```
Active ⇄ Inactive (Inactive → E-1004 on request)
Editable ─(first 2xx request)──► Locked (one-way; independent axis from Active/Inactive)
No delete transition exists once traffic has occurred (i.e., once Locked).
```

### Parameter

```
Enabled ⇄ Disabled (Disabled guarded by the BR-10 impact-warning flow — a confirmation, not a hard block)
Renameable-API-field ─(first request using it)──► Locked (one-way; built-ins start Locked)
No hard-delete transition exists, ever, for either origin.
```

### ParameterMapping

```
(client-side Draft, never persisted) ──Save──► Active (a persisted row)
Active ──Delete (confirmed)──► removed (immediate read-time effect; no restore)
```

### Result-code selection (FR-F0-02/F0-03, the request-validation pipeline's outcome)

```
TLS fail            → (connection refused, no application-level code)
Auth fail            → 401 E-1401
Rate limit exceeded  → 429 E-1429
Payload > 2MB        → 413 E-1413
Unknown channel      → 404 E-1001
Inactive channel     → 409 E-1004
Missing required param → 400 E-1002
Type/rule validation fail → 422 E-1003
All pass             → scenario-specific 202/200 (Feature 0's FR-F0-01 per-scenario artifact)
Any unexpected internal failure → 500 E-1500 (retry-idempotent message)
```
