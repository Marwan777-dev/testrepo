-- =============================================================================
-- M-06 CX Metrics & KPI Engine — Tenant-Schema Baseline ROLLBACK (T007)
-- =============================================================================
-- Drops the four M-06 tables in reverse foreign-key order (children before
-- parents) within the current tenant schema. Run with `search_path` pointed at
-- the target `tenant_{slug}` schema. Indexes drop with their tables.
-- =============================================================================

DROP TABLE IF EXISTS cxi_weights;
DROP TABLE IF EXISTS kpi_perspectives;
DROP TABLE IF EXISTS kpi_thresholds;
DROP TABLE IF EXISTS kpi_definitions;
