# Contract — Questions (F8, F9)

**Related**: [surveys.md](./surveys.md) · [sections-and-sets.md](./sections-and-sets.md) · [data-model.md § 2.4](../data-model.md#24-questions) · [research.md § 5](../research.md#5-question-type-catalogue--ef-mapping-strategy)

**Base path**: `/api/v1/surveys/{id}/questions`

Question payloads use a discriminated `type_payload` JSON — see the per-type shapes below. All writes require `If-Match` on the question's `row_version`; the parent survey's `row_version` is bumped alongside so a full-survey re-GET can detect any child mutation.

---

## POST /api/v1/surveys/{id}/questions

**Purpose**: Add a question to a section or Questions Set.

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<survey.row_version>"`.
- **Request body**:
  ```json
  {
    "id": "…optional",
    "section_id": "…",
    "set_id": "…|null",
    "type": "Scale|InputField|SingleSelect|MultiSelect|YesNo|Matrix|Ranking|KPI",
    "subtype": "…required per type…",
    "text": "How satisfied are you?",
    "description": null,
    "required": false,
    "comments": false,
    "comment_label": "Comments",
    "comment_max_length": 200,
    "sentiment": false,
    "order": 4,
    "type_payload": { "$type": "…discriminator…", "…": "…" },
    "kpi_binding": { "kpi_code": "CSAT", "perspective": null, "bound_journey_on": true, "stage_id": "…", "touchpoint_id": "…|null" }
  }
  ```
- **Response 201**: `Location: …/questions/{questionId}` + `ETag: W/"1"`.
- **Response 400** — one of:
  - `question.type.invalid` — type not in catalogue.
  - `question.subtype.required` — FR-8.8.
  - `question.subtype.incompatible` — subtype not valid for the parent type.
  - `question.text.required`.
  - `scale.slider.steps.min` — slider steps < 1.
  - `scale.points.range` — non-slider point count out of [2..10].
  - `singleselect.options.min` — < 2 options.
  - `kpi.not_found` — kpi_code not in the M-06 catalogue.
  - `kpi.binding_invalid` — `IJourneyReader.IsBindingValidAsync(...)` returned false.
  - `kpi.touchpoint.requires_stage` — touchpoint present without stage.
- **Response 200 + warning header** `X-Warning: kpi.binding_ignored_when_bound_journey_off` — BR-8.2 (Warn+Strip); the stripped bindings are cleared in the persisted row.
- **Response 200 + warning header** `X-Warning: sentiment.ignored_for_non_text` — FR-8.11 (Warn+Strip).

**Type payload shapes**:

- **Scale** (`$type = "scale"`):
  ```json
  { "$type": "scale", "sub_display": "Labels|Stars|Smileys|Slider",
    "point_count": 5, "point_labels": ["Poor", "Fair", "Good", "Very good", "Excellent"],
    "slider_lower": null, "slider_higher": null, "slider_steps": null }
  ```
  - Slider mode: `point_count/point_labels null`; `slider_lower/higher/steps` set.
  - Non-slider mode: `slider_*` null; `point_count 2..10`; `point_labels` length = `point_count`.

- **InputField** (`$type = "input_field"`):
  ```json
  { "$type": "input_field", "input_kind": "Text|Paragraph|Number|Date|Time|DateTime|Month",
    "placeholder": null, "regex_pattern": null, "min_length": null, "max_length": null }
  ```

- **SingleSelect** (`$type = "single_select"`):
  ```json
  { "$type": "single_select", "display": "List|Dropdown",
    "options": [{"id": "…", "label": "…"}, …] }
  ```

- **MultiSelect** (`$type = "multi_select"`):
  ```json
  { "$type": "multi_select",
    "options": [{"id": "…", "label": "…"}, …],
    "min_selections": null, "max_selections": null }
  ```

- **YesNo** (`$type = "yes_no"`):
  ```json
  { "$type": "yes_no", "yes_label": "Yes", "no_label": "No" }
  ```

- **Matrix** (`$type = "matrix"`):
  ```json
  { "$type": "matrix", "mode": "CustomColumns|KPIScale",
    "rows": [{"id": "…", "label": "…"}, …],
    "columns": [{"id": "…", "label": "…"}, …] }
  ```
  Rows in `KPIScale` mode seeded from `IKpiCatalogReader.ListPerspectivesAsync(kpi_code)` (BR-8.3); additional rows contribute to the KPI overall but are not perspectives.

- **Ranking** (`$type = "ranking"`):
  ```json
  { "$type": "ranking", "items": [{"id": "…", "label": "…"}, …] }
  ```

- **KPI** (`$type = "kpi"`):
  ```json
  { "$type": "kpi", "representation": "…per-KPI…", "allow_na": false,
    "reason_follow_up": { "enabled": false, "trigger": "…score-based…", "mode": "single_select|multi_select", "items": [], "allow_other": false } }
  ```

---

## PUT /api/v1/surveys/{id}/questions/{questionId}

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<question.row_version>"`.
- **Request body**: same shape as POST minus `id`.
- **Response 200**: updated question + new ETag.
- **KPI-change side effects** (BR-8.5, executed inside `ExecuteAsync`):
  - `perspective` cleared if not in the new KPI's perspective list.
  - `stage_id` cleared if not valid for the new KPI + journey.
  - `touchpoint_id` retained if `IJourneyReader.IsBindingValidAsync(newKpi, journey, currentStage, currentTouchpoint)` returns true; else cleared.
  - `reason_follow_up.items` — same retain-if-valid pattern (research.md § extends BR-8.5).

---

## DELETE /api/v1/surveys/{id}/questions/{questionId}

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<question.row_version>"`.
- **Response 200** — empty body.
- **Cascade** (inside `ExecuteAsync`):
  - `routing_maps.source_question_id = questionId` → cascade delete (FR-9.x).
  - `routing_maps.target_question_id = questionId` → set NULL, App layer removes those rows (routes reset to next-in-order default, FR-2.7).
  - `survey_translations.keys` for question-scoped keys → removed from every locale bundle (FR-2.8).

---

## POST /api/v1/surveys/{id}/questions/{questionId}/move

**Purpose**: FR-8.2 drag-and-drop reorder across section/set boundaries.

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<question.row_version>"`.
- **Request body**:
  ```json
  { "section_id": "…", "set_id": "…|null", "order": 2 }
  ```
- **Response 200**: updated question + new ETag. Sibling `order` values compact within `(section_id, set_id)`.
- **Response 400** `question.move.invalid_parent` if the target section is not in the same survey.
- **Cross-set moves invalidate routing eligibility** — a moved question that becomes a set member automatically loses its routing entries (FR-9.5). The App layer cascades this in the same transaction.

---

## PUT /api/v1/surveys/{id}/questions/{questionId}/routing

**Purpose**: F9 per-question routing map save.

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<question.row_version>"`.
- **Request body**:
  ```json
  { "map": { "<answer_key>": "<target_question_id>|__end", "…": "…" } }
  ```
  Only entries that deviate from the default (next-in-order) need to be present. Absent entries fall back to the default (research.md § 6).
- **Response 200**: updated question view (with `has_routing = true` when the map is non-empty).
- **Response 409** `routing.layout_required` — survey `layout != 'question'`.
- **Response 409** `routing.source_ineligible` — question not routing-eligible (type / subtype / inside a set — FR-9.5, Question Type Catalogue).
- **Response 400** `routing.target_ineligible` — target is inside a set or does not exist.
- **Response 400** `routing.inside_set_forbidden` — the question is inside a Questions Set.

---

## GET /api/v1/surveys/{id}/questions/{questionId}/routing

- **P**: `survey.read` · **S**: `organisation` · **Personas**: P-01, P-02, P-03, P-06
- **Response 200**:
  ```json
  { "map": { "<answer_key>": "<target_question_id>|__end" } }
  ```
  Missing entries default to next-in-order — the client rehydrates defaults locally.

---

## POST /api/v1/surveys/{id}/routing

**Purpose**: Survey-level routing toggle (F9 builder header switch).

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<survey.row_version>"`.
- **Request body**: `{ "enabled": true, "confirm": true }`
- **Response 200**: updated Survey view. Side effects: `shuffle = false` and locked whenever `routing_on = true` (LayoutRoutingCoupler).
- **Response 409** `routing.layout_required` — layout not `question`.
- **Response 409** `routing.confirmation_required` when enabling and `confirm != true` — FR-9.1 confirmation modal.
