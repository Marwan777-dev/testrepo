# Contract — Templates (F6, F7)

**Related**: [surveys.md](./surveys.md) · [data-model.md § 2.8](../data-model.md#28-templates)

**Base path**: `/api/v1/templates`

Templates are snapshots (BR-7.1 — no link to instantiated surveys). Built-in templates are locked (FR-7.1). Customized templates carry tags.

---

## GET /api/v1/templates

**Purpose**: F6 template picker + Templates tab in F1.

- **P**: `template.read` · **S**: `organisation` · **Personas**: P-01, P-03
- **Query params**:
  - `q` — matches `name_en` or `tags` (FR-6.2).
  - `class` — `BuiltIn` \| `Customized`.
  - `sector` — for BuiltIn only.
  - `sort` — `updated_at` \| `name_en` (default `updated_at` for Customized; `name_en` for BuiltIn).
  - `order`, `page_size`, `page_token` — standard pagination (API-04).
- **Response 200**: paginated list. Customized first (FR-6.1), then BuiltIn (each set sorted per `sort`).

## GET /api/v1/templates/{id}

- **P**: `template.read` · **S**: `organisation` · **Personas**: P-01, P-03
- **Response 200**: template metadata + a **redacted** snapshot summary (question count, section count, has-KPI-bindings flag). To read the full snapshot, use the preview endpoint below.
- **`ETag: W/"<row_version>"`** returned for Customized templates. BuiltIn templates omit the ETag (they are read-only).

## GET /api/v1/templates/{id}/preview

**Purpose**: FR-6.4 preview a template without creating a survey.

- **P**: `template.read` · **S**: `organisation` · **Personas**: P-01, P-03
- **Response 200**: the full Survey view derived from the template snapshot (no persistence). Consumed by the client to render the preview UI reusing F12's `LivePreviewFrame`.

## POST /api/v1/templates

**Purpose**: F7 — save an existing survey as a Customized template (FR-7.4).

- **P**: `template.write` · **S**: `organisation` · **Personas**: P-01, P-03
- **Headers**: `Idempotency-Key: <uuid>` — required.
- **Request body**:
  ```json
  { "source_survey_id": "…", "name_en": "…", "name_ar": null, "description": null, "tags": ["onboarding"] }
  ```
- **Response 201**: `Location: /api/v1/templates/{id}` + `ETag: W/"1"`.
- **Behaviour**: `TemplateSnapshotBuilder.Build(source_survey)` copies **all** data — settings, appearance, welcome/thank-you HTML (sanitiser policy version carried forward), sections/sets/questions, KPI bindings **including journey/stage/touchpoint** (FR-7.4), translations, and routing maps — into `template_snapshots.snapshot` with `schema_version = 1`.
- **Response 404** `survey.not_found` when the source survey does not exist.

## PATCH /api/v1/templates/{id}

- **P**: `template.write` · **S**: `organisation` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<row_version>"`.
- **Request body**: any subset of `{name_en, name_ar, description, tags, preview_thumbnail_file_handle}`. Snapshot content is not editable via PATCH — a full snapshot rebuild uses `POST /templates/{id}/rebuild-from-survey` (below).
- **Response 403** `template.built_in_not_editable` when `class = BuiltIn`.
- **Response 200** + new ETag.

## POST /api/v1/templates/{id}/rebuild-from-survey

**Purpose**: Refresh a Customized template's snapshot from an updated source survey (author intent: "I improved the survey; refresh the template").

- **P**: `template.write` · **S**: `organisation` · **Personas**: P-01, P-03
- **Headers**: `Idempotency-Key: <uuid>` — required.
- **Request body**: `{ "source_survey_id": "…" }`.
- **Response 200**: updated template view + new ETag.
- **Response 403** `template.built_in_not_editable` when `class = BuiltIn`.

## DELETE /api/v1/templates/{id}

- **P**: `template.write` · **S**: `organisation` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<row_version>"`.
- **Response 200**: empty body.
- **Response 403** `template.built_in_not_editable` when `class = BuiltIn`.
- **Behaviour**: hard delete of the template row + its snapshot. **No cascade to already-instantiated surveys** (Q4 / BR-7.1) — they hold their own independent data.

## POST /api/v1/templates/{id}/instantiate

**Purpose**: FR-6.3 "Use this template" — create a new survey from a template.

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `Idempotency-Key: <uuid>` — required.
- **Request body** (all optional; defaults resolved from the snapshot):
  ```json
  { "name_en": "…override…" }
  ```
- **Response 201**: `Location: /api/v1/surveys/{newSurveyId}` + `ETag: W/"1"`. Body is the full Survey view.
- **Behaviour**: `TemplateInstantiator.CreateSurveyFrom(template)` copies every element of the snapshot into a fresh Survey aggregate. The new Survey has `owner_user_id = caller`, `status = Draft`, `created_by/updated_by = caller`, and **no foreign-key back-reference** to the template (Q4 / BR-7.1).
