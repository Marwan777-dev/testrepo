# Data Model: Customer Journey Mapping Module (M-16)

**Feature**: 002-customer-journey-mapping
**Date**: 2026-06-08

All tables reside in the per-tenant schema (`tenant_{slug}`). No `tenant_id` columns — isolation is at the schema level (DB-02, AD-02). All primary keys are UUID. Every table carries `created_at` and `updated_at` in UTC.

---

## Summary

| Table | Description |
|-------|-------------|
| `journeys` | Customer journey root entity |
| `stages` | Ordered phases within a journey |
| `touchpoints` | Journey-local interaction points within a stage |
| `kpi_bindings` | KPI assignments on a touchpoint |
| `scoring_configs` | Strategic scoring parameters — **one row per tenant** (singleton) |
| `personas` | Reusable customer archetypes |
| `journey_persona_bindings` | Many-to-many: journey ↔ active personas |
| `journey_versions` | Immutable published version snapshots |
| `detection_configs` | Pain/happy threshold rules per journey |
| `detection_threshold_overrides` | Per-stage or per-touchpoint threshold overrides |
| `report_contracts` | M-07 report metadata per journey |
| `kpi_type_definitions` | Tenant-defined custom KPI types |
| `journey_scores` | Latest computed score snapshot per journey |

---

## `journeys`

Root entity representing a customer journey.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `journey_id` | `uuid` | PK, not null | Generated on creation |
| `name` | `varchar(255)` | not null | Unique per tenant (case-insensitive, via functional partial index; see below) |
| `description` | `text` | nullable | |
| `journey_type` | `varchar(64)` | not null | e.g. `Purchase`, `Support`, `Onboarding` — free-form tenant-defined value |
| `status` | `varchar(16)` | not null, default `'Draft'` | `Draft` \| `Active` \| `Inactive` \| `Archived` |
| `created_by` | `uuid` | not null | M-10 `user_id` reference (no FK across modules) |
| `updated_by` | `uuid` | nullable | M-10 `user_id` of last editor |
| `created_at` | `timestamptz` | not null | UTC |
| `updated_at` | `timestamptz` | not null | UTC |

**Indexes**:
- `journey_id` (PK)
- `CREATE UNIQUE INDEX idx_journeys_name_ci ON journeys (LOWER(name)) WHERE status <> 'Archived'` — enforces case-insensitive name uniqueness for non-Archived journeys; Archived journeys release their name for reuse
- `status` — filter by lifecycle state

**Soft semantics**: `status = 'Archived'` is the terminal state; rows are retained (not hard-deleted). Journey cannot be deleted while it has `Active` survey bindings (enforced at service layer).

---

## `stages`

Ordered phase within a journey. All touchpoints belong to a stage.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `stage_id` | `uuid` | PK, not null | |
| `journey_id` | `uuid` | not null, FK → `journeys.journey_id` ON DELETE CASCADE | |
| `sequence_number` | `integer` | not null | 1-based, unique within journey |
| `name` | `varchar(255)` | not null | |
| `description` | `text` | nullable | |
| `customer_goal` | `text` | nullable | What the customer is trying to achieve in this stage |
| `expected_emotion` | `varchar(64)` | nullable | e.g. `excited`, `anxious`, `frustrated`, `satisfied` |
| `duration_hint` | `varchar(64)` | nullable | Human-readable estimate e.g. `2–5 minutes` |
| `created_at` | `timestamptz` | not null | UTC |
| `updated_at` | `timestamptz` | not null | UTC |

**Indexes**:
- `journey_id` + `sequence_number` (UNIQUE within journey — enforces ordering invariant)
- `journey_id` — all-stages-by-journey query

**Max stages**: enforced at service layer via `JourneyLimitEnforcer` (default 20 per journey, configurable per tenant via M-11).

---

## `touchpoints`

Journey-local interaction point within a stage.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `touchpoint_id` | `uuid` | PK, not null | |
| `stage_id` | `uuid` | not null, FK → `stages.stage_id` ON DELETE CASCADE | |
| `name` | `varchar(255)` | not null | |
| `description` | `text` | nullable | |
| `channels` | `text[]` | not null, default `'{}'` | Array of channel codes e.g. `{IVR, Web, App, Email, Branch}` |
| `importance` | `varchar(16)` | not null, default `'Medium'` | `Low` \| `Medium` \| `High` \| `Critical` |
| `is_mot` | `boolean` | not null, default `false` | Moment of Truth flag — author-set, elevates priority in detection/reporting |
| `is_mandatory` | `boolean` | not null, default `false` | Mandatory touchpoints are always included in score calculation |
| `created_at` | `timestamptz` | not null | UTC |
| `updated_at` | `timestamptz` | not null | UTC |

**Indexes**:
- `stage_id` — all-touchpoints-by-stage query
- `is_mot` — filter for MoT touchpoints

**Unmeasured touchpoints**: A touchpoint with no `kpi_bindings` rows is considered unmeasured. It is excluded from score computation (by M-06 and M-16's detection logic) and visually flagged in the UI.

**Max touchpoints per stage**: enforced at service layer via `JourneyLimitEnforcer` (default 30 per stage).

---

## `kpi_bindings`

KPI assignment on a touchpoint. All bindings on a touchpoint must sum to 100% weight.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `kpi_binding_id` | `uuid` | PK, not null | |
| `touchpoint_id` | `uuid` | not null, FK → `touchpoints.touchpoint_id` ON DELETE CASCADE | |
| `kpi_type` | `varchar(64)` | not null | Platform-standard (`NPS`, `CSAT`, `CES`, `FCR`, `AgentSatisfaction`, `VFM`) or tenant-defined type key from `kpi_type_definitions` |
| `is_platform_standard` | `boolean` | not null | `true` for the six platform-standard types; `false` for tenant-defined types |
| `weight` | `numeric(5,2)` | not null, CHECK (`weight > 0 AND weight <= 100`) | Percentage; all bindings per touchpoint must sum to 100 (enforced at service layer) |
| `created_at` | `timestamptz` | not null | UTC |
| `updated_at` | `timestamptz` | not null | UTC |

**Indexes**:
- `touchpoint_id` — all-kpi-bindings-by-touchpoint query (the primary access pattern)
- `(touchpoint_id, kpi_type)` UNIQUE — prevents duplicate KPI type on the same touchpoint

**Weight sum invariant**: Enforced by `KpiWeightValidator` in the service layer. The save operation is always a full replace (DELETE + INSERT) inside one transaction to avoid transient sum violations.

**Scoring direction**: Platform-standard types carry known scoring direction:
- `CES` — inverted (lower effort = better performance); all others ascending.
- Tenant-defined types use `scoring_direction` from `kpi_type_definitions`.

---

## `scoring_configs`

Tenant-level strategic scoring parameters consumed by M-06 (SRS §4.2.9 / §11.7, Q11 RESOLVED — **per-tenant, not per-journey**). **Exactly one row per tenant schema** (singleton). Owned by M-16; read by M-06 via `IScoringConfigStore`; edited via the Platform Settings → Customer Journey surface (feature 003). No `journey_id` and no `tenant_id` — the schema boundary is the tenant scope.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK, not null, default `gen_random_uuid()` | |
| `alpha` | `numeric(4,3)` | not null, default `0.500`, CHECK `alpha BETWEEN 0.000 AND 1.000` | α blend weight; β is derived as `1 − α` (not stored) |
| `mot_multiplier` | `numeric(3,1)` | not null, default `1.5`, CHECK `BETWEEN 1.0 AND 2.0` | Moment-of-Truth weight multiplier |
| `n_floor` | `integer` | not null, default `100`, CHECK `>= 1` | Hard minimum response count; below it a touchpoint is excluded from scoring |
| `flag_percentile` | `integer` | not null, default `25`, CHECK `BETWEEN 1 AND 49` | Percentile k for the low-sample flag threshold |
| `rolling_window_days` | `integer` | not null, default `30`, CHECK `>= 7` | Rolling response window |
| `created_at` | `timestamptz` | not null, default `now()` | UTC |
| `updated_at` | `timestamptz` | not null, default `now()` | UTC |
| `updated_by` | `uuid` | not null | M-10 `user_id` (P-01 only can edit) |

**Indexes**: `CREATE UNIQUE INDEX scoring_configs_singleton_uniq ON scoring_configs ((true))` — enforces the single per-tenant row.

> **Migration note**: feature 002 originally shipped this table as per-journey (`scoring_config_id` PK, `journey_id` UNIQUE FK, `model_type`/`stage_weight_mode`/`normalization_params`). The per-tenant reshape supersedes that — see `tasks.md` US-2 Amendment. The redundant per-tenant `tenant_scoring_config` table introduced by feature 003's first US-4 cut is retired in favour of this table.

---

## `personas`

Reusable customer archetype. Lifecycle: `Draft` → `Active` ↔ `Inactive` → `Archived`.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `persona_id` | `uuid` | PK, not null | |
| `name_ar` | `varchar(255)` | not null | Arabic label (فصحى) |
| `name_en` | `varchar(255)` | not null | English label |
| `description_ar` | `text` | nullable | |
| `description_en` | `text` | nullable | |
| `status` | `varchar(16)` | not null, default `'Draft'` | `Draft` \| `Active` \| `Inactive` \| `Archived` |
| `created_by` | `uuid` | not null | M-10 `user_id` (P-01 only can create) |
| `updated_by` | `uuid` | nullable | |
| `created_at` | `timestamptz` | not null | UTC |
| `updated_at` | `timestamptz` | not null | UTC |

**Indexes**:
- `status` — filter Active personas for the binding selector

**Terminal state**: `Archived` is irreversible. Archived personas cannot be bound to journeys.
**Binding guard**: `PersonaBindingService` rejects binding a non-`Active` persona to a journey.

---

## `journey_persona_bindings`

Many-to-many join between journeys and their bound personas.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `journey_id` | `uuid` | not null, FK → `journeys.journey_id` ON DELETE CASCADE | Part of composite PK |
| `persona_id` | `uuid` | not null, FK → `personas.persona_id` | Part of composite PK |
| `bound_at` | `timestamptz` | not null | UTC, when the binding was created |

**Primary key**: `(journey_id, persona_id)`

**Guard**: Only `Active` personas can be bound (enforced at service layer). Unbinding is allowed at any time.

---

## `journey_versions`

Immutable published snapshot of a journey configuration. Written once; never updated.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `version_id` | `uuid` | PK, not null | |
| `journey_id` | `uuid` | not null, FK → `journeys.journey_id` ON DELETE RESTRICT | Restrict prevents accidental cascade; journeys with versions cannot be hard-deleted |
| `version_number` | `integer` | not null | Sequential integer per journey, starting at 1 |
| `published_by` | `uuid` | not null | M-10 `user_id` of the P-01 user who published (no FK across modules) |
| `published_at` | `timestamptz` | not null | UTC |
| `snapshot_payload` | `jsonb` | not null | Full journey tree at publish time (see research.md §1) |

**Indexes**:
- `(journey_id, version_number)` UNIQUE — enforces sequential versioning per journey
- `journey_id` — all-versions-by-journey query

**Immutability**: `snapshot_payload` is written once at publish time. The `UPDATE` operation on `journey_versions` is not permitted in normal operation.

---

## `detection_configs`

Journey-level pain/happy detection thresholds.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `detection_config_id` | `uuid` | PK, not null | |
| `journey_id` | `uuid` | not null, UNIQUE, FK → `journeys.journey_id` ON DELETE CASCADE | One config per journey |
| `pain_threshold` | `numeric(5,2)` | not null, CHECK (`pain_threshold >= 0 AND pain_threshold <= 100`) | Score ≤ this value = pain point |
| `happy_threshold` | `numeric(5,2)` | not null, CHECK (`happy_threshold >= 0 AND happy_threshold <= 100`) | Score ≥ this value = happy moment |
| `created_at` | `timestamptz` | not null | UTC |
| `updated_at` | `timestamptz` | not null | UTC |

**Indexes**: `journey_id` (UNIQUE)

**Threshold invariant**: `pain_threshold < happy_threshold` enforced at service layer (no gap between pain and happy zones is valid — the spec allows a neutral band between the two thresholds).

---

## `detection_threshold_overrides`

Per-stage or per-touchpoint threshold overrides. The most specific override wins (touchpoint > stage > journey).

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `override_id` | `uuid` | PK, not null | |
| `detection_config_id` | `uuid` | not null, FK → `detection_configs.detection_config_id` ON DELETE CASCADE | |
| `scope_type` | `varchar(16)` | not null, CHECK (`scope_type IN ('stage', 'touchpoint')`) | Level of override |
| `scope_id` | `uuid` | not null | `stage_id` or `touchpoint_id` — no FK (cross-table reference to stage/touchpoint, enforced at service layer) |
| `pain_threshold` | `numeric(5,2)` | nullable, CHECK (`pain_threshold >= 0 AND pain_threshold <= 100`) | null means "inherit from parent" |
| `happy_threshold` | `numeric(5,2)` | nullable, CHECK (`happy_threshold >= 0 AND happy_threshold <= 100`) | null means "inherit from parent" |
| `created_at` | `timestamptz` | not null | UTC |
| `updated_at` | `timestamptz` | not null | UTC |

**Indexes**:
- `detection_config_id` — all-overrides-by-config query
- `(detection_config_id, scope_type, scope_id)` UNIQUE — prevents duplicate overrides for the same entity

**Cross-table reference note**: `scope_id` references either `stage_id` or `touchpoint_id` depending on `scope_type`. A FK cannot span two possible parent tables in PostgreSQL without a polymorphic pattern. Application-layer enforcement in `DetectionConfigService` ensures the referenced `scope_id` exists in the correct parent table.

---

## `report_contracts`

M-07 report metadata per journey. Stored as a `jsonb` payload; rebuilt on each write to journey configuration.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `report_contract_id` | `uuid` | PK, not null | |
| `journey_id` | `uuid` | not null, UNIQUE, FK → `journeys.journey_id` ON DELETE CASCADE | One contract per journey |
| `contract_payload` | `jsonb` | not null | Report metadata (see research.md §8 for structure) |
| `generated_at` | `timestamptz` | not null | UTC, last rebuild time |
| `created_at` | `timestamptz` | not null | UTC |
| `updated_at` | `timestamptz` | not null | UTC |

**Indexes**: `journey_id` (UNIQUE)

**Rebuild trigger**: `ReportContractService.RebuildContractAsync(journeyId)` is called transactionally after any write to `stages`, `touchpoints`, `kpi_bindings`, or `detection_configs`. M-07 reads the stored payload via `IReportContractReader`.

---

## `kpi_type_definitions`

Tenant-defined custom KPI types. Platform-standard types (`NPS`, `CSAT`, `CES`, `FCR`, `AgentSatisfaction`, `VFM`) are built into the platform and not stored here.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `kpi_type_definition_id` | `uuid` | PK, not null | |
| `type_key` | `varchar(64)` | not null, UNIQUE | Unique key within the tenant; referenced by `kpi_bindings.kpi_type` |
| `label_ar` | `varchar(255)` | not null | Arabic label |
| `label_en` | `varchar(255)` | not null | English label |
| `scoring_direction` | `varchar(16)` | not null, default `'Ascending'` | `Ascending` \| `Descending` |
| `created_at` | `timestamptz` | not null | UTC |
| `updated_at` | `timestamptz` | not null | UTC |

**Indexes**: `type_key` (UNIQUE)

**Validation**: When saving `kpi_bindings`, `KpiBindingService` validates that any non-platform-standard `kpi_type` value exists in this table. Unknown type → 422 with `kpi.unknown_type` error code.

---

## `journey_scores`

Latest computed score snapshot per journey. Updated on every call to `IJourneyScoreProvider.GetScoresAsync()`. One row per journey.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `journey_score_id` | `uuid` | PK, not null | |
| `journey_id` | `uuid` | not null, UNIQUE, FK → `journeys.journey_id` ON DELETE CASCADE | One record per journey |
| `computed_at` | `timestamptz` | not null | UTC, when scores were last computed |
| `journey_score` | `numeric(5,2)` | nullable | Composite journey score (null if no measured touchpoints) |
| `stage_scores` | `jsonb` | nullable | `[{ stageId, score, measuredTouchpointCount }]` |
| `touchpoint_scores` | `jsonb` | nullable | `[{ touchpointId, score, kpiScores }]` |

**Indexes**: `journey_id` (UNIQUE)

**Upsert pattern**: `JourneyScoreRepository.UpsertAsync(journeyId, scoreData)` uses `INSERT ... ON CONFLICT (journey_id) DO UPDATE`. Written inside the same transaction as the `journey.score.updated` M-17 event.

---

## Migration Notes

- All 13 tables are provisioned in a single migration file per the M-16 module baseline.
- The migration targets all existing tenant schemas atomically (DB-05).
- Phase 2 reservation tables (`ai_recommendations`, `anomaly_events`, `trend_snapshots`, `branching_rules`, `targeting_rules`, `ab_test_configs`, `action_plans`, `action_assignments`, `action_progress`, `webhook_configs`, `connector_configs`) are provisioned by the Phase 1 baseline migration per DB-06 — not by M-16 specifically.
- The `idx_journeys_name_ci` partial functional index must be created with `CREATE UNIQUE INDEX CONCURRENTLY` in production to avoid table locking.

---

## Entity Relationship Summary

```
journeys
  ├── stages (1:N)
  │     └── touchpoints (1:N)
  │           └── kpi_bindings (1:N)
  ├── detection_configs (1:1)
  │     └── detection_threshold_overrides (1:N)
  ├── report_contracts (1:1)
  ├── journey_versions (1:N, immutable snapshots — each snapshots the active scoring_configs values)
  ├── journey_scores (1:1, updated on score call)
  └── journey_persona_bindings (N:M)
        └── personas (N:M)
              (lifecycle: Draft → Active ↔ Inactive → Archived)

scoring_configs (tenant-level singleton — one row per tenant; NOT under a journey)
kpi_type_definitions (tenant-level registry, referenced by kpi_bindings.kpi_type)
```
