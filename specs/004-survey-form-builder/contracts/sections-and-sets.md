# Contract — Sections & Questions Sets (F2, F10)

**Related**: [surveys.md](./surveys.md) · [questions.md](./questions.md) · [data-model.md](../data-model.md)

**Base paths**:
- `/api/v1/surveys/{id}/sections`
- `/api/v1/surveys/{id}/sections/{sectionId}/sets`

All endpoints require JWT auth and `If-Match` on writes. Every write on a section OR set bumps the parent survey's `row_version` (per research.md § 2) AND its own `row_version`.

---

## POST /api/v1/surveys/{id}/sections

**Purpose**: F2 add section.

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<survey.row_version>"`.
- **Request body**:
  ```json
  { "id": "…optional", "name": "General", "description": null, "order": 3 }
  ```
  If `order` is omitted, appended to the end.
- **Response 201**: `Location: …/sections/{sectionId}` + `ETag: W/"1"` (of the section).
- **Response 400** `section.name.required`.

## PATCH /api/v1/surveys/{id}/sections/{sectionId}

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<section.row_version>"`.
- **Request body**: any subset of `{name, description, order}`. Reordering compacts within `(survey_id, order)`.
- **Response 200**: updated section + new ETag.
- **Response 409** `section.conflict` on stale ETag.

## DELETE /api/v1/surveys/{id}/sections/{sectionId}

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Query params**: `confirm=true` — required when the section is non-empty (has any children).
- **Headers**: `If-Match: W/"<section.row_version>"`.
- **Response 200** — empty body when confirmed OR when the section is already empty.
- **Response 409** `section.delete.requires_confirmation` when the section is non-empty and `confirm != true`. Body: `details: { standalone_questions: N, questions_sets: M, set_questions: K }`.
- **Cascade**: FR-2.5 — all standalone questions and Questions Sets (and their child questions) are deleted; FR-2.7 — inbound routes reset to default; FR-2.8 — translation keys purged. Atomic inside `ExecuteAsync`.
- **Post-delete**: the survey's response count is NOT touched here; response purging happens only via BR-1.6 or Archive-then-erasure.

---

## POST /api/v1/surveys/{id}/sections/{sectionId}/sets

**Purpose**: F10 add a Questions Set to a section.

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<section.row_version>"`.
- **Request body**:
  ```json
  { "id": "…optional",
    "title": "Optional questions", "description": null,
    "selection_mode": "random", "count": 3,
    "order": 1
  }
  ```
- **Response 201**: `Location: …/sets/{setId}` + `ETag: W/"1"`.
- **Response 400** `questionsset.count.exceeds_size` when `count > (SELECT COUNT(*) FROM questions WHERE set_id = @setId)` — impossible on create (set is empty), but validated once questions are added via [questions.md](./questions.md).

## PATCH /api/v1/surveys/{id}/sections/{sectionId}/sets/{setId}

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<set.row_version>"`.
- **Request body**: any subset of `{title, description, selection_mode, count, order}`.
- **Response 200** + new ETag.
- **Response 400** `questionsset.count.exceeds_size` when the new `count` exceeds the current member count.

## DELETE /api/v1/surveys/{id}/sections/{sectionId}/sets/{setId}

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Query params**: `confirm=true` when the set has ≥ 1 question.
- **Headers**: `If-Match: W/"<set.row_version>"`.
- **Response 200** — empty body.
- **Response 409** `questionsset.delete.requires_confirmation` when non-empty and `confirm != true`. Body: `details: { questions_count: N }`.
- **Cascade**: all set questions deleted (FR-2.6); routing/translation cleanup as per DELETE `/sections`.

---

## Notes

- Section and set writes emit no `survey.published` / `survey.archived` events (constitution § 4 catalogue does not require them). Every write is audited via `IEventLogWriter` under the enclosing survey `correlation_id`.
- **`If-Match` scope**: `PATCH /sets/{setId}` uses the SET's `row_version`, not the section's. This lets two admins edit two different sets in parallel without ETag collision.
