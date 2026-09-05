-- =============================================================================
-- M-10 User and Role Management — Control-Plane Baseline (T011)
-- =============================================================================
-- Control-plane tables carry an explicit `tenant_id` column (DB-02 exemption,
-- same pattern as M-18/M-19). The logical FK `tenant_id → tenants.id` lives in
-- the control-plane database owned by the provisioning module; it is documented
-- but NOT enforced here so this script applies cleanly in a standalone test DB
-- that has no `tenants` table.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- persona_baselines — per-tenant default permission assignments per persona
-- -----------------------------------------------------------------------------
CREATE TABLE persona_baselines (
    baseline_id                   uuid         PRIMARY KEY,
    tenant_id                     uuid         NOT NULL,
    persona_id                    varchar(8)   NOT NULL,
    permission_module_assignments jsonb        NOT NULL,
    default_data_scope_rules      jsonb        NOT NULL DEFAULT '{}',
    is_customised                 boolean      NOT NULL DEFAULT false,
    created_at                    timestamptz  NOT NULL,
    updated_at                    timestamptz  NOT NULL,
    CONSTRAINT uq_persona_baselines_tenant_persona UNIQUE (tenant_id, persona_id)
);

CREATE INDEX ix_persona_baselines_tenant_id ON persona_baselines (tenant_id);

-- -----------------------------------------------------------------------------
-- identity_provider_configs — per-tenant SSO config (forward-compatible; no
-- provider logic executed in Phase 1). settings is open jsonb (no hardcoded keys).
-- -----------------------------------------------------------------------------
CREATE TABLE identity_provider_configs (
    provider_id   uuid         PRIMARY KEY,
    tenant_id     uuid         NOT NULL,
    provider_type varchar(32)  NOT NULL,
    settings      jsonb        NOT NULL,
    is_active     boolean      NOT NULL DEFAULT false,
    created_at    timestamptz  NOT NULL,
    updated_at    timestamptz  NOT NULL,
    CONSTRAINT uq_identity_provider_configs_tenant_type UNIQUE (tenant_id, provider_type)
);

CREATE INDEX ix_identity_provider_configs_tenant_id ON identity_provider_configs (tenant_id);
