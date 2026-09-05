# Data Model: CX Metrics & KPI Engine (M-06) + Platform Settings

**Feature**: 003-kpi-engine-settings | **Date**: 2026-06-21

Defines the PostgreSQL schema for the new tables introduced by this feature, plus seed-data rows. All tables live in `tenant_{slug}` schemas per AD-02 / DB-02 — there are no `tenant_id` columns. Migrations:

- `M06_Baseline.sql` — creates the four M-06 tables and seeds the eight standard KPIs (this feature).
- `M11_OrganizationSettings.sql` — creates the `organization_settings` table (this feature; extends the existing M-11 baseline).
- `scoring_configs` — **already provisioned by feature 002** (M-16 baseline). This feature does NOT touch the schema; only the editing surface is new.

---

## 1. M-06 Tables

### 1.1 `kpi_definitions`

```sql
CREATE TABLE kpi_definitions (
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
    -- NULL for the composite KPI (CXI) AND for NPS (renders via the fixed NPS gauge, not a
    -- configurable representation style); every other non-composite KPI carries one. Matches §4 seed.
    CONSTRAINT representation_null_iff_composite_or_nps
        CHECK ((representation_style IS NULL) = (is_composite OR calculation_method = 'NPSStandard')),
    CONSTRAINT target_required_when_active   CHECK (NOT is_active OR target IS NOT NULL),
    CONSTRAINT show_on_dashboard_implies_active CHECK (NOT show_on_dashboard OR is_active)
);

-- Case-insensitive uniqueness per tenant (enforced inside the tenant schema)
CREATE UNIQUE INDEX kpi_definitions_short_name_lower_uniq
    ON kpi_definitions (LOWER(short_name));

CREATE INDEX kpi_definitions_is_active_idx ON kpi_definitions (is_active);
CREATE INDEX kpi_definitions_kpi_type_idx  ON kpi_definitions (kpi_type);
CREATE INDEX kpi_definitions_created_at_id_idx ON kpi_definitions (created_at DESC, id);  -- cursor pagination
```

**Field notes:**

- `short_name` is **immutable after first save** for every KPI (FR-004) — enforced at the application layer (no DB constraint can prevent updates; the application throws `KPI_SHORT_NAME_IMMUTABLE`).
- For standard KPIs whose `calculation_method` and `scale` are immutable (FR-005), the application enforces it; no DB constraint encodes the seed-set knowledge.
- `target` range: `0..100` for non-NPS KPIs; `-100..+100` for NPS — enforced by `KpiDefinitionValidator` against the row's `scale`.

---

### 1.2 `kpi_thresholds`

One row per KPI; FK to `kpi_definitions`.

```sql
CREATE TABLE kpi_thresholds (
    kpi_id       uuid         PRIMARY KEY REFERENCES kpi_definitions(id) ON DELETE RESTRICT,
    lower_bound  numeric(5,1) NOT NULL,
    x            numeric(5,1) NOT NULL,
    y            numeric(5,1) NOT NULL,
    upper_bound  numeric(5,1) NOT NULL,
    CONSTRAINT threshold_ascending
        CHECK (lower_bound < x AND x < y AND y < upper_bound)
);
```

**Field notes:**

- `lower_bound` / `upper_bound` are conceptually fixed per KPI type (`(0, 100)` for normalised KPIs; `(-100, 100)` for NPS) but stored explicitly so a single integration test can inspect them without re-deriving from the KPI's `scale`.
- The `ON DELETE RESTRICT` is paranoia — no `DELETE` route exists for `kpi_definitions` (FR-002). If a future hard-delete is ever introduced it would have to be a SQL operation that explicitly chains to this table.

---

### 1.3 `kpi_perspectives`

0..10 rows per KPI; FK to `kpi_definitions`.

```sql
CREATE TABLE kpi_perspectives (
    id            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    kpi_id        uuid        NOT NULL REFERENCES kpi_definitions(id) ON DELETE CASCADE,
    label         varchar(60) NOT NULL,
    display_order smallint    NOT NULL,
    created_at    timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX kpi_perspectives_kpi_id_order_idx
    ON kpi_perspectives (kpi_id, display_order);
```

**Field notes:**

- Per-perspective score storage is **deferred to a later M-06 release** per Clarifications session 2026-06-21 (Q1, round 1). This feature persists only the definitions.
- `id` is the stable PK referenced by M-01 question bindings AND (in the future) per-perspective score records.
- `label` is bilingual via free-text; the language is tenant-determined at authoring time. No `label_ar` / `label_en` split because the user enters one label per perspective per save.
- `ON DELETE CASCADE` matches the FR-028 "full replace" save semantics: when the KPI's perspectives are re-saved, the application DELETEs all existing rows and INSERTs the new set in one transaction.

---

### 1.4 `cxi_weights`

0..N rows; only used for the CXI KPI (the row exists only when CXI has members).

```sql
CREATE TABLE cxi_weights (
    cxi_kpi_id    uuid     NOT NULL REFERENCES kpi_definitions(id) ON DELETE RESTRICT,
    member_kpi_id uuid     NOT NULL REFERENCES kpi_definitions(id) ON DELETE RESTRICT,
    weight        smallint NOT NULL,
    created_at    timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (cxi_kpi_id, member_kpi_id),
    CONSTRAINT weight_positive          CHECK (weight > 0),
    CONSTRAINT cxi_cannot_include_itself CHECK (member_kpi_id <> cxi_kpi_id)
);

CREATE INDEX cxi_weights_member_kpi_id_idx ON cxi_weights (member_kpi_id);  -- for cascade lookups
```

**Field notes:**

- `weight` is `smallint` (range 1..32767) — relative integer (FR-042).
- `weight > 0` per-row CHECK ensures the BR-2.3 "weight of 0 = not configured" rule (a 0-weight member is simply not inserted).
- The `cxi_weights_member_kpi_id_idx` covers the deactivation-cascade scan ("for KPI X, which CXI rows have it as a member?") that runs on every deactivation.
- The application enforces FR-043 (CXI cannot be activated unless ≥ 2 non-zero weights) — no DB constraint encodes this cross-row rule.

---

## 2. M-11 Table (Organization Settings)

### 2.1 `organization_settings`

Exactly one row per tenant.

```sql
CREATE TABLE organization_settings (
    id              uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(150) NOT NULL,
    logo_blob_ref   varchar(500) NULL,
    industry        varchar(32)  NOT NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NOT NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NOT NULL,
    CONSTRAINT industry_valid
        CHECK (industry IN ('Banking','Telecommunications','Government','Automotive','Entertainment','Services'))
);
```

**Singleton enforcement** (one row per tenant) via a partial unique index:

```sql
CREATE UNIQUE INDEX organization_settings_singleton_uniq ON organization_settings ((true));
```

(Yields a single allowable row in the tenant schema; INSERTs of a second row collide and fail.)

**Field notes:**

- `name` is required; ≤ 150 chars (FR-050).
- `logo_blob_ref` is the opaque storage key returned by `ILogoStore.PutAsync` (R3); nullable when no logo has been uploaded yet.
- `industry` enum mirrors `IIndustryEnumProvider.GetAll()` exactly (R13); the CHECK is a defence in depth — the application validates against the same list pre-write.

**Seed**: organisation_settings is seeded **only with `industry=null` placeholder** — no, that doesn't work, the column is `NOT NULL`. Actually: per the spec, the row exists on a freshly provisioned tenant (FR-S5/T-04). The provisioning workflow (M-11 owner) inserts a row with `name = <tenantName from provisioning>`, `industry = <provisioning's industry choice>`, `logo_blob_ref = NULL`. This feature does NOT introduce the row — it provides the editing surface for it.

---

## 3. M-16 Table (Referenced, NOT Re-Created)

### 3.1 `scoring_configs`

**Provisioned by feature 002** (M-16 baseline, reshaped to a **per-tenant singleton** by feature 002 US-2 Amendment — SRS §4.2.9 Q11). This feature surfaces it but does not own the schema.

> ⚠️ **Canonical store / `tenant_scoring_config` retired.** `scoring_configs` (one row per tenant) is the single canonical ScoringConfig store consumed by both M-16 and M-06. The separate `tenant_scoring_config` table introduced by this feature's first US-4 cut is **dropped** — `IScoringConfigStore` reads/writes `scoring_configs` (see tasks.md T098a). Do NOT create a new per-tenant table.

Reference shape (per feature 002 `data-model.md` §`scoring_configs`):

```sql
-- (Existing schema — referenced for context, not re-created)
CREATE TABLE scoring_configs (
    id                  uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
    alpha               numeric(4,3)  NOT NULL DEFAULT 0.500,
    mot_multiplier      numeric(3,1)  NOT NULL DEFAULT 1.5,
    n_floor             integer       NOT NULL DEFAULT 100,
    flag_percentile     integer       NOT NULL DEFAULT 25,
    rolling_window_days integer       NOT NULL DEFAULT 30,
    created_at          timestamptz   NOT NULL DEFAULT now(),
    updated_at          timestamptz   NOT NULL DEFAULT now(),
    updated_by          uuid          NOT NULL,
    CONSTRAINT alpha_range            CHECK (alpha BETWEEN 0.000 AND 1.000),
    CONSTRAINT mot_range              CHECK (mot_multiplier BETWEEN 1.0 AND 2.0),
    CONSTRAINT n_floor_min            CHECK (n_floor >= 1),
    CONSTRAINT flag_percentile_range  CHECK (flag_percentile BETWEEN 1 AND 49),
    CONSTRAINT rolling_window_min     CHECK (rolling_window_days >= 7)
);

-- Singleton per tenant
CREATE UNIQUE INDEX scoring_configs_singleton_uniq ON scoring_configs ((true));
```

**Feature 002 default mismatch (Q-S1 — RESOLVED by the reshape)**: the original feature 002 table shipped `n_floor` default = 5 (M-16 SRS v0). The feature 002 US-2 Amendment reshape sets the column default to **100** (aligned with this feature's FR-056), so new tenants seed `n_floor = 100` directly. The corrective `UPDATE` below remains only as a one-time fix-up for any tenant schema provisioned before the reshape; it is owned by **M-16** (not M-06) per the schema-ownership rule — M-06 must not directly mutate M-16-owned tables, even at migration time. The fix-up ships as a new M-16-owned migration `tools/Nabadat.Migrations/Migrations/M16_NFloorDefaultBump.sql`:

```sql
-- Owned by M-16; runs once per tenant schema; aligns n_floor default
-- to the value required by Feature 003 spec.md FR-056.
UPDATE scoring_configs
   SET n_floor = 100
 WHERE n_floor = 5;
```

The migration runner orders migrations alphabetically by filename — `M06_Baseline.sql` runs before `M11_OrganizationSettings.sql`, and `M16_NFloorDefaultBump.sql` runs after both. A future M-16 SRS revision will additionally align the schema-level DEFAULT at the column definition.

---

## 4. Seed Data (`M06_Baseline.sql`)

Eight rows inserted in canonical order (NPS, CSAT, CES, CXI, FCR, VFM, AgentScore, CHS) in one transaction with `ON CONFLICT (LOWER(short_name)) DO NOTHING`.

| short_name | full_name | kpi_type | is_composite | calc_method | scale | top_n | repr_style | emoji_set | target | thresholds (lower / x / y / upper) |
|------------|-----------|----------|:------------:|-------------|-------|------:|------------|-----------|-------:|-------------------------------:|
| NPS | Net Promoter Score | Standard | false | NPSStandard | Scale0_10 | NULL | NULL | NULL | 50 | -100 / 0 / 30 / 100 |
| CSAT | Customer Satisfaction Score | Standard | false | WeightedAverage | Scale1_5 | NULL | Number | NULL | 80 | 0 / 20 / 70 / 100 |
| CES | Customer Effort Score | Standard | false | WeightedAverage | Scale1_7 | NULL | Number | NULL | 80 | 0 / 20 / 70 / 100 |
| CXI | Customer Experience Index | Standard | **true** | WeightedComposite | NULL | NULL | NULL | NULL | 80 | 0 / 20 / 70 / 100 |
| FCR | First Contact Resolution | Standard | false | WeightedAverage | Scale1_3 | NULL | Number | NULL | 80 | 0 / 20 / 70 / 100 |
| VFM | Value for Money | Standard | false | WeightedAverage | Scale1_5 | NULL | Number | NULL | 80 | 0 / 20 / 70 / 100 |
| AgentScore | Agent Score | Standard | false | WeightedAverage | Scale1_5 | NULL | Number | NULL | 80 | 0 / 20 / 70 / 100 |
| CHS | Customer Happiness Score | Standard | false | WeightedAverage | Scale1_5 | NULL | Number | NULL | 80 | 0 / 20 / 70 / 100 |

Notes:

- NPS threshold `(x=0, y=30)` per Clarifications round 2 Q1 (session 2026-06-21).
- CXI: `scale` and `representation_style` are NULL (composite). CXI has no rows in `kpi_perspectives` and no rows in `cxi_weights` until the tenant configures it.
- All seeds: `is_active=true`, `show_on_dashboard=false`, `created_by=updated_by=<system_uuid>` (a reserved actor for migration-time writes).
- `kpi_perspectives` is NOT pre-seeded for any KPI — perspectives are tenant-authored.

The seed is followed by one M-17 `event_log` row per KPI with `event_type='settings.changed'`, `action='created'`, and the full document captured in the diff payload — produced by the migration runner so the audit trail is complete from provisioning onward.

---

## 5. Cross-Module Reads (Published Interfaces — Recap)

This feature does not introduce any cross-schema queries. All cross-module data exchange happens through published interfaces:

| Caller | Interface | Direction | Purpose |
|--------|-----------|-----------|---------|
| M-06 | `M-16.IJourneyBindingQuery` | M-06 → M-16 | FR-026 binding-usage probe |
| M-06 | `M-16.IScoringConfigStore` | M-06 → M-16 | FR-053–FR-061 read/write of `scoring_configs` |
| M-06 | `M-11.IIndustryEnumProvider` | M-06 → M-11 | FR-050 industry dropdown list |
| M-06 | `M-11.IOrganizationSettingsStore` | M-06 → M-11 | FR-050–FR-052 Organization read/write |
| M-06 | `M-11.ILogoStore` | M-06 → M-11 | FR-050 logo upload + retrieval |
| M-06 | `M-10.IPermissionService` | M-06 → M-10 | API-03 RBAC checks (mirrored in UI) |
| M-06 | `M-17.IEventPublisher` | M-06 → M-17 | `settings.changed` event emission |
| M-01 / M-07 / M-09 | `M-06.IKpiConfigReader` | M-06 ← consumers | Read active KPI definitions |

Interface signatures and DTOs are defined in [contracts/published-interfaces.md](contracts/published-interfaces.md).

---

## 6. Migration Ordering

1. Apply existing M-11 baseline (already in place — no change).
2. Apply existing M-16 baseline (feature 002; already in place — no change).
3. Apply `M06_Baseline.sql` — creates four M-06 tables + seeds 8 standard KPIs.
4. Apply `M11_OrganizationSettings.sql` — creates `organization_settings` table + singleton index.
5. Apply `M16_NFloorDefaultBump.sql` (M-16-owned) — corrective `UPDATE scoring_configs SET n_floor=100 WHERE n_floor=5` per §3.1.

The platform's migration runner sorts by filename so the order is deterministic (M06 → M11 → M16). Steps 3, 4, and 5 are otherwise independent — none depends on schema additions from the others.

**Rollback** files (`M06_Baseline_Rollback.sql`, `M11_OrganizationSettings_Rollback.sql`) drop the tables in reverse FK order; `M16_NFloorDefaultBump.sql` does NOT have a rollback file (n_floor=100 is the canonical default going forward and reversing it would re-create the original bug).

---

## 7. State Transitions

### 7.1 `kpi_definitions.is_active`

Active ⇄ Inactive — bidirectional toggle. Side-effects on transition to Inactive:

- `show_on_dashboard` forced to `false` (per FR-027).
- Every row in `cxi_weights` where `member_kpi_id = <id>` deleted in the same transaction (per FR-026 / FR-044).
- Exactly ONE `settings.changed` event emitted (per FR-026, Clarifications round 1 Q2).

Re-activation does NOT auto-restore `show_on_dashboard` or `cxi_weights` rows — the tenant manually re-enables them.

### 7.2 `kpi_definitions.show_on_dashboard`

Always `false` when `is_active = false` (CHECK constraint `show_on_dashboard_implies_active`). The application toggles it independently when `is_active = true`.

### 7.3 No other lifecycle states

`kpi_definitions` has no soft-delete flag, no archived status, no draft state. Active and Inactive are the only states.

---

## 8. Audit Trail Coverage

Every save operation against any of the six tables emits one M-17 `event_log` row in the **same transaction** as the data change:

| Operation | event_type | entity_type | Notes |
|-----------|-----------|-------------|-------|
| Create custom KPI | `settings.changed` | `kpi` | action=`created`, diff = full document |
| Edit KPI | `settings.changed` | `kpi` | action=`updated`, diff = changed fields only |
| Activate KPI | `settings.changed` | `kpi` | action=`activated`, diff includes `is_active` |
| Deactivate KPI | `settings.changed` | `kpi` | action=`deactivated`, diff includes `is_active`, `show_on_dashboard`, and the `cxi_side_effect` array |
| Update CXI weights | `settings.changed` | `kpi` | action=`updated`, diff carries `cxi_weights` array |
| Update perspectives | `settings.changed` | `kpi` | action=`updated`, diff carries full new perspectives list |
| Update Organization settings | `settings.changed` | `organization` | diff = changed fields only |
| Upload logo | `settings.changed` | `organization` | action=`logo_replaced`, diff carries `from_blob_ref` and `to_blob_ref` |
| Update ScoringConfig | `journey.scoring_config.updated` | `scoring_config` | per AMENDMENT-007 |

A no-op save (POST identical payload as current state) emits NO event (per the Edge Cases "ScoringConfig idempotent save" rule, extended to all entities).
