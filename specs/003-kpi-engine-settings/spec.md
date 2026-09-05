# Feature Specification: CX Metrics & KPI Engine (M-06) + Platform Settings

**Feature Branch**: `003-kpi-engine-settings`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "@SRS-M06-KPI-Engine-and-Settings-v1_0.docx — Module 06 (CX Metrics & KPI Engine) KPI Management + KPI Configuration surfaces, plus the Platform Settings page (Organization + Customer Journey scoring configuration), aligned to prototype screens."

---

## Overview *(non-normative summary)*

A CX Program Manager (P-01) at a tenant uses this feature to:

1. Browse the **KPI catalogue** (eight standard KPIs seeded at provisioning — NPS, CSAT, CES, CXI, FCR, VFM, Agent Score, CHS — plus any custom KPIs they have added), filter and search it, and pick a KPI to configure.
2. **Configure a single KPI** through a two-panel page: a configuration form on the left (Short Name, Full Name, Perspectives, Calculation Method, Scale, Scale Endpoint Descriptions, Representation Style, Threshold bands, Target, Active, Show on Dashboard) and a live preview on the right (a Question Preview card and a Dashboard Preview semicircular gauge that re-renders within ~100ms of any edit).
3. **Configure the CXI composite KPI** through a special variant of the same page that replaces scale/representation/method fields with a weights table over the tenant's currently active KPIs (excluding CXI itself), with relative-integer weights that the engine normalises into effective proportions.
4. **Activate or deactivate** any KPI — including standard KPIs — with a confirmation flow when the KPI is bound to active touchpoints in M-16 Journey Mapping, preserving historical data and unbinding the KPI from future scoring.
5. **Configure tenant-wide Customer Journey scoring parameters** (α / β blend, MOT multiplier, n_floor, flag percentile, rolling-window days) on the Platform Settings page — the canonical editing surface for the M-16-owned ScoringConfig entity, consumed by both M-16 and M-06.
6. **Configure Organization settings** (Name, Logo, Industry) on the Platform Settings page.

The feature spans backend (M-06 KPI definition CRUD + M-11 ScoringConfig surfacing + Organization settings) and frontend (the `frontend/portal/` SPA — KPI Management list, KPI Configuration form, Settings landing + section pages). The score-computation engine, the M-01 question→KPI binding surface, M-07 dashboard rendering, and M-09 alerting are explicitly out of scope.

---

## Clarifications

### Session 2026-06-21

- Q: Does this feature ship per-perspective score storage + computation, or only perspective definitions? → A: B — definitions only; per-perspective score computation and storage are explicitly deferred to a later M-06 release. This feature owns `KPIPerspective` (id, label, display_order) so M-01 can bind questions against them; the perspective-score table and its compute pipeline ship with the M-06 score-computation engine release.
- Q: When a CXI member KPI is deactivated and CXI weights recompute as a side effect, how many `settings.changed` events are emitted? → A: B — exactly ONE `settings.changed` event, sourced on the member KPI's deactivation, with the CXI side-effect captured inside the diff payload as a nested `cxi_side_effect: { cxi_kpi_id, removed_member_kpi_id, recomputed_effective_percentages }`. No separate event is emitted for the CXI mutation. Audit consumers read both facts from a single atomic event row.
- Q: How does the live `[X] Active KPIs` subtitle reflect activation changes from other browser sessions? → A: A — same-session only. The subtitle updates immediately on activations done in the current session; activations from other sessions appear on the next route navigation or explicit reload. No polling, no SSE, no WebSocket. The frontend does NOT open a long-lived connection and does NOT poll the catalogue endpoint on a timer.
- Q: What named Emoji Sets ship in v1, and how are they defined? → A: A — exactly two **static** sets shipped as code-level definitions: `FaceClassic` (graduated faces: 😞 🙁 😐 🙂 😊 😄 😍 — the reference sequence from worst to best) and `HandThumbs` (👎 / 👎🏻 / ✋ / 👍🏻 / 👍 — the reference sequence from worst to best). Each set is a platform-owned ordered emoji pool. At render time, the platform picks the K consecutive glyphs from a set's sequence that match the active scale's K value-count (e.g., for a 1–5 scale K=5; for a 0–10 scale K=11, with `FaceClassic` extending its sequence accordingly — the exact per-K slot assignments are an implementation detail finalised in /plan). Tenants cannot define new sets in v1; adding a set is a platform release.
- Q: Does deactivating a *standard* KPI require additional guardrails beyond the standard binding-usage confirmation applied to custom KPIs? → A: A — same guardrails for standard and custom. Both follow the existing FR-026 binding-usage confirmation. No extra warning copy for standards, no additional role gate. Audit captures actor + diff identically. Standard KPIs cannot be deleted (BR-1.1) but they CAN be deactivated by P-01 under the same flow used for custom KPIs.
- Q: What are the create-mode threshold defaults for NPS, whose threshold inputs run on `−100..+100` rather than `0..100`? → A: B — `x = 0, y = 30`. This maps the bands to the industry-standard NPS interpretation: Detractors-dominant (−100..0) = Unsatisfactory, Mixed (0..30) = Average, Promoters-leaning (30..+100) = Satisfactory. These are **seed defaults only** and remain tenant-editable like any other KPI's thresholds — a tenant admin (P-01) can reconfigure them on the KPI Configuration page at any time.
- Q: How does the platform handle the XSS risk on SVG logo upload? → A: B — server-side sanitisation on upload. SVG is accepted (per the SRS), but the payload MUST be run through a hardened SVG sanitiser before persistence: every `<script>`, `<foreignObject>`, `<iframe>`, `<use>` with external `href`, and every `on*` event-handler attribute is stripped. The PERSISTED logo is the SANITISED payload, never the original. Any upload whose payload cannot be made safe (e.g., bytes are not parseable as SVG, or contains content the sanitiser cannot strip) is rejected with API code `LOGO_SVG_UNSAFE_CONTENT` and the UI shows "Logo could not be uploaded — the SVG file contains content that is not allowed." PNG and JPEG uploads are unaffected.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse the KPI catalogue and pick a KPI to configure (Priority: P1)

As a CX Program Manager, I open the KPI Management page from the platform's PLATFORM nav section, see a live "[X] Active KPIs" subtitle, filter the table by Type (All / Standard / Custom) and "Active only", search by Short or Full Name, and either click an existing row to edit it or click "+ Add KPI" to create a new custom KPI. The eight standard KPIs are always present in their canonical order and cannot be deleted — there is no delete control on any row and no delete endpoint exposed.

**Why this priority**: Without the catalogue surface there is no entry point to the rest of the module — every other M-06 task starts here. This is the MVP slice that proves the KPI seed-data and tenant isolation work end-to-end.

**Independent Test**: A signed-in P-01 navigates to `/kpi-management`, sees 8 standard KPIs (NPS / CSAT / CES / CXI / FCR / VFM / Agent Score / CHS) in canonical order, sees "8 Active KPIs" in the subtitle, toggles "Active only" off and confirms inactive KPIs become visible (dimmed), switches Type filter to "Standard" and confirms only standards are listed, types "NPS" in the search box and confirms the list narrows in real time, and clicks the NPS row to land on `/kpi-management/<id>` in edit mode. Delivers the catalogue value on its own.

**Acceptance Scenarios**:

1. **Given** a freshly provisioned tenant, **When** a P-01 user opens `/kpi-management`, **Then** the table lists exactly the 8 seeded standard KPIs in the canonical order (NPS, CSAT, CES, CXI, FCR, VFM, Agent Score, CHS) and the header subtitle reads "8 Active KPIs".
2. **Given** a tenant with 8 standard + 3 custom KPIs (1 inactive), **When** the user toggles "Active only" off, **Then** all 11 rows appear with the inactive row visually dimmed, and the subtitle still reads "10 Active KPIs".
3. **Given** the user on the catalogue with the "Active only" checkbox on, **When** they deactivate a KPI elsewhere, **Then** the "[X] Active KPIs" subtitle decrements immediately without a page refresh.
4. **Given** a P-02 Analyst signed in, **When** they open `/kpi-management`, **Then** the list is visible but "+ Add KPI" is hidden and rows open in read-only mode.
5. **Given** any user, **When** they look at any row of the table, **Then** no delete control is rendered, and the API surface exposes no `DELETE /api/v1/kpis/{id}` endpoint.
6. **Given** the user types "NPS" in the search box, **When** they release the key, **Then** only rows whose Short Name or Full Name match "NPS" (case-insensitive substring) remain visible.

**Unit Test Coverage**:

- **Units under test**: `KpiCatalogueQuery` (filter + search + ordering composition), `KpiListItemMapper` (KPIDefinition → list-row DTO), `KpiSeedDataProvider` (defines the 8 canonical KPIs + their canonical order).
- **Required cases**:
  - `KpiCatalogueQuery.Build(type: All, activeOnly: true, search: null)` against a tenant with 8 active + 1 inactive custom KPI → returns 8 rows, standards first in canonical order [NPS, CSAT, CES, CXI, FCR, VFM, AgentScore, CHS].
  - `KpiCatalogueQuery.Build(type: Custom, activeOnly: false, search: null)` against the same tenant → returns 1 row (the inactive custom KPI).
  - `KpiCatalogueQuery.Build(type: All, activeOnly: false, search: "nps")` → returns the NPS row only (case-insensitive substring against ShortName ∪ FullName).
  - `KpiCatalogueQuery.Build(type: All, activeOnly: false, search: "  ")` → treats whitespace-only search as null (returns all rows).
  - `KpiListItemMapper.Map(KPIDefinition{shortName:"NPS", calculationMethod:NPSStandard, scale:Scale0_10})` → `{shortName:"NPS", calcMethodLabel:"NPS Standard", scaleLabel:"0–10", ...}`.
  - `KpiListItemMapper.Map(KPIDefinition{isComposite:true, scale:null})` → `{scaleLabel:"—"}`.
  - `KpiSeedDataProvider.Seed()` returns exactly 8 rows in the canonical order with the expected `(shortName, fullName, isComposite, calculationMethod)` tuples.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `GET /api/v1/kpis` returns 200 with the 8 seeded KPIs after fresh provisioning; ordering matches canonical seed order.
  - `GET /api/v1/kpis?type=Standard&active_only=true` returns the standard active subset; `?search=NPS` filters to NPS only.
  - `GET /api/v1/kpis` as P-02 Analyst returns 200; as a persona with no list permission returns 403 with the API-05 envelope.
  - There is **no** `DELETE /api/v1/kpis/{id}` route registered (asserted via a route-table inspection test).
- **What's intentionally NOT covered end-to-end**: Case-insensitive substring matching mechanics (covered by `KpiCatalogueQuery` unit tests).

**Scenario Test**:

- `scenario-test: not-needed — the Independent Test is a single GET round-trip; the API test covers the same surface.`

**E2E Test Coverage**:

- **User flows under test**: KPI Management page (`/kpi-management`).
- **Required scenarios**:
  - `KpiManagement_lists_eight_standard_kpis_in_canonical_order_when_tenant_is_freshly_provisioned`
  - `KpiManagement_filters_by_type_when_user_selects_Standard`
  - `KpiManagement_dims_inactive_rows_when_active_only_is_off`
  - `KpiManagement_narrows_list_when_user_types_in_search`
  - `KpiManagement_navigates_to_config_edit_when_row_is_clicked`
  - `KpiManagement_navigates_to_config_create_when_add_kpi_is_clicked`
  - `KpiManagement_hides_add_kpi_button_when_user_is_analyst`
  - `KpiManagement_redirects_to_login_when_user_is_signed_out`

---

### User Story 2 - Create or edit a non-CXI KPI with a live preview (Priority: P1)

As a CX Program Manager, I open the KPI Configuration page in create mode (via "+ Add KPI") or edit mode (via a row click). I fill in the form fields — Short Name (max 20 chars, unique per tenant, immutable after create), Full Name (max 100 chars), Perspectives (0–10 tag chips), Calculation Method (Weighted Average / TOP n Box / NPS Standard, with the n input revealed for TOP n Box), Scale (0–10, 1–3, 1–5, 1–7, 1–10, 1–100), Minimum/Maximum Scale Description (≤60 chars each, optional, bilingual), Representation Style (Number / Stars / Emoji / Slider, with the Emoji Set selector revealed for Emoji and Slider only enabled for 1–3 scales), Threshold band edges x and y, Target, Active, and Show on Dashboard. The right panel re-renders the Question Preview and the Dashboard Preview gauge within ~100ms of any change. Save persists atomically; Cancel guards unsaved changes; every change writes to the M-17 audit log via a `settings.changed` event with field-level previous/new values.

**Why this priority**: This is the central editing surface of the module. Without it the catalogue is read-only and tenants cannot operationalise their KPI programme. P1 because it gates everything downstream (M-01 question binding, M-16 journey scoring, M-07 dashboards).

**Independent Test**: A P-01 opens `/kpi-management/new`, fills Short Name="QUAL", Full Name="Service Quality", Calculation Method="Weighted Average", Scale="1–7", Threshold x=20 / y=70, Target=80, Active checked, hits Save, and lands back on the catalogue with the new QUAL row visible. They then open the QUAL row, change Full Name to "Service Quality Score", confirm the Dashboard Preview gauge label updates live, hit Save, and confirm the catalogue row reflects the new full name. Delivers the editing value on its own.

**Acceptance Scenarios**:

1. **Given** the user on `/kpi-management/new`, **When** they enter Short Name="QUAL" and a Short Name "QUAL" already exists for the tenant, **Then** an inline blur-time error "This short name is already in use." appears on the field and Save remains disabled.
2. **Given** the user editing an existing KPI, **When** they look at the Short Name field, **Then** it is read-only, visually greyed, and shows an info tooltip "Short name cannot be changed after creation."
3. **Given** the user selecting Calculation Method = "TOP n Box" on a 1–7 scale, **When** they enter n=4, **Then** a non-blocking warning "Using a high n value may overstate satisfaction. Consider a stricter threshold." renders (because 4 > ½ × (7−1) = 3) and Save remains enabled; with n=7 a blocking inline error "n must be less than the maximum scale value." appears and Save is disabled.
4. **Given** the user editing the NPS standard KPI, **When** they view the form, **Then** Short Name, Calculation Method (NPS Standard), and Scale (0–10) are read-only; the Threshold rows run from −100 to +100; the Dashboard Preview gauge runs −100 to +100 with NPS-specific labels.
5. **Given** the user editing a KPI bound to 3 active touchpoints across 2 journeys, **When** they change Scale from "1–5" to "1–7" and hit Save, **Then** a blocking confirmation "Changing the scale will affect 3 active touchpoints and their historical normalisation. This is a structural change. Continue?" appears; cancelling reverts; confirming saves.
6. **Given** the user with Threshold x=20 / y=70, **When** they drag y to 90, **Then** the gauge's amber/green band boundary repositions within ~100ms and the central numeral, the target marker, and band-coloured visualisation update without a page refresh.
7. **Given** the user has unsaved changes, **When** they click the back arrow, **Then** an unsaved-changes confirmation appears with Save / Discard / Cancel options.
8. **Given** the user enters Target=−5 on a non-NPS KPI, **When** they blur the field, **Then** an inline error "Target must be between 0 and 100." appears and Save is disabled.
9. **Given** a successful Save, **When** the M-17 audit log is inspected, **Then** one `settings.changed` event with `kpi.<short_name>` scope and a per-field `{from, to}` diff exists.

**Unit Test Coverage**:

- **Units under test**: `KpiDefinitionValidator` (cross-field rules), `KpiNormalisationCalculator` (raw → 0–100, includes inverted CES and FCR binary), `KpiThresholdValidator` (band ordering), `KpiBindingUsageProbe` (M-16 binding-count lookup interface), `KpiSaveService` (atomic persistence + audit emission), `TopNBoxWarningRule`.
- **Required cases**:
  - `KpiDefinitionValidator.Validate({shortName:"NPS", calcMethod:NPSStandard, scale:Scale0_10, x:-50, y:50, target:42})` for the seeded NPS row → Valid.
  - `KpiDefinitionValidator.Validate({shortName:"QUAL", calcMethod:WeightedAverage, scale:Scale1_5, x:70, y:20, target:80})` → Invalid("threshold.must_be_ascending").
  - `KpiDefinitionValidator.Validate({shortName:"qual", existingShortNames:["QUAL"]})` → Invalid("short_name.duplicate") (case-insensitive).
  - `KpiDefinitionValidator.Validate({shortName:" QUAL ", existingShortNames:["QUAL"]})` → Invalid("short_name.duplicate") (trim).
  - `KpiDefinitionValidator.Validate({shortName:"X", representationStyle:Slider, scale:Scale1_5})` → Invalid("representation_style.slider_requires_scale_1_3").
  - `KpiDefinitionValidator.Validate({shortName:"X", calcMethod:NPSStandard, isStandard:false})` → Invalid("calculation_method.nps_standard_reserved_for_nps").
  - `KpiDefinitionValidator.Validate({isActive:true, target:null})` → Invalid("target.required_when_active").
  - `KpiNormalisationCalculator.Normalise(Scale1_5, raw:3)` → 50.0.
  - `KpiNormalisationCalculator.Normalise(Scale1_7, raw:7)` → 100.0.
  - `KpiNormalisationCalculator.NormaliseCes(raw:7)` → 0.0 (inverted: high effort = low score).
  - `KpiNormalisationCalculator.NormaliseCes(raw:1)` → 100.0.
  - `KpiNormalisationCalculator.NormaliseFcrBinary(raw:1)` → 100; `(raw:0)` → 0.
  - `KpiNormalisationCalculator.Normalise(Scale.Nps, raw:42)` → 42 (raw passthrough).
  - `KpiThresholdValidator.Validate(lower:0, x:20, y:70, upper:100)` → Valid.
  - `KpiThresholdValidator.Validate(lower:0, x:70, y:20, upper:100)` → Invalid("threshold.not_ascending").
  - `KpiThresholdValidator.Validate(lower:-100, x:-50, y:50, upper:100)` for NPS → Valid.
  - `TopNBoxWarningRule.ShouldWarn(scale:Scale1_7, n:4)` → true (4 > 3 = ½ × 6).
  - `TopNBoxWarningRule.ShouldWarn(scale:Scale0_10, n:5)` → false (5 ≤ 5 = ½ × 10).
  - `TopNBoxWarningRule.IsBlockingError(scale:Scale1_7, n:7)` → true (n ≥ scale max).
  - `KpiSaveService.Save(create, valid definition)` → persists KPIDefinition + KPIThreshold + KPIPerspectives in one transaction; emits one `settings.changed` event with `kpi.<short_name>.created` action; returns the new id.
  - `KpiSaveService.Save(edit, change full_name only)` → persists; emits one `settings.changed` event with a single-field diff.
  - `KpiSaveService.Save(edit, validation failure)` → no rows written, no event emitted, returns the validation error.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/v1/kpis` with a valid custom KPI → 201, new row visible in `GET /api/v1/kpis`, one row in M-17 `event_log` with type `settings.changed`.
  - `POST /api/v1/kpis` with a duplicate Short Name → 400, API-05 envelope code `KPI_SHORT_NAME_DUPLICATE`.
  - `PUT /api/v1/kpis/{nps_id}` attempting to change Short Name → 400 `KPI_SHORT_NAME_IMMUTABLE`.
  - `PUT /api/v1/kpis/{nps_id}` attempting to change Scale or Calculation Method → 400 `KPI_FIELD_IMMUTABLE_FOR_STANDARD`.
  - `PUT /api/v1/kpis/{id}` for a KPI bound to active touchpoints, with a Scale change → 200 only when the caller passes `confirm_structural_change=true`; without that flag → 409 `KPI_SCALE_CHANGE_AFFECTS_BINDINGS` with `affected_touchpoints` and `affected_journeys` in the body.
  - `GET /api/v1/kpis/{id}/binding-usage` returns `{touchpoint_count, journey_count}` from M-16 via its published interface.
  - Atomicity: an induced failure mid-save (e.g., perspective insert violates a constraint) → no KPIDefinition / KPIThreshold / KPIPerspective row, no event emitted.
- **What's intentionally NOT covered end-to-end**: pure validation truth tables (`KpiDefinitionValidator`, `KpiThresholdValidator`, `TopNBoxWarningRule` unit tests).

**Scenario Test**:

- `scenario-test: KpiCreateThenEditScenarioTests` — exercises the create → edit → activate flow: create a custom KPI, retrieve it, edit its Full Name, retrieve and confirm the change, assert exactly two `settings.changed` events on `event_log` (`created` + `updated`).

**E2E Test Coverage**:

- **User flows under test**: KPI Configuration page (`/kpi-management/new`, `/kpi-management/:id`).
- **Required scenarios**:
  - `KpiConfig_creates_custom_kpi_when_form_is_valid_and_saved`
  - `KpiConfig_disables_save_when_required_fields_are_empty`
  - `KpiConfig_shows_inline_error_when_short_name_is_duplicate`
  - `KpiConfig_renders_short_name_read_only_in_edit_mode`
  - `KpiConfig_renders_scale_and_method_read_only_for_nps`
  - `KpiConfig_reveals_emoji_set_dropdown_when_representation_is_emoji`
  - `KpiConfig_resets_representation_to_number_when_scale_leaves_1_3_with_slider_active`
  - `KpiConfig_renders_top_n_warning_when_n_exceeds_half_scale_minus_one`
  - `KpiConfig_blocks_save_when_top_n_equals_scale_max`
  - `KpiConfig_updates_question_preview_within_100ms_of_field_change`
  - `KpiConfig_updates_dashboard_gauge_bands_when_threshold_x_or_y_changes`
  - `KpiConfig_renders_min_max_scale_descriptions_as_anchor_labels_in_preview`
  - `KpiConfig_prompts_unsaved_changes_when_user_navigates_away`
  - `KpiConfig_shows_blocking_confirmation_when_scale_changes_on_bound_kpi`
  - `KpiConfig_renders_form_read_only_for_analyst`

---

### User Story 3 - Configure the CXI composite KPI (Priority: P1)

As a CX Program Manager, I open the seeded CXI KPI on the same configuration page and use its **composite variant**: the Question Preview card is hidden (CXI is computed, not surveyed), the Scale / Representation Style fields are removed, the Calculation Method is locked to "Weighted Composite", and a **KPI Weights table** lists every currently active non-CXI KPI with a positive-integer weight input plus a live "Effective %" column whose engine-normalised proportions update as I type. CXI's Active checkbox is disabled until at least two member KPIs have non-zero weights. When a member KPI is deactivated elsewhere, it disappears from the table and the remaining proportions recompute. The Dashboard Preview gauge runs 0–100 with a proportional weight legend beneath it; the snapshot returned to M-07 carries the member breakdown alongside the composite value.

**Why this priority**: CXI is a seeded standard KPI and the headline dashboard composite. The form variant is unusual enough that it warrants its own story and its own test class.

**Independent Test**: A P-01 opens the seeded CXI row, sees the weights table with all currently active KPIs, sets NPS=3, CSAT=2, CES=1 (total relative units 6), confirms the "Effective %" column shows 50% / 33.3% / 16.7%, confirms the CXI Active checkbox is enabled, hits Save, then deactivates the CSAT KPI and confirms the CXI page now lists only NPS + CES with recomputed proportions (75% / 25%) and a single `settings.changed` audit row. Delivers the CXI editing value on its own.

**Acceptance Scenarios**:

1. **Given** the user on CXI configuration, **When** they look at the form, **Then** the Question Preview card is hidden, the Scale and Representation Style fields are absent, and the Calculation Method field reads "Weighted Composite" and is read-only.
2. **Given** the user assigning weights NPS=3 / CSAT=2 / CES=1, **When** the table renders, **Then** Effective % shows 50% / 33.3% / 16.7% and the total row shows 100%.
3. **Given** the user with only one non-zero weight, **When** they view the Active checkbox, **Then** it is disabled with tooltip "CXI requires at least 2 active KPIs with assigned weights."
4. **Given** the user has saved CXI with NPS / CSAT / CES weights, **When** an admin deactivates CSAT elsewhere, **Then** the CXI weights table no longer lists CSAT and NPS / CES proportions recompute (75% / 25%).
5. **Given** the user views the CXI form, **When** the weights table renders, **Then** CXI itself does not appear in the table (cannot include itself).
6. **Given** CXI is shown on the main dashboard, **When** M-07 reads the CXI score snapshot, **Then** the snapshot includes the composite normalised score AND a `member_breakdown` array of `{kpi_id, kpi_short_name, score, effective_percentage}`.

**Unit Test Coverage**:

- **Units under test**: `CxiWeightNormaliser` (relative integers → effective percentages), `CxiActivationRule` (enables Active only when ≥2 non-zero weights), `CxiMemberMembershipRule` (auto-removes deactivated KPIs, forbids self), `CxiSnapshotComposer` (composes the member-breakdown payload).
- **Required cases**:
  - `CxiWeightNormaliser.Normalise([{nps:3},{csat:2},{ces:1}])` → `[{nps:50.0}, {csat:33.3}, {ces:16.7}]` (sum = 100.0 within ±0.1).
  - `CxiWeightNormaliser.Normalise([{nps:1},{csat:1}])` → `[{nps:50.0},{csat:50.0}]`.
  - `CxiWeightNormaliser.Normalise([])` → `[]` (no division-by-zero).
  - `CxiWeightNormaliser.Normalise([{nps:0},{csat:0}])` → `[]` (zero weights treated as not-included per BR-2.3).
  - `CxiActivationRule.CanActivate([])` → false; `([{nps:5}])` → false (only 1 non-zero); `([{nps:1},{csat:1}])` → true.
  - `CxiMemberMembershipRule.OnKpiDeactivated(memberSet=[nps,csat,ces], deactivated=csat)` → memberSet=[nps,ces].
  - `CxiMemberMembershipRule.Add(memberSet=[], candidate=CXI_ID)` → throws `CxiCannotIncludeItself`.
  - `CxiSnapshotComposer.Compose(composite:78.4, members=[{nps:60,weight:3},{csat:90,weight:2},{ces:80,weight:1}])` → `{composite:78.4, member_breakdown:[{kpi:"NPS",score:60,effective_pct:50.0}, …]}`.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `PUT /api/v1/kpis/{cxi_id}/weights` with valid weights → 200, weights persisted, one `settings.changed` event.
  - `PUT /api/v1/kpis/{cxi_id}/weights` with only one non-zero weight → 400 `CXI_INSUFFICIENT_MEMBERS`.
  - `PUT /api/v1/kpis/{cxi_id}/weights` referencing the CXI id as a member → 400 `CXI_CANNOT_INCLUDE_ITSELF`.
  - `PATCH /api/v1/kpis/{csat_id}/activation` setting Active=false when CSAT is a CXI member → CXI weights table no longer includes CSAT on the next `GET /api/v1/kpis/{cxi_id}`.
- **What's intentionally NOT covered end-to-end**: Normalisation arithmetic (covered by `CxiWeightNormaliser` unit tests).

**Scenario Test**:

- `scenario-test: CxiConfiguresAndRebalancesScenarioTests` — multi-step: configure CXI weights, retrieve the snapshot composer's `member_breakdown`, deactivate a member KPI, confirm the CXI snapshot's `member_breakdown` recomputes on the next read with the deactivated member removed and the remaining proportions summed to 100%.

**E2E Test Coverage**:

- **User flows under test**: CXI configuration variant of `/kpi-management/:id` for the seeded CXI row.
- **Required scenarios**:
  - `CxiConfig_hides_question_preview_card`
  - `CxiConfig_locks_calculation_method_to_weighted_composite`
  - `CxiConfig_renders_weights_table_with_active_non_cxi_kpis_only`
  - `CxiConfig_updates_effective_percent_live_when_weights_change`
  - `CxiConfig_disables_active_checkbox_when_fewer_than_two_non_zero_weights`
  - `CxiConfig_removes_member_row_when_member_kpi_is_deactivated_elsewhere`
  - `CxiConfig_renders_weight_legend_beneath_gauge`

---

### User Story 4 - Configure tenant Customer Journey scoring (Priority: P1)

As a CX Program Manager, I open the Platform Settings page, select the Customer Journey section, and edit the tenant-wide ScoringConfig — Alpha (α, slider 0.000–1.000, default 0.500; β is derived as 1−α and shown read-only), MOT Multiplier (1.0–2.0, default 1.5, step 0.1), Responses Count Floor (integer ≥ 1, default 100), Flag Percentile (integer 1–49, default 25), Rolling Window Days (integer ≥ 7, default 30). Each parameter carries an info icon ("?") whose tooltip explains the parameter, its range/default, and practical guidance. Saving validates every field server-side and writes the new values via the M-16-owned `scoring_configs` table; the M-11 audit log records actor / timestamp / parameter / previous / new. Subsequent M-06 computation cycles use the new values; historical ScoreSnapshots are unaffected (JourneyVersion snapshots at publish time).

**Why this priority**: M-16 and M-06 already require these parameters; without an editing surface tenants cannot tune their Strategic Satisfaction Scoring Model and the platform falls back to seeded defaults indefinitely.

**Independent Test**: A P-01 opens `/settings/customer-journey`, sees the five parameters with their defaults (α=0.500 / β=0.500, MOT=1.5, n_floor=100, flag percentile=25, rolling window=30), drags α to 0.7 and confirms β updates live to 0.3, hits Save, refreshes the page, confirms the values persisted, opens the M-17 `event_log` and confirms one `journey.scoring_config.updated` event with the actor and the per-parameter diff. Delivers the ScoringConfig editing value on its own.

**Acceptance Scenarios**:

1. **Given** a freshly provisioned tenant, **When** a P-01 opens `/settings/customer-journey`, **Then** α=0.500, β=0.500, MOT=1.5, n_floor=100, flag percentile=25, rolling window days=30 are rendered.
2. **Given** the user drags the α slider, **When** they release at 0.7, **Then** β displays "0.300" read-only beside it within ~100ms.
3. **Given** the user attempts to set MOT=2.5, **When** they submit the form, **Then** an inline error "MOT multiplier must be between 1.0 and 2.0." appears and the API returns 400 `MOT_MULTIPLIER_OUT_OF_RANGE`.
4. **Given** the user attempts to set flag percentile=50, **When** they submit, **Then** an inline error "Flag percentile must be between 1 and 49." appears and Save is blocked.
5. **Given** the user attempts to set rolling window days=3, **When** they submit, **Then** an inline error "Rolling window must be at least 7 days." appears.
6. **Given** the user hovers the "?" icon next to Alpha, **When** the tooltip renders, **Then** it explains the parameter, gives range 0.0–1.0 / default 0.5, and renders bilingual EN/AR copy.
7. **Given** a P-07 IT Admin opens the section, **When** they view the parameters, **Then** the fields are read-only and the Save button is hidden (per the permission matrix).
8. **Given** a successful Save, **When** the M-17 `event_log` is queried, **Then** one `journey.scoring_config.updated` event is present with the actor, timestamp, and a per-parameter `{from, to}` diff.

**Unit Test Coverage**:

- **Units under test**: `ScoringConfigValidator` (per-field rules + cross-field α/β consistency), `AlphaBetaDeriver` (β = 1 − α with the documented precision), `ScoringConfigUpdateService` (atomic persist + audit emission).
- **Required cases**:
  - `ScoringConfigValidator.Validate({alpha:0.5, mot:1.5, nFloor:100, flagP:25, window:30})` → Valid.
  - `ScoringConfigValidator.Validate({alpha:-0.01})` → Invalid("alpha.out_of_range").
  - `ScoringConfigValidator.Validate({alpha:1.01})` → Invalid("alpha.out_of_range") (returns API code `INVALID_ALPHA_BETA_SUM` on the wire).
  - `ScoringConfigValidator.Validate({mot:0.9})` → Invalid("mot_multiplier.out_of_range") → API code `MOT_MULTIPLIER_OUT_OF_RANGE`.
  - `ScoringConfigValidator.Validate({mot:2.01})` → Invalid("mot_multiplier.out_of_range").
  - `ScoringConfigValidator.Validate({nFloor:0})` → Invalid("n_floor.below_minimum").
  - `ScoringConfigValidator.Validate({flagPercentile:0})` → Invalid("flag_percentile.out_of_range"); `(flagPercentile:50)` → Invalid.
  - `ScoringConfigValidator.Validate({rollingWindowDays:6})` → Invalid("rolling_window.below_minimum").
  - `AlphaBetaDeriver.Beta(0.500)` → 0.500 exactly.
  - `AlphaBetaDeriver.Beta(0.700)` → 0.300 exactly.
  - `AlphaBetaDeriver.Beta(0.123)` → 0.877 (3-dp precision).
  - `ScoringConfigUpdateService.Update(tenant, valid payload)` → 1 row persisted, 1 `journey.scoring_config.updated` event with the per-field diff.
  - `ScoringConfigUpdateService.Update(tenant, payload with no changes)` → returns Idempotent and emits **no** event (asserted negatively).

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `GET /api/v1/tenant/scoring-config` returns the seeded defaults on a fresh tenant (200, payload matches the documented defaults).
  - `PUT /api/v1/tenant/scoring-config` with a valid payload → 200, one row updated in `scoring_configs`, one `journey.scoring_config.updated` event in `event_log`.
  - `PUT /api/v1/tenant/scoring-config` with `alpha: 1.5` → 400 `INVALID_ALPHA_BETA_SUM` with the API-05 envelope.
  - `PUT /api/v1/tenant/scoring-config` with `mot_multiplier: 2.5` → 400 `MOT_MULTIPLIER_OUT_OF_RANGE`.
  - `PUT /api/v1/tenant/scoring-config` as a P-07 IT Admin → 403; as a P-04 Operational Manager → 403; as a P-01 → 200.
  - Atomicity: a save failure (e.g., FK violation simulated) → row unchanged AND no event emitted.
- **What's intentionally NOT covered end-to-end**: per-field validation truth table (`ScoringConfigValidator` unit tests).

**Scenario Test**:

- `scenario-test: ScoringConfigEditAndPersistScenarioTests` — multi-step: read defaults → update three fields → re-read → confirm persisted values → confirm exactly one `journey.scoring_config.updated` event with the three-field diff (no extra events from no-op re-saves).

**E2E Test Coverage**:

- **User flows under test**: Settings landing (`/settings`) navigation to Customer Journey section page (`/settings/customer-journey`).
- **Required scenarios**:
  - `CustomerJourneySettings_shows_defaults_when_tenant_is_freshly_provisioned`
  - `CustomerJourneySettings_updates_beta_live_when_alpha_slider_moves`
  - `CustomerJourneySettings_blocks_save_when_mot_is_out_of_range`
  - `CustomerJourneySettings_blocks_save_when_flag_percentile_is_50`
  - `CustomerJourneySettings_blocks_save_when_rolling_window_below_7`
  - `CustomerJourneySettings_renders_tooltip_when_question_icon_focused_or_hovered`
  - `CustomerJourneySettings_renders_form_read_only_for_it_admin`
  - `CustomerJourneySettings_prompts_unsaved_changes_when_user_navigates_away`

---

### User Story 5 - Activate or deactivate a KPI with binding-aware confirmation (Priority: P2)

As a CX Program Manager, I toggle the Active checkbox on any KPI (standard or custom). When the KPI is bound to active touchpoints in one or more journeys, a blocking confirmation reports the binding usage ("This KPI is bound to N active touchpoints across M journeys. Deactivating excludes it from future scoring. Existing data is preserved. Continue?"). On deactivation, Show-on-Dashboard auto-unchecks and disables, and the KPI is removed from every CXI weight table on the tenant (CXI proportions recompute). Historical ScoreSnapshots are preserved. Re-activation does not re-bind the KPI to any touchpoint (those bindings are owned by M-16).

**Why this priority**: Activation is the only lifecycle control (no delete) and has the most ripple effects (CXI weights, Show on Dashboard, M-16 bindings, M-09 alerts). P2 because the basic create/edit flow (US2) and the catalogue (US1) already cover the simpler subset; this is the dedicated workflow for the side-effects.

**Independent Test**: A P-01 deactivates an unbound custom KPI, sees no confirmation, confirms the row goes inactive in the catalogue. They then deactivate a custom KPI bound to 3 touchpoints in 2 journeys, see the confirmation with "3 active touchpoints across 2 journeys", confirm, and confirm that the row is inactive, that Show-on-Dashboard is forced off, that the KPI no longer appears in any CXI weights table, and that exactly one `settings.changed` event with the deactivation diff is in the audit log.

**Acceptance Scenarios**:

1. **Given** an active custom KPI not bound to any touchpoint, **When** the user unchecks Active and Saves, **Then** no confirmation appears, the KPI persists as inactive, Show-on-Dashboard is forced off, and one audit row is written.
2. **Given** an active KPI bound to 3 touchpoints across 2 journeys, **When** the user unchecks Active, **Then** a blocking confirmation "This KPI is bound to 3 active touchpoints across 2 journeys. Deactivating excludes it from future scoring. Existing data is preserved. Continue?" appears.
3. **Given** the user confirms deactivation of a KPI that is a CXI member, **When** the save completes, **Then** the KPI no longer appears in the CXI weights table on the next read AND CXI's effective percentages on its remaining members recompute.
4. **Given** the user deactivates a KPI with Show-on-Dashboard=true, **When** the save completes, **Then** Show-on-Dashboard is forced false in the persisted record.
5. **Given** the user re-activates a previously deactivated KPI, **When** the save completes, **Then** M-16 touchpoint bindings are NOT automatically re-created (the prior bindings were severed at deactivation and the user must re-bind in M-16); historical ScoreSnapshots remain visible in M-07 reporting.

**Unit Test Coverage**:

- **Units under test**: `KpiDeactivationSideEffects` (clears Show-on-Dashboard; calls `CxiMemberMembershipRule.OnKpiDeactivated` for every CXI on the tenant), `KpiActivationCommandHandler`.
- **Required cases**:
  - `KpiDeactivationSideEffects.Apply(kpi)` with `kpi.show_on_dashboard=true` → returns mutated kpi with `show_on_dashboard=false`.
  - `KpiDeactivationSideEffects.Apply(kpi)` with no CXI on tenant → returns mutated kpi, no CXI mutation.
  - `KpiDeactivationSideEffects.Apply(kpi)` with kpi as a CXI member → CXI member list excludes the kpi; effective % recomputed on the remaining members.
  - `KpiActivationCommandHandler.Handle({kpiId:X, active:false, confirm:false})` with 3 bindings → returns `RequiresConfirmation{touchpoints:3, journeys:2}` (no write).
  - `KpiActivationCommandHandler.Handle({kpiId:X, active:false, confirm:true})` with 3 bindings → persists, side-effects applied, exactly ONE `settings.changed` event whose diff includes the nested `cxi_side_effect` payload for each affected CXI (if any).
  - `KpiActivationCommandHandler.Handle({kpiId:X, active:true})` on a previously inactive KPI → persists Active=true; does NOT re-create M-16 bindings (asserts no M-16 binding mutation call was made).

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `PATCH /api/v1/kpis/{id}/activation` with `active=false` on an unbound KPI → 200; the KPI is inactive; one event.
  - `PATCH /api/v1/kpis/{id}/activation` with `active=false` on a bound KPI without `confirm=true` → 409 `KPI_DEACTIVATION_REQUIRES_CONFIRMATION` with `touchpoint_count` and `journey_count` in the body.
  - Same call with `confirm=true` → 200; the KPI is inactive; Show-on-Dashboard is forced false; if it was a CXI member, CXI's weights table excludes it on the next read.
  - `PATCH /api/v1/kpis/{id}/activation` as a persona without the activation permission → 403.
- **What's intentionally NOT covered end-to-end**: deactivation side-effect arithmetic (`KpiDeactivationSideEffects` unit tests).

**Scenario Test**:

- `scenario-test: KpiDeactivationCascadeScenarioTests` — multi-step: create a CXI with NPS+CSAT+CES as members, deactivate CSAT with `confirm=true`, read CXI, confirm CSAT is gone from CXI weights AND CXI's NPS+CES proportions sum to 100%, confirm **exactly one** `settings.changed` event in `event_log` whose `entity_ref` points at CSAT and whose diff payload includes a nested `cxi_side_effect: { cxi_kpi_id, removed_member_kpi_id: <csat_id>, recomputed_effective_percentages: { <nps_id>: 75.0, <ces_id>: 25.0 } }`.

**E2E Test Coverage**:

- **User flows under test**: the deactivation-confirmation dialog rendered inline on the KPI Configuration page (`/kpi-management/:id`). US-5 adds NEW E2E methods to the existing `KpiConfigTests.cs` (the US-2 file); no new test class is created.
- **Required scenarios**:
  - `KpiConfig_shows_deactivation_confirmation_when_active_toggle_off_with_bindings` — open a custom KPI bound to N touchpoints, untick Active, confirm the blocking dialog renders "This KPI is bound to N active touchpoints across M journeys. Deactivating excludes it from future scoring. Existing data is preserved. Continue?" and Save is blocked until the dialog is dismissed.
  - `KpiConfig_skips_deactivation_confirmation_when_no_bindings` — open an unbound custom KPI, untick Active, confirm NO dialog appears and Save proceeds directly.
  - `KpiConfig_cascades_show_on_dashboard_off_when_kpi_deactivated` — open a KPI with Show-on-Dashboard checked, deactivate via the dialog, confirm Show-on-Dashboard is forced false in the persisted record (re-fetch + assert).

---

### User Story 6 - Configure Organization settings (Priority: P2)

As a CX Program Manager or Tenant IT Administrator, I open the Platform Settings page, select the Organization section, and edit the tenant's display Name (required, max 150 chars), Logo (image upload — PNG/JPG/SVG, recommended max 2 MB, shown as the platform brand mark), and Industry (dropdown sourced from the same canonical list M-11 uses at provisioning — Banking, Telecommunications, Government, Automotive, Entertainment, Services). Save persists the tenant-wide values and writes a `settings.changed` audit event with field-level diff. The Industry choice drives industry-default templates downstream (out of scope for this feature; we only need to persist the value).

**Why this priority**: Organization is the simpler of the two Settings sections; it is required for branding and downstream industry-default templates but it does not gate the scoring engine, so it sits below US4. P2 because the platform can launch without a custom logo or industry override — defaults work — but the section is part of the v1 Settings surface.

**Independent Test**: A P-01 opens `/settings/organization`, edits the Name to "Acme Bank", uploads a 500 KB PNG logo, selects Industry="Banking", hits Save, refreshes, and confirms the values persist. They then sign in as a P-07 IT Admin and verify they have the same edit capability per the tenant's RBAC configuration. Delivers the Organization value on its own.

**Acceptance Scenarios**:

1. **Given** a freshly provisioned tenant, **When** a P-01 opens `/settings/organization`, **Then** the current Name, Logo (or "no logo set" placeholder), and Industry are displayed.
2. **Given** the user attempts to clear the Name field, **When** they Save, **Then** an inline error "Organization name is required." appears.
3. **Given** the user uploads a 3 MB JPEG, **When** they pick the file, **Then** a soft warning "Logo is recommended ≤ 2 MB; large files may slow down the portal." appears but the upload proceeds.
4. **Given** the user uploads a non-image file (`.pdf`), **When** the upload attempts, **Then** it is rejected with "Logo must be a PNG, JPG, or SVG file."
5. **Given** the user opens the Industry dropdown, **When** they view the options, **Then** the options match exactly the M-11 tenant-provisioning industry list (Banking, Telecommunications, Government, Automotive, Entertainment, Services) — no extras, no missing values.
6. **Given** a successful Save, **When** the audit log is inspected, **Then** one `settings.changed` event with `organization.*` field diffs is present.

**Unit Test Coverage**:

- **Units under test**: `OrganizationSettingsValidator`, `LogoUploadValidator`, `SvgSanitiser` (strips disallowed nodes/attrs and reports unsafe-when-unstrippable), `IndustryEnumProvider` (single source of truth shared with M-11 tenant provisioning).
- **Required cases**:
  - `OrganizationSettingsValidator.Validate({name:"Acme", industry:"Banking"})` → Valid.
  - `OrganizationSettingsValidator.Validate({name:"", industry:"Banking"})` → Invalid("organization.name.required").
  - `OrganizationSettingsValidator.Validate({name: string of 151 chars, industry:"Banking"})` → Invalid("organization.name.too_long").
  - `OrganizationSettingsValidator.Validate({name:"Acme", industry:"Aerospace"})` → Invalid("organization.industry.unknown") (not in the canonical list).
  - `LogoUploadValidator.Validate({contentType:"image/png", sizeBytes:500_000})` → Valid.
  - `LogoUploadValidator.Validate({contentType:"image/png", sizeBytes:3_000_000})` → Warning("logo.size.over_recommended") (non-blocking).
  - `LogoUploadValidator.Validate({contentType:"application/pdf"})` → Invalid("logo.content_type.unsupported").
  - `SvgSanitiser.Sanitise(svgBytes: "<svg xmlns='http://www.w3.org/2000/svg'><circle r='5'/></svg>")` → returns the same SVG bytes (no script/event-handler content).
  - `SvgSanitiser.Sanitise(svgBytes: "<svg><script>alert(1)</script><circle r='5'/></svg>")` → returns SVG bytes WITHOUT the `<script>` node; `circle` remains.
  - `SvgSanitiser.Sanitise(svgBytes: "<svg><circle r='5' onload='alert(1)'/></svg>")` → returns SVG bytes with the `onload` attribute stripped; `circle` retains its `r` attribute.
  - `SvgSanitiser.Sanitise(svgBytes: "<svg><foreignObject><iframe src='evil'/></foreignObject></svg>")` → returns SVG bytes WITHOUT the `<foreignObject>` subtree.
  - `SvgSanitiser.Sanitise(svgBytes: "<svg><use href='http://evil.example/x.svg#a'/></svg>")` → returns SVG bytes with the external-`href` `<use>` stripped.
  - `SvgSanitiser.Sanitise(svgBytes: "not actually svg bytes")` → throws `SvgUnsafeContentException` (parser cannot make payload safe → upload rejected with `LOGO_SVG_UNSAFE_CONTENT`).
  - `IndustryEnumProvider.GetAll()` returns the exact six values [Banking, Telecommunications, Government, Automotive, Entertainment, Services] in canonical order.
  - `IndustryEnumProvider.GetAll()` returns the same set as the M-11 industry enum (asserted by comparing both providers' outputs in a single test).

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `GET /api/v1/tenant/organization` returns 200 with the seeded defaults on a fresh tenant.
  - `PUT /api/v1/tenant/organization` with valid payload → 200; one row updated; one `settings.changed` event in `event_log`.
  - `PUT /api/v1/tenant/organization` with empty Name → 400 `ORGANIZATION_NAME_REQUIRED`.
  - `PUT /api/v1/tenant/organization` with Industry="Aerospace" → 400 `ORGANIZATION_INDUSTRY_UNKNOWN`.
  - `POST /api/v1/tenant/organization/logo` with a valid PNG ≤ 2 MB → 200; the response includes the new logo URL.
  - `POST /api/v1/tenant/organization/logo` with `application/pdf` → 400 `LOGO_CONTENT_TYPE_UNSUPPORTED`.
  - `POST /api/v1/tenant/organization/logo` with a benign SVG → 200; the **persisted** bytes equal the sanitiser output (asserted by re-fetching the logo URL and byte-comparing against the sanitised payload).
  - `POST /api/v1/tenant/organization/logo` with an SVG containing `<script>` → 200; the persisted SVG has the `<script>` stripped (re-fetched payload contains no `<script>` substring).
  - `POST /api/v1/tenant/organization/logo` with a non-parseable SVG → 400 `LOGO_SVG_UNSAFE_CONTENT`.
- **What's intentionally NOT covered end-to-end**: name/industry validation truth tables (`OrganizationSettingsValidator` unit tests).

**Scenario Test**:

- `scenario-test: not-needed — each acceptance scenario is a single endpoint round-trip; the API tests above cover them.`

**E2E Test Coverage**:

- **User flows under test**: Settings landing (`/settings`) navigation to Organization section page (`/settings/organization`).
- **Required scenarios**:
  - `OrganizationSettings_shows_current_values_when_user_opens_section`
  - `OrganizationSettings_blocks_save_when_name_is_empty`
  - `OrganizationSettings_uploads_png_logo_when_file_is_valid`
  - `OrganizationSettings_rejects_pdf_logo`
  - `OrganizationSettings_sanitises_svg_logo_when_payload_contains_script_or_event_handlers`
  - `OrganizationSettings_rejects_unparseable_svg_with_logo_svg_unsafe_content`
  - `OrganizationSettings_industry_dropdown_lists_canonical_six_values`
  - `OrganizationSettings_redirects_to_login_when_user_is_signed_out`
  - `OrganizationSettings_renders_form_read_only_for_persona_without_edit_rights`

---

### User Story 7 - Analyst opens KPI Configuration in read-only mode (Priority: P3)

As a CX Analyst (P-02), I can open the KPI Configuration page for any KPI to inspect its definition, but every field renders read-only, the Save button is hidden, and the activation control is hidden. I see the same live Question Preview and Dashboard Preview cards as the editor, so I can verify how a KPI is configured without risk of accidental change.

**Why this priority**: This is a UI-gated permission variation of US2; it depends on US2 existing but does not add new business behaviour. P3 because it ships an Analyst persona affordance, not new core functionality.

**Independent Test**: A P-02 Analyst opens `/kpi-management/<id>` for the NPS KPI, sees every field rendered but greyed/disabled, sees the live preview cards on the right, attempts (programmatically) to call `PUT /api/v1/kpis/<id>` and confirms the API returns 403 with the API-05 envelope. Delivers the analyst read-only value on its own.

**Acceptance Scenarios**:

1. **Given** a P-02 Analyst, **When** they open `/kpi-management/<nps_id>`, **Then** every form field is rendered but disabled, the Save button is hidden, and the activation control is hidden.
2. **Given** a P-02 Analyst, **When** they observe the right panel, **Then** the Question Preview and Dashboard Preview cards render with the same content the P-01 editor would see for the same KPI.
3. **Given** a P-02 Analyst, **When** they call `PUT /api/v1/kpis/<id>` directly, **Then** the API returns 403 with API-05 envelope code `PERMISSION_DENIED`.

**Unit Test Coverage**:

- `unit-tests: skipped — this story adds no new business logic; it gates existing endpoints under the M-10 RBAC primitive and renders an alternative UI variant. Backend permission enforcement is covered by integration tests; UI variant is covered by E2E.`

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `GET /api/v1/kpis/{id}` as P-02 Analyst → 200.
  - `PUT /api/v1/kpis/{id}` as P-02 Analyst → 403 `PERMISSION_DENIED` (API-05 envelope).
  - `PATCH /api/v1/kpis/{id}/activation` as P-02 Analyst → 403.
- **What's intentionally NOT covered end-to-end**: pure-logic verification — none applies; this story is permission-only.

**Scenario Test**:

- `scenario-test: not-needed — single-call permission checks; covered by the API tests above.`

**E2E Test Coverage**:

- **User flows under test**: `/kpi-management/:id` rendered for a P-02 Analyst.
- **Required scenarios**:
  - `KpiConfig_hides_save_button_for_analyst`
  - `KpiConfig_hides_activation_control_for_analyst`
  - `KpiConfig_renders_preview_cards_for_analyst`

  *(`KpiConfig_renders_form_read_only_for_analyst` is owned by US2's E2E coverage matrix to avoid duplicate entries.)*

---

### Edge Cases

- **Empty Short Name search**: `?search=` (empty) and `?search=  ` (whitespace) both return the unfiltered list.
- **Search across whitespace-trimmed Short Names**: searching "  NPS  " returns the NPS row (search input is trimmed before matching).
- **TOP n Box on a 1–3 scale**: the upper bound for n is 2 (less than the scale maximum of 3); n=2 is allowed; n=3 is a blocking error; the warning rule (n > ½ × 2 = 1) fires for n=2.
- **Slider style with scale change**: user selects Slider with Scale=1–3, then changes Scale to 1–5 — representation resets to Number, a tooltip explains why, and the change is reversible (going back to 1–3 does NOT auto-restore Slider — the user re-selects it).
- **Emoji set deprecation**: the user previously saved a KPI with an emoji set that has been removed from the v1 catalogue — the form opens with a soft warning beside the Emoji Set field ("This emoji set is no longer available. Pick a new one to save changes.") and Save is disabled until they pick a current set.
- **CXI with zero non-zero weights**: the Save call returns 400 `CXI_INSUFFICIENT_MEMBERS`; the UI's Active checkbox is disabled with its tooltip; the gauge renders with a "No members selected" placeholder rather than `NaN`.
- **CXI member deactivated mid-edit**: the user is editing CXI weights with NPS+CSAT+CES; an admin in another session deactivates CSAT; on the editor's next Save, CSAT is silently removed from the persisted CXI weights and the API returns 200 with the recomputed proportions; the editor sees a non-blocking toast "CSAT was deactivated by another user and removed from CXI."
- **ScoringConfig idempotent save**: posting the same payload twice writes exactly one row update and emits exactly one event (the second save is a no-op when the diff is empty).
- **α slider precision**: dragging the slider produces α to 3 decimal places (e.g., 0.567); β is derived to the same precision (1 − 0.567 = 0.433); rounding does not introduce α+β ≠ 1.000 because β is never stored.
- **Logo upload of a 0-byte file**: rejected with `LOGO_CONTENT_TYPE_UNSUPPORTED` (mime cannot be determined) or `LOGO_SIZE_ZERO` — implementer chooses the closer of the two error codes; the UI shows "Logo file is empty or unreadable."
- **SVG logo containing scripts or event handlers**: the server-side SVG sanitiser (FR-050) strips the disallowed content before persistence and returns 200 with the sanitised payload. The UI MAY surface a non-blocking notice "Your SVG was sanitised — disallowed content was removed before saving." on re-fetch when the persisted bytes differ from the upload bytes. A payload that cannot be made safe (non-parseable SVG) is rejected with 400 `LOGO_SVG_UNSAFE_CONTENT`.
- **Concurrent edits on the same KPI**: two P-01 sessions open the same KPI; both Save; the second Save wins (last-writer-wins per row); both audit events are recorded with their timestamps; no optimistic-concurrency check in v1.
- **Audit gap on hot reload**: a frontend hot-reload during a Save MUST NOT cause a duplicate save (Save button disables on submit and re-enables on response).
- **RTL layout**: in Arabic, the two-panel arrangement (form left, preview right) flips visually — preview is on the user's right in LTR and on the user's right in RTL too (because the page reads from the right). Both the gauge labels and the emoji ordering follow the reading direction.
- **Standard KPI deactivation**: a P-01 may deactivate a standard KPI under the **same** binding-usage confirmation flow used for custom KPIs (no extra warning copy, no extra role gate — see Clarifications session 2026-06-21). Standards remain non-deletable; they can be hidden from scoring and rebinding. Reactivation restores them without a seed-data re-fetch (the row was always present).
- **Tenant isolation**: a user from Tenant A authenticates and queries `GET /api/v1/kpis/<id>` with an id belonging to Tenant B → 404 (per the schema-per-tenant boundary, the row does not exist in Tenant A's connection) and an M-17 `audit_log` entry records the denied attempt (GP-04 pass condition).

---

## Requirements *(mandatory)*

### Functional Requirements

#### KPI Catalogue & Lifecycle (M-06)

- **FR-001**: The platform MUST seed exactly eight standard KPIs at tenant provisioning, in the canonical order **NPS, CSAT, CES, CXI, FCR, VFM, Agent Score, CHS**, each with the seed `short_name`, `full_name`, `kpi_type=Standard`, and (for NPS) the locked `calculation_method=NPSStandard` and `scale=0–10`.
- **FR-002**: The platform MUST NOT expose any delete endpoint for KPIs (no `DELETE /api/v1/kpis/{id}` route is registered) and the catalogue UI MUST NOT render any delete control on any row.
- **FR-003**: Short Name uniqueness MUST be enforced per tenant, case-insensitive and after trimming whitespace, across both standard and custom KPIs.
- **FR-004**: Short Name MUST be immutable after first save for every KPI (standard and custom); the API rejects changes with `KPI_SHORT_NAME_IMMUTABLE` and the UI renders the field read-only with an info tooltip in edit mode.
- **FR-005**: For NPS, `calculation_method` MUST be locked to `NPSStandard` and `scale` MUST be locked to `0–10`; the API rejects changes to either with `KPI_FIELD_IMMUTABLE_FOR_STANDARD` and the UI renders both fields read-only.
- **FR-006**: The KPI Catalogue list endpoint (`GET /api/v1/kpis`) MUST support optional query parameters `type` (`All`|`Standard`|`Custom`), `active_only` (boolean, default true), and `search` (case-insensitive substring matched against Short Name ∪ Full Name after trimming).
- **FR-007**: The catalogue list MUST return rows in this order: all standard KPIs in the canonical order (FR-001), followed by custom KPIs sorted by `created_at` descending.
- **FR-008**: The "[X] Active KPIs" subtitle MUST count every KPI whose `is_active=true`, including CXI, and the count MUST update without a page refresh whenever any KPI activation changes **in the current browser session** (driven by local mutation triggers — no polling, no server-sent events, no WebSocket). Activations performed in other sessions MUST appear on the next route navigation or explicit reload only.
- **FR-009**: P-02 Analyst MUST be able to view the catalogue and open any KPI Configuration page in read-only mode; the "+ Add KPI", Save, and activation controls MUST be hidden in the UI and the corresponding endpoints MUST return 403 on direct call.

#### KPI Configuration form fields (M-06)

- **FR-010**: Short Name input MUST accept up to 20 characters, MUST be required, and MUST surface the duplicate error inline at blur time.
- **FR-011**: Full Name input MUST accept up to 100 characters and MUST be required; Full Name changes MUST update the Question Preview question label live.
- **FR-012**: Perspectives MUST be entered as a chip/tag input (Enter or comma to add), MUST allow 0–10 chips with each chip ≤ 60 characters; perspectives MUST be modelled as independent drill-down dimensions (FR-019/020), not as components of the overall score.
- **FR-013**: Calculation Method MUST be a required dropdown with options `Weighted Average`, `TOP n Box`, `NPS Standard` (the last selectable only for NPS); selecting `TOP n Box` MUST reveal the integer `n` input.
- **FR-014**: The TOP n Box `n` input MUST accept positive integers strictly less than the configured scale maximum; values ≥ the maximum MUST return inline error "n must be less than the maximum scale value."
- **FR-015**: A non-blocking warning MUST render when `n > ½ × (scale_value_count − 1)` where `scale_value_count` is the count of distinct scale points (e.g., 7 for a 1–7 scale, 11 for a 0–10 scale).
- **FR-016**: Scale MUST be a required dropdown with the exact six options `0–10, 1–3, 1–5, 1–7, 1–10, 1–100`; for NPS the field MUST be locked to `0–10` and read-only.
- **FR-017**: Scale changes on a KPI bound to one or more active touchpoints in M-16 MUST trigger a blocking confirmation reporting the affected touchpoint and journey counts (`KPI_SCALE_CHANGE_AFFECTS_BINDINGS` on the API).
- **FR-018**: The platform MUST expose two optional bilingual (EN + AR) free-text fields — `min_scale_description` and `max_scale_description` — each ≤ 60 characters; they MUST render as the left/right anchor labels beneath the scale in the Question Preview and MUST NOT affect scoring.
- **FR-019**: This feature MUST persist perspective **definitions** (id, kpi_id, label ≤ 60 chars, display_order, 0–10 per KPI) so that M-01 question authoring can bind survey questions to a specific perspective. Per-perspective **score computation and storage** are explicitly deferred to a later M-06 score-computation engine release and are out of scope here. No `perspective_score` table is provisioned by this feature.
- **FR-020**: Once per-perspective scoring ships in a later release, the overall KPI score MUST be computed solely from the KPI's own surveyed question(s) — never as an arithmetic roll-up of perspective scores. This rule is recorded now because it constrains the *definition* model (perspectives are independent drill-down dimensions, not a decomposition of the headline score) even though no scoring runs in this feature.
- **FR-021**: Representation Style MUST be a required dropdown with options `Number`, `Stars`, `Emoji`, `Slider`; selecting `Emoji` MUST reveal an Emoji Set dropdown sourced from the v1 catalogue of **exactly two platform-owned, code-level static sets — `FaceClassic` and `HandThumbs`** (see Clarifications session 2026-06-21). Tenants MUST NOT be able to define additional sets in v1. `Slider` MUST be selectable only when the current scale is `1–3`; if the scale leaves `1–3` while Slider is active, representation MUST reset to `Number` with an inline tooltip.
- **FR-022**: Threshold configuration MUST render three contiguous bands — Unsatisfactory (`lower ≤ score ≤ x`), Average (`x < score ≤ y`), Satisfactory (`y < score ≤ upper`) — with editable integer `x` and `y` inputs and non-editable display `lower`/`upper` bounds. Create-mode defaults are KPI-type-aware: **`x = 20, y = 70` for KPIs on the `0..100` normalised scale (all non-NPS KPIs)**; **`x = 0, y = 30` for NPS** (mapping to industry-standard Detractors / Mixed / Promoter-leaning bands on `−100..+100`). All defaults are seed values only and remain tenant-editable via the form. The API MUST reject violations of `lower < x < y < upper` with `KPI_THRESHOLD_NOT_ASCENDING`.
- **FR-023**: For NPS the threshold inputs MUST run on the raw scale (`lower=-100, upper=+100`); for all other KPIs the inputs MUST run on the normalised 0–100 scale.
- **FR-024**: Target MUST be a required integer when Active is checked; range `0–100` for all KPIs except NPS (`-100..+100`); out-of-range is a blocking inline error.
- **FR-025**: A non-blocking warning MUST render when Target < y (the lower bound of the Satisfactory band): "This target is not within the Satisfactory range."
- **FR-026**: Active MUST default checked on create; unchecking on a KPI bound to one or more active touchpoints MUST trigger a blocking confirmation reporting the touchpoint and journey counts; deactivating MUST auto-unset and disable Show-on-Dashboard AND MUST remove the KPI from every CXI weight table on the tenant. The deactivation flow MUST apply identically to standard and custom KPIs — no extra warning copy and no extra role gate for standards; the only difference is that standard KPIs cannot be deleted (BR-1.1), they can only be deactivated. Exactly ONE `settings.changed` event MUST be emitted, sourced on the deactivated KPI, whose diff payload includes a nested `cxi_side_effect: { cxi_kpi_id, removed_member_kpi_id, recomputed_effective_percentages }` for every CXI on the tenant where the deactivated KPI was a member (one nested entry per affected CXI when more than one exists; empty / omitted when no CXI included it).
- **FR-027**: Show on Main Dashboard MUST default unchecked, MUST be enabled only when Active is checked, and MUST be auto-forced false when the KPI is deactivated.
- **FR-028**: Save MUST be disabled until all required fields are valid (Short Name, Full Name, Calculation Method, Scale, Threshold x, Threshold y, and — when Active — Target); Save MUST persist KPIDefinition + KPIThreshold + KPIPerspectives + CXIWeights atomically in a single transaction.
- **FR-029**: Cancel and the back arrow MUST present an unsaved-changes confirmation when changes exist, with options to Save, Discard, or stay on the page.
- **FR-030**: Every create / edit / activation toggle MUST write to the M-17 audit log via a `settings.changed` event carrying actor, timestamp, kpi short_name, and per-field `{from, to}` diff.

#### Live preview (M-06)

- **FR-031**: The Question Preview card MUST render the selected representation style for the current scale; for Emoji it renders one emoji per scale value drawn from the selected Emoji Set; for Slider it renders only when scale = 1–3.
- **FR-032**: The Question Preview MUST display the Minimum Scale Description and Maximum Scale Description as left/right anchor labels beneath the scale, updated live.
- **FR-033**: The Dashboard Preview MUST render a single universal semicircular arc gauge for every KPI type (not NPS-specific), with a red → amber → green gradient and band boundaries driven live by `x` and `y`.
- **FR-034**: The Dashboard Preview central numeral MUST display the Target value when set, defaulting to the arc midpoint (`50` for 0–100 KPIs, `0` for NPS) when Target is empty; the KPI Short Name MUST render beneath the numeral (or `KPI` placeholder in create mode before a Short Name is entered).
- **FR-035**: The Dashboard Preview MUST display a directional target marker (▼) at the Target position on the arc, coloured by its band; arc range and labels MUST switch automatically by KPI type (NPS = −100..+100, all others = 0–100, CXI = 0–100).
- **FR-036**: The Dashboard Preview card MUST additionally display, alongside the gauge: the KPI short and full name above the gauge; a "Target: [value]" caption beside the marker; a sample period-over-period delta (e.g., "vs Last Quarter +4 points"); and a sample response count (e.g., "responses 3,420").
- **FR-037**: For NPS, the Dashboard Preview MUST additionally display the Promoters / Passives / Detractors breakdown as three coloured segments with percentages (sample values, e.g., 51% / 40% / 9%).
- **FR-038**: When the user has not yet set `x` and `y`, the gauge MUST render using the KPI-type-aware create-time defaults from FR-022 (`x=20, y=70` for normalised-scale KPIs; `x=0, y=30` for NPS) and display the notice "Using default thresholds — configure fields to update."
- **FR-039**: Any change to Short Name, Full Name, Scale, Representation Style, Emoji Set, Threshold x, Threshold y, Target, or (CXI) member weights MUST re-render the relevant preview surface within ~100ms.

#### CXI composite KPI (M-06)

- **FR-040**: For CXI the Calculation Method MUST be fixed to `Weighted Composite` and read-only; the Scale and Representation Style fields MUST not be rendered.
- **FR-041**: The CXI configuration MUST render a KPI Weights table listing every currently active KPI on the tenant **except CXI itself**, each row with a positive-integer weight input and a live Effective % column (computed by the platform, not the user).
- **FR-042**: Weights MUST be stored as relative positive integers; the engine MUST normalise them at computation time; no client-side sum-to-100 constraint applies; a weight of 0 MUST be treated as not-configured (BR-2.3).
- **FR-043**: CXI MUST NOT be activatable unless at least two member KPIs carry non-zero weights; the API MUST return `CXI_INSUFFICIENT_MEMBERS` on attempts; the UI MUST disable the Active checkbox and surface the tooltip "CXI requires at least 2 active KPIs with assigned weights."
- **FR-044**: When a CXI member KPI is deactivated elsewhere (via the activation endpoint), the platform MUST remove that member from CXI's weights and recompute the remaining Effective %.
- **FR-045**: CXI MUST NOT be selectable as a member of itself; the API MUST return `CXI_CANNOT_INCLUDE_ITSELF` on attempts.
- **FR-046**: For CXI the Question Preview card MUST be hidden (CXI is computed, not surveyed); the Dashboard Preview gauge MUST run 0–100 and MUST render a proportional weight legend beneath it.
- **FR-047**: The CXI ScoreSnapshot returned to M-07 MUST include both the composite normalised score AND a `member_breakdown` array of `{kpi_id, kpi_short_name, score, effective_percentage}` so M-07 can render the composite with its member contributions.

#### Platform Settings page

- **FR-048**: The Settings landing page MUST list the v1 configuration sections — **Organization, Customer Journey, Notifications, Branding Theme, Localization Defaults** — as navigable entries; selecting a section MUST open that section's content on a dedicated section page (master → detail), not as an inline accordion.
- **FR-049**: Each section page MUST display the section title as its page header, MUST guard unsaved-changes navigation with "You have unsaved changes. Are you sure you want to leave?", and MUST be tenant-scoped (changes apply tenant-wide).

#### Organization section (Settings)

- **FR-050**: Organization MUST expose three fields — **Name** (text, required, ≤ 150 chars), **Logo** (image upload accepting PNG/JPG/SVG, recommended ≤ 2 MB, with a "replace" control), **Industry** (dropdown sourced from the M-11 canonical industry list: Banking, Telecommunications, Government, Automotive, Entertainment, Services). For SVG uploads the platform MUST run the payload through a hardened SVG sanitiser before persistence — stripping every `<script>`, `<foreignObject>`, `<iframe>`, `<use>` with external `href`, and every `on*` event-handler attribute. The **sanitised** payload is the only version persisted; the original is discarded. If sanitisation cannot make the payload safe (non-parseable SVG, or content the sanitiser cannot strip without breaking the file), the upload MUST be rejected with `LOGO_SVG_UNSAFE_CONTENT` and the UI MUST surface "Logo could not be uploaded — the SVG file contains content that is not allowed." PNG and JPEG uploads bypass the sanitiser.
- **FR-051**: Saving Organization MUST persist atomically AND emit one `settings.changed` audit event with the per-field `{from, to}` diff.
- **FR-052**: Organization MUST be editable by both P-01 (CX Program Manager) and P-07 (Tenant IT Administrator) per the tenant's RBAC configuration; the UI MUST hide write controls for personas without edit rights.

#### Customer Journey section (Settings) — ScoringConfig

- **FR-053**: The Customer Journey section MUST expose five parameters — **Alpha (α)**, **MOT Multiplier**, **Responses Count Floor (n_floor)**, **Flag Percentile**, **Rolling Window Days** — as the canonical editing surface for the tenant-level `ScoringConfig` entity owned by M-16 and consumed by both M-16 and M-06.
- **FR-054**: Alpha (α) MUST be entered exclusively via a linked slider spanning 0.000–1.000 (default 0.500); β MUST be derived as `1 − α`, displayed read-only beside the slider, and MUST NOT be independently editable; β MUST NOT be stored (always derived at computation time).
- **FR-055**: MOT Multiplier MUST accept values 1.0–2.0 with step 0.1 (default 1.5).
- **FR-056**: n_floor MUST accept integers ≥ 1 (default 100).
- **FR-057**: Flag Percentile MUST accept integers 1–49 (default 25).
- **FR-058**: Rolling Window Days MUST accept integers ≥ 7 (default 30).
- **FR-059**: Each parameter MUST have an info ("?") icon whose tooltip explains the parameter, its valid range and default, and gives practical guidance; tooltips MUST be bilingual EN/AR, accessible on hover (desktop) and tap (touch), focusable, dismissible with Esc, and MUST NOT block field interaction (WCAG 2.1 AA).
- **FR-060**: `PUT /api/v1/tenant/scoring-config` MUST re-validate every constraint server-side; out-of-range α MUST return `INVALID_ALPHA_BETA_SUM`; out-of-range MOT MUST return `MOT_MULTIPLIER_OUT_OF_RANGE`; all other violations return their documented codes; every save emits one `journey.scoring_config.updated` event with the per-parameter `{from, to}` diff.
- **FR-061**: ScoringConfig changes MUST take effect on the next M-06 computation cycle and MUST NOT retroactively alter historical ScoreSnapshots; each JourneyVersion published by M-16 snapshots the active ScoringConfig values, so historical recomputation uses the parameters that were live for that version.
- **FR-062**: ScoringConfig editing MUST be restricted to P-01 (CX Program Manager); P-07 (Tenant IT Admin) MAY view the section in read-only mode; all other personas MUST NOT see the section.

#### Cross-cutting

- **FR-063**: All KPI Management, KPI Configuration, and Settings endpoints MUST be versioned under `/api/v1/...` (API-01) and tenant-isolated by schema (AD-02 / GP-04).
- **FR-064**: All error responses MUST follow the API-05 envelope with `code`, `message`, `correlation_id`, and `tenant_id`; user-facing messages MUST be bilingual (EN + AR).
- **FR-065**: Every endpoint MUST declare its `required_permission`, `required_scope`, and `default_personas` per API-03; permission checks MUST be enforced at the API layer by the M-10 middleware (no business module implements its own permission check) AND mirrored in the UI (controls hidden/disabled).
- **FR-066**: Every page in this feature MUST render correctly in RTL when the tenant's language is Arabic (per T-01); the two-panel KPI Configuration arrangement, the gauge labels, the threshold rows, the emoji ordering, and the Settings section navigation MUST all flip; logical CSS properties (`ps-*`, `me-*`, `text-start`, etc.) MUST be used per the Nabadat Design System.
- **FR-067**: All gauge colour bands MUST carry a non-colour cue (band label and target marker) for colour-blind users (WCAG 2.1 AA — NFR-4).
- **FR-068**: KPI list and KPI Configuration pages MUST load within 1.5s on standard tenant data volumes; live preview re-renders MUST complete within 100ms of a field change.

### Key Entities

- **KPIDefinition** — represents a single KPI on a tenant. Carries `short_name` (≤20 char, unique per tenant, immutable after create), `full_name` (≤100 char), `kpi_type` (Standard|Custom), `is_composite` (true only for CXI), `calculation_method` (WeightedAverage|TopNBox|NPSStandard|WeightedComposite), `top_n_value` (required when method=TopNBox), `scale` (one of `0–10, 1–3, 1–5, 1–7, 1–10, 1–100`; null for composite), `min_scale_description` and `max_scale_description` (≤60 char each, optional, bilingual), `representation_style` (Number|Stars|Emoji|Slider; null for composite), `emoji_set` (required when representation_style=Emoji), `target` (numeric; required when active; range per type), `is_active` (default true), `show_on_dashboard` (default false; forced false when inactive), plus standard audit fields (`id`, `tenant_id`, `created_at`, `created_by`, `updated_at`, `updated_by`).
- **KPIThreshold** — one per KPI. Carries `kpi_id`, `x` (upper bound of Unsatisfactory), `y` (upper bound of Average), `lower_bound` (0 normally; −100 for NPS), `upper_bound` (100 normally; +100 for NPS). Constraint: `lower_bound < x < y < upper_bound`.
- **KPIPerspective** — 0..10 per KPI. Carries `id` (stable PK; referenced by M-01 question bindings; will be referenced by per-perspective score records once that capability ships in a later release), `kpi_id` (FK), `label` (≤60 char), and `display_order` (smallint). Per-perspective scores are NOT stored in this feature (see FR-019, Clarifications session 2026-06-21).
- **CXIWeight** — 0..N rows, only for the CXI KPI. Carries `cxi_kpi_id`, `member_kpi_id` (an included active KPI; unique with `cxi_kpi_id`), and `weight` (positive integer; engine normalises).
- **ScoringConfig** — exactly one row per tenant (owned by M-16, surfaced and edited by this feature). Carries `tenant_id` (unique), `alpha` (numeric(4,3); 0.000–1.000; default 0.500), `mot_multiplier` (numeric(3,1); 1.0–2.0; default 1.5), `n_floor` (integer ≥ 1; default 100), `flag_percentile` (integer 1–49; default 25), `rolling_window_days` (integer ≥ 7; default 30). β is **not** stored — always derived as `1 − alpha` at computation time.
- **OrganizationSettings** — one row per tenant. Carries `name` (≤150 char, required), `logo_blob_ref` (storage URL/key for the uploaded logo; nullable), and `industry` (enum, sourced from the M-11 canonical industry list).

### API Endpoints (informative; finalised in `/plan`)

The following endpoints define the surface area for this feature. Full request/response contracts go in `contracts/` during planning.

- KPI catalogue: `GET /api/v1/kpis`, `GET /api/v1/kpis/{id}`, `POST /api/v1/kpis`, `PUT /api/v1/kpis/{id}`, `PATCH /api/v1/kpis/{id}/activation`, `GET /api/v1/kpis/{id}/binding-usage`.
- CXI weights: `PUT /api/v1/kpis/{cxi_id}/weights`.
- Settings — ScoringConfig: `GET /api/v1/tenant/scoring-config`, `PUT /api/v1/tenant/scoring-config`.
- Settings — Organization: `GET /api/v1/tenant/organization`, `PUT /api/v1/tenant/organization`, `POST /api/v1/tenant/organization/logo`.
- **No** `DELETE /api/v1/kpis/{id}` is registered. KPIs are removed from active use only via deactivation.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A newly provisioned tenant sees the eight standard KPIs in the catalogue immediately, in the canonical order, with zero manual setup steps and within 1.5s of opening `/kpi-management` (per NFR-1).
- **SC-002**: A CX Program Manager can configure a new custom KPI end-to-end — Short Name, Full Name, Calculation Method, Scale, Threshold x/y, Target, Active — in under 2 minutes on a standard tenant (excluding deliberation time on values), with the live preview rendering each field change visibly within 100ms.
- **SC-003**: The Customer Journey settings page persists a complete ScoringConfig edit (all five parameters touched and saved) end-to-end (load → edit → save → reload → verify) in under 1 minute, with the new values active for the next M-06 computation cycle without any service restart.
- **SC-004**: A CXI weights edit involving 5 member KPIs produces Effective % values whose sum equals 100.0% within ±0.1 (rounding tolerance) and recomputes within 100ms of any weight change.
- **SC-005**: Tenant isolation: a randomised cross-tenant probe (Tenant A's session attempting to GET / PUT / PATCH a Tenant B KPI or settings row) returns 404/403 in 100% of attempts and writes 100% of denied attempts to `audit_log` (GP-04 pass condition).
- **SC-006**: Every configuration change — KPI create, edit, activation, CXI weights, ScoringConfig, Organization — produces exactly one corresponding event in `event_log` and one entry in `audit_log` per save, with field-level `{from, to}` diff. 100% of save operations satisfy this; idempotent no-op saves emit zero events.
- **SC-007**: Permission enforcement: 100% of write attempts by a persona without the required permission return 403 with the API-05 envelope, and 100% of read attempts by a permitted persona succeed with 200 — measured across the eight personas in Section 8 of the constitution.
- **SC-008**: Bilingual parity: every UI label, helper text, validation message, confirmation dialog, and tooltip on the KPI Configuration page and the Settings sections renders in both EN and AR; RTL parity passes a manual layout audit for every page.
- **SC-009**: Accessibility: the KPI Configuration page and both Settings section pages pass an automated WCAG 2.1 AA audit (axe-core or equivalent) with zero serious or critical violations; gauge colour bands carry a non-colour cue verifiable by colour-blind simulation.
- **SC-010**: Adoption: within 30 days of the feature shipping to a tenant, the CX Program Manager activates at least three KPIs (beyond the seeded defaults) for at least one journey — measured by a non-zero count of KPI changes attributed to P-01 in `audit_log` over the rolling 30-day window.
- **SC-011**: The "[X] Active KPIs" subtitle reflects activation changes made in the current browser session immediately (no page refresh); 100% of same-session activation toggles update the subtitle within the same tick of the affected mutation. Cross-session changes are explicitly out of scope of this success criterion (they appear on the next navigation or reload).

---

## Assumptions

- The eight standard KPIs are seeded by **M-11 tenant provisioning** at the time the tenant schema is created; M-06 only owns the catalogue surfaces after provisioning. The same seed values apply to every tenant.
- The Industry enum on the Organization section is sourced from the **same canonical list** that M-11 uses at tenant provisioning (Banking, Telecommunications, Government, Automotive, Entertainment, Services) — there is one shared source of truth, not two divergent enums (Q-S2 in the SRS).
- The Customer Journey ScoringConfig entity is **owned by M-16** (table: `scoring_configs`) per the constitution module registry; this feature provides the editing surface only and does not own the table. A future M-16 SRS revision aligns `n_floor` default to 100 to match this spec (Q-S1).
- The KPI binding-usage lookup (touchpoint and journey counts referenced by FR-017 and FR-026) is exposed by M-16 through its **published interface** (per AD-01 / AMENDMENT-006), not by direct cross-schema queries from M-06.
- The `settings.changed` event covers Organization and KPI configuration changes; the `journey.scoring_config.updated` event (already registered in AMENDMENT-007) covers ScoringConfig changes.
- The v1 Emoji Set catalogue is **exactly two** platform-owned code-level sets: `FaceClassic` (graduated faces 😞 🙁 😐 🙂 😊 😄 😍, worst → best) and `HandThumbs` (👎 / 👎🏻 / ✋ / 👍🏻 / 👍, worst → best). No tenant-customisable catalogue. Adding a set requires a platform release. Per-scale slot assignments (which glyph for K=3, K=5, K=7, K=11, K=100) are finalised in `/plan`.
- KPI deletion is **permanently unsupported** in v1 and beyond. KPIs are removed from active use only by deactivation (per SRS FR-1.14).
- Last-writer-wins concurrency on KPI edits is acceptable in v1; optimistic-concurrency tokens are out of scope. Two simultaneous edits from different sessions both persist; the audit log preserves the full history.
- Mobile and tablet viewports for the KPI Configuration page collapse the two-panel layout to stacked (form above preview). Desktop is the primary target per the SRS Operating Environment.
- The Notifications, Branding Theme, and Localization Defaults Settings sections (referenced in FR-048) are listed on the Settings landing page in v1 but are specified by their own briefs and are **out of scope for this feature**.
- The customer-controlled CMK envelope-encryption regime (GP-02) does not apply to KPI definitions or ScoringConfig — these are configuration metadata, not high-sensitivity personal data. Logo blobs are stored under the tenant's configured object-storage region per data residency (T-04) without CMK envelope encryption in v1.
- The CXI member-breakdown payload exposed to M-07 (FR-047) does not introduce a new event; it is part of the regular CXI ScoreSnapshot read API consumed by M-07.

---

## Constitution Check

This feature is in scope of GP-01 – GP-05.

- **GP-01 — Single Source of Truth**: PostgreSQL is the authoritative store for `kpi_definitions`, `thresholds`, perspectives, CXI weights, ScoringConfig, and Organization settings. Elasticsearch is not used by this feature (no read-side analytics). No other store is authoritative.
- **GP-02 — Customer-Controlled Encryption**: not applicable — this feature stores configuration metadata, not high-sensitivity personal data. Logo blobs follow the platform's standard storage encryption, not CMK envelope.
- **GP-03 — Right to Erasure**: not applicable — no per-subject personal data is stored here.
- **GP-04 — Tenant / Scope Isolation**: every KPI and Settings record lives in `tenant_{slug}` schemas; no `tenant_id` column on tenant tables (AD-02 / DB-02); cross-tenant probes return 404/403 and are written to `audit_log` (SC-005).
- **GP-05 — Constitution Compliance Gate**: this Constitution Check passes before implementation begins (verified in the `/speckit-plan` Constitution Check step).

Additional architectural checks:

- **AD-01 — Modular Monolith**: M-06 calls M-16 only through M-16's published interface (`IJourneyBindingQuery` / `IScoringConfigStore` or equivalent — finalised in `/plan`). No direct cross-schema or direct concrete-type access.
- **AD-02 — Schema-per-tenant**: all M-06 tenant tables (`kpi_definitions`, `thresholds`, perspectives, CXI weights) live in `tenant_{slug}` without `tenant_id` columns; ScoringConfig is owned by M-16 and lives in the same schema; Organization settings live with M-11's tenant configuration tables.
- **AD-04 — Elasticsearch for read-side analytics**: NOT applicable — this feature is a configuration surface, not an analytics consumer. No ES queries are introduced.
- **AD-05 — Single codebase, two deployment modes**: this feature's flow does not branch on `ENABLE_MULTI_TENANT`, `ENABLE_BILLING`, or `ENABLE_TENANT_MGMT`. The KPI Configuration surface is available in both SaaS and on-premises modes.
- **AD-07 — Tenant context immutable per request**: tenant resolution is read once from the request (JWT or subdomain per API-02); no endpoint here modifies tenant context.
- **Event catalogue (Section 4)**: this feature emits **only** registered events: `settings.changed` (for KPI configuration, KPI activation, Organization edits) and `journey.scoring_config.updated` (already registered in AMENDMENT-007 for ScoringConfig edits). No new event types are required.
- **Persona registry (Section 8)**: the feature uses **only** P-01, P-02, P-06, and P-07 — all already registered. No new personas needed.
- **T-01 — Multi-language by design**: every page renders in EN + AR with RTL parity (FR-066). T-06 (AI advisory) and T-07 (industry flexibility) are honoured implicitly — no AI auto-execution, KPI definitions are configuration not code.
