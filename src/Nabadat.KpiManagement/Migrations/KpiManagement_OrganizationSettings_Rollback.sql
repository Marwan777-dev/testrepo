-- =============================================================================
-- M-06 CX Metrics & KPI Engine — Organization Settings ROLLBACK (T008)
-- =============================================================================
-- Manual teardown for KpiManagement_OrganizationSettings.sql. Not auto-applied
-- by the bootstrapper. Drops the singleton index then the table within the
-- current `tenant_{slug}` schema (set search_path before running).
-- =============================================================================

DROP INDEX IF EXISTS organization_settings_singleton_uniq;
DROP TABLE IF EXISTS organization_settings;
