# API Contracts: KPI Catalogue & Configuration

**Feature**: 003-kpi-engine-settings | **Module**: M-06 | **Date**: 2026-06-21

All endpoints versioned at `/api/v1/` per API-01. All error responses follow the API-05 envelope. All endpoints are tenant-scoped (resolved from the JWT `tenant_id` claim per API-02 / AD-07). Permission attributes per API-03.

---

## Common: Error Envelope (API-05)

```json
{
  "error": {
    "code": "string",
    "message": "string (bilingual EN+AR per tenant locale)",
    "correlation_id": "uuid",
    "tenant_id": "uuid"
  }
}
```

Status codes:

- **400** — validation error (specific `code` enumerated below).
- **403** — `PERMISSION_DENIED` (caller's persona lacks `required_permission`).
- **404** — `KPI_NOT_FOUND` / `CXI_NOT_FOUND` (also returned for cross-tenant probes per GP-04).
- **409** — workflow conflict (e.g., `KPI_DEACTIVATION_REQUIRES_CONFIRMATION`, `KPI_SCALE_CHANGE_AFFECTS_BINDINGS`).
- **5xx** — internal errors.

---

## `GET /api/v1/kpis`

List the catalogue. Cursor-paginated (API-04).

**Permission**: `kpis:read` | **Scope**: `tenant` | **Personas**: P-01, P-02, P-06.

**Query params:**

| Name | Type | Default | Notes |
|------|------|---------|-------|
| `type` | enum `All` / `Standard` / `Custom` | `All` | Filter by KPI type. |
| `active_only` | bool | `true` | When true, omits inactive rows. |
| `search` | string | _empty_ | Case-insensitive substring against `short_name ∪ full_name` after trimming. |
| `cursor` | opaque base64 | _empty_ | Encodes `(created_at, id)` tuple per R8. |
| `limit` | int | 50 | Max 200. |

**200 Response:**

```json
{
  "items": [
    {
      "id": "uuid",
      "short_name": "NPS",
      "full_name": "Net Promoter Score",
      "kpi_type": "Standard",
      "is_composite": false,
      "scale": "Scale0_10",
      "calculation_method": "NPSStandard",
      "calculation_method_label": "NPS Standard",
      "scale_label": "0–10",
      "target": 50,
      "is_active": true,
      "show_on_dashboard": false,
      "created_at": "2026-06-21T00:00:00Z"
    }
    // …
  ],
  "next_cursor": null
}
```

**Ordering**: standards in canonical order (NPS, CSAT, CES, CXI, FCR, VFM, AgentScore, CHS), then custom KPIs by `created_at` DESC (per R7).

**Errors**: 403 `PERMISSION_DENIED`.

---

## `GET /api/v1/kpis/{id}`

Read a single KPI's full configuration. `{id}` accepts either the KPI's GUID id or its
(case-insensitive) **Short Name**, so the configuration page can load from a human-readable URL
(e.g. `/kpi-management/cxi`). 404 `KPI_NOT_FOUND` when neither resolves.

**Permission**: `kpis:read` | **Scope**: `tenant` | **Personas**: P-01, P-02 (read-only UI), P-06.

**200 Response:**

```json
{
  "id": "uuid",
  "short_name": "QUAL",
  "full_name": "Service Quality",
  "kpi_type": "Custom",
  "is_composite": false,
  "calculation_method": "WeightedAverage",
  "top_n_value": null,
  "scale": "Scale1_7",
  "min_scale_description": {"en": "Very poor", "ar": "ضعيف جدًا"},
  "max_scale_description": {"en": "Excellent", "ar": "ممتاز"},
  "representation_style": "Number",
  "emoji_set": null,
  "target": 80,
  "is_active": true,
  "show_on_dashboard": false,
  "thresholds": {"lower_bound": 0, "x": 20, "y": 70, "upper_bound": 100},
  "perspectives": [
    {"id": "uuid", "label": "Attitude", "display_order": 0},
    {"id": "uuid", "label": "Knowledge", "display_order": 1}
  ],
  "cxi_weights": null,
  "audit": {
    "created_at": "...", "created_by": "uuid",
    "updated_at": "...", "updated_by": "uuid"
  }
}
```

For CXI: `is_composite=true`, `scale=null`, `representation_style=null`, and `cxi_weights` carries an array `[{member_kpi_id, member_short_name, weight, effective_percentage}, ...]`.

**Errors**: 403 `PERMISSION_DENIED`, 404 `KPI_NOT_FOUND`.

---

## `POST /api/v1/kpis`

Create a custom KPI. Custom KPIs cannot have `calculation_method = 'NPSStandard'` or `calculation_method = 'WeightedComposite'` (the second is CXI-only; the first is NPS-only).

**Permission**: `kpis:create` | **Scope**: `tenant` | **Personas**: P-01.

**Request body:**

```json
{
  "short_name": "QUAL",
  "full_name": "Service Quality",
  "perspectives": [{"label": "Attitude", "display_order": 0}],
  "calculation_method": "WeightedAverage",
  "top_n_value": null,
  "scale": "Scale1_7",
  "min_scale_description": {"en": "Very poor", "ar": "ضعيف جدًا"},
  "max_scale_description": {"en": "Excellent", "ar": "ممتاز"},
  "representation_style": "Number",
  "emoji_set": null,
  "thresholds": {"x": 20, "y": 70},
  "target": 80,
  "is_active": true,
  "show_on_dashboard": false
}
```

**201 Response:** the full configuration as for `GET /api/v1/kpis/{id}`.

**Errors:**

- 400 `KPI_SHORT_NAME_DUPLICATE` — Short Name already in use (case-insensitive).
- 400 `KPI_VALIDATION_FAILED` — generic validation envelope with `details` array carrying per-field codes (`short_name.required`, `short_name.too_long`, `full_name.required`, `full_name.too_long`, `calculation_method.required`, `scale.required`, `representation_style.slider_requires_scale_1_3`, `target.required_when_active`, `target.out_of_range`, etc.).
- 400 `KPI_THRESHOLD_NOT_ASCENDING` — `lower < x < y < upper` violated.
- 400 `KPI_TOP_N_OUT_OF_RANGE` — `n` ≥ scale max (FR-014).
- 400 `KPI_CALCULATION_METHOD_RESERVED` — caller attempts to create with `NPSStandard` or `WeightedComposite`.
- 403 `PERMISSION_DENIED`.

---

## `PUT /api/v1/kpis/{id}`

Update an existing KPI.

**Permission**: `kpis:update` | **Scope**: `tenant` | **Personas**: P-01.

**Request body**: same shape as POST. Server enforces immutability rules (FR-004, FR-005).

**Query param**: `confirm_structural_change=true` (optional) — required when a Scale change affects existing M-16 touchpoint bindings (FR-017).

**200 Response**: the full updated configuration.

**Errors:**

- 400 `KPI_SHORT_NAME_IMMUTABLE` — caller attempts to change `short_name`.
- 400 `KPI_FIELD_IMMUTABLE_FOR_STANDARD` — caller attempts to change `scale` or `calculation_method` on NPS.
- 400 `KPI_VALIDATION_FAILED` / `KPI_THRESHOLD_NOT_ASCENDING` / `KPI_TOP_N_OUT_OF_RANGE` — as for POST.
- 409 `KPI_SCALE_CHANGE_AFFECTS_BINDINGS` — Scale changed AND M-16 reports ≥ 1 affected touchpoint AND `confirm_structural_change` is not `true`. Response body carries `affected_touchpoints` and `affected_journeys` counts.
- 403 `PERMISSION_DENIED`, 404 `KPI_NOT_FOUND`.

---

## `PATCH /api/v1/kpis/{id}/activation`

Activate or deactivate a KPI.

**Permission**: `kpis:activate` | **Scope**: `tenant` | **Personas**: P-01.

**Request body:**

```json
{ "active": false, "confirm": false }
```

**Behaviour:**

- `active=true` always succeeds with a 200 (idempotent).
- `active=false` with no current M-16 bindings → 200, KPI deactivated, exactly one event.
- `active=false` with current bindings AND `confirm=false` → 409 `KPI_DEACTIVATION_REQUIRES_CONFIRMATION` with `{ touchpoint_count, journey_count }` in body.
- `active=false` with current bindings AND `confirm=true` → 200, KPI deactivated, `show_on_dashboard` forced false, every `cxi_weights` row where `member_kpi_id = <id>` deleted, exactly ONE `settings.changed` event with the nested `cxi_side_effect` payload (per R5 and Clarifications round 1 Q2).

**Errors:** 403, 404, 409 as above.

---

## `GET /api/v1/kpis/{id}/binding-usage`

Probe M-16 for the binding-usage counts (used by the UI to prefetch the confirmation message before the user toggles Active).

**Permission**: `kpis:read` | **Scope**: `tenant` | **Personas**: P-01.

**200 Response:**

```json
{ "touchpoint_count": 3, "journey_count": 2 }
```

Implementation: M-06's `KpiBindingUsageProbe` calls `M-16.IJourneyBindingQuery.GetKpiBindingUsageAsync(kpiId)` and returns the result verbatim.

**Errors:** 403, 404 `KPI_NOT_FOUND`.

---

## `PUT /api/v1/kpis/{cxi_id}/weights`

Replace CXI's weights table. Only valid when `{cxi_id}` resolves to a KPI with `is_composite=true`.

**Permission**: `kpis:cxi_weights:update` | **Scope**: `tenant` | **Personas**: P-01.

**Request body:**

```json
{
  "weights": [
    {"member_kpi_id": "uuid (NPS)", "weight": 3},
    {"member_kpi_id": "uuid (CSAT)", "weight": 2},
    {"member_kpi_id": "uuid (CES)", "weight": 1}
  ]
}
```

Weights are FULL REPLACE: any member not in the body is removed; any new member is inserted. Zero-weight entries are silently dropped (BR-2.3).

**200 Response:**

```json
{
  "weights": [
    {"member_kpi_id": "uuid", "member_short_name": "NPS", "weight": 3, "effective_percentage": 50.0},
    {"member_kpi_id": "uuid", "member_short_name": "CSAT", "weight": 2, "effective_percentage": 33.3},
    {"member_kpi_id": "uuid", "member_short_name": "CES", "weight": 1, "effective_percentage": 16.7}
  ]
}
```

Effective percentages sum to 100.0 within ±0.1 (per SC-004).

**Errors:**

- 400 `CXI_CANNOT_INCLUDE_ITSELF` — `member_kpi_id == cxi_id`.
- 400 `CXI_MEMBER_NOT_ACTIVE` — a referenced `member_kpi_id` is not an active KPI.
- 400 `CXI_INSUFFICIENT_MEMBERS` — fewer than 2 non-zero weights AND the CXI is `is_active=true` (FR-043). The save is rejected to keep CXI activatable.
- 400 `CXI_WEIGHT_INVALID` — `weight <= 0` after the zero-drop rule (i.e., a non-integer or negative weight was sent).
- 403 `PERMISSION_DENIED`, 404 `CXI_NOT_FOUND`.

---

## Permission Matrix

| Action | P-01 (CX PM) | P-02 (Analyst) | P-06 (Executive) | P-07 (IT Admin) | Others |
|--------|:------------:|:-------------:|:----------------:|:---------------:|:------:|
| `GET /kpis` | ✓ | ✓ | ✓ | — | — |
| `GET /kpis/{id}` | ✓ | ✓ (read-only) | — | — | — |
| `POST /kpis` | ✓ | — | — | — | — |
| `PUT /kpis/{id}` | ✓ | — | — | — | — |
| `PATCH /kpis/{id}/activation` | ✓ | — | — | — | — |
| `GET /kpis/{id}/binding-usage` | ✓ | — | — | — | — |
| `PUT /kpis/{cxi_id}/weights` | ✓ | — | — | — | — |

The UI mirrors the matrix (controls hidden / disabled per FR-009 / FR-065).

---

## Wire Format Reminders (CLAUDE.md "Backend Integration")

- Enum serialization: **integers on the wire by default** (System.Text.Json without `JsonStringEnumConverter`). The frontend `api.ts` MUST normalise via `normalize<Enum>()` helpers at the response boundary AND convert via int-converters at the request boundary. The TS types stay as string unions; the controller-side DTOs use canonical string values.
- 2xx responses with empty bodies: treated as `undefined` by the fetch helper. The cancel-confirm-after-save UX path relies on this.
- `Authorization: Bearer <opaque session token>` per API-06.
- Vite dev proxy: `target: "https://localhost:7002"`, `secure: false` for the self-signed dev cert.
