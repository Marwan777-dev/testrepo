-- =============================================================================
-- M-06 CX Metrics & KPI Engine — Tenant-Schema Baseline (T007)
-- =============================================================================
-- DB-02 / AD-02: tenant-schema tables carry NO `tenant_id` column. Isolation is
-- at the PostgreSQL schema level; this script runs once per tenant schema (the
-- bootstrapper / provisioner sets `search_path` to the target `tenant_{slug}`
-- schema before applying). Mirrors src/Nabadat.CustomerJourneyManagement/Migrations.
--
-- Creates the four M-06 tables (data-model.md §1) and seeds the eight standard
-- KPIs in canonical order (data-model.md §4) with their default thresholds.
-- Idempotent: CREATE TABLE IF NOT EXISTS + ON CONFLICT (LOWER(short_name)) DO NOTHING.
--
-- Cross-table FKs are declared only between the four tables M-06 owns. References
-- to externally-owned identifiers — M-10 `user_id` in created_by/updated_by — are
-- logical and documented, not enforced here, so the baseline applies cleanly in a
-- standalone module-test database.
--
-- Tables are created parents-before-children so inline foreign keys resolve.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- kpi_definitions — root KPI definition entity
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS kpi_definitions (
    id                       uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
    short_name               varchar(20)   NOT NULL,
    full_name                varchar(100)  NOT NULL,
    kpi_type                 varchar(16)   NOT NULL,            -- 'Standard' | 'Custom'
    is_composite             boolean       NOT NULL DEFAULT false,
    calculation_method       varchar(32)   NOT NULL,            -- 'WeightedAverage' | 'TopNBox' | 'NPSStandard' | 'WeightedComposite'
    top_n_value              smallint      NULL,                -- required when calculation_method = 'TopNBox'
    scale                    varchar(16)   NULL,                -- 'Scale0_10' | 'Scale1_3' | ... | 'Nps' | NULL for composite
    min_scale_description_en varchar(60)   NULL,
    min_scale_description_ar varchar(60)   NULL,
    max_scale_description_en varchar(60)   NULL,
    max_scale_description_ar varchar(60)   NULL,
    representation_style     varchar(16)   NULL,                -- 'Number' | 'Stars' | 'Emoji' | 'Slider' | NULL for composite
    emoji_set                varchar(32)   NULL,                -- 'FaceClassic' | 'HandThumbs' (when representation_style = 'Emoji')
    target                   numeric(5,1)  NULL,                -- required when is_active = true; range per type
    is_active                boolean       NOT NULL DEFAULT true,
    show_on_dashboard        boolean       NOT NULL DEFAULT false,
    created_at               timestamptz   NOT NULL DEFAULT now(),
    created_by               uuid          NOT NULL,
    updated_at               timestamptz   NOT NULL DEFAULT now(),
    updated_by               uuid          NOT NULL,
    CONSTRAINT kpi_type_valid                CHECK (kpi_type IN ('Standard', 'Custom')),
    CONSTRAINT calc_method_valid             CHECK (calculation_method IN ('WeightedAverage', 'TopNBox', 'NPSStandard', 'WeightedComposite')),
    CONSTRAINT scale_valid                   CHECK (scale IS NULL OR scale IN ('Scale0_10', 'Scale1_3', 'Scale1_5', 'Scale1_7', 'Scale1_10', 'Scale1_100', 'Nps')),
    CONSTRAINT representation_style_valid    CHECK (representation_style IS NULL OR representation_style IN ('Number', 'Stars', 'Emoji', 'Slider')),
    CONSTRAINT emoji_set_valid               CHECK (emoji_set IS NULL OR emoji_set IN ('FaceClassic', 'HandThumbs')),
    CONSTRAINT top_n_required_when_top_n_box CHECK ((calculation_method = 'TopNBox') = (top_n_value IS NOT NULL)),
    CONSTRAINT emoji_set_required_when_emoji CHECK ((representation_style = 'Emoji') = (emoji_set IS NOT NULL)),
    CONSTRAINT scale_null_iff_composite      CHECK (is_composite = (scale IS NULL)),
    -- representation_style is NULL for the composite KPI (CXI) AND for the NPS standard KPI:
    -- NPS keeps a scale (Scale0_10) but renders via the fixed -100..+100 NPS gauge, not a
    -- configurable Number/Stars/Emoji/Slider style, so its representation_style is NULL. This
    -- matches the canonical seed (data-model.md §4) and KpiSeedDataProvider. Every other
    -- non-composite KPI (WeightedAverage / TopNBox) MUST carry a representation_style.
    CONSTRAINT representation_null_iff_composite_or_nps
        CHECK ((representation_style IS NULL) = (is_composite OR calculation_method = 'NPSStandard')),
    CONSTRAINT target_required_when_active   CHECK (NOT is_active OR target IS NOT NULL),
    CONSTRAINT show_on_dashboard_implies_active CHECK (NOT show_on_dashboard OR is_active)
);

-- Case-insensitive Short Name uniqueness per tenant (FR — functional unique index).
CREATE UNIQUE INDEX IF NOT EXISTS kpi_definitions_short_name_lower_uniq
    ON kpi_definitions (LOWER(short_name));

CREATE INDEX IF NOT EXISTS kpi_definitions_is_active_idx ON kpi_definitions (is_active);
CREATE INDEX IF NOT EXISTS kpi_definitions_kpi_type_idx  ON kpi_definitions (kpi_type);
-- Cursor-pagination index (created_at DESC, id) per research.md R8.
CREATE INDEX IF NOT EXISTS kpi_definitions_created_at_id_idx ON kpi_definitions (created_at DESC, id);

-- -----------------------------------------------------------------------------
-- kpi_thresholds — one row per KPI (1:1)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS kpi_thresholds (
    kpi_id       uuid         PRIMARY KEY REFERENCES kpi_definitions(id) ON DELETE RESTRICT,
    lower_bound  numeric(5,1) NOT NULL,
    x            numeric(5,1) NOT NULL,
    y            numeric(5,1) NOT NULL,
    upper_bound  numeric(5,1) NOT NULL,
    CONSTRAINT threshold_ascending
        CHECK (lower_bound < x AND x < y AND y < upper_bound)
);

-- -----------------------------------------------------------------------------
-- kpi_perspectives — 0..10 rows per KPI
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS kpi_perspectives (
    id            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    kpi_id        uuid        NOT NULL REFERENCES kpi_definitions(id) ON DELETE CASCADE,
    label         varchar(60) NOT NULL,
    display_order smallint    NOT NULL,
    created_at    timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS kpi_perspectives_kpi_id_order_idx
    ON kpi_perspectives (kpi_id, display_order);

-- -----------------------------------------------------------------------------
-- cxi_weights — 0..N rows; only populated when the CXI KPI has members
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cxi_weights (
    cxi_kpi_id    uuid        NOT NULL REFERENCES kpi_definitions(id) ON DELETE RESTRICT,
    member_kpi_id uuid        NOT NULL REFERENCES kpi_definitions(id) ON DELETE RESTRICT,
    weight        smallint    NOT NULL,
    created_at    timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (cxi_kpi_id, member_kpi_id),
    CONSTRAINT weight_positive           CHECK (weight > 0),
    CONSTRAINT cxi_cannot_include_itself CHECK (member_kpi_id <> cxi_kpi_id)
);

CREATE INDEX IF NOT EXISTS cxi_weights_member_kpi_id_idx ON cxi_weights (member_kpi_id);  -- cascade lookups

-- -----------------------------------------------------------------------------
-- event_log — shared M-17 audit table (NOT M-06-owned). Created by whichever
-- module baseline runs first (M-10 / M-16 also issue CREATE TABLE IF NOT EXISTS).
-- M-06 writes `settings.changed` rows here in the SAME transaction as the KPI /
-- settings change (data-model.md §8). Idempotent so it is harmless when another
-- module already created it. Mirrors the M-16 baseline definition exactly.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS event_log (
    event_id        uuid         PRIMARY KEY,
    event_type      varchar(64)  NOT NULL,
    actor_id        uuid         NULL,
    actor_persona   varchar(16)  NULL,
    entity_type     varchar(128) NULL,
    entity_id       uuid         NULL,
    old_value       jsonb        NULL,
    new_value       jsonb        NULL,
    occurred_at_utc timestamptz  NOT NULL,
    correlation_id  uuid         NULL
);

-- =============================================================================
-- Seed: the eight standard KPIs in canonical order (NPS, CSAT, CES, CXI, FCR,
-- VFM, AgentScore, CHS) per data-model.md §4. Deterministic UUIDs so the seed is
-- stable and re-runnable. created_by/updated_by = reserved system actor
-- (all-zero UUID) for migration-time writes.
--
-- One INSERT ... ON CONFLICT (LOWER(short_name)) DO NOTHING for the definitions,
-- then the matching threshold rows. NOTE: the functional unique index makes the
-- ON CONFLICT inference target `(LOWER(short_name))`.
-- =============================================================================
INSERT INTO kpi_definitions
    (id, short_name, full_name, kpi_type, is_composite, calculation_method,
     scale, representation_style, target, is_active, show_on_dashboard,
     created_by, updated_by)
VALUES
    ('00000006-0000-0000-0000-000000000001', 'NPS',        'Net Promoter Score',          'Standard', false, 'NPSStandard',       'Scale0_10', NULL,     50,  true, false, '00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000'),
    ('00000006-0000-0000-0000-000000000002', 'CSAT',       'Customer Satisfaction Score', 'Standard', false, 'WeightedAverage',   'Scale1_5',  'Number', 80,  true, false, '00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000'),
    ('00000006-0000-0000-0000-000000000003', 'CES',        'Customer Effort Score',       'Standard', false, 'WeightedAverage',   'Scale1_7',  'Number', 80,  true, false, '00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000'),
    ('00000006-0000-0000-0000-000000000004', 'CXI',        'Customer Experience Index',   'Standard', true,  'WeightedComposite', NULL,        NULL,     80,  true, false, '00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000'),
    ('00000006-0000-0000-0000-000000000005', 'FCR',        'First Contact Resolution',    'Standard', false, 'WeightedAverage',   'Scale1_3',  'Number', 80,  true, false, '00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000'),
    ('00000006-0000-0000-0000-000000000006', 'VFM',        'Value for Money',             'Standard', false, 'WeightedAverage',   'Scale1_5',  'Number', 80,  true, false, '00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000'),
    ('00000006-0000-0000-0000-000000000007', 'AgentScore', 'Agent Score',                 'Standard', false, 'WeightedAverage',   'Scale1_5',  'Number', 80,  true, false, '00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000'),
    ('00000006-0000-0000-0000-000000000008', 'CHS',        'Customer Happiness Score',    'Standard', false, 'WeightedAverage',   'Scale1_5',  'Number', 80,  true, false, '00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000')
ON CONFLICT (LOWER(short_name)) DO NOTHING;

-- Default thresholds. NPS uses (lower=-100, x=0, y=30, upper=100) per Clarifications
-- round 2 Q1; every other standard KPI uses (0, 20, 70, 100). Seeded only for the
-- definition rows that were just inserted (skipped on conflict re-runs).
INSERT INTO kpi_thresholds (kpi_id, lower_bound, x, y, upper_bound)
SELECT d.id,
       CASE WHEN d.short_name = 'NPS' THEN -100 ELSE 0   END,
       CASE WHEN d.short_name = 'NPS' THEN 0    ELSE 20  END,
       CASE WHEN d.short_name = 'NPS' THEN 30   ELSE 70  END,
       100
  FROM kpi_definitions d
 WHERE d.short_name IN ('NPS','CSAT','CES','CXI','FCR','VFM','AgentScore','CHS')
   AND NOT EXISTS (SELECT 1 FROM kpi_thresholds t WHERE t.kpi_id = d.id);
