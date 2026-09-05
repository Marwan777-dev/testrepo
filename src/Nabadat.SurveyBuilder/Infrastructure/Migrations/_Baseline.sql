-- ============================================================================
-- Nabadat.SurveyBuilder (M-01) — tenant-schema baseline (T008, Feature 004)
--
-- DDL for the 9 tables M-01 owns (data-model.md §2.1–2.9): surveys, sections,
-- questions_sets, questions, routing_maps, themes, survey_translations,
-- templates, template_snapshots.
--
-- Applied per tenant into the tenant_{slug} schema. The runner (dev:
-- DevTenantSchemaBootstrapper; tests: SurveyBuilderApplicationFactory) sets
-- `search_path` to the target schema first, so every object below is UNQUALIFIED.
-- No `tenant_id` columns anywhere (DB-02) — isolation is schema-per-tenant (AD-02).
-- EF Core generates no migrations (DB-08 rule 6); TenantDbContext maps onto this file.
-- Idempotency: the runner gates this whole script on the `surveys` sentinel table,
-- so it runs once per schema. Keep statements additive-safe regardless.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 2.1 surveys — aggregate root
-- ----------------------------------------------------------------------------
CREATE TABLE surveys (
    id                        uuid        NOT NULL PRIMARY KEY,
    name_en                   text        NOT NULL,
    description               text        NULL,
    survey_type               text        NOT NULL,
    bound_journey_id          uuid        NULL,
    status                    text        NOT NULL,
    owner_user_id             uuid        NOT NULL,
    submitted_by              uuid        NULL,
    submitted_at              timestamptz NULL,
    reviewed_by               uuid        NULL,
    reviewed_at               timestamptz NULL,
    review_remarks            text        NULL,
    theme_mode                text        NOT NULL DEFAULT 'Inherited',
    welcome_html              text        NULL,
    thanks_html               text        NULL,
    sanitiser_policy_version  int4        NOT NULL DEFAULT 1,
    redirect_url              text        NULL,
    redirect_after_s          int4        NOT NULL DEFAULT 0,
    layout                    text        NOT NULL DEFAULT 'section',
    questions_per_page        int4        NULL,
    active_period             jsonb       NULL,
    activated_at              timestamptz NULL,
    record_time               bool        NOT NULL DEFAULT true,
    shuffle                   bool        NOT NULL DEFAULT false,
    shuffle_mode              text        NOT NULL DEFAULT 'random',
    routing_on                bool        NOT NULL DEFAULT false,
    theme_logo_file_handle    text        NULL,
    created_at                timestamptz NOT NULL,
    created_by                uuid        NOT NULL,
    updated_at                timestamptz NOT NULL,
    updated_by                uuid        NOT NULL,
    row_version               int4        NOT NULL DEFAULT 1,

    CONSTRAINT ck_surveys_name_en_length      CHECK (char_length(name_en) BETWEEN 1 AND 200),
    CONSTRAINT ck_surveys_survey_type         CHECK (survey_type IN ('Transactional', 'SeasonalRelational')),
    CONSTRAINT ck_surveys_status              CHECK (status IN ('Draft', 'PendingReview', 'Active', 'Paused', 'Archived')),
    CONSTRAINT ck_surveys_theme_mode          CHECK (theme_mode IN ('Inherited', 'Customized')),
    CONSTRAINT ck_surveys_layout              CHECK (layout IN ('single', 'section', 'question', 'count')),
    CONSTRAINT ck_surveys_shuffle_mode        CHECK (shuffle_mode IN ('random', 'low_response')),
    -- BR-3.3: a bound journey forces Transactional.
    CONSTRAINT ck_surveys_bound_journey_type  CHECK (bound_journey_id IS NULL OR survey_type = 'Transactional'),
    -- layout=count ⇒ questions_per_page present and ≥ 1.
    CONSTRAINT ck_surveys_count_layout        CHECK (layout <> 'count' OR (questions_per_page IS NOT NULL AND questions_per_page >= 1)),
    -- routing_on ⇒ layout=question AND shuffle off (F9 coupling).
    CONSTRAINT ck_surveys_routing_coupling    CHECK (routing_on = false OR (layout = 'question' AND shuffle = false))
);

CREATE INDEX idx_surveys_status_updated_at ON surveys (status, updated_at DESC);
CREATE INDEX idx_surveys_bound_journey_id  ON surveys (bound_journey_id);
CREATE INDEX idx_surveys_owner_user_id     ON surveys (owner_user_id);
CREATE INDEX idx_surveys_name_en_lower     ON surveys (LOWER(name_en) text_pattern_ops);

-- ----------------------------------------------------------------------------
-- 2.2 sections
-- ----------------------------------------------------------------------------
CREATE TABLE sections (
    id          uuid        NOT NULL PRIMARY KEY,
    survey_id   uuid        NOT NULL REFERENCES surveys (id) ON DELETE CASCADE,
    name        text        NOT NULL,
    description text        NULL,
    "order"     int4        NOT NULL,
    created_at  timestamptz NOT NULL,
    updated_at  timestamptz NOT NULL,
    row_version int4        NOT NULL DEFAULT 1,

    CONSTRAINT ck_sections_name_length CHECK (char_length(name) BETWEEN 1 AND 200)
);

-- (survey_id, order) unique + the F1 ordering index.
CREATE UNIQUE INDEX idx_sections_survey_id_order ON sections (survey_id, "order");

-- ----------------------------------------------------------------------------
-- 2.3 questions_sets — rotating pool inside a section (F10)
-- ----------------------------------------------------------------------------
CREATE TABLE questions_sets (
    id             uuid        NOT NULL PRIMARY KEY,
    section_id     uuid        NOT NULL REFERENCES sections (id) ON DELETE CASCADE,
    title          text        NOT NULL,
    description    text        NULL,
    selection_mode text        NOT NULL DEFAULT 'random',
    count          int4        NOT NULL,
    "order"        int4        NOT NULL,
    created_at     timestamptz NOT NULL,
    updated_at     timestamptz NOT NULL,
    row_version    int4        NOT NULL DEFAULT 1,

    CONSTRAINT ck_questions_sets_title_length   CHECK (char_length(title) BETWEEN 1 AND 200),
    CONSTRAINT ck_questions_sets_selection_mode  CHECK (selection_mode IN ('random', 'low_response')),
    -- count <= size(set) is enforced at the App layer (cross-row); the floor is enforced here.
    CONSTRAINT ck_questions_sets_count_floor     CHECK (count >= 0)
);

CREATE INDEX idx_questions_sets_section_id ON questions_sets (section_id);

-- ----------------------------------------------------------------------------
-- 2.4 questions — single-table with per-type jsonb payload (research.md §5)
-- ----------------------------------------------------------------------------
CREATE TABLE questions (
    id                 uuid        NOT NULL PRIMARY KEY,
    survey_id          uuid        NOT NULL REFERENCES surveys (id)  ON DELETE CASCADE,
    section_id         uuid        NOT NULL REFERENCES sections (id) ON DELETE CASCADE,
    set_id             uuid        NULL     REFERENCES questions_sets (id) ON DELETE CASCADE,
    type               text        NOT NULL,
    subtype            text        NOT NULL,
    text               text        NOT NULL,
    description        text        NULL,
    required           bool        NOT NULL DEFAULT false,
    comments           bool        NOT NULL DEFAULT false,
    comment_label      text        NOT NULL DEFAULT 'Comments',
    comment_max_length int4        NOT NULL DEFAULT 200,
    sentiment          bool        NOT NULL DEFAULT false,
    kpi_code           text        NULL,
    perspective        text        NULL,
    bound_journey_on   bool        NOT NULL DEFAULT true,
    stage_id           uuid        NULL,
    touchpoint_id      uuid        NULL,
    type_payload       jsonb       NOT NULL,
    "order"            int4        NOT NULL,
    created_at         timestamptz NOT NULL,
    updated_at         timestamptz NOT NULL,
    row_version        int4        NOT NULL DEFAULT 1,

    CONSTRAINT ck_questions_type CHECK (type IN ('Scale', 'InputField', 'SingleSelect', 'MultiSelect', 'YesNo', 'Matrix', 'Ranking', 'KPI')),
    -- KPI questions (and Matrix KPI-scale mode) require a kpi_code.
    CONSTRAINT ck_questions_kpi_code_present CHECK (
        NOT (type = 'KPI' OR (type = 'Matrix' AND subtype = 'KPIScale')) OR kpi_code IS NOT NULL
    ),
    -- FR-8.4: a touchpoint requires a stage when the journey binding is on.
    CONSTRAINT ck_questions_touchpoint_requires_stage CHECK (
        NOT (bound_journey_on = true AND touchpoint_id IS NOT NULL) OR stage_id IS NOT NULL
    ),
    -- BR-8.2: binding off ⇒ no stage/touchpoint retained.
    CONSTRAINT ck_questions_binding_off_clears CHECK (
        bound_journey_on = true OR (stage_id IS NULL AND touchpoint_id IS NULL)
    )
);

CREATE INDEX idx_questions_survey_id        ON questions (survey_id);
CREATE INDEX idx_questions_section_id_order ON questions (section_id, "order");
CREATE INDEX idx_questions_set_id_order     ON questions (set_id, "order") WHERE set_id IS NOT NULL;
CREATE INDEX idx_questions_kpi_code         ON questions (kpi_code)        WHERE kpi_code IS NOT NULL;

-- ----------------------------------------------------------------------------
-- 2.5 routing_maps — sparse per-answer routing overrides (F9)
-- ----------------------------------------------------------------------------
CREATE TABLE routing_maps (
    id                 uuid        NOT NULL PRIMARY KEY,
    survey_id          uuid        NOT NULL REFERENCES surveys (id)   ON DELETE CASCADE,
    source_question_id uuid        NOT NULL REFERENCES questions (id) ON DELETE CASCADE,
    answer_key         text        NOT NULL,
    target_question_id uuid        NULL     REFERENCES questions (id) ON DELETE SET NULL,
    created_at         timestamptz NOT NULL,
    updated_at         timestamptz NOT NULL
    -- Same-survey (source/target share survey_id) and standalone-only (set_id IS NULL)
    -- invariants are cross-row and enforced at the App layer (RoutingEligibilityService).
);

CREATE UNIQUE INDEX idx_routing_maps_source_answer   ON routing_maps (source_question_id, answer_key);
CREATE INDEX        idx_routing_maps_survey_id        ON routing_maps (survey_id);
CREATE INDEX        idx_routing_maps_target_question_id ON routing_maps (target_question_id);

-- ----------------------------------------------------------------------------
-- 2.6 themes — per-survey F4 customisation (1:1, only when theme_mode=Customized)
-- ----------------------------------------------------------------------------
CREATE TABLE themes (
    id                  uuid        NOT NULL PRIMARY KEY,
    survey_id           uuid        NOT NULL REFERENCES surveys (id) ON DELETE CASCADE,
    primary_color       text        NULL,
    text_color          text        NULL,
    button_radius_px    int4        NULL,
    button_border_color text        NULL,
    button_text_color   text        NULL,
    header_show_logo    bool        NOT NULL DEFAULT true,
    header_show_title   bool        NOT NULL DEFAULT true,
    header_alignment    text        NOT NULL DEFAULT 'start',
    footer_text         text        NULL,
    background_type     text        NOT NULL DEFAULT 'Solid',
    background_config   jsonb       NULL,
    background_opacity  int4        NOT NULL DEFAULT 100,
    advanced_status_colors jsonb    NULL,
    advanced_surfaces   jsonb       NULL,
    advanced_typography jsonb       NULL,
    advanced_layout     jsonb       NULL,
    created_at          timestamptz NOT NULL,
    updated_at          timestamptz NOT NULL,
    row_version         int4        NOT NULL DEFAULT 1,

    CONSTRAINT ck_themes_header_alignment CHECK (header_alignment IN ('start', 'center', 'end')),
    CONSTRAINT ck_themes_background_type  CHECK (background_type IN ('Solid', 'Gradient', 'Image', 'Pattern')),
    CONSTRAINT ck_themes_background_opacity CHECK (background_opacity BETWEEN 0 AND 100)
);

CREATE UNIQUE INDEX idx_themes_survey_id ON themes (survey_id);

-- ----------------------------------------------------------------------------
-- 2.7 survey_translations — per-locale bundle (one row per (survey_id, locale))
-- ----------------------------------------------------------------------------
CREATE TABLE survey_translations (
    id          uuid        NOT NULL PRIMARY KEY,
    survey_id   uuid        NOT NULL REFERENCES surveys (id) ON DELETE CASCADE,
    locale      text        NOT NULL,
    keys        jsonb       NOT NULL,
    created_at  timestamptz NOT NULL,
    updated_at  timestamptz NOT NULL,
    row_version int4        NOT NULL DEFAULT 1
);

CREATE UNIQUE INDEX idx_survey_translations_survey_locale ON survey_translations (survey_id, locale);

-- ----------------------------------------------------------------------------
-- 2.8 templates — full snapshot of a survey's authoring state (Q4/BR-7.1)
-- ----------------------------------------------------------------------------
CREATE TABLE templates (
    id                             uuid        NOT NULL PRIMARY KEY,
    class                          text        NOT NULL,
    name_en                        text        NOT NULL,
    name_ar                        text        NULL,
    description                    text        NULL,
    tags                           text[]      NOT NULL DEFAULT '{}'::text[],
    sectors                        text[]      NOT NULL DEFAULT '{}'::text[],
    preview_thumbnail_file_handle  text        NULL,
    created_at                     timestamptz NOT NULL,
    created_by                     uuid        NULL,
    updated_at                     timestamptz NOT NULL,
    updated_by                     uuid        NULL,
    row_version                    int4        NOT NULL DEFAULT 1,

    CONSTRAINT ck_templates_class CHECK (class IN ('BuiltIn', 'Customized')),
    -- BuiltIn: system-authored, no tags. Customized: authored, no sectors.
    CONSTRAINT ck_templates_builtin_shape CHECK (
        class <> 'BuiltIn' OR (created_by IS NULL AND updated_by IS NULL AND cardinality(tags) = 0)
    ),
    CONSTRAINT ck_templates_customized_shape CHECK (
        class <> 'Customized' OR (cardinality(sectors) = 0 AND created_by IS NOT NULL)
    )
);

CREATE INDEX idx_templates_class_name_en ON templates (class, LOWER(name_en) text_pattern_ops);
CREATE INDEX idx_templates_tags_gin      ON templates USING gin (tags);
CREATE INDEX idx_templates_sectors_gin   ON templates USING gin (sectors);

-- ----------------------------------------------------------------------------
-- 2.9 template_snapshots — authoritative jsonb payload attached to a template
-- ----------------------------------------------------------------------------
CREATE TABLE template_snapshots (
    template_id    uuid        NOT NULL PRIMARY KEY REFERENCES templates (id) ON DELETE CASCADE,
    snapshot       jsonb       NOT NULL,
    schema_version int4        NOT NULL DEFAULT 1,
    created_at     timestamptz NOT NULL,

    CONSTRAINT ck_template_snapshots_schema_version CHECK (schema_version >= 1)
);
