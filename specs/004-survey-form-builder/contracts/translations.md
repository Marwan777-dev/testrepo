# Contract — Translations (F11)

**Related**: [data-model.md § 2.7](../data-model.md#27-survey_translations) · [research.md § 10](../research.md#10-localisation-model)

**Base path**: `/api/v1/surveys/{id}/translations`

Bilingual by design (T-01). Arabic (four dialects — MSA + Gulf/Levantine/Egyptian) + English at Phase 1. English is always the source; Arabic and any additional locale are authored in the Translate workspace.

---

## GET /api/v1/surveys/{id}/translations

**Purpose**: List the locales available for the survey (F11 workspace top selector).

- **P**: `survey.read` · **S**: `organisation` · **Personas**: P-01, P-02, P-03, P-06
- **Response 200**:
  ```json
  { "locales": [
      { "locale": "en", "coverage_percent": 100, "keys_count": 34, "keys_translated": 34, "updated_at": "…" },
      { "locale": "ar", "coverage_percent": 82, "keys_count": 34, "keys_translated": 28, "updated_at": "…" }
    ] }
  ```

## GET /api/v1/surveys/{id}/translations/{locale}

- **P**: `survey.read` · **S**: `organisation` · **Personas**: P-01, P-02, P-03, P-06
- **Response 200**:
  ```json
  { "locale": "ar",
    "keys": {
      "survey.name": "استبيان ما بعد الزيارة",
      "survey.welcome": "…",
      "survey.thanks": "…",
      "section.<sectionId>.title": "…",
      "question.<questionId>.text": "…",
      "question.<questionId>.description": "…",
      "question.<questionId>.options.0.label": "…",
      "question.<questionId>.scale_labels.0": "…",
      "question.<questionId>.comment_label": "التعليقات",
      "question.<questionId>.reason_items.0": "…"
    },
    "missing_keys": ["question.<otherId>.text", "…"]
  }
  ```
  `missing_keys` lists source keys with no target translation — `LocaleFallbackPolicy` renders them from `en` at runtime.
- **`ETag: W/"<row_version>"`** returned.

## PUT /api/v1/surveys/{id}/translations/{locale}

**Purpose**: Save the target locale bundle.

- **P**: `survey.write` · **S**: `own` · **Personas**: P-01, P-03
- **Headers**: `If-Match: W/"<row_version>"` on updates; omitted for a first-time locale create (a POST-like semantic on the same URL — the server treats `If-Match: *` or missing as first-write).
- **Request body**: partial or full bundle:
  ```json
  { "keys": { "survey.name": "…", "question.<id>.text": "…" } }
  ```
  Keys not present in the body are preserved unchanged (merge semantics).
- **Response 200**: updated bundle view + new ETag.
- **Response 400** `translation.locale.not_configured` when the locale is not permitted by the tenant's `ITenantSettingsReader.GetSupportedLocalesAsync()` return value.
- **Response 400** `translation.key.unknown` when the request carries a key that does not correspond to any current source string (e.g., referring to a deleted question). Body: `details: { unknown_keys: ["…"] }`.
