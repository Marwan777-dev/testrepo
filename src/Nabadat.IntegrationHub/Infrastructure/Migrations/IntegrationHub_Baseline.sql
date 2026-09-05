-- =============================================================================
-- Nabadat.IntegrationHub (M-13) — tenant-schema baseline (T009, feature 006)
-- =============================================================================
-- DDL for the 8 tables M-13 owns (data-model.md §1–8): service_channels,
-- parameters, channel_parameter_assignments, parameter_mappings,
-- unmapped_value_occurrences, integrations, credentials,
-- integration_request_logs. Plus the shared M-17 `event_log` (NOT M-13-owned,
-- created IF NOT EXISTS so a standalone M-13 test schema has it) and the seed
-- for the 23 normative built-in parameters (FR-F0-10 / BR-23).
--
-- DB-02 / AD-02: tenant-schema tables carry NO `tenant_id` column — isolation is
-- schema-per-tenant. The runner (dev: DevTenantSchemaBootstrapper; tests:
-- IntegrationHubApplicationFactory) points `search_path` at the target
-- `tenant_{slug}` schema first, so every object below is UNQUALIFIED.
-- EF Core generates no migrations (DB-08 rule 6); TenantDbContext maps onto this
-- file. Idempotent throughout (CREATE ... IF NOT EXISTS + ON CONFLICT DO NOTHING),
-- and the dev runner additionally gates the whole script on the `service_channels`
-- sentinel table.
--
-- Enum-valued columns are stored as the snake_case wire values from
-- data-model.md (`redirect_link`, `oauth_client`, `built_in`, `date_time`, …),
-- NOT the PascalCase .NET member names — the Configurations/*Converter.cs value
-- converters do that mapping, and the CHECK constraints below pin it.
--
-- Tables are created parents-before-children so inline foreign keys resolve.
-- Cross-module identifiers (M-10 `user_id` in created_by/generated_by) are
-- logical references, never enforced FKs (Article 4.1), so this baseline applies
-- cleanly to a standalone module-test database.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- §3 service_channels — the root of the transaction data model.
-- No delete transition exists (BR-07); `channel_id_locked` is a one-way flag set
-- on the channel's first 2xx request (BR-05) and is the enforcement mechanism for
-- both the ID lock and BR-07's "channels with traffic history cannot be deleted".
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS service_channels (
    id                 uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    name_en            text        NOT NULL,
    name_ar            text        NOT NULL,
    channel_id         text        NOT NULL,
    description        text        NULL,
    active             boolean     NOT NULL DEFAULT true,
    channel_id_locked  boolean     NOT NULL DEFAULT false,
    created_at         timestamptz NOT NULL DEFAULT now(),
    updated_at         timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_service_channels_name_en_length CHECK (char_length(name_en) BETWEEN 1 AND 50),
    CONSTRAINT ck_service_channels_name_ar_length CHECK (char_length(name_ar) BETWEEN 1 AND 50),
    -- VR-F04 / BR-04: [A-Za-z0-9-]+, capped at 19 characters ("under 20").
    CONSTRAINT ck_service_channels_channel_id_format CHECK (channel_id ~ '^[A-Za-z0-9-]+$'),
    CONSTRAINT ck_service_channels_channel_id_length CHECK (char_length(channel_id) BETWEEN 1 AND 19)
);

-- VR-F02 (EN name) and VR-F04 (channel ID) are unique per tenant CASE-INSENSITIVELY.
CREATE UNIQUE INDEX IF NOT EXISTS service_channels_name_en_lower_uniq    ON service_channels (LOWER(name_en));
CREATE UNIQUE INDEX IF NOT EXISTS service_channels_channel_id_lower_uniq ON service_channels (LOWER(channel_id));
-- ...but the URL is matched EXACTLY as entered (VR-F04), so the hot inbound resolve
-- path needs its own literal-cased index — the LOWER() one above cannot serve it.
CREATE INDEX IF NOT EXISTS service_channels_channel_id_idx ON service_channels (channel_id);
CREATE INDEX IF NOT EXISTS service_channels_active_idx     ON service_channels (active);

-- -----------------------------------------------------------------------------
-- §4 parameters — the tenant's catalogue: 23 built-ins (seeded at the bottom) +
-- custom parameters. `data_type_locked` is NOT a column: it is derived from
-- `origin = 'built_in'` in the entity ([PO-G27], BR-09) so the two cannot drift.
-- No hard-delete transition exists for either origin (BR-09).
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS parameters (
    id                   uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
    name_en              text          NOT NULL,
    name_ar              text          NOT NULL,
    api_field            text          NOT NULL,
    api_field_locked     boolean       NOT NULL DEFAULT false,
    data_type            text          NOT NULL,
    range_min            numeric       NULL,
    range_max            numeric       NULL,
    range_unit           text          NULL,
    validation_rule      text          NULL,
    origin               text          NOT NULL,
    enabled              boolean       NOT NULL DEFAULT true,
    required_by_default  boolean       NOT NULL DEFAULT false,
    filterable           boolean       NOT NULL DEFAULT true,
    reporting_visibility boolean       NOT NULL DEFAULT true,
    dashboard_visibility boolean       NOT NULL DEFAULT false,
    mapping_support      boolean       NOT NULL DEFAULT false,
    created_at           timestamptz   NOT NULL DEFAULT now(),
    updated_at           timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_parameters_name_en_length CHECK (char_length(name_en) BETWEEN 1 AND 50),
    CONSTRAINT ck_parameters_name_ar_length CHECK (char_length(name_ar) BETWEEN 1 AND 50),
    -- BR-11: snake_case wire key.
    CONSTRAINT ck_parameters_api_field_format CHECK (api_field ~ '^[a-z][a-z0-9_]*$'),
    -- FR-F0-04: the type list is CLOSED — 13 types, and `duration` / `identifier`
    -- must never appear ([PO-G17]). Adding a member here is a spec change.
    CONSTRAINT ck_parameters_data_type CHECK (data_type IN (
        'text', 'number', 'boolean', 'email', 'phone', 'list', 'range',
        'date', 'date_time', 'currency', 'percentage', 'url', 'geolocation')),
    CONSTRAINT ck_parameters_origin CHECK (origin IN ('built_in', 'custom')),
    -- VR-F07: min < max, and the Range sub-configuration exists only for Range.
    CONSTRAINT ck_parameters_range_bounds CHECK (
        range_min IS NULL OR range_max IS NULL OR range_min < range_max),
    CONSTRAINT ck_parameters_range_only_for_range CHECK (
        data_type = 'range' OR (range_min IS NULL AND range_max IS NULL AND range_unit IS NULL)),
    -- BR-27 / [PO-G25]: mapping support is determined by the data type. List is
    -- always on; text/boolean/url may opt in; every other type cannot.
    CONSTRAINT ck_parameters_mapping_support_by_type CHECK (
        (data_type = 'list'  AND mapping_support = true) OR
        (data_type IN ('text', 'boolean', 'url')) OR
        (data_type NOT IN ('list', 'text', 'boolean', 'url') AND mapping_support = false))
);

-- VR-F06: unique per tenant across built-in + custom + enabled + disabled — a
-- disabled parameter still reserves its API field name. The format CHECK above
-- already forces lower case, so a plain unique index is sufficient.
CREATE UNIQUE INDEX IF NOT EXISTS parameters_api_field_uniq ON parameters (api_field);
-- SCR-05's origin-tab + type-filter combination (FR-S5-01).
CREATE INDEX IF NOT EXISTS parameters_origin_data_type_idx ON parameters (origin, data_type);

-- -----------------------------------------------------------------------------
-- §5 channel_parameter_assignments — the channel contract. THIS table, not
-- parameters.required_by_default, is the authority on requiredness at request
-- time (BR-08). Composite PK is allowed because neither half is a tenant
-- identifier (DB-03 forbids only tenant-identifier composites).
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS channel_parameter_assignments (
    service_channel_id uuid    NOT NULL REFERENCES service_channels (id) ON DELETE CASCADE,
    parameter_id       uuid    NOT NULL REFERENCES parameters (id)       ON DELETE CASCADE,
    supported          boolean NOT NULL DEFAULT false,
    required           boolean NOT NULL DEFAULT false,

    CONSTRAINT pk_channel_parameter_assignments PRIMARY KEY (service_channel_id, parameter_id),
    -- FR-S4-04: Required is only meaningful while Supported. Clearing Supported
    -- must force-clear Required in the same write.
    CONSTRAINT ck_channel_parameter_assignments_required_needs_supported
        CHECK (required = false OR supported = true)
);

CREATE INDEX IF NOT EXISTS channel_parameter_assignments_parameter_id_idx
    ON channel_parameter_assignments (parameter_id);

-- -----------------------------------------------------------------------------
-- §6 parameter_mappings — source value → bilingual display value. Resolved at
-- READ time (BR-13 / FR-F0-05): an edit or delete retroactively relabels
-- historical data by design, there is no version history, and Replace-all is
-- irreversible. `status` only ever holds 'active' in storage — `draft` is a
-- client-side pre-save state for SCR-07's inline add-row and is NEVER persisted.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS parameter_mappings (
    id           uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    parameter_id uuid        NOT NULL REFERENCES parameters (id) ON DELETE CASCADE,
    source_value text        NOT NULL,
    display_en   text        NOT NULL,
    display_ar   text        NOT NULL,
    status       text        NOT NULL DEFAULT 'active',
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_parameter_mappings_status CHECK (status IN ('draft', 'active'))
);

-- VR-F08 (Clarifications 2026-07-27): unique within the parameter,
-- CASE-INSENSITIVELY, while the entered casing is preserved in the column.
CREATE UNIQUE INDEX IF NOT EXISTS parameter_mappings_parameter_source_lower_uniq
    ON parameter_mappings (parameter_id, LOWER(source_value));

-- -----------------------------------------------------------------------------
-- §7 unmapped_value_occurrences — backs SCR-07's trailing-7-day queue
-- (FR-S7-02). A purpose-built small table rather than a live query over the
-- high-volume partitioned request log.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS unmapped_value_occurrences (
    id               uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    parameter_id     uuid        NOT NULL REFERENCES parameters (id) ON DELETE CASCADE,
    raw_value        text        NOT NULL,
    first_seen_at    timestamptz NOT NULL DEFAULT now(),
    last_seen_at     timestamptz NOT NULL DEFAULT now(),
    occurrence_count integer     NOT NULL DEFAULT 1,

    CONSTRAINT ck_unmapped_value_occurrences_count CHECK (occurrence_count >= 1)
);

-- The queue read: one parameter's rows inside the 7-day window.
CREATE INDEX IF NOT EXISTS unmapped_value_occurrences_parameter_first_seen_idx
    ON unmapped_value_occurrences (parameter_id, first_seen_at);
-- Upsert probe on repeat sightings — matched case-insensitively (see §7 notes).
CREATE UNIQUE INDEX IF NOT EXISTS unmapped_value_occurrences_parameter_raw_lower_uniq
    ON unmapped_value_occurrences (parameter_id, LOWER(raw_value));

-- -----------------------------------------------------------------------------
-- §1 integrations — one provisioned inbound endpoint, in exactly one scenario
-- (BR-02, immutable after create). Active ⇄ Inactive only; no delete ever.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS integrations (
    id                         uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    name                       text        NOT NULL,
    description                text        NULL,
    service_channel_id         uuid        NOT NULL REFERENCES service_channels (id),
    scenario                   text        NOT NULL,
    active                     boolean     NOT NULL DEFAULT true,
    allowed_origins            text[]      NULL,
    link_expiry_override_hours integer     NULL,
    created_by                 uuid        NOT NULL,
    created_at                 timestamptz NOT NULL DEFAULT now(),
    updated_at                 timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_integrations_name_length CHECK (char_length(name) BETWEEN 1 AND 120),
    CONSTRAINT ck_integrations_scenario CHECK (scenario IN (
        'dispatch', 'redirect_link', 'json_render', 'iframe_embed', 'response_ingestion')),
    -- FR-S2-10: each scenario-specific field exists only for its own scenario.
    CONSTRAINT ck_integrations_allowed_origins_scenario CHECK (
        allowed_origins IS NULL OR scenario = 'iframe_embed'),
    CONSTRAINT ck_integrations_link_expiry_scenario CHECK (
        link_expiry_override_hours IS NULL OR scenario = 'redirect_link'),
    CONSTRAINT ck_integrations_link_expiry_positive CHECK (
        link_expiry_override_hours IS NULL OR link_expiry_override_hours > 0)
);

-- VR-F01: name unique per tenant, implemented case-insensitively for consistency
-- with VR-F04/F06/F08 (data-model.md §1 records the reasoning).
CREATE UNIQUE INDEX IF NOT EXISTS integrations_name_lower_uniq   ON integrations (LOWER(name));
CREATE INDEX        IF NOT EXISTS integrations_service_channel_idx ON integrations (service_channel_id);
CREATE INDEX        IF NOT EXISTS integrations_active_idx          ON integrations (active);

-- -----------------------------------------------------------------------------
-- §2 credentials — at most ONE 'active' row per integration: generating a new
-- credential atomically revokes the prior active one (BR-16). Revoked rows are
-- retained for audit, never deleted; there is no un-revoke.
-- `secret_hash` is NEVER the plaintext (BR-16, NFR-6). Grant type, token
-- lifetime, expiry, sandbox flag and source-IP allowlist are fixed in code and
-- deliberately absent as columns ([PO-G13], BR-17).
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS credentials (
    id                   uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    integration_id       uuid        NOT NULL REFERENCES integrations (id) ON DELETE CASCADE,
    mechanism            text        NOT NULL,
    label_or_client_name text        NOT NULL,
    secret_hash          text        NOT NULL,
    scopes               text[]      NULL,
    status               text        NOT NULL DEFAULT 'active',
    generated_at         timestamptz NOT NULL DEFAULT now(),
    generated_by         uuid        NULL,
    revoked_at           timestamptz NULL,

    CONSTRAINT ck_credentials_mechanism CHECK (mechanism IN ('api_key', 'oauth_client')),
    CONSTRAINT ck_credentials_status CHECK (status IN ('active', 'revoked')),
    -- VR-F10.
    CONSTRAINT ck_credentials_label_present CHECK (char_length(label_or_client_name) >= 1),
    -- Scopes (BR-26) exist only for the OAuth mechanism.
    CONSTRAINT ck_credentials_scopes_mechanism CHECK (scopes IS NULL OR mechanism = 'oauth_client'),
    -- revoked_at is set exactly when the row is revoked.
    CONSTRAINT ck_credentials_revoked_at CHECK (
        (status = 'revoked' AND revoked_at IS NOT NULL) OR
        (status = 'active'  AND revoked_at IS NULL))
);

-- The "current active credential" lookup on every inbound authentication.
CREATE INDEX IF NOT EXISTS credentials_integration_status_idx ON credentials (integration_id, status);
-- BR-16 as a hard invariant: at most one active credential per integration.
CREATE UNIQUE INDEX IF NOT EXISTS credentials_one_active_per_integration_uniq
    ON credentials (integration_id) WHERE status = 'active';

-- -----------------------------------------------------------------------------
-- §8 integration_request_logs — immutable, append-only, DB-04 MONTHLY-PARTITIONED
-- on `timestamp`. Retention is 90 days (NFR-8) enforced by DETACHING old
-- partitions, not row-level DELETEs.
--
-- Two consequences of partitioning, both deliberate:
--   1. The primary key MUST include the partition column, hence (id, timestamp).
--   2. `integration_id` is a real FK (supported from a partitioned table since
--      PG 12) and stays NULLABLE — an auth-rejected request can fail before the
--      integration is resolved and must still be logged.
--
-- `parameters_received` stores ALL parameters (registered + unregistered) RAW.
-- PII is masked at display/export time only (FR-S8-03), never at write time —
-- the raw value must remain usable for reprocessing and audit.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS integration_request_logs (
    id                  uuid        NOT NULL DEFAULT gen_random_uuid(),
    integration_id      uuid        NULL REFERENCES integrations (id),
    timestamp           timestamptz NOT NULL,
    method              text        NOT NULL,
    path                text        NOT NULL,
    scenario            text        NULL,
    parameters_received jsonb       NOT NULL DEFAULT '{}'::jsonb,
    response_returned   jsonb       NOT NULL DEFAULT '{}'::jsonb,
    http_status         integer     NOT NULL,
    result_code         text        NOT NULL,
    latency_ms          integer     NOT NULL,
    credential_label    text        NULL,
    rejection_stage     text        NULL,

    CONSTRAINT pk_integration_request_logs PRIMARY KEY (id, timestamp),
    CONSTRAINT ck_integration_request_logs_scenario CHECK (scenario IS NULL OR scenario IN (
        'dispatch', 'redirect_link', 'json_render', 'iframe_embed', 'response_ingestion')),
    CONSTRAINT ck_integration_request_logs_http_status CHECK (http_status BETWEEN 100 AND 599),
    CONSTRAINT ck_integration_request_logs_latency CHECK (latency_ms >= 0)
) PARTITION BY RANGE (timestamp);

-- SCR-08 reads: per-integration history, the global newest-first default order,
-- and the status-class chip filters (a range query on http_status — the class is
-- cheaply derivable, so no redundant status_class column, DB Article 4.6).
CREATE INDEX IF NOT EXISTS integration_request_logs_integration_timestamp_idx
    ON integration_request_logs (integration_id, timestamp DESC);
CREATE INDEX IF NOT EXISTS integration_request_logs_timestamp_idx
    ON integration_request_logs (timestamp DESC);
CREATE INDEX IF NOT EXISTS integration_request_logs_http_status_idx
    ON integration_request_logs (http_status);

-- Monthly partitions covering the previous 3 months through the next 12, created
-- up front so the module runs without a maintenance job on day one. Plus a DEFAULT
-- partition so an out-of-window insert NEVER fails the inbound pipeline.
--
-- Operational caveat (tracked in TODO.md): once rows for month M have landed in
-- the DEFAULT partition, attaching a real partition for M fails until those rows
-- are moved. The 12-month lead exists so a retention/roll-forward job lands well
-- before that matters.
DO $$
DECLARE
    bucket     date := (date_trunc('month', now() AT TIME ZONE 'UTC') - interval '3 months')::date;
    last_month date := (date_trunc('month', now() AT TIME ZONE 'UTC') + interval '12 months')::date;
    part_name  text;
BEGIN
    WHILE bucket <= last_month LOOP
        part_name := 'integration_request_logs_' || to_char(bucket, 'YYYY_MM');
        EXECUTE format(
            'CREATE TABLE IF NOT EXISTS %I PARTITION OF integration_request_logs '
            || 'FOR VALUES FROM (%L) TO (%L)',
            part_name,
            to_char(bucket, 'YYYY-MM-DD') || ' 00:00:00+00',
            to_char((bucket + interval '1 month')::date, 'YYYY-MM-DD') || ' 00:00:00+00');
        bucket := (bucket + interval '1 month')::date;
    END LOOP;

    EXECUTE 'CREATE TABLE IF NOT EXISTS integration_request_logs_default '
        || 'PARTITION OF integration_request_logs DEFAULT';
END $$;

-- -----------------------------------------------------------------------------
-- event_log — shared M-17 audit table (NOT M-13-owned). Created by whichever
-- module baseline runs first (M-10 / M-06 / M-16 also issue CREATE TABLE IF NOT
-- EXISTS). M-13 appends configuration-change rows here in the SAME transaction as
-- the change itself (DB-08). Definition mirrors the M-06 baseline exactly.
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
-- Seed: the 23 normative built-in parameters (FR-F0-10), all ENABLED (BR-23).
--
-- Deterministic UUIDs (0000000d-… = module 13) so the seed is stable and
-- re-runnable, mirroring the M-06 baseline's standard-KPI seed. Built-ins ship
-- with api_field_locked = true (BR-09/BR-11: never renamed at the API-field
-- level) and their data type is read-only by virtue of origin = 'built_in'
-- ([PO-G27]) — no separate column.
--
-- mapping_support follows BR-27 exactly: every `list` parameter is true (always
-- on, not changeable); the text/boolean parameters ship false (available but off
-- by default). Arabic names are authored natively in فصحى, not translated.
--
-- ON CONFLICT targets the api_field unique index, so a re-run is a no-op and a
-- tenant that already disabled a built-in keeps its state.
-- =============================================================================
INSERT INTO parameters
    (id, name_en, name_ar, api_field, api_field_locked, data_type, origin, enabled, mapping_support)
VALUES
    ('0000000d-0000-0000-0000-000000000001', 'Customer ID',      'معرّف العميل',        'customer_id',      true, 'text',      'built_in', true, false),
    ('0000000d-0000-0000-0000-000000000002', 'Customer Name',    'اسم العميل',          'customer_name',    true, 'text',      'built_in', true, false),
    ('0000000d-0000-0000-0000-000000000003', 'Customer Type',    'نوع العميل',          'customer_type',    true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-000000000004', 'Customer Segment', 'فئة العميل',          'customer_segment', true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-000000000005', 'VIP',              'عميل مميّز',          'vip',              true, 'boolean',   'built_in', true, false),
    ('0000000d-0000-0000-0000-000000000006', 'Gender',           'الجنس',               'gender',           true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-000000000007', 'Nationality',      'الجنسية',             'nationality',      true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-000000000008', 'Mobile',           'رقم الجوال',          'mobile',           true, 'phone',     'built_in', true, false),
    ('0000000d-0000-0000-0000-000000000009', 'Email',            'البريد الإلكتروني',   'email',            true, 'email',     'built_in', true, false),
    ('0000000d-0000-0000-0000-00000000000a', 'Transaction ID',   'معرّف العملية',       'transaction_id',   true, 'text',      'built_in', true, false),
    ('0000000d-0000-0000-0000-00000000000b', 'Transaction Date', 'تاريخ العملية',       'transaction_date', true, 'date_time', 'built_in', true, false),
    ('0000000d-0000-0000-0000-00000000000c', 'Service',          'الخدمة',              'service',          true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-00000000000d', 'Product',          'المنتج',              'product',          true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-00000000000e', 'Branch',           'الفرع',               'branch',           true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-00000000000f', 'Department',       'الإدارة',             'department',       true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-000000000010', 'Region',           'المنطقة',             'region',           true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-000000000011', 'Journey',          'الرحلة',              'journey',          true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-000000000012', 'Journey Stage',    'مرحلة الرحلة',        'journey_stage',    true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-000000000013', 'Touchpoint',       'نقطة التواصل',        'touchpoint',       true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-000000000014', 'Agent',            'موظف الخدمة',         'agent',            true, 'text',      'built_in', true, false),
    ('0000000d-0000-0000-0000-000000000015', 'Employee',         'الموظف',              'employee',         true, 'text',      'built_in', true, false),
    -- System-populated from the {channelId} path segment on every inbound request.
    ('0000000d-0000-0000-0000-000000000016', 'Service Channel',  'قناة الخدمة',         'service_channel',  true, 'list',      'built_in', true, true),
    ('0000000d-0000-0000-0000-000000000017', 'Source System',    'النظام المصدر',       'source_system',    true, 'text',      'built_in', true, false)
ON CONFLICT (api_field) DO NOTHING;
