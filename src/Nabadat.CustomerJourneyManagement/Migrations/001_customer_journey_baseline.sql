-- =============================================================================
-- M-16 Customer Journey Mapping — Tenant-Schema Baseline (T012)
-- =============================================================================
-- DB-02 / AD-02: tenant-schema tables carry NO `tenant_id` column. Isolation is
-- at the PostgreSQL schema level; this script runs once per tenant schema (the
-- runner sets `search_path` to the target `tenant_{slug}` schema before applying).
--
-- Cross-table foreign keys are declared only between the 13 tables M-16 owns
-- within this schema. References to externally-owned identifiers — M-10
-- `user_id` values in `created_by`/`updated_by`/`published_by`, and M-17's
-- `event_log` — are logical and documented, not enforced here, so the baseline
-- applies cleanly in a standalone module-test database.
--
-- Tables are created in dependency order (parents before children) so the inline
-- foreign keys resolve without deferral.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- journeys — root customer-journey entity
-- -----------------------------------------------------------------------------
CREATE TABLE journeys (
    journey_id    uuid          PRIMARY KEY,
    name          varchar(255)  NOT NULL,
    description   text          NULL,
    journey_type  varchar(64)   NOT NULL,
    status        varchar(16)   NOT NULL DEFAULT 'Draft',
    created_by    uuid          NOT NULL,
    updated_by    uuid          NULL,
    created_at    timestamptz   NOT NULL,
    updated_at    timestamptz   NOT NULL
);

-- Case-insensitive name uniqueness for non-Archived journeys; Archived rows
-- release their name for reuse. Functional + partial → must be an index, not a
-- table constraint. NOTE: in production this index is built with
-- CREATE UNIQUE INDEX CONCURRENTLY to avoid table locking; CONCURRENTLY cannot
-- run inside the migration's transaction, so the baseline builds it normally
-- (the table is empty at baseline time, so the lock is momentary).
CREATE UNIQUE INDEX idx_journeys_name_ci ON journeys (LOWER(name)) WHERE status <> 'Archived';
CREATE INDEX ix_journeys_status ON journeys (status);

-- -----------------------------------------------------------------------------
-- personas — reusable customer archetypes (Draft → Active ↔ Inactive → Archived)
-- -----------------------------------------------------------------------------
CREATE TABLE personas (
    persona_id      uuid          PRIMARY KEY,
    name_ar         varchar(255)  NOT NULL,
    name_en         varchar(255)  NOT NULL,
    description_ar  text          NULL,
    description_en  text          NULL,
    status          varchar(16)   NOT NULL DEFAULT 'Draft',
    created_by      uuid          NOT NULL,
    updated_by      uuid          NULL,
    created_at      timestamptz   NOT NULL,
    updated_at      timestamptz   NOT NULL
);

CREATE INDEX ix_personas_status ON personas (status);

-- -----------------------------------------------------------------------------
-- kpi_type_definitions — tenant-defined custom KPI types (platform-standard
-- types are built in and NOT stored here)
-- -----------------------------------------------------------------------------
CREATE TABLE kpi_type_definitions (
    kpi_type_definition_id  uuid          PRIMARY KEY,
    type_key                varchar(64)   NOT NULL,
    label_ar                varchar(255)  NOT NULL,
    label_en                varchar(255)  NOT NULL,
    scoring_direction       varchar(16)   NOT NULL DEFAULT 'Ascending',
    created_at              timestamptz   NOT NULL,
    updated_at              timestamptz   NOT NULL,
    CONSTRAINT uq_kpi_type_definitions_type_key UNIQUE (type_key)
);

-- -----------------------------------------------------------------------------
-- stages — ordered phases within a journey
-- -----------------------------------------------------------------------------
CREATE TABLE stages (
    stage_id         uuid          PRIMARY KEY,
    journey_id       uuid          NOT NULL REFERENCES journeys (journey_id) ON DELETE CASCADE,
    sequence_number  integer       NOT NULL,
    name             varchar(255)  NOT NULL,
    description      text          NULL,
    customer_goal    text          NULL,
    expected_emotion varchar(64)   NULL,
    duration_hint    varchar(64)   NULL,
    created_at       timestamptz   NOT NULL,
    updated_at       timestamptz   NOT NULL,
    CONSTRAINT uq_stages_journey_sequence UNIQUE (journey_id, sequence_number)
);

CREATE INDEX ix_stages_journey_id ON stages (journey_id);

-- -----------------------------------------------------------------------------
-- touchpoints — journey-local interaction points within a stage
-- -----------------------------------------------------------------------------
CREATE TABLE touchpoints (
    touchpoint_id  uuid          PRIMARY KEY,
    stage_id       uuid          NOT NULL REFERENCES stages (stage_id) ON DELETE CASCADE,
    name           varchar(255)  NOT NULL,
    description    text          NULL,
    channels       text[]        NOT NULL DEFAULT '{}',
    importance     varchar(16)   NOT NULL DEFAULT 'Medium',
    is_mot         boolean       NOT NULL DEFAULT false,
    is_mandatory   boolean       NOT NULL DEFAULT false,
    created_at     timestamptz   NOT NULL,
    updated_at     timestamptz   NOT NULL
);

CREATE INDEX ix_touchpoints_stage_id ON touchpoints (stage_id);
CREATE INDEX ix_touchpoints_is_mot ON touchpoints (is_mot);

-- -----------------------------------------------------------------------------
-- kpi_bindings — KPI assignments on a touchpoint (all bindings sum to 100%,
-- enforced at the service layer via full-replace save)
-- -----------------------------------------------------------------------------
CREATE TABLE kpi_bindings (
    kpi_binding_id        uuid           PRIMARY KEY,
    touchpoint_id         uuid           NOT NULL REFERENCES touchpoints (touchpoint_id) ON DELETE CASCADE,
    kpi_type              varchar(64)    NOT NULL,
    is_platform_standard  boolean        NOT NULL,
    weight                numeric(5,2)   NOT NULL,
    -- Logical reference to M-06's kpi_definitions.id (Feature 003 / T020). NOT an enforced FK:
    -- kpi_definitions is owned by a separate module (M-06) and provisioned by its own baseline,
    -- which has not run when this M-16 baseline applies — so the link is documented + app-enforced,
    -- matching the convention used for created_by/updated_by (M-10 user_id). Lets M-06's
    -- IJourneyBindingQuery count touchpoints/journeys for a given KPI id (FR-026 / FR-017).
    kpi_id                uuid           NULL,
    created_at            timestamptz    NOT NULL,
    updated_at            timestamptz    NOT NULL,
    CONSTRAINT chk_kpi_bindings_weight_range CHECK (weight > 0 AND weight <= 100),
    CONSTRAINT uq_kpi_bindings_touchpoint_type UNIQUE (touchpoint_id, kpi_type)
);

-- Supports M-06's binding-usage probe (IJourneyBindingQuery): "for KPI X, how many touchpoints
-- / journeys bind it?" — keyed on the logical kpi_id reference above.
CREATE INDEX ix_kpi_bindings_kpi_id ON kpi_bindings (kpi_id);

CREATE INDEX ix_kpi_bindings_touchpoint_id ON kpi_bindings (touchpoint_id);

-- -----------------------------------------------------------------------------
-- scoring_configs — tenant-level strategic scoring parameters (SINGLETON: one row per tenant).
-- SRS §4.2.9 / §11.7 (Q11 RESOLVED — per-tenant, NOT per-journey). All journeys in the tenant share
-- these five parameters; β is derived (1 − α), never stored. No journey_id / no tenant_id (the schema
-- boundary is the tenant scope, AD-02).
-- -----------------------------------------------------------------------------
CREATE TABLE scoring_configs (
    scoring_config_id    uuid          PRIMARY KEY,
    alpha                numeric(4,3)  NOT NULL DEFAULT 0.500,
    mot_multiplier       numeric(3,1)  NOT NULL DEFAULT 1.5,
    n_floor              integer       NOT NULL DEFAULT 100,
    flag_percentile      integer       NOT NULL DEFAULT 25,
    rolling_window_days  integer       NOT NULL DEFAULT 30,
    created_at           timestamptz   NOT NULL,
    updated_at           timestamptz   NOT NULL,
    updated_by           uuid          NOT NULL,
    CONSTRAINT ck_scoring_configs_alpha           CHECK (alpha BETWEEN 0.000 AND 1.000),
    CONSTRAINT ck_scoring_configs_mot             CHECK (mot_multiplier BETWEEN 1.0 AND 2.0),
    CONSTRAINT ck_scoring_configs_n_floor         CHECK (n_floor >= 1),
    CONSTRAINT ck_scoring_configs_flag_percentile CHECK (flag_percentile BETWEEN 1 AND 49),
    CONSTRAINT ck_scoring_configs_rolling_window  CHECK (rolling_window_days >= 7)
);

-- Singleton: at most one row per tenant schema.
CREATE UNIQUE INDEX scoring_configs_singleton_uniq ON scoring_configs ((true));

-- Seed the tenant's default scoring parameters (system actor; updated_by = all-zeros).
INSERT INTO scoring_configs (scoring_config_id, created_at, updated_at, updated_by)
VALUES (gen_random_uuid(), now(), now(), '00000000-0000-0000-0000-000000000000');

-- -----------------------------------------------------------------------------
-- journey_persona_bindings — N:M join (only Active personas bound, enforced at
-- the service layer)
-- -----------------------------------------------------------------------------
CREATE TABLE journey_persona_bindings (
    journey_id  uuid         NOT NULL REFERENCES journeys (journey_id) ON DELETE CASCADE,
    persona_id  uuid         NOT NULL REFERENCES personas (persona_id),
    bound_at    timestamptz  NOT NULL,
    CONSTRAINT pk_journey_persona_bindings PRIMARY KEY (journey_id, persona_id)
);

-- Supports the persona archive guard (count active bindings for a persona) and
-- selector queries filtered by persona_id; the composite PK only serves the
-- journey_id-leading access pattern.
CREATE INDEX ix_journey_persona_bindings_persona_id ON journey_persona_bindings (persona_id);

-- -----------------------------------------------------------------------------
-- journey_versions — immutable published snapshots (written once, never updated;
-- ON DELETE RESTRICT prevents hard-deleting a journey that has versions)
-- -----------------------------------------------------------------------------
CREATE TABLE journey_versions (
    version_id        uuid         PRIMARY KEY,
    journey_id        uuid         NOT NULL REFERENCES journeys (journey_id) ON DELETE RESTRICT,
    version_number    integer      NOT NULL,
    published_by      uuid         NOT NULL,
    published_at      timestamptz  NOT NULL,
    snapshot_payload  jsonb        NOT NULL,
    CONSTRAINT uq_journey_versions_journey_version UNIQUE (journey_id, version_number)
);

CREATE INDEX ix_journey_versions_journey_id ON journey_versions (journey_id);

-- -----------------------------------------------------------------------------
-- detection_configs — journey-level pain/happy thresholds (1:1)
-- (pain_threshold < happy_threshold enforced at the service layer)
-- -----------------------------------------------------------------------------
CREATE TABLE detection_configs (
    detection_config_id  uuid          PRIMARY KEY,
    journey_id           uuid          NOT NULL REFERENCES journeys (journey_id) ON DELETE CASCADE,
    pain_threshold       numeric(5,2)  NOT NULL,
    happy_threshold      numeric(5,2)  NOT NULL,
    created_at           timestamptz   NOT NULL,
    updated_at           timestamptz   NOT NULL,
    CONSTRAINT chk_detection_configs_pain_range  CHECK (pain_threshold  >= 0 AND pain_threshold  <= 100),
    CONSTRAINT chk_detection_configs_happy_range CHECK (happy_threshold >= 0 AND happy_threshold <= 100),
    CONSTRAINT uq_detection_configs_journey_id UNIQUE (journey_id)
);

-- -----------------------------------------------------------------------------
-- detection_threshold_overrides — per-stage / per-touchpoint overrides
-- (most-specific wins: touchpoint > stage > journey; null fields inherit parent)
-- scope_id is a polymorphic reference (stage_id OR touchpoint_id) — no FK;
-- existence is enforced at the service layer.
-- -----------------------------------------------------------------------------
CREATE TABLE detection_threshold_overrides (
    override_id          uuid          PRIMARY KEY,
    detection_config_id  uuid          NOT NULL REFERENCES detection_configs (detection_config_id) ON DELETE CASCADE,
    scope_type           varchar(16)   NOT NULL,
    scope_id             uuid          NOT NULL,
    pain_threshold       numeric(5,2)  NULL,
    happy_threshold      numeric(5,2)  NULL,
    created_at           timestamptz   NOT NULL,
    updated_at           timestamptz   NOT NULL,
    CONSTRAINT chk_detection_overrides_scope_type   CHECK (scope_type IN ('stage', 'touchpoint')),
    CONSTRAINT chk_detection_overrides_pain_range   CHECK (pain_threshold  IS NULL OR (pain_threshold  >= 0 AND pain_threshold  <= 100)),
    CONSTRAINT chk_detection_overrides_happy_range  CHECK (happy_threshold IS NULL OR (happy_threshold >= 0 AND happy_threshold <= 100)),
    CONSTRAINT uq_detection_overrides_config_scope  UNIQUE (detection_config_id, scope_type, scope_id)
);

CREATE INDEX ix_detection_threshold_overrides_detection_config_id ON detection_threshold_overrides (detection_config_id);

-- -----------------------------------------------------------------------------
-- report_contracts — M-07 report metadata per journey (1:1, rebuilt on config
-- writes; read by M-07 via IReportContractReader)
-- -----------------------------------------------------------------------------
CREATE TABLE report_contracts (
    report_contract_id  uuid          PRIMARY KEY,
    journey_id          uuid          NOT NULL REFERENCES journeys (journey_id) ON DELETE CASCADE,
    contract_payload    jsonb         NOT NULL,
    generated_at        timestamptz   NOT NULL,
    created_at          timestamptz   NOT NULL,
    updated_at          timestamptz   NOT NULL,
    CONSTRAINT uq_report_contracts_journey_id UNIQUE (journey_id)
);

-- -----------------------------------------------------------------------------
-- journey_scores — latest computed score snapshot per journey (1:1, upserted on
-- IJourneyScoreProvider.GetScoresAsync via INSERT ... ON CONFLICT (journey_id))
-- -----------------------------------------------------------------------------
CREATE TABLE journey_scores (
    journey_score_id   uuid          PRIMARY KEY,
    journey_id         uuid          NOT NULL REFERENCES journeys (journey_id) ON DELETE CASCADE,
    computed_at        timestamptz   NOT NULL,
    journey_score      numeric(5,2)  NULL,
    stage_scores       jsonb         NULL,
    touchpoint_scores  jsonb         NULL,
    CONSTRAINT uq_journey_scores_journey_id UNIQUE (journey_id)
);

-- -----------------------------------------------------------------------------
-- event_log — owned by M-17 (Audit). Created here (idempotent safeguard) so
-- M-16's transactional event writes (M17EventPublisher, FR-015) are testable in
-- a standalone module DB. Mirrors the M-10 baseline definition exactly; when
-- M-17 ships its own baseline this block becomes a no-op.
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

CREATE INDEX IF NOT EXISTS ix_event_log_event_type ON event_log (event_type);
CREATE INDEX IF NOT EXISTS ix_event_log_occurred_at_utc ON event_log (occurred_at_utc);
CREATE INDEX IF NOT EXISTS ix_event_log_actor_id ON event_log (actor_id);
CREATE INDEX IF NOT EXISTS ix_event_log_entity_id ON event_log (entity_id);
