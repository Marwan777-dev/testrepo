-- =============================================================================
-- M-10 User and Role Management — Tenant-Schema Baseline (T010)
-- =============================================================================
-- DB-02 / AD-02: tenant-schema tables carry NO `tenant_id` column. Isolation is
-- at the PostgreSQL schema level; this script runs once per tenant schema.
--
-- Cross-table foreign keys are declared only between tables M-10 owns within this
-- schema. References to externally-owned tables (e.g. M-17's `event_log`, the
-- control-plane `tenants` table) are logical and documented, not enforced here,
-- so the baseline applies cleanly in a standalone module test database.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- tenant_users — primary user entity within a tenant boundary
-- -----------------------------------------------------------------------------
CREATE TABLE tenant_users (
    user_id                          uuid          PRIMARY KEY,
    username                         varchar(254)  NOT NULL,
    password_hash                    varchar(72)   NOT NULL,
    is_mfa_enrolled                  boolean       NOT NULL DEFAULT false,
    mfa_secret_encrypted             bytea         NULL,
    mfa_secret_key_ref               varchar(512)  NULL,
    last_used_totp_step              bigint        NULL,
    persona                          varchar(16)   NOT NULL,
    status                           varchar(32)   NOT NULL DEFAULT 'active',
    failed_attempt_count             smallint      NOT NULL DEFAULT 0,
    locked_until_utc                 timestamptz   NULL,
    organization_node_id             uuid          NULL,
    last_permission_snapshot_version bigint        NOT NULL DEFAULT 0,
    requires_password_change         boolean       NOT NULL DEFAULT false,
    created_at                       timestamptz   NOT NULL,
    updated_at                       timestamptz   NOT NULL,
    CONSTRAINT uq_tenant_users_username UNIQUE (username)
);

CREATE INDEX ix_tenant_users_status ON tenant_users (status);
CREATE INDEX ix_tenant_users_organization_node_id ON tenant_users (organization_node_id);

-- -----------------------------------------------------------------------------
-- auth_sessions — authenticated user sessions (append-only except is_active)
-- -----------------------------------------------------------------------------
CREATE TABLE auth_sessions (
    session_id                  uuid         PRIMARY KEY,
    user_id                     uuid         NOT NULL REFERENCES tenant_users (user_id),
    token_hash                  bytea        NOT NULL,
    issued_at_utc               timestamptz  NOT NULL,
    absolute_expires_at_utc     timestamptz  NOT NULL,
    last_activity_at_utc        timestamptz  NOT NULL,
    sliding_ttl_minutes         smallint     NOT NULL,
    permission_snapshot_version bigint       NOT NULL,
    permission_snapshot         jsonb        NOT NULL,
    is_active                   boolean      NOT NULL DEFAULT true,
    created_at                  timestamptz  NOT NULL,
    CONSTRAINT uq_auth_sessions_token_hash UNIQUE (token_hash)
);

CREATE INDEX ix_auth_sessions_user_id ON auth_sessions (user_id);
CREATE INDEX ix_auth_sessions_active ON auth_sessions (user_id) WHERE is_active;

-- -----------------------------------------------------------------------------
-- password_reset_tokens — single-use, time-limited reset tokens
-- -----------------------------------------------------------------------------
CREATE TABLE password_reset_tokens (
    token_id       uuid         PRIMARY KEY,
    user_id        uuid         NOT NULL REFERENCES tenant_users (user_id),
    token_hash     bytea        NOT NULL,
    expires_at_utc timestamptz  NOT NULL,
    used_at_utc    timestamptz  NULL,
    revoked        boolean      NOT NULL DEFAULT false,
    issued_by      varchar(16)  NOT NULL,
    issued_via     varchar(16)  NOT NULL,
    created_at     timestamptz  NOT NULL,
    CONSTRAINT uq_password_reset_tokens_token_hash UNIQUE (token_hash)
);

CREATE INDEX ix_password_reset_tokens_user_id ON password_reset_tokens (user_id);
CREATE INDEX ix_password_reset_tokens_expires_at_utc ON password_reset_tokens (expires_at_utc);

-- -----------------------------------------------------------------------------
-- password_reset_rate_limit_records — application-layer rate-limit state
-- -----------------------------------------------------------------------------
CREATE TABLE password_reset_rate_limit_records (
    email_hash        bytea        PRIMARY KEY,
    window_start_utc  timestamptz  NOT NULL,
    request_count     smallint     NOT NULL DEFAULT 0,
    updated_at        timestamptz  NOT NULL
);

-- -----------------------------------------------------------------------------
-- permission_module_assignments — a user's access to a DOC-02 permission module
-- -----------------------------------------------------------------------------
CREATE TABLE permission_module_assignments (
    assignment_id uuid         PRIMARY KEY,
    user_id       uuid         NOT NULL REFERENCES tenant_users (user_id),
    module_id     varchar(64)  NOT NULL,
    allowed_modes varchar[]    NOT NULL,
    assigned_by   uuid         NOT NULL REFERENCES tenant_users (user_id),
    created_at    timestamptz  NOT NULL,
    updated_at    timestamptz  NOT NULL,
    CONSTRAINT uq_permission_module_assignments_user_module UNIQUE (user_id, module_id)
);

CREATE INDEX ix_permission_module_assignments_user_id ON permission_module_assignments (user_id);
CREATE INDEX ix_permission_module_assignments_module_id ON permission_module_assignments (module_id);

-- -----------------------------------------------------------------------------
-- custom_authorization_rules — per-user fine-grained action/scope overrides
-- -----------------------------------------------------------------------------
CREATE TABLE custom_authorization_rules (
    rule_id                     uuid         PRIMARY KEY,
    user_id                     uuid         NOT NULL REFERENCES tenant_users (user_id),
    allowed_actions             varchar[]    NOT NULL,
    parameter_scope_assignments jsonb        NOT NULL DEFAULT '{}',
    created_by                  uuid         NOT NULL REFERENCES tenant_users (user_id),
    created_at                  timestamptz  NOT NULL,
    updated_at                  timestamptz  NOT NULL
);

CREATE INDEX ix_custom_authorization_rules_user_id ON custom_authorization_rules (user_id);

-- -----------------------------------------------------------------------------
-- data_scope_assignments — M-13 parameter allowed-values assigned to a user
-- -----------------------------------------------------------------------------
CREATE TABLE data_scope_assignments (
    assignment_id  uuid          PRIMARY KEY,
    user_id        uuid          NOT NULL REFERENCES tenant_users (user_id),
    parameter_name varchar(128)  NOT NULL,
    allowed_values varchar[]     NOT NULL,
    created_at     timestamptz   NOT NULL,
    updated_at     timestamptz   NOT NULL,
    CONSTRAINT uq_data_scope_assignments_user_parameter UNIQUE (user_id, parameter_name)
);

CREATE INDEX ix_data_scope_assignments_user_id ON data_scope_assignments (user_id);

-- -----------------------------------------------------------------------------
-- data_scope_parameter_definitions — M-13-supplied parameter definitions
-- -----------------------------------------------------------------------------
CREATE TABLE data_scope_parameter_definitions (
    parameter_name varchar(128)  PRIMARY KEY,
    label          varchar(256)  NOT NULL,
    allowed_values varchar[]     NOT NULL,
    source_module  varchar(8)    NOT NULL DEFAULT 'M-13',
    created_at     timestamptz   NOT NULL,
    updated_at     timestamptz   NOT NULL
);

-- -----------------------------------------------------------------------------
-- organization_hierarchy_nodes — tenant org scope nodes (M-11/M-13 owned; M-10 reads)
-- -----------------------------------------------------------------------------
CREATE TABLE organization_hierarchy_nodes (
    node_id        uuid          PRIMARY KEY,
    parent_node_id uuid          NULL REFERENCES organization_hierarchy_nodes (node_id),
    name           varchar(256)  NOT NULL,
    path           varchar(2048) NOT NULL,
    source         varchar(16)   NOT NULL,
    external_ref   varchar(512)  NULL,
    created_at     timestamptz   NOT NULL,
    updated_at     timestamptz   NOT NULL
);

CREATE INDEX ix_organization_hierarchy_nodes_path ON organization_hierarchy_nodes (path varchar_pattern_ops);
CREATE INDEX ix_organization_hierarchy_nodes_parent_node_id ON organization_hierarchy_nodes (parent_node_id);

-- Deferred FK: tenant_users.organization_node_id → organization_hierarchy_nodes.node_id
ALTER TABLE tenant_users
    ADD CONSTRAINT fk_tenant_users_organization_node
    FOREIGN KEY (organization_node_id) REFERENCES organization_hierarchy_nodes (node_id);

-- -----------------------------------------------------------------------------
-- event_log — owned by M-17 (Audit). Created here so M-10's transactional event
-- writes (M17EventPublisher, FR-015) are testable in a standalone module DB.
-- When M-17 ships its own baseline, this block becomes a no-op safeguard.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS event_log (
    event_id       uuid         PRIMARY KEY,
    event_type     varchar(64)  NOT NULL,
    actor_id       uuid         NULL,
    actor_persona  varchar(16)  NULL,
    entity_type    varchar(128) NULL,
    entity_id      uuid         NULL,
    old_value      jsonb        NULL,
    new_value      jsonb        NULL,
    occurred_at_utc timestamptz NOT NULL,
    correlation_id uuid         NULL
);

CREATE INDEX IF NOT EXISTS ix_event_log_event_type ON event_log (event_type);
CREATE INDEX IF NOT EXISTS ix_event_log_occurred_at_utc ON event_log (occurred_at_utc);
CREATE INDEX IF NOT EXISTS ix_event_log_actor_id ON event_log (actor_id);
CREATE INDEX IF NOT EXISTS ix_event_log_entity_id ON event_log (entity_id);
