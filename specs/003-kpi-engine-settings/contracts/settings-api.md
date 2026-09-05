# API Contracts: Platform Settings (Organization + Customer Journey)

**Feature**: 003-kpi-engine-settings | **Modules**: M-11 (Organization), M-16 (ScoringConfig surfaced via M-06) | **Date**: 2026-06-21

All endpoints versioned at `/api/v1/` per API-01. All error responses follow the API-05 envelope. All endpoints are tenant-scoped (resolved from the JWT `tenant_id` claim per API-02 / AD-07). Permission attributes per API-03.

---

## Organization Section

### `GET /api/v1/tenant/organization`

Read the current Organization settings.

**Permission**: `organization:read` | **Scope**: `tenant` | **Personas**: P-01, P-07.

**200 Response:**

```json
{
  "name": "Acme Bank",
  "logo": {
    "url": "https://storage.example.com/tenants/{tenantId}/branding/logo.png",
    "content_type": "image/png",
    "size_bytes": 524288
  },
  "industry": "Banking",
  "industry_options": [
    "Banking", "Telecommunications", "Government",
    "Automotive", "Entertainment", "Services"
  ],
  "audit": {"updated_at": "...", "updated_by": "uuid"}
}
```

`logo` is `null` when no logo has been uploaded. `industry_options` is the canonical list (sourced from `M-11.IIndustryEnumProvider.GetAll()` — R13).

**Errors**: 403 `PERMISSION_DENIED`.

---

### `PUT /api/v1/tenant/organization`

Update the tenant's display name and industry. Logo is uploaded separately (see `POST /tenant/organization/logo`).

**Permission**: `organization:update` | **Scope**: `tenant` | **Personas**: P-01, P-07 (per tenant RBAC — both may edit, per FR-052).

**Request body:**

```json
{ "name": "Acme Bank International", "industry": "Banking" }
```

**200 Response:** the full Organization payload (as for GET).

**Errors:**

- 400 `ORGANIZATION_NAME_REQUIRED` — `name` is null, empty, or whitespace.
- 400 `ORGANIZATION_NAME_TOO_LONG` — `name` exceeds 150 characters.
- 400 `ORGANIZATION_INDUSTRY_UNKNOWN` — `industry` is not in the canonical list.
- 403 `PERMISSION_DENIED`.

A no-op update (the payload matches current state) writes nothing and emits no event (per `data-model.md` §8).

---

### `POST /api/v1/tenant/organization/logo`

Upload (or replace) the tenant's logo.

**Permission**: `organization:logo:update` | **Scope**: `tenant` | **Personas**: P-01, P-07.

**Request**: `multipart/form-data` with a single file field `logo`. Accepted content types: `image/png`, `image/jpeg`, `image/svg+xml`. Recommended size ≤ 2 MB (soft limit; warning only).

**Server behaviour:**

1. Validate content type via `LogoUploadValidator`.
2. For `image/svg+xml`: run the bytes through `SvgSanitiser` (R1). The PERSISTED bytes are the SANITISED output, never the upload bytes.
3. Call `M-11.ILogoStore.PutAsync(tenantId, contentType, sanitisedPayload)` (R3) — returns a `LogoBlobRef`.
4. Update `organization_settings.logo_blob_ref` in the same transaction as an M-17 `settings.changed` event with `action='logo_replaced'` and diff carrying `from_blob_ref` and `to_blob_ref`.

**200 Response:**

```json
{
  "url": "https://storage.example.com/tenants/{tenantId}/branding/logo.svg",
  "content_type": "image/svg+xml",
  "size_bytes": 12345,
  "was_sanitised": true
}
```

`was_sanitised` is `true` if (a) content type was `image/svg+xml` AND (b) the sanitiser stripped at least one node or attribute. The frontend uses this flag to surface the "Your SVG was sanitised — disallowed content was removed before saving." notice (spec Edge Cases).

**Errors:**

- 400 `LOGO_CONTENT_TYPE_UNSUPPORTED` — content type is not PNG/JPG/SVG.
- 400 `LOGO_SIZE_ZERO` — payload is 0 bytes.
- 400 `LOGO_SVG_UNSAFE_CONTENT` — sanitiser cannot make the SVG payload safe (non-parseable, or content that cannot be stripped without breaking the file).
- 413 `LOGO_TOO_LARGE` — payload exceeds 10 MB hard cap (the soft 2 MB limit emits a non-blocking warning in the frontend; the hard cap is 10 MB to prevent denial-of-storage attacks).
- 403 `PERMISSION_DENIED`.

---

## Customer Journey Section — ScoringConfig

### `GET /api/v1/tenant/scoring-config`

Read the tenant's ScoringConfig (β is derived).

**Permission**: `scoring_config:read` | **Scope**: `tenant` | **Personas**: P-01, P-07.

**200 Response:**

```json
{
  "alpha": 0.500,
  "beta": 0.500,
  "mot_multiplier": 1.5,
  "n_floor": 100,
  "flag_percentile": 25,
  "rolling_window_days": 30,
  "audit": {"updated_at": "...", "updated_by": "uuid"}
}
```

`beta` is derived (`1.000 - alpha`) per R6; never persisted.

**Errors**: 403 `PERMISSION_DENIED`.

---

### `PUT /api/v1/tenant/scoring-config`

Update the tenant's ScoringConfig. Only P-01 can write (FR-062); P-07 has read-only access.

**Permission**: `scoring_config:update` | **Scope**: `tenant` | **Personas**: P-01.

**Request body:**

```json
{
  "alpha": 0.7,
  "mot_multiplier": 1.5,
  "n_floor": 100,
  "flag_percentile": 25,
  "rolling_window_days": 30
}
```

β is NOT in the payload — caller cannot send it.

**Server behaviour:** delegates to `M-16.IScoringConfigStore.UpdateAsync(tenantId, payload)`. The store persists the row, emits one `journey.scoring_config.updated` event with the per-field diff (or zero events on a no-op save), and returns the updated DTO.

**200 Response**: the full ScoringConfig payload (as for GET).

**Errors:**

- 400 `INVALID_ALPHA_BETA_SUM` — `alpha` is out of `[0.000, 1.000]`. (Error code retains the name from the M-16 SRS for cross-module consistency.)
- 400 `MOT_MULTIPLIER_OUT_OF_RANGE` — `mot_multiplier` is out of `[1.0, 2.0]`.
- 400 `N_FLOOR_BELOW_MINIMUM` — `n_floor < 1`.
- 400 `FLAG_PERCENTILE_OUT_OF_RANGE` — `flag_percentile` not in `[1, 49]`.
- 400 `ROLLING_WINDOW_BELOW_MINIMUM` — `rolling_window_days < 7`.
- 403 `PERMISSION_DENIED` — P-07 attempts to write; or any other persona.

---

## Permission Matrix

| Action | P-01 | P-07 | Others |
|--------|:----:|:----:|:------:|
| `GET /tenant/organization` | ✓ | ✓ | — |
| `PUT /tenant/organization` | ✓ | ✓ | — |
| `POST /tenant/organization/logo` | ✓ | ✓ | — |
| `GET /tenant/scoring-config` | ✓ | ✓ (read-only) | — |
| `PUT /tenant/scoring-config` | ✓ | — | — |

The Settings landing page (`/settings`) is visible to any persona that holds read on at least one section; the section pages enforce their own read permissions on entry.

---

## Settings Landing Endpoint (Information Only)

The Settings landing page does NOT have its own endpoint. Section navigation entries are computed client-side from the per-section permissions returned in the user's `/me` payload (M-10).

---

## Wire Format Reminders

Same as `kpi-api.md` — enum integer-on-wire serialisation, 2xx-empty-body handling, Bearer auth, Vite dev proxy `https://localhost:7002` with `secure: false`.
