# Data Model: M-15 Action Management

**Feature**: `005-action-management` | Derived from spec.md "Key Entities" (SRS Appendix A) +
FR-M01..M17 (measurement model) + FR-203..FR-210 (field-level validation).

All tables live in the tenant schema (`tenant_{slug}`), owned exclusively by
`Nabadat.ActionManagement` (DB-02/AD-02 — no `tenant_id` column). Primary keys are UUID
(DB-03). `created_at`/`updated_at` in UTC (Article 4.3); presentation applies tenant timezone
per BR-022/NFR-8. Time-dependent fields are computed via injected `TimeProvider`, never
`DateTime.UtcNow` (DB-08 rule 7).

---

## 1. `Action`

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `action_name` | text | required, ≤120 chars, **unique per tenant, case-insensitive, across all statuses incl. Archived** (VAL-201/202) | |
| `description` | text | optional, ≤500 chars, plain text (VAL-205) | live "{n}/500" counter is a frontend concern only |
| `action_start_date` | date | required (VAL-203); D1; retro-dating allowed | Baseline snapshot anchor |
| `action_end_date` | date | required (VAL-203); D2; `≥ action_start_date` (VAL-204) | boundary only, never itself evaluated |
| `archived` | boolean | default `false` | standalone presentation status (BR-009); never combined with Planned/Active/Completed in the UI |
| `created_by` | UUID (user id) | required | audit attribution only — **no assignee concept** (R-6) |
| `created_at` | timestamptz | required, UTC | |
| `updated_at` | timestamptz | required, UTC | bumped on every field-level edit; drives ERR-8 stale-save detection |
| `last_kpi_event_watermark` | UUID, nullable | FK-by-value only (no cross-module FK, Article 4.1) → `event_log.id` | high-water mark for the lazy `KpiForceDeactivationCascade` consumer (research.md §4.3); **not exposed in any API response** |

**Derived (never stored as columns — computed at read time per BR-F2/FR-102)**:
- `target_start_date` = `action_end_date + 1 day` (D3, BR-006 — always derived, read-only)
- `latest_target_date` = `max(kpi_targets.target_date)` for this action's targets
- `status` ∈ `Planned | Active | Completed | Archived` — computed by `ActionStatusCalculator`
  (FR-102, FR-L01): `archived → Archived`; else `action_start_date > today → Planned`;
  `latest_target_date < today → Completed`; else `Active` (day-granular, tenant timezone)

**Validation summary** (owned by `ThresholdValidator`/action-level validators, VAL-201..207,
211 — exact messages in spec FR-208, shipped copy, not to be reworded):
- VAL-201 Name required · VAL-202 Name unique (case-insensitive, all statuses) ·
  VAL-203 Start/End required · VAL-204 `End ≥ Start` · VAL-205 Description ≤ 500 (input-limited) ·
  VAL-207 ≥ 1 active Target required · VAL-211 one Target per KPI (enforced via disabled options,
  not a Target-level DB constraint beyond the unique index below)

**Indexes**: unique index on `lower(action_name)` per tenant (VAL-202); index on
`(action_start_date)` and computed status is not indexed (computed, not stored) — list
filtering (FR-107) queries raw dates and joins `kpi_targets` for KPI/date-range filters.

---

## 2. `KpiTarget`

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `action_id` | UUID | FK → `Action.id`, required | intra-module FK (Article 4.1 permits FKs within a module's own tables) |
| `kpi_id` | UUID | required; **unique per `action_id`** (BR-001/VAL-211) | references M-06's KPI id **by identifier only** — no FK (Article 4.1: cross-module references use the target's identifier, never a FK) |
| `target_date` | date | required; `> action_end_date` (VAL-206); D4 | retro-dating allowed |
| `lower_threshold` (L) | numeric(6,1) | required; `0 ≤ L ≤ U`; step 0.5 (VAL-209) | delta over Baseline |
| `upper_threshold` (U) | numeric(6,1) | required; `U > 0` (VAL-210); `L ≤ U ≤ X` (tenant `action_settings.max_upper_threshold`) | delta over Baseline |
| `active` | boolean | default `true` | manual toggle (FR-207) |
| `deactivation_source` | text, nullable | `manual` \| `forced` \| `null` | `null` when `active = true`; set on deactivate, cleared on reactivate |
| `baseline_score` | numeric(9,4), nullable | captured, not user-entered | null until Action reaches/passes `action_start_date` (FR-M07) |
| `baseline_captured_for_date` | date, nullable | | the date the baseline was captured *for* (= `action_start_date` at capture time; changes on recapture, BR-B2) |
| `final_score` | numeric(9,4), nullable | | populated once `target_date < today` (FR-M14) |
| `outcome` | text, nullable | `successful` \| `partially_successful` \| `unsuccessful` \| `null` | **derived and cached at evaluation time** from `{baseline_score, lower_threshold, upper_threshold, final_score}` (BR-O6 — computed from stored data, never a hand-set label); recomputed if any of those four inputs changes before the Target Date (e.g. threshold edit mid-monitoring, DLG-3) |
| `created_at` / `updated_at` | timestamptz | UTC | |

**Derived (computed at read time from the row + a live M-06 current score — never stored)**:
- `score_progress_raw` = `(current_score − baseline_score) / (baseline_score + upper_threshold − baseline_score)` = `(current − baseline) / upper_threshold` (FR-M08)
- `score_progress_display` = `clamp(score_progress_raw, 0, 100%)` (BR-F2)
- `time_progress_raw` = `(today − target_start_date) / (target_date − target_start_date)`, forced to `0` while `today ≤ action_end_date` (BR-F1/FR-M09/M11)
- `time_progress_display` = `clamp(time_progress_raw, 0, 100%)`
- `timer_state` ∈ `Green | Yellow | Red | Grey | Empty` (FR-M10, `TimerColourResolver`)
- `is_lowest_performing` — computed tenant/action-scope, not stored (FR-M15)

**Validation summary** (VAL-206, 208, 209, 210, 211 — spec FR-208): Target Date after Action End
Date · KPI required per active Target · `0 ≤ L ≤ U ≤ X` (clamped by the control, 1 dp) ·
`U > 0` on every active Target (division guard, BR-F3) · one Target per KPI per Action.

**Lifecycle** (`TargetLifecycleStateMachine`, FR-L03): `Active → Deactivated(manual)` (toggle
off) → `Active` (reactivate, always allowed) or `Deleted` (DLG-1, only while deactivated,
BR-012); `Active → Deactivated(forced)` (M-06 KPI deactivation cascade, BR-011) →
`Active` (reactivate, **only** once the KPI is Active again in M-06) or `Deleted`. **Delete is
refused if it is the Action's last remaining Target, in any state** (R-17, stakeholder-ratified
22 Jul 2026 — prevents an orphan Action with an undefined `latest_target_date`).

**Indexes**: unique index on `(action_id, kpi_id)` (VAL-211); index on `(target_date)` for
lifecycle-transition sweeps; index on `kpi_id` for the force-deactivation cascade lookup.

---

## 3. `ActionSettings` (SET-1/SET-2, SRS §11)

Single row per tenant (mirrors `Nabadat.KpiManagement`'s `OrganizationSettingsStore` singleton
pattern) — not a per-Action or per-user table.

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `tenant_key` | text | PK (fixed literal, e.g. `"default"`) — single-row-per-tenant-schema convention | schema boundary already isolates tenants (AD-02); no `tenant_id` column needed |
| `max_upper_threshold` (X) | numeric(6,1) | default `20`; `> 0`; **cannot be set below `max(kpi_targets.upper_threshold)` across the tenant, including Archived Actions** (SET-1 guard) | |
| `slider_padding` (PAD) | integer | default `3`; `≥ 1` | |
| `last_kpi_event_watermark` | UUID, nullable | | the tenant-wide watermark referenced from `Action.last_kpi_event_watermark` (kept here too so a first-Action tenant still has a well-defined starting point) |
| `updated_at` | timestamptz | UTC | |

**Validation** (`SettingsUpdateValidator`, SET-1/SET-2): `X > 0` and `X ≥ max(saved U)` (blocked
message: "Cannot set the maximum below an existing Upper Threshold ({largest U})"); `PAD` a
positive integer `≥ 1` ("PAD must be a positive integer").

---

## 4. `EventLog` (shared, write-side mapping only — not owned by this module)

`Nabadat.ActionManagement` maps its own `Domain/Entities/EventLog.cs` onto the **existing**
per-tenant `event_log` table (the same table `Nabadat.KpiManagement`, `Nabadat.UserManagement`,
and `Nabadat.CustomerJourneyManagement` already map independently) — never a new table, and
never read/written via another module's DbContext (Article 3.2). Event-type catalogue this
module writes (INT-04, exact names):

`action.created` · `action.field_edited` · `baseline.captured` · `baseline.recaptured` ·
`target.added` · `target.activated` · `target.deactivated` (attribute: `source = manual|forced`)
· `target.deleted` · `action.archived` · `action.unarchived` · `action.status_transitioned` ·
`outcome.evaluated` · `settings.X_changed` · `settings.PAD_changed`.

`action.created` and `action.completed` are the two rows already registered in the constitution
Event Catalogue (Section 4, source `M-15`, no downstream consumers registered at Phase 1). The
remaining event types above are M-15-internal audit detail (INT-04 — "audit trail data
requirement is in scope; the viewer screen is not") and do not require Event Catalogue
registration unless a downstream module later subscribes to them.

---

## 5. Entity relationship summary

```
Action (1) ──< (many) KpiTarget
   │
   └── status, target_start_date, latest_target_date: ALL DERIVED, never columns

ActionSettings — single row, tenant-wide, referenced by validation only (not FK'd)

EventLog — shared table, written by this module for every mutation (INT-04)

KpiTarget.kpi_id — identifier reference only, no FK, to M-06's kpi_definitions
  (Article 4.1: cross-module references use the target's identifier, never a FK)
```

## 6. State transitions

### Action status (FR-L01/L02 — computed, never a manual transition among the three)

```
        start_date > today                  latest_target_date < today
Planned ───────────────────► Active ───────────────────────────────► Completed (read-only, BR-023)
   ▲                            ▲                                         │
   │        Archive (any status, no confirm) ──► Archived ◄────────────────┘
   │                            │
   └────────── Unarchive (recomputes from dates; may land directly in Completed) ─┘
```

Archived is a **presentation overlay**, not a branch of this diagram — measurement, per-Target
evaluation, and this exact transition graph keep running underneath while `archived = true`
(BR-009). Unarchive re-enters the graph at whatever status the dates currently compute to.

### KpiTarget lifecycle (FR-L03)

```
Active ──deactivate(manual)──► Deactivated(manual) ──reactivate──► Active
   │                                    │
   │                              delete (DLG-1, blocked if last remaining Target — R-17)
   │
   └──M-06 deactivates KPI (BR-011)──► Deactivated(forced) ──reactivate (only if KPI active again)──► Active
                                                │
                                          delete (DLG-1, same last-remaining-Target guard)
```

### Outcome evaluation (FR-M14, computed once `target_date < today`, never re-computed after
unless a guarded edit — DLG-2/3 — explicitly reopens it before the Target Date)

```
final_score ≥ baseline + U            → Successful       (--d2)
baseline + L ≤ final_score < baseline + U → Partially Successful (--d3)
final_score < baseline + L            → Unsuccessful      (--d5)
U = L (equality, BR-O4)               → binary: Successful iff final_score ≥ baseline + U, else Unsuccessful
```
