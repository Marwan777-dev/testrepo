# Data Model — Survey & Form Builder (M-01)

**Feature branch**: `004-survey-form-builder`
**Related**: [plan.md](./plan.md) · [spec.md](./spec.md) · [research.md](./research.md) · [contracts/](./contracts/)

**Scope**: This document catalogues the **PostgreSQL tenant-schema tables** owned by `Nabadat.SurveyBuilder` (module `M-01`), their columns, indexes, invariants, state transitions, and relationships. It does **not** enumerate Elasticsearch indices — those are AD-04 read-side projections owned by M-04/M-05/M-06/M-07 and consumed by M-01 via `IReportAggregator` / `IAnalyticsAggregator` ports (see [research.md § 3](./research.md#3-elasticsearch-client--query-patterns-for-report--analytics)).

**Storage isolation**: every table below lives in the `tenant_{slug}` schema (AD-02). **No `tenant_id` column** anywhere (DB-02). Primary keys are `uuid` unless noted.

**Baseline SQL**: the DDL for every table below lives in [`src/Nabadat.SurveyBuilder/Infrastructure/Migrations/_Baseline.sql`](../../src/Nabadat.SurveyBuilder/Infrastructure/Migrations/_Baseline.sql) (to be created in the Foundational task) and is applied by the platform's DB-05 migration runner (`tools/Nabadat.Migrations`). EF Core does not generate migrations (DB-08 rule 6).

**Constitution corrections needed**:
- **M-01 owned-tables entry in constitution Section 3** currently lists `surveys, questions, question_bank, survey_versions, survey_templates`. This is a placeholder — the actual Feature 004 set below (9 tables, §2.1–2.9) drops `survey_versions` (Q6 obviates versioning) and `question_bank` (KPI-catalogue concern owned by M-06 per AMENDMENT-011) and adds `sections, questions_sets, survey_translations, themes, routing_maps, template_snapshots`. (`question_translations` in the ERD below is a **logical** grouping stored as jsonb keys inside `survey_translations` — not a separate physical table; `/speckit-analyze` 2026-07-15 corrected an earlier draft of this note that miscounted it as one.) A constitution AMENDMENT correcting M-01's owned-tables list is filed as a Foundational task (see [research.md § 12](./research.md#12-open-items-surfaced-during-phase-0-not-blocking)).

---

## 1. Entity relationship overview

```
tenants (schema-per-tenant, not a table)
└── surveys (aggregate root)
    ├── sections
    │   ├── standalone questions (questions.set_id IS NULL)
    │   └── questions_sets
    │       └── set questions (questions.set_id IS NOT NULL)
    ├── themes (1:1 when theme_mode = customized; otherwise NULL — inherited)
    ├── routing_maps (M:N Question → Question, keyed by (source_question_id, answer_key))
    └── survey_translations (1:N by locale)

templates (parallel to surveys — full snapshot of a survey's authoring state)
├── template_snapshots (jsonb blob of the entire Survey aggregate at snapshot time)
└── (no back-reference from instantiated surveys — Q4/BR-7.1 snapshot-no-link rule)

questions
├── question_translations (1:N by locale, but stored inside survey_translations as jsonb keys per research.md § 10)
└── routing_maps (edges out)
```

Cross-module identifiers (never FKs — Article 4.1):

- `surveys.bound_journey_id` — references M-16 `journeys.id`; nullable; validated at write time via `IJourneyReader`.
- `questions.kpi_code` (KPI questions) — references M-06 KPI catalogue; validated via `IKpiCatalogReader`.
- `questions.stage_id`, `questions.touchpoint_id` (KPI questions with bound journey ON) — references M-16 stages / touchpoints; validated via `IJourneyReader.IsBindingValidAsync`.
- `surveys.owner_user_id`, `surveys.submitted_by`, `surveys.reviewed_by` — reference M-10 `users`.
- `surveys.theme_logo_file_handle` — reference to a file-storage handle (opaque string).

---

## 2. Entities

### 2.1 `surveys`

The aggregate root. One row per survey.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK. Client-generated UUIDs are permitted for idempotency (`Idempotency-Key`). |
| `name_en` | `text` | no | 1–200 chars. `survey.name_en.required` / `survey.name_en.max_length`. |
| `description` | `text` | yes | Internal note. |
| `survey_type` | `text` | no | Enum: `Transactional` \| `SeasonalRelational`. Derived from `bound_journey_id` (BR-3.3). |
| `bound_journey_id` | `uuid` | yes | Cross-module identifier — no FK. Empty ⇒ `SeasonalRelational`; set ⇒ `Transactional`. |
| `status` | `text` | no | Enum: `Draft` \| `PendingReview` \| `Active` \| `Paused` \| `Archived`. Transitions per the Status Transition Matrix in spec.md. |
| `owner_user_id` | `uuid` | no | M-10 user id of the author-of-record. Q8: authoring is P-03-team-scoped; `owner_user_id` is used for the "Publish own surveys" grant scope (per-individual) and for audit attribution. |
| `submitted_by` | `uuid` | yes | Set when status transitions Draft → PendingReview. |
| `submitted_at` | `timestamptz` | yes | UTC (Article 4.3). |
| `reviewed_by` | `uuid` | yes | Set when Publish or Return-to-draft is performed. |
| `reviewed_at` | `timestamptz` | yes | UTC. |
| `review_remarks` | `text` | yes | Optional remarks from Publish / Return-to-draft. |
| `theme_mode` | `text` | no | Enum: `Inherited` \| `Customized`. Default `Inherited`. |
| `welcome_html` | `text` | yes | Sanitised at ingress (Q3). |
| `thanks_html` | `text` | yes | Sanitised at ingress (Q3). |
| `sanitiser_policy_version` | `int4` | no | Records which sanitiser allowlist version was applied to `welcome_html` / `thanks_html` (audit trail — Q3 "auditable and versioned"). Default `1`. |
| `redirect_url` | `text` | yes | Optional post-submit redirect. |
| `redirect_after_s` | `int4` | no | Default `0`. |
| `layout` | `text` | no | Enum: `single` \| `section` \| `question` \| `count`. Default `section`. |
| `questions_per_page` | `int4` | yes | Present only when `layout = count`. Must be ≥ 1. |
| `active_period` | `jsonb` | yes | Shape: `{"days": int, "hours": int}`. NULL ⇒ never auto-expires (FR-3.4). |
| `activated_at` | `timestamptz` | yes | UTC (Article 4.3). The active-period "start" instant — stamped on every transition into Active (FR-3.4/BR-3.4); `activated_at + active_period` is the absolute expiry M-04 enforces. NULL until first published. |
| `record_time` | `bool` | no | System-managed; always `true` (FR-3.5). |
| `shuffle` | `bool` | no | Default `false`. |
| `shuffle_mode` | `text` | no | Enum: `random` \| `low_response`. Default `random`. |
| `routing_on` | `bool` | no | Default `false`. Requires `layout = question`; disables and locks `shuffle`. |
| `theme_logo_file_handle` | `text` | yes | Opaque file-storage handle (F4). NULL when no logo uploaded. |
| `created_at` | `timestamptz` | no | UTC. |
| `created_by` | `uuid` | no | M-10 user id. |
| `updated_at` | `timestamptz` | no | UTC. |
| `updated_by` | `uuid` | no | M-10 user id. |
| `row_version` | `int4` | no | Monotonic ETag counter (research.md § 2). Default `1`; incremented on every write. |

**Invariants** (enforced in `SurveyValidator` + database constraints):

- `name_en` NOT NULL AND length ≤ 200 (`survey.name_en.required`, `survey.name_en.max_length`).
- If `bound_journey_id` IS NOT NULL then `survey_type = 'Transactional'` (BR-3.3).
- If `layout = 'count'` then `questions_per_page` IS NOT NULL.
- If `routing_on = true` then `layout = 'question'` AND `shuffle = false` (constitution invariant + F9 coupling).
- `status ∈ {Draft, PendingReview, Active, Paused, Archived}`.
- Transitions follow the Status Transition Matrix (spec.md). `Archived → Active` and `Archived → Paused` are rejected.
- **BR-1.6 destructive Return-to-Draft** transitions from `Active` or `Paused` to `Draft` MUST be executed inside `ITenantDbContext.ExecuteAsync` and paired with a post-commit call to `IResponsePurgeService.PurgeSurveyResponsesAsync(...)`; compensate on failure (see [research.md § 4.5](./research.md#45-iresponsepurgeservice-m-04-new-port)).
- **BR-1.7 Publish gate** — transitioning to `Active` from `Draft` or `PendingReview` requires the survey to have `sections.count > 0` AND `total questions across all sections > 0`; enforced by `PublishGateService` before the transition commits. Reactivating a `Paused` → `Active` skips this check.

**Indexes**:

- PK on `id`.
- `idx_surveys_status_updated_at` on `(status, updated_at DESC)` — powers F1 library filtering + default ordering.
- `idx_surveys_bound_journey_id` on `bound_journey_id` — powers F1 Journey filter.
- `idx_surveys_owner_user_id` on `owner_user_id` — powers the "Publish own surveys" grant check.
- `idx_surveys_name_en_lower` on `LOWER(name_en) text_pattern_ops` — powers real-time English-name search (FR-1.2).

### 2.2 `sections`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK. |
| `survey_id` | `uuid` | no | FK → `surveys.id` (intra-module FK). ON DELETE CASCADE. |
| `name` | `text` | no | 1–200 chars. |
| `description` | `text` | yes | |
| `order` | `int4` | no | Contiguous within the survey; compacted on reorder (FR-8.2). |
| `created_at` | `timestamptz` | no | |
| `updated_at` | `timestamptz` | no | |
| `row_version` | `int4` | no | |

**Invariants**:

- `(survey_id, order)` unique — enforced by a partial unique index.
- FR-2.3: **the last section CAN be deleted** — no minimum-count invariant. Publish gate handles the "no sections" case separately (BR-1.7).
- FR-2.5: delete of a non-empty section requires a client-confirmed API call — enforced by an explicit `?confirm=true` query parameter on `DELETE /sections/{id}`.
- FR-2.7: on delete, all `routing_maps` rows referencing any child question as source OR target are cascaded via `ON DELETE CASCADE` from questions.

**Indexes**:

- PK on `id`.
- `idx_sections_survey_id_order` on `(survey_id, order)`.

### 2.3 `questions_sets`

Rotating pool inside a section (F10).

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK. |
| `section_id` | `uuid` | no | FK → `sections.id`. ON DELETE CASCADE. |
| `title` | `text` | no | 1–200 chars. |
| `description` | `text` | yes | |
| `selection_mode` | `text` | no | Enum: `random` \| `low_response`. Default `random`. |
| `count` | `int4` | no | Questions delivered per respondent per dispatch. `count >= 0` AND `count <= (SELECT COUNT(*) FROM questions WHERE set_id = questions_sets.id)`. |
| `order` | `int4` | no | Position within the section (alongside standalone questions). |
| `created_at` | `timestamptz` | no | |
| `updated_at` | `timestamptz` | no | |
| `row_version` | `int4` | no | |

**Invariants**:

- `count >= 0 AND count <= size(set)` — enforced by `QuestionsSetValidator`. Empty set with `count = 0` is a valid (no-op) configuration.
- FR-2.6: delete of a non-empty set requires `?confirm=true`.
- Set questions cannot be routing sources or targets (FR-9.5) — enforced by `RoutingEligibilityService`.

**Indexes**:

- PK on `id`.
- `idx_questions_sets_section_id` on `section_id`.

### 2.4 `questions`

Single-table with per-type payload — see [research.md § 5](./research.md#5-question-type-catalogue--ef-mapping-strategy).

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK. |
| `survey_id` | `uuid` | no | FK → `surveys.id`. Denormalised from `sections.survey_id` for fast `render-plan` queries. ON DELETE CASCADE. |
| `section_id` | `uuid` | no | FK → `sections.id`. ON DELETE CASCADE. |
| `set_id` | `uuid` | yes | FK → `questions_sets.id`. NULL ⇒ standalone. Standalone questions are also referred to as `set_id IS NULL`. |
| `type` | `text` | no | Enum: `Scale` \| `InputField` \| `SingleSelect` \| `MultiSelect` \| `YesNo` \| `Matrix` \| `Ranking` \| `KPI`. |
| `subtype` | `text` | no | Enum per parent type (see Question Type Catalogue in spec.md). Required (FR-8.8) — `question.subtype.required` if missing. |
| `text` | `text` | no | Required. |
| `description` | `text` | yes | |
| `required` | `bool` | no | Default `false`. |
| `comments` | `bool` | no | Default `false`. FR-8.9. |
| `comment_label` | `text` | no | Default `"Comments"`. Translatable. |
| `comment_max_length` | `int4` | no | Default `200`. |
| `sentiment` | `bool` | no | Default `false`. FR-8.11 (Text/Paragraph only — enforced by `SentimentFlagPolicy`). |
| `kpi_code` | `text` | yes | KPI questions and Matrix KPI-scale mode. Cross-module identifier — no FK. Validated by `IKpiCatalogReader`. |
| `perspective` | `text` | yes | Optional; options come from `IKpiCatalogReader.ListPerspectivesAsync(kpi_code)`. |
| `bound_journey_on` | `bool` | no | Default `true` for KPI questions. |
| `stage_id` | `uuid` | yes | Cross-module identifier. Required before `touchpoint_id` may be set (FR-8.4). |
| `touchpoint_id` | `uuid` | yes | Cross-module identifier. Optional (FR-8.4). Validated by `IJourneyReader.IsBindingValidAsync`. Cleared per BR-8.5 on KPI change. |
| `type_payload` | `jsonb` | no | Per-type validated payload (research.md § 5). |
| `order` | `int4` | no | Ordering within `(section_id, set_id)` — contiguous. |
| `created_at` | `timestamptz` | no | |
| `updated_at` | `timestamptz` | no | |
| `row_version` | `int4` | no | |

**Invariants**:

- `subtype` present ⇒ `question.subtype.required` (FR-8.8).
- `type_payload` shape matches `type` — polymorphic `System.Text.Json` deserialisation enforces this at the App layer.
- If `type = 'KPI'` OR (`type = 'Matrix' AND subtype = 'KPIScale'`) → `kpi_code IS NOT NULL`.
- If `bound_journey_on = true` AND `touchpoint_id IS NOT NULL` → `stage_id IS NOT NULL` (`kpi.touchpoint.requires_stage`).
- If `bound_journey_on = false` → `stage_id IS NULL AND touchpoint_id IS NULL` (BR-8.2 — stripped by `KpiBindingValidator`).
- `sentiment = true` only allowed when `type = 'InputField' AND subtype IN ('Text','Paragraph')` (FR-8.11 — otherwise Warn+Strip).
- `(section_id, set_id, order)` contiguous — reorders atomically inside `ExecuteAsync`.

**Indexes**:

- PK on `id`.
- `idx_questions_survey_id` on `survey_id` — powers `render-plan` full-survey read.
- `idx_questions_section_id_order` on `(section_id, order)`.
- `idx_questions_set_id_order` on `(set_id, order)` where `set_id IS NOT NULL`.
- `idx_questions_kpi_code` on `kpi_code` where `kpi_code IS NOT NULL` — powers per-KPI lookups.

### 2.5 `routing_maps`

Sparse per-answer routing overrides (F9). See [research.md § 6](./research.md#6-routing-map-storage--default-targeting).

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK. |
| `survey_id` | `uuid` | no | FK → `surveys.id`. Denormalised for cascade-on-survey-delete and for fast survey-scoped invalidation. ON DELETE CASCADE. |
| `source_question_id` | `uuid` | no | FK → `questions.id`. ON DELETE CASCADE. |
| `answer_key` | `text` | no | Per-type answer identifier (Scale point index, YesNo "yes"/"no", SingleSelect option id, KPI score bucket). |
| `target_question_id` | `uuid` | yes | FK → `questions.id`. NULL ⇒ `__end` (end of survey). ON DELETE SET NULL (routes to a deleted target reset to next-in-order default per FR-2.7 via App-layer logic; the SET NULL keeps the constraint satisfied while the App layer replays "reset to default" for the affected sources). |
| `created_at` | `timestamptz` | no | |
| `updated_at` | `timestamptz` | no | |

**Invariants**:

- `(source_question_id, answer_key)` unique — enforced by unique index.
- Source and target MUST be standalone (i.e., `set_id IS NULL`) — validated by `RoutingEligibilityService` (FR-9.5).
- Source and target MUST be in the same `survey_id` — enforced by CHECK constraint via the denormalised `survey_id`.
- Only present when a route deviates from the default (next-in-order) — missing rows mean "use the default".

**Indexes**:

- PK on `id`.
- Unique index on `(source_question_id, answer_key)`.
- `idx_routing_maps_survey_id` on `survey_id`.
- `idx_routing_maps_target_question_id` on `target_question_id` — powers the FR-2.7 "reset to default" cascade query.

### 2.6 `themes`

Per-survey customisation for F4 (only present when `surveys.theme_mode = 'Customized'`).

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK. |
| `survey_id` | `uuid` | no | FK → `surveys.id`. Unique — 1:1 relationship. ON DELETE CASCADE. |
| `primary_color` | `text` | yes | Hex `#RRGGBB` or NULL (inherit at token level). |
| `text_color` | `text` | yes | |
| `button_radius_px` | `int4` | yes | |
| `button_border_color` | `text` | yes | |
| `button_text_color` | `text` | yes | |
| `header_show_logo` | `bool` | no | Default `true`. |
| `header_show_title` | `bool` | no | Default `true`. |
| `header_alignment` | `text` | no | Enum: `start` \| `center` \| `end`. Default `start`. |
| `footer_text` | `text` | yes | |
| `background_type` | `text` | no | Enum: `Solid` \| `Gradient` \| `Image` \| `Pattern`. Default `Solid`. |
| `background_config` | `jsonb` | yes | Shape per type (solid: `{color}`; gradient: `{stops[], angle}`; image: `{file_handle, opacity}`; pattern: `{pattern_id, color}`). Validated at App layer. |
| `background_opacity` | `int4` | no | 0–100. Default `100`. |
| `advanced_status_colors` | `jsonb` | yes | Per-D-level overrides. |
| `advanced_surfaces` | `jsonb` | yes | Background/card/border. |
| `advanced_typography` | `jsonb` | yes | Heading/body fonts. |
| `advanced_layout` | `jsonb` | yes | Card radius, progress-bar style. |
| `created_at` | `timestamptz` | no | |
| `updated_at` | `timestamptz` | no | |
| `row_version` | `int4` | no | |

**Invariants**:

- `background_type = 'Image'` → `background_config->>'file_handle' IS NOT NULL`.
- `background_type = 'Gradient'` → `background_config->'stops'` is a JSON array of ≥ 2 stops.

**Indexes**:

- PK on `id`.
- Unique index on `survey_id`.

### 2.7 `survey_translations`

Per-locale bundle. One row per `(survey_id, locale)`.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK. |
| `survey_id` | `uuid` | no | FK → `surveys.id`. ON DELETE CASCADE. |
| `locale` | `text` | no | BCP-47 tag. `en` and `ar` at Phase 1 (T-01); the design supports N locales. |
| `keys` | `jsonb` | no | Flat map `{"survey.name": "…", "section.{id}.title": "…", "question.{id}.text": "…", …}`. See [research.md § 10](./research.md#10-localisation-model). |
| `created_at` | `timestamptz` | no | |
| `updated_at` | `timestamptz` | no | |
| `row_version` | `int4` | no | |

**Invariants**:

- Unique on `(survey_id, locale)`.
- On question delete: the App layer removes the deleted question's keys from every locale bundle (FR-2.8) inside the same `ExecuteAsync` transaction.
- `keys.survey.name` — required for `locale != 'en'` if the user wants to display the translated survey name in a rendered survey; not required for save (BR-3.2).

**Indexes**:

- PK on `id`.
- Unique index on `(survey_id, locale)`.

### 2.8 `templates`

Templates are parallel to surveys — they hold a full snapshot of a survey's authoring state (Q4 / BR-7.1 snapshot-no-link).

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK. |
| `class` | `text` | no | Enum: `BuiltIn` \| `Customized`. `BuiltIn` templates are seeded per tenant at provisioning; only `Customized` are user-editable (FR-7.1). |
| `name_en` | `text` | no | Required. |
| `name_ar` | `text` | yes | Optional. |
| `description` | `text` | yes | |
| `tags` | `text[]` | no | `Customized` templates carry tags; `BuiltIn` — empty array (their filter facet is `sectors`). |
| `sectors` | `text[]` | no | `BuiltIn` only — Banking, Telecom, Government, etc. |
| `preview_thumbnail_file_handle` | `text` | yes | Optional file-storage handle for the F6 preview card image. |
| `created_at` | `timestamptz` | no | |
| `created_by` | `uuid` | yes | NULL for `BuiltIn` (system-authored). |
| `updated_at` | `timestamptz` | no | |
| `updated_by` | `uuid` | yes | |
| `row_version` | `int4` | no | |

**Invariants**:

- `class = 'BuiltIn'` → `created_by IS NULL AND updated_by IS NULL AND cardinality(tags) = 0`.
- `class = 'Customized'` → `sectors = '{}'::text[]` AND `created_by IS NOT NULL`.

**Indexes**:

- PK on `id`.
- `idx_templates_class_name_en` on `(class, LOWER(name_en) text_pattern_ops)` — powers the Templates tab filter + name search (FR-6.2).
- GIN index on `tags` — powers tag search (`template.tag_search`).
- GIN index on `sectors` — powers built-in sector filter.

### 2.9 `template_snapshots`

The authoritative payload attached to a template row — a **full copy** of the source survey's authoring state (Q4). Instantiation copies this back into a new `Survey` aggregate.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `template_id` | `uuid` | no | PK. FK → `templates.id`. ON DELETE CASCADE. Also unique — 1:1 relationship. |
| `snapshot` | `jsonb` | no | Full snapshot: `{"survey": {…settings…}, "sections": [{"section": {…}, "questions": [{…}, ...], "sets": [{"set": {…}, "questions": [{…}]}]}], "theme": {…} | null, "translations": {"en": {keys}, "ar": {keys}}, "routing_maps": [{…}]}` — see [contracts/templates.md](./contracts/templates.md) for the exact JSON schema. |
| `schema_version` | `int4` | no | Snapshot schema version. Default `1`. Older versions are migrated on read by `TemplateInstantiator` (T-08 forward compatibility). |
| `created_at` | `timestamptz` | no | |

**Invariants**:

- `schema_version >= 1`.
- Snapshot integrity is validated on write by `TemplateSnapshotBuilder.Validate(snapshot)` — every referenced question/section/set id inside the snapshot must be internally consistent.

**Indexes**:

- PK on `template_id`.

---

## 3. State transitions

### 3.1 Survey lifecycle (FSM)

```
             ┌─────────────────────────────── Archive (P-01) ──────────────────────────────┐
             │                                                                              ▼
[Draft] ────Submit (P-03 own)───▶ [PendingReview] ──Publish (P-01 | P-03+grant)──▶ [Active] ─Archive→ [Archived]
   ▲                │                                                        ▲                    │
   │                └──Return to draft (P-01)───────────────────────────────┐│                    │
   │                                                                        ││                    │
   ├──Publish (P-01 | P-03+grant)──────────────────────────────────────────┘│                    │
   │                                                                          │                    │
   │◀──Return to draft **destructive** (P-01, BR-1.6) ────────────────────┐  │                    │
   │                                                                       │  │                    │
   │                                                          [Paused] ───┘  │                    │
   │                                                             ▲            │                    │
   │◀──Return to draft **destructive** (P-01, BR-1.6) ───────────┤            │                    │
   │                                                             │            │                    │
   │                                                             └──Pause───[Active]               │
   │                                                                                                │
   └──Unarchive───────────────────────────────────────────────────────────────────────────[Archived]┘
```

Rules per BR-1.4 / BR-1.5 / BR-1.6 / BR-1.7 (all enforced by `SurveyLifecycleService`):

- `Draft → Active` and `PendingReview → Active` are gated by `PublishGateService` (BR-1.7: ≥1 section + ≥1 question).
- `Active → Draft` and `Paused → Draft` are **destructive** (BR-1.6): atomic purge + status change + M-04 in-flight session invalidation. Requires a `?confirm=true` query param + `Idempotency-Key`.
- `Active → Paused` when `rules_count > 0` requires `?confirm=true` (FR-1.10).
- `Archived → Draft` (unarchive) is not destructive — no responses exist for an Archived survey (any prior responses were purged the last time it transitioned out of Active via BR-1.6, or it never went Active).
- `Paused → Active` (reactivate) skips the Publish content gate (Q9).

State transition matrix reproduced in [contracts/surveys.md](./contracts/surveys.md).

### 3.2 Route reset on question delete (FR-2.7)

When a `questions` row is deleted:

1. `ON DELETE CASCADE` on `routing_maps.source_question_id` removes the deleted question's outbound routes.
2. `ON DELETE SET NULL` on `routing_maps.target_question_id` nulls the target — the App layer then removes those rows (default routing = next-in-order applies automatically when no override exists).
3. `TranslationBundleService.PurgeQuestionKeys(surveyId, questionId, ct)` scrubs the question's keys from every `survey_translations.keys` bundle in the same `ExecuteAsync` transaction (FR-2.8).

### 3.3 Destructive Return-to-Draft (BR-1.6)

Executed by `DestructiveReturnToDraftService.ReturnAndPurgeAsync(surveyId, actor, correlationId, ct)`:

1. Compute the current `responses_count` via `IResponsePurgeService` (or an accompanying `IResponseCountReader` port — coordinate with M-04). Return it in the confirmation payload so the UI can show `N` before the user confirms (FR spec).
2. On confirm — atomically inside `ITenantDbContext.ExecuteAsync`:
   - Update `surveys.status → 'Draft'`; clear `submitted_by/at`, `reviewed_by/at`, `review_remarks`.
   - Bump `row_version`.
3. After the M-01 transaction commits: call `IResponsePurgeService.PurgeSurveyResponsesAsync(surveyId, actor, correlationId, ct)`. If it fails: compensate (revert M-01 status to prior) + surface 503; retryable via Idempotency-Key replay.
4. Write M-11 audit entry via M-17: actor, timestamp, previous status, purged response count.

### 3.4 Publish gate (BR-1.7)

Executed by `PublishGateService.EnsureContent(survey, ct)` before any `Draft → Active` or `PendingReview → Active`:

1. `SELECT COUNT(*) FROM sections WHERE survey_id = @id` — if 0 → 409 `publish.requires_content` with `details.missing_sections = true`.
2. `SELECT COUNT(*) FROM questions WHERE survey_id = @id` — if 0 → 409 `publish.requires_content` with `details.missing_questions = true`.
3. Both > 0 → proceed with the transition.

---

## 4. Cross-module identifier resolution

M-01 stores cross-module identifiers as opaque `uuid` (or `text` for enum-like codes) with no FK. Validation happens at write time via the published-interface ports:

| Field | Owning module | Validation call |
|---|---|---|
| `surveys.bound_journey_id` | M-16 | `IJourneyReader.GetJourneyAsync(id, ct)` → NULL ⇒ reject with `survey.bound_journey.not_found`. |
| `surveys.owner_user_id`, `.submitted_by`, `.reviewed_by`, `.created_by`, `.updated_by` | M-10 | No pre-validation (users are session identities from the JWT); referential integrity is by application-layer contract, not DB constraint. |
| `questions.kpi_code` | M-06 | `IKpiCatalogReader.GetKpiAsync(code, ct)` → NULL ⇒ reject with `kpi.not_found`. |
| `questions.stage_id`, `.touchpoint_id` | M-16 | `IJourneyReader.IsBindingValidAsync(kpi, journey, stage, touchpoint, ct)` on every write. Retain-if-valid, else clear per BR-8.5. |
| `templates.preview_thumbnail_file_handle`, `surveys.theme_logo_file_handle` | Shared file-storage | `IFileStorageService.ExistsAsync(handle, ct)` → validated when the survey save arrives with a fresh handle from the upload API. |

Elasticsearch identifiers used at read time (F13 / F14):

- `tenant_{tenantId}_analytics` documents keyed by `(survey_id, period, question_id)` for aggregated Report metrics.
- `tenant_{tenantId}_responses` documents keyed by `response_id`, filtered by `survey_id` and `submitted_at` window.

---

## 5. Data volumes & partitioning

**M-01 owns no partition-heavy tables.** The response history lives in M-04's `responses` (DB-04 monthly partitioning). M-01 tables are small — a few thousand surveys per active tenant at most.

| Table | Estimated rows per active tenant | Partitioning |
|---|---|---|
| `surveys` | 50 – 5 000 | None |
| `sections` | ≤ 20 × surveys | None |
| `questions_sets` | ≤ 20 × surveys | None |
| `questions` | ≤ 100 × surveys | None |
| `routing_maps` | 0 – 10 × questions (sparse) | None |
| `themes` | ≤ surveys | None |
| `survey_translations` | 2 × surveys (en + ar) | None |
| `templates` | 20 built-in + ≤ 500 customized | None |
| `template_snapshots` | 1:1 with `templates` | None |

Baseline indexes are documented per-table above; no partitioning required.

---

## 6. Deletion semantics (Article 4.4)

Per database-constitution Article 4.4:

| Entity | Deletion | Rationale |
|---|---|---|
| `surveys` (Archive) | **Soft** — status = `Archived`, row retained. | Historical reference; audit trail. |
| `surveys` (Hard delete) | Not supported through the API. Erasure requests are handled via M-04's response purge (GP-03); the survey definition itself is not PII. | Definition is business content, not PII. |
| `sections`, `questions_sets`, `questions`, `routing_maps` | **Hard** — cascade delete from `surveys` on Archive-only (no); *only* individual DELETE operations. | These are child records; the aggregate itself is retained via the survey. |
| `templates` (Customized) | **Hard** — direct row delete (Q4/BR-7.1: instantiated surveys are unaffected). | No dependent references. |
| `template_snapshots` | **Hard** — cascade from `templates`. | 1:1 with the parent template. |
| `themes` | **Hard** — cascade from `surveys`. | 1:1 child. |
| `survey_translations` | **Hard** — cascade from `surveys`, or scrubbed by question delete (FR-2.8). | Locale bundle. |
| **Responses** (Q6 destructive Return-to-Draft) | **Hard** — via M-04's `IResponsePurgeService`. Not an M-01 delete. | Response data lives in M-04 tables. |

---

## 7. Audit and events

Every write on M-01 aggregates writes an M-17 event via `IEventLogWriter`. The M-01-published events are:

- `survey.published` — emitted on any transition into `Active` (from `Draft`, `PendingReview`, or `Paused` → `Active` via Reactivate). Payload: `{survey_id, actor_id, from_status, to_status, correlation_id, sanitiser_policy_version}`.
- `survey.archived` — emitted on any transition into `Archived`. Payload: `{survey_id, actor_id, from_status, correlation_id}`.

**New M-17 event required** (see [research.md § 12](./research.md#12-open-items-surfaced-during-phase-0-not-blocking)):

- `survey.responses.purged` — emitted by **M-04** at the tail of `IResponsePurgeService.PurgeSurveyResponsesAsync(...)`, consumed by M-05 / M-06 / M-07 to drop derived aggregates for the survey. Requires a constitution AMENDMENT to Section 4 to register the event.

For everything else — writes on Sections, Questions Sets, Questions, Themes, Templates, Translations, Routing Maps — M-01 records to `M-17.audit_log` via M-17's published `IEventLogWriter` (constitution § 5 — Audit Log owned by M-17; no direct writes to `audit_log`). The event catalogue does not enumerate per-child writes because they are attributed as sub-actions of the enclosing survey operation via the shared `correlation_id`.

---

## 8. Post-design constitution re-check ledger

Referenced in [plan.md § Post-Design Constitution Re-check](./plan.md#post-design-constitution-re-check). Populated after all Phase 1 artefacts land.
