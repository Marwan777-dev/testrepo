-- =============================================================================
-- M-06 CX Metrics & KPI Engine — Organization Settings (T008)
-- =============================================================================
-- DB-02 / AD-02: tenant-schema tables carry NO `tenant_id` column. Isolation is
-- at the PostgreSQL schema level; this script runs once per tenant schema (the
-- bootstrapper / provisioner sets `search_path` to the target `tenant_{slug}`
-- schema before applying). Mirrors KpiManagement_Baseline.sql.
--
-- `organization_settings` is M-06-OWNED (re-homed from the never-built M-11
-- `Nabadat.TenantAdministration` per the 2026-06-24 decision) and lives in the
-- tenant DB only — NO control-plane involvement. M-06 owns the table AND its
-- entire editing surface (controller, validators, SVG sanitiser, save-service).
--
-- Exactly one row per tenant (data-model.md §2.1): a partial unique index on the
-- constant expression (true) admits a single allowable row; a second INSERT
-- collides and fails. A default row is seeded so GET returns defaults on a fresh
-- tenant before any edit (T-04 / FR-S5).
--
-- Idempotent: CREATE TABLE IF NOT EXISTS + a guarded seed INSERT (skipped when a
-- row already exists), so re-runs are safe.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- organization_settings — exactly one row per tenant (data-model.md §2.1)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS organization_settings (
    id              uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(150) NOT NULL,
    logo_blob_ref   varchar(500) NULL,
    industry        varchar(32)  NOT NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NOT NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NOT NULL,
    CONSTRAINT industry_valid
        CHECK (industry IN ('Banking', 'Telecommunications', 'Government', 'Automotive', 'Entertainment', 'Services'))
);

-- Singleton enforcement: one row per tenant schema. The partial unique index on
-- the constant (true) yields a single allowable row — a second INSERT collides.
CREATE UNIQUE INDEX IF NOT EXISTS organization_settings_singleton_uniq
    ON organization_settings ((true));

-- =============================================================================
-- Seed: the single default row so a freshly provisioned tenant returns sensible
-- defaults from GET before any edit. created_by/updated_by = reserved system
-- actor (all-zero UUID), matching the baseline's migration-time write convention.
-- Guarded by NOT EXISTS so the seed is skipped once any row is present (the
-- singleton index would reject a second row anyway — this keeps re-runs clean).
-- 'Services' is the most generic of the six canonical industries.
-- =============================================================================
INSERT INTO organization_settings (name, industry, created_by, updated_by)
SELECT 'My Organization', 'Services',
       '00000000-0000-0000-0000-000000000000',
       '00000000-0000-0000-0000-000000000000'
 WHERE NOT EXISTS (SELECT 1 FROM organization_settings);
