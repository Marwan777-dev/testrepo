# API Contracts: M-15 Action Management

**Status**: proposed technical design, subject to architecture review — the source SRS (§17)
declares API shapes explicitly out of scope, and the spec's rev-1.1 governance note reiterates
that every path/verb/header/error-code sketched in its test-coverage blocks is non-binding. The
*behaviours* below are normative (traced to FR-IDs); the exact shapes are this plan's proposal,
corrected against binding constitution articles that postdate the SRS (research.md §5):
`/api/v1/` prefix (API-01), cursor pagination (API-04), the full `{code, message,
correlation_id, tenant_id}` error envelope (API-05), and RBAC declarations (API-03).

All endpoints require `Authorization: Bearer <JWT>` (API-06); tenant resolved from the JWT
claim (API-02). All error responses use the standard envelope; only per-endpoint `code` values
are listed below.

---

## Actions collection

### `GET /api/v1/actions`

- **required_permission**: `actions.read` · **required_scope**: `organisation` ·
  **default_personas**: P-01, P-02, P-06 (all three interim roles can view, PERM-01)
- **Query params**: `tab` (`active|planned|completed|archived`, optional — omitted returns all
  four grouped), `q` (free-text, Action Name substring, cross-tab per FR-106), `kpi` (repeatable,
  KPI id multi-select, FR-107), `start_from` / `start_to` (date, `action_start_date` range,
  FR-107), `page_size` (default 50, max 200), `page_token` (cursor, API-04).
- **200 response**:
  ```json
  {
    "items": [ {
      "id": "uuid", "action_name": "string", "status": "active|planned|completed|archived",
      "archived": false, "action_start_date": "date", "action_end_date": "date",
      "target_start_date": "date", "latest_target_date": "date",
      "targets": [ {
        "id": "uuid", "kpi_id": "uuid", "kpi_name": "string", "target_date": "date",
        "lower_threshold": 3.0, "upper_threshold": 6.0, "active": true,
        "deactivation_source": null, "baseline_score": 70.0, "current_score": 74.5,
        "final_score": null, "outcome": null
      } ]
    } ],
    "next_page_token": "string|null", "total_count": 42,
    "cross_tab_match_count": 7
  }
  ```
  Raw `baseline_score`/`current_score`/dates are returned so the client computes
  `score_progress`/`time_progress`/`timer_state`/lowest-performing selection (NFR-5: server is
  authoritative for evaluation-time facts only; live pace values are client-computed).
  `cross_tab_match_count` is present only when `q` is set (FR-106 hint line).
- **Behaviours covered**: FR-101..FR-111, FR-M15 (lowest-performing raw inputs).

### `POST /api/v1/actions`

- **required_permission**: `actions.write` · **required_scope**: `organisation` ·
  **default_personas**: P-01 only (PERM-01 — create is Program-Manager-only)
- **Request**: `{ action_name, description?, action_start_date, action_end_date, targets: [ { kpi_id, target_date, lower_threshold, upper_threshold } ] }` (≥ 1 target, VAL-207)
- **201** → full Action shape (as above) + `Location: /api/v1/actions/{id}`. Writes
  `action.created` + one `baseline.captured` per Target with a capturable baseline (US1).
- **400** `validation.*` (VAL-201..211, first-failing-rule → the exact spec message, e.g.
  `validation.action_name_required` → "Action Name is required").
- **409** `validation.duplicate_action_name` (VAL-202, case-insensitive, incl. Archived).
- **409** `kpi.no_historical_score` (ERR-5 — retro-dated Start Date, M-06 has no historical score
  for that date; **today this always fires** because M-06 has no historical-score capability yet,
  research.md §4 C-01 — surfaced as a blocking dialog client-side, never a silent fallback).
- **403** (ERR-3, non-Program-Manager).
- **Behaviours covered**: US1, US10 (retro-dating), FR-201..FR-210, FR-M01..M07.

### `GET /api/v1/actions/{id}`

- **required_permission**: `actions.read` · **required_scope**: `organisation` ·
  **default_personas**: P-01, P-02, P-06
- **200** → full Action + Targets, each Target carrying `score_progress_raw`/`time_progress_raw`
  computation inputs (baseline, thresholds, dates, current score) plus server-authoritative
  `final_score`/`outcome` once evaluated (FR-304..FR-308).
- **404** `action.not_found` (ERR-6 — missing or foreign-tenant id; same code whether the id
  never existed or belongs to another tenant, API-04.6 indistinguishable-absence rule).
- **Behaviours covered**: US3, FR-301..FR-309.

### `PUT /api/v1/actions/{id}`

- **required_permission**: `actions.write` · **required_scope**: `organisation` ·
  **default_personas**: P-01 only
- **Request**: full Action + Targets payload (edit mode = same shape as create, FR-201).
- **200** → updated Action. Guarded fields trigger server-side recomputation (not gated on the
  client having shown DLG-2/3/4 — those dialogs are UI-only confirmation, per the spec's own
  proposed-design note): `action_start_date` change on a started Action → baseline recapture +
  `field_edited(start_date)` + `baseline.recaptured`; `action_end_date` change → `target_start_date`
  moves, all Time Progress recomputes + `field_edited(end_date)`; threshold change mid-monitoring
  → outcome/progress recompute for that Target + `field_edited(upper_threshold|lower_threshold)`.
- **409** `action.read_only` (ERR-11 — Completed, BR-023) · `action.archived` (ERR-11 —
  Archived, must unarchive first) · `kpi.no_historical_score` (ERR-5, Start Date recapture).
- **403** (ERR-3, non-Program-Manager or non-owner-scope).
- **Response header** `X-Nabadat-Stale-Save: true` when the write is accepted despite a stale
  `updated_at` (ERR-8, last-write-wins per spec R-4 — see plan.md Complexity Tracking for the
  documented Article-7.2 exception this represents).
- **Behaviours covered**: US4, FR-209, FR-M06.

### `POST /api/v1/actions/{id}/archive`

- **required_permission**: `actions.write` · **required_scope**: `organisation` ·
  **default_personas**: P-01 only
- **200** → Action with `status: "archived"`. No request body (BR-009 — no confirmation, always
  available from any non-archived status). Writes `action.archived`.
- **409** `action.already_archived` (idempotency guard — re-archiving is refused, not a no-op).
- **Behaviours covered**: US6, FR-309, FR-L04.

### `POST /api/v1/actions/{id}/unarchive`

- **required_permission**: `actions.write` · **required_scope**: `organisation` ·
  **default_personas**: P-01 only
- **200** → Action with the recomputed date-driven status (may be `completed` if the latest
  Target Date passed while Archived, FR-L04). Writes `action.unarchived`.
- **Behaviours covered**: US6, FR-309, FR-L04.

## KPI Target sub-resource

### `PATCH /api/v1/actions/{id}/targets/{targetId}`

- **required_permission**: `actions.write` · **required_scope**: `organisation` ·
  **default_personas**: P-01 only
- **Request**: `{ "active": true|false }`
- **200** → updated Target; sets `deactivation_source = "manual"` on deactivate, clears it on
  reactivate. Writes `target.deactivated` / `target.activated`.
- **409** `target.kpi_inactive` — reactivation attempted on a `deactivation_source = "forced"`
  Target while the KPI remains inactive in M-06 (BR-011).
- **Behaviours covered**: US7, FR-207.

### `DELETE /api/v1/actions/{id}/targets/{targetId}`

- **required_permission**: `actions.write` · **required_scope**: `organisation` ·
  **default_personas**: P-01 only
- **200** → the Action's remaining Targets (client renumbers display order). Writes
  `target.deleted`.
- **409** `target.must_be_deactivated` (BR-012 — active Targets cannot be deleted) ·
  `action.requires_target` (R-17 — refused when it is the last remaining Target in any state).
- **Behaviours covered**: US7, BR-012, R-17.

## Settings

### `GET /api/v1/settings/actions`

- **required_permission**: `settings.read` · **required_scope**: `organisation` ·
  **default_personas**: P-01 (others per PERM-01 — read visibility TBD by M-10 refinement,
  fields hidden/read-only for non-Program-Manager per Story 9 AC "Permissions")
- **200** → `{ "max_upper_threshold": 20.0, "slider_padding": 3 }` (defaults for a fresh tenant).

### `PUT /api/v1/settings/actions`

- **required_permission**: `settings.write` · **required_scope**: `organisation` ·
  **default_personas**: P-01 only
- **Request**: `{ "max_upper_threshold"?: number, "slider_padding"?: integer }`
- **200** → updated settings. Writes `settings.X_changed` / `settings.PAD_changed` (old/new).
- **400** `settings.x_below_saved_upper` (SET-1 guard, message carries the largest saved U) ·
  `settings.pad_out_of_range` (SET-2, non-integer or `< 1`).
- **403** (ERR-3, non-Program-Manager).
- **Behaviours covered**: US9, SET-1..3.

---

## Internal / cross-module (not part of the public API surface)

No `/api/internal/kpi-deactivation-events` webhook is implemented (research.md §4 — the lazy
`event_log` consumer replaces it). If a future architecture review reinstates a push-based
webhook (coordination-log.md C-02), it would be documented here as a new contract at that time,
authenticated as a service-to-service call per APIs-constitution Article 3.6, never as a public
endpoint.
