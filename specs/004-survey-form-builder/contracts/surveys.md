# Contract — Surveys (F1, F3, F5)

**Related**: [plan.md](../plan.md) · [data-model.md](../data-model.md) · [spec.md](../spec.md)

**Base path**: `/api/v1/surveys` — Admin portal surface. JWT auth (`Authorization: Bearer …`), tenant resolved from JWT claim `tenant_id` (API-02). All list responses use cursor pagination (API-04). Error envelope per API-05.

**Legend (permission columns per API-03)**:
- **P**: required M-10 permission.
- **S**: required scope — `organisation` \| `region` \| `branch` \| `own`. Q8 team-owned semantics apply: `own` for P-03 = any P-03-authored survey in the tenant.
- **Personas**: default personas granted the permission.

---

## GET /api/v1/surveys

**Purpose**: F1 Survey Library listing.

- **P**: `survey.read` · **S**: `organisation` · **Personas**: P-01, P-02, P-03, P-06
- **Query params** (all optional):
  - `q` — real-time search over `LOWER(name_en)` (FR-1.2). On the Templates tab also matches tags — see [templates.md](./templates.md).
  - `type` — comma-separated list of `Transactional` \| `SeasonalRelational`.
  - `status` — comma-separated list of `Draft`, `PendingReview`, `Active`, `Paused`, `Archived`.
  - `journey_id` — filter by bound journey (uuid).
  - `sort` — one of `name_en`, `updated_at`, `status` (default `updated_at`).
  - `order` — `asc` \| `desc` (default `desc`).
  - `page_size` — default 50, max 200 (API-04).
  - `page_token` — cursor.
- **Response 200**:
  ```json
  {
    "items": [
      {
        "id": "…", "name_en": "…", "survey_type": "Transactional",
        "bound_journey_id": "…", "status": "Active",
        "rules_count": 3, "theme_mode": "Inherited",
        "updated_at": "2026-07-14T09:00:00Z", "updated_by": "…"
      }
    ],
    "next_page_token": "…",
    "total_count": 128
  }
  ```
- **Response 400** `survey.filter.invalid` if `type` / `status` values not in the allowed set.
- **No ETag** — collection endpoint (research.md § 9).

---

## GET /api/v1/surveys/{id}

**Purpose**: F3 Survey Settings payload (also the row-click deep-link).

- **P**: `survey.read` · **S**: `organisation` · **Personas**: P-01, P-02, P-03, P-06
- **Response 200**: full Survey view — every column from `surveys` plus theme reference, resolved appearance tokens (when `Inherited`, resolved from `ITenantDesignGuidelinesReader`), and the derived `rules_count` and `responses_count` (for the BR-1.6 confirmation payload).
- **Response 404** `survey.not_found` — indistinguishable from out-of-scope (APIs-constitution Article 4.6).
- **`ETag: W/"<row_version>"`** returned; client stores for `If-Match` on the next write.

---

## POST /api/v1/surveys

**Purpose**: Create a Draft survey (F5 Continue out of Survey Settings).

- **P**: `survey.write` · **S**: `own` (P-03 = tenant-team-scoped per Q8) · **Personas**: P-01, P-03
- **Headers**:
  - `Idempotency-Key: <uuid>` — **required** (APIs-constitution Article 7.1).
- **Request body**:
  ```json
  {
    "id": "…optional client uuid…",
    "name_en": "Post-visit satisfaction",
    "description": "…",
    "bound_journey_id": "…|null",
    "welcome_html": "…", "thanks_html": "…",
    "redirect_url": "…", "redirect_after_s": 0,
    "layout": "section", "questions_per_page": null,
    "active_period": null,
    "shuffle": false, "shuffle_mode": "random",
    "routing_on": false,
    "theme_mode": "Inherited"
  }
  ```
- **Response 201**:
  - `Location: /api/v1/surveys/{id}`.
  - `ETag: W/"1"`.
  - Body: full Survey view.
- **Response 400** `survey.name_en.required` / `survey.name_en.max_length`.
- **Response 400** `survey.bound_journey.not_found` when the referenced journey does not exist.
- **Response 400** `survey.html.sanitiser_failed` (rare — indicates the Ganss sanitiser itself failed; input is not persisted).

---

## PUT /api/v1/surveys/{id}

**Purpose**: Update settings (F3).

- **P**: `survey.write` · **S**: `own` (Q8 team-owned) · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<row_version>"` — **required** (Q1). Missing → 400 `survey.etag_required`.
- **Request body**: same shape as POST minus `id`.
- **Response 200**: `ETag: W/"<row_version+1>"` + full Survey view.
- **Response 409** `survey.conflict` when `If-Match` does not match (Q1).
- **Response 409** `survey.edit_locked` when status is `Active` or `PendingReview` (P-03) — BR-1.5 requires Return-to-Draft first for Active; BR-15.1 locks Pending review for the submitter.

---

## POST /api/v1/surveys/{id}/clone

**Purpose**: FR-1.8 clone as `Copy of — <name>` Draft.

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `Idempotency-Key: <uuid>` — required.
- **Request body**: `{}` (all data cloned per Assumption line — including journey binding, theme, translations).
- **Response 201**: same as POST /surveys — the new Draft's Location + Survey view. Response count starts at zero.

---

## POST /api/v1/surveys/{id}/status

**Purpose**: All status transitions (F1 Change status + US2 Publish/Return-to-draft/Submit). See [approval-workflow.md](./approval-workflow.md) for the Submit / Publish / Return-to-draft variants; this endpoint covers the *self-serve* transitions (Pause, Reactivate, Archive, Unarchive, plus the destructive Return-to-Draft-to-edit).

- **P**: `survey.status.change` · **S**: `organisation` · **Personas**: P-01 (only P-01 changes status per Permissions & Roles table).
- **Headers**:
  - `If-Match: W/"<row_version>"` — required.
  - `Idempotency-Key: <uuid>` — required when the transition is **destructive** (Active/Paused → Draft) OR when Pausing an Active survey with `rules_count > 0`.
- **Request body**:
  ```json
  {
    "to": "Draft|Active|Paused|Archived",
    "reason": "optional short reason for the audit log",
    "confirm": false
  }
  ```
- **Response 200**: updated Survey view + new ETag.
- **Response 409** `survey.status.invalid_transition` when the transition is not in the Status Transition Matrix (BR-1.4).
- **Response 409** `survey.archived.only_unarchive_allowed` when caller sends `to != "Draft"` on an Archived survey.
- **Response 409** `survey.pause.requires_rules_confirmation` when `to = "Paused"` on an Active survey with `rules_count > 0` AND `confirm = false`. The response body includes `details: { rules_count: N }` so the UI can render the exact count.
- **Response 409** `survey.publish.requires_content` when the transition targets `Active` and BR-1.7 fails. Body includes `details: { missing_sections: true|false, missing_questions: true|false }`.
- **Response 409** `survey.return_to_draft.destructive_confirmation_required` when `to = "Draft"` from `Active` or `Paused` AND `confirm = false`. The response body includes `details: { responses_count: N }` so the UI can render the exact count.
- **Response 501** `survey.return_to_draft.purge_service_unavailable` (temporary — only until M-04 ships `IResponsePurgeService`; see [research.md § 4.5](../research.md#45-iresponsepurgeservice-m-04-new-port)).
- **Response 503** `survey.return_to_draft.purge_failed` when the M-04 purge call fails after the M-01 status change; M-01 compensates (reverts to prior status).

**Status Transition Matrix** (canonical — reproduced from spec.md):

| Current | to | Permitted | Notes |
|---|---|---|---|
| Draft | Active | P-01; P-03 with `PublishOwnSurveys` grant on own draft | Publish gate (BR-1.7). |
| Draft | PendingReview | P-03 (own), P-01 | Via `/submit` endpoint — see [approval-workflow.md](./approval-workflow.md). |
| PendingReview | Active | P-01; P-03 with grant on own draft | Publish gate. |
| PendingReview | Draft | P-01 | Non-destructive (FR-15.4 — no responses collected). Via `/return-to-draft`. |
| Active | Draft | P-01 | **Destructive** (BR-1.6) — requires `?confirm=true` + Idempotency-Key. |
| Active | Paused | P-01 | If `rules_count > 0` requires `?confirm=true`. |
| Paused | Active | P-01 | Publish gate SKIPPED (Q9). |
| Paused | Draft | P-01 | **Destructive** (BR-1.6). |
| Draft \| Active \| Paused | Archived | P-01 | Terminal. |
| Archived | Draft | P-01 | Unarchive — non-destructive. |

**PendingReview NEVER appears in the builder status dropdown** (FR-8.12) — it is entered only via `/submit`. The API rejects `POST /status {to:"PendingReview"}` with 409 `survey.status.invalid_transition`.

---

## GET /api/v1/surveys/{id}/render-plan

**Purpose**: Server-side low-response selection endpoint (FR-10.4) — the seam consumed by M-02 / M-04 at dispatch time. Also directly callable by admins for diagnostics.

- **P**: `survey.render_plan.read` · **S**: `organisation` · **Personas**: P-01 (admins), plus service-to-service identity via `X-API-Key` for M-02's dispatcher. (Article 3.6 — scoped service-to-service identity.)
- **Query params**:
  - `respondent_id` — required (opaque tenant-side identifier used to seed the Random selector deterministically per respondent).
- **Response 200**:
  ```json
  {
    "survey_id": "…",
    "layout": "section",
    "sections_order": [
      { "section_id": "…",
        "items": [
          { "kind": "question", "question_id": "…" },
          { "kind": "set", "set_id": "…", "questions": ["…", "…", "…"] }
        ]
      }
    ],
    "routing_map": { "<question_id>": { "<answer_key>": "<target_question_id>|__end" } }
  }
  ```
- **Response 404** `survey.not_found` OR `survey.not_active` — the same 404 whether the survey does not exist or is not Active (indistinguishable-absence per APIs-constitution Article 4.6).
- Performance: 50 ms p95 for surveys ≤ 100 questions / 20 sections / 20 sets.

---

## DELETE /api/v1/surveys/{id}

**Purpose**: Not exposed. Surveys are archived, not hard-deleted (Article 4.4). Erasure requests are handled by M-04 via `IResponsePurgeService` (GP-03).

- **Response 405** `method_not_allowed`.
