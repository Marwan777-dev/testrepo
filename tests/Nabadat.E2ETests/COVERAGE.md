# Nabadat E2E Coverage Matrix

Browser end-to-end coverage for the `frontend/` SPA, in the **single `Nabadat.E2ETests` project**
(Playwright + MSTest, per CLAUDE.md **E2E Test Policy**). Tests are grouped into **module-named
folders** mirroring the `Nabadat.<Module>` unit/integration test taxonomy:

| Module folder | Tests |
|---|---|
| `KpiManagement/` | KpiManagement, KpiConfig, CxiConfig |
| `CustomerJourneyManagement/` | CustomerJourneySettings, JourneyBuilder, JourneyVersion, PersonaVersion, DetectionRules, KpiScoring |
| `UserManagement/` | Auth, UserManagement, PersonaBaseline, DataScope, AuditLog |
| `OrganizationSettings/` | OrganizationSettings |
| `IntegrationHub/` | ServiceChannel, ParameterCatalogue |
| `Accessibility/` | AccessibilityAudit |

All tests share **one harness**, `Infrastructure/E2ETestBase.cs` (a Playwright MSTest `PageTest`
subclass) — the repo ships one frontend app, so there is one sign-in flow: the real MFA-gated
`/login` → TOTP challenge, landing the token in `sessionStorage.session_token`. The harness exposes
persona sign-in (`SignInAsync("P-01")`), the active-user convenience (`SignInAsync()`), explicit
credentials, and dev-fixture reseed/reset helpers; `Infrastructure/E2ETenantDb.cs` seeds the one
KPI-binding row no UI can create. (If a second, separately authenticated SPA is ever added, it gets
its own `Infrastructure/<App>/` base class rather than branching this one.)

**How to run:** start the stack (Postgres + backend host + `npm run dev` for the SPA under test),
set `E2E_BASE_URL` to that SPA's dev-server URL, ensure Playwright browsers are installed
(`pwsh tests/Nabadat.E2ETests/bin/Debug/net10.0/playwright.ps1 install`), then filter to the
module/feature you want:
`dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~<Feature>Tests"`.

**Spec traceability (T150):** every E2E scenario listed in a `**E2E Test Coverage**` block of
[spec.md](../../specs/003-kpi-engine-settings/spec.md) has at least one row below. **US-4 (Customer
Journey settings, [T116]) and US-6 (Organization settings, [T146]) are now authored** in
`CustomerJourneySettingsTests.cs` / `OrganizationSettingsTests.cs` and run green against the live
stack. Two scenarios are recorded `not covered` (with `Assert.Inconclusive`) rather than masked,
because they are structurally unreachable through the shipped UI / seeded fixtures (CJS-E2E-03: MOT is
a clamped slider; ORG-E2E-09: no View-only TenantConfiguration persona is seeded) — each is covered
end-to-end by the backend integration lane. **Implementation divergence:** the standalone
`/settings/customer-journey` and `/settings/organization` pages the spec describes were merged into a
single unified `/settings` screen (the old routes 301-redirect there); the tests drive `/settings` and
scope to each section's stable `data-testid` hooks.

| ID | Feature | Scenario | Spec ref | Persona | Test method | Status |
|----|---------|----------|----------|---------|-------------|--------|
| KPI-E2E-01 | KPI Management (US-1) | Lists the 8 standard KPIs in canonical order on a fresh tenant | spec.md §US-1 (L90) | P-01 | `KpiManagement_lists_eight_standard_kpis_in_canonical_order_when_tenant_is_freshly_provisioned` | Authored — run pending (stack + creds) |
| KPI-E2E-02 | KPI Management (US-1) | Filters to Standard when the Type filter is set | spec.md §US-1 (L91) | P-01 | `KpiManagement_filters_by_type_when_user_selects_Standard` | Authored — run pending |
| KPI-E2E-03 | KPI Management (US-1) | Reveals inactive rows when Active-only is turned off | spec.md §US-1 (L92) | P-01 | `KpiManagement_dims_inactive_rows_when_active_only_is_off` | Authored — run pending |
| KPI-E2E-04 | KPI Management (US-1) | Narrows the list as the user types in search | spec.md §US-1 (L93) | P-01 | `KpiManagement_narrows_list_when_user_types_in_search` | Authored — run pending |
| KPI-E2E-05 | KPI Management (US-1) | Row click navigates to the config edit page | spec.md §US-1 (L94) | P-01 | `KpiManagement_navigates_to_config_edit_when_row_is_clicked` | Authored — **needs US-2 route (T070)** |
| KPI-E2E-06 | KPI Management (US-1) | "+ Add KPI" navigates to the create page | spec.md §US-1 (L95) | P-01 | `KpiManagement_navigates_to_config_create_when_add_kpi_is_clicked` | Authored — **needs US-2 route (T070)** |
| KPI-E2E-07 | KPI Management (US-1) | Hides "+ Add KPI" for the Analyst persona | spec.md §US-1 (L96) | P-02 | `KpiManagement_hides_add_kpi_button_when_user_is_analyst` | Authored — run pending |
| KPI-E2E-08 | KPI Management (US-1) | Redirects to /login when signed out | spec.md §US-1 (L97) | _(none)_ | `KpiManagement_redirects_to_login_when_user_is_signed_out` | Authored — run pending |
| KPI-E2E-09 | KPI Management (US-1) | Full catalogue-page contract: header count (incl. CXI), Type/Active-only/Search controls, 8 columns, canonical order, Short Name link target, Type pill, Scale (0–10 / "—"), Calc. Method, Dashboard, Status, and no row delete control | spec.md §US-1 (L88, AC) | P-01 | `KpiManagement_catalogue_presents_the_full_contract_when_loaded_as_program_manager` | Authored — run pending (stack + creds) |
| KPI-E2E-10 | KPI Config (US-2) | Creates a custom KPI when the form is valid and saved (writes a real row — unique Short Name per run) | spec.md §US-2 (L168) | P-01 | `KpiConfig_creates_custom_kpi_when_form_is_valid_and_saved` | pass |
| KPI-E2E-11 | KPI Config (US-2) | Save is disabled while required fields are empty | spec.md §US-2 (L169) | P-01 | `KpiConfig_disables_save_when_required_fields_are_empty` | pass |
| KPI-E2E-12 | KPI Config (US-2) | Inline error (KPI_SHORT_NAME_DUPLICATE) when Short Name collides | spec.md §US-2 (L170) | P-01 | `KpiConfig_shows_inline_error_when_short_name_is_duplicate` | pass |
| KPI-E2E-13 | KPI Config (US-2) | Short Name is read-only in edit mode | spec.md §US-2 (L171) | P-01 | `KpiConfig_renders_short_name_read_only_in_edit_mode` | pass |
| KPI-E2E-14 | KPI Config (US-2) | Scale + Calc Method are read-only for the NPS standard | spec.md §US-2 (L172) | P-01 | `KpiConfig_renders_scale_and_method_read_only_for_nps` | pass |
| KPI-E2E-15 | KPI Config (US-2) | Emoji Set dropdown appears when representation is Emoji | spec.md §US-2 (L173) | P-01 | `KpiConfig_reveals_emoji_set_dropdown_when_representation_is_emoji` | pass |
| KPI-E2E-16 | KPI Config (US-2) | Representation resets to Number when scale leaves 1–3 with Slider active | spec.md §US-2 (L174) | P-01 | `KpiConfig_resets_representation_to_number_when_scale_leaves_1_3_with_slider_active` | pass |
| KPI-E2E-17 | KPI Config (US-2) | TOP-n warning shows when n exceeds half the scale span | spec.md §US-2 (L175) | P-01 | `KpiConfig_renders_top_n_warning_when_n_exceeds_half_scale_minus_one` | pass |
| KPI-E2E-18 | KPI Config (US-2) | Save blocked when n reaches the scale's box count | spec.md §US-2 (L176) | P-01 | `KpiConfig_blocks_save_when_top_n_equals_scale_max` | pass |
| KPI-E2E-19 | KPI Config (US-2) | Question preview reflects a field change within the <100 ms budget (R10) | spec.md §US-2 (L177) | P-01 | `KpiConfig_updates_question_preview_within_100ms_of_field_change` | pass |
| KPI-E2E-20 | KPI Config (US-2) | Dashboard gauge bands move when threshold x / y change | spec.md §US-2 (L178) | P-01 | `KpiConfig_updates_dashboard_gauge_bands_when_threshold_x_or_y_changes` | pass |
| KPI-E2E-21 | KPI Config (US-2) | Min/Max Scale Descriptions render as preview anchor labels | spec.md §US-2 (L179) | P-01 | `KpiConfig_renders_min_max_scale_descriptions_as_anchor_labels_in_preview` | pass |
| KPI-E2E-22 | KPI Config (US-2) | Unsaved-changes prompt when navigating away after an edit | spec.md §US-2 (L180) | P-01 | `KpiConfig_prompts_unsaved_changes_when_user_navigates_away` | pass |
| KPI-E2E-23 | KPI Config (US-2) | Blocking confirmation when scale changes on a bound KPI (FR-017) — **needs an M-16-bound KPI fixture; no UI path to bind** | spec.md §US-2 (L181) | P-01 | `KpiConfig_shows_blocking_confirmation_when_scale_changes_on_bound_kpi` | not covered — fixture unavailable (Assert.Inconclusive) |
| KPI-E2E-24 | KPI Config (US-2) | Form renders read-only for the Analyst persona (FR-009) | spec.md §US-2 (L182) | P-02 | `KpiConfig_renders_form_read_only_for_analyst` | pass |
| CXI-E2E-01 | CXI Config (US-3) | Question Preview card is hidden for the composite KPI (FR-046); Dashboard Preview still renders | spec.md §US-3 (L233) | P-01 | `CxiConfig_hides_question_preview_card` | pass |
| CXI-E2E-02 | CXI Config (US-3) | Calculation Method is locked read-only to "Weighted Composite" (no editable select) | spec.md §US-3 (L234) | P-01 | `CxiConfig_locks_calculation_method_to_weighted_composite` | pass |
| CXI-E2E-03 | CXI Config (US-3) | Weights table lists active non-composite KPIs (NPS/CSAT/CES) and never the CXI itself | spec.md §US-3 (L235) | P-01 | `CxiConfig_renders_weights_table_with_active_non_cxi_kpis_only` | pass |
| CXI-E2E-04 | CXI Config (US-3) | Effective % column updates live (50.0/33.3/16.7) as weights change | spec.md §US-3 (L236) | P-01 | `CxiConfig_updates_effective_percent_live_when_weights_change` | pass |
| CXI-E2E-05 | CXI Config (US-3) | Active checkbox stays disabled until ≥2 members carry a weight (FR-043) | spec.md §US-3 (L237) | P-01 | `CxiConfig_disables_active_checkbox_when_fewer_than_two_non_zero_weights` | pass |
| CXI-E2E-06 | CXI Config (US-3) | A member KPI deactivated elsewhere drops out of the weights table — mutates state (disposable custom KPI, left inactive) | spec.md §US-3 (L238) | P-01 | `CxiConfig_removes_member_row_when_member_kpi_is_deactivated_elsewhere` | pass |
| CXI-E2E-07 | CXI Config (US-3) | Proportional weight legend renders beneath the 0–100 dashboard gauge | spec.md §US-3 (L239) | P-01 | `CxiConfig_renders_weight_legend_beneath_gauge` | pass |
| CJS-E2E-01 | Customer Journey Settings (US-4) | Shows current/default values when the section opens (β = 1 − α invariant; all 5 params populated) | spec.md §US-4 (L299) | P-01 | `CustomerJourneySettings_shows_defaults_when_tenant_is_freshly_provisioned` | pass — drives unified `/settings` |
| CJS-E2E-02 | Customer Journey Settings (US-4) | β updates live when the α slider moves (End→0.000, Home→1.000) | spec.md §US-4 (L300) | P-01 | `CustomerJourneySettings_updates_beta_live_when_alpha_slider_moves` | pass |
| CJS-E2E-03 | Customer Journey Settings (US-4) | Blocks save when MOT multiplier is out of range | spec.md §US-4 (L301) | P-01 | `CustomerJourneySettings_blocks_save_when_mot_is_out_of_range` | not covered — no UI path (MOT is a slider clamped to 1.0–2.0; the OUT_OF_RANGE guard is covered by the backend integration lane). `Assert.Inconclusive` |
| CJS-E2E-04 | Customer Journey Settings (US-4) | Blocks save when flag percentile is 50 (field marked aria-invalid, no persist) | spec.md §US-4 (L302) | P-01 | `CustomerJourneySettings_blocks_save_when_flag_percentile_is_50` | pass |
| CJS-E2E-05 | Customer Journey Settings (US-4) | Blocks save when rolling window is below 7 | spec.md §US-4 (L303) | P-01 | `CustomerJourneySettings_blocks_save_when_rolling_window_below_7` | pass |
| CJS-E2E-06 | Customer Journey Settings (US-4) | Renders the info tooltip when a question icon is hovered (data-slot hooks) | spec.md §US-4 (L304) | P-01 | `CustomerJourneySettings_renders_tooltip_when_question_icon_focused_or_hovered` | pass |
| CJS-E2E-07 | Customer Journey Settings (US-4) | Renders the section read-only for the IT Admin persona (notice shown, Save hidden, inputs disabled) | spec.md §US-4 (L305) | P-07 | `CustomerJourneySettings_renders_form_read_only_for_it_admin` | pass |
| CJS-E2E-08 | Customer Journey Settings (US-4) | Unsaved-changes guard. **Divergence:** unified page is a `<BrowserRouter>`, so in-app nav is intentionally NOT prompted; the guard is browser-level `beforeunload` — test proves it engages when dirty | spec.md §US-4 (L306) | P-01 | `CustomerJourneySettings_prompts_unsaved_changes_when_user_navigates_away` | pass |
| CJS-E2E-09 | Customer Journey Settings (US-4) | Successful save round-trip + confirmation (valid flag percentile persists, success toast). New home for the scoring-config save that left `KpiScoringTests` when strategic scoring moved to tenant Settings | spec.md §US-4 | P-01 | `CustomerJourneySettings_saves_and_confirms_when_values_are_valid` | authored — run pending |
| KPI-E2E-28 | KPI Config (US-5) | Unticking Active on a KPI bound to M-16 touchpoints opens the blocking deactivation dialog (FR-026); cancelling writes nothing. Seeds the `kpi_bindings` row via SQL (`E2ETenantDb`) — no portal UI can bind by KPI id. | spec.md §US-5 (L354) | P-01 | `KpiConfig_shows_deactivation_confirmation_when_active_toggle_off_with_bindings` | pass |
| KPI-E2E-29 | KPI Config (US-5) | Unticking Active on an unbound KPI shows NO dialog and deactivates directly (FR-026) | spec.md §US-5 (L355) | P-01 | `KpiConfig_skips_deactivation_confirmation_when_no_bindings` | pass |
| KPI-E2E-30 | KPI Config (US-5) | Deactivating a Show-on-Dashboard KPI via the confirmation dialog forces Show-on-Dashboard off in the persisted record (re-fetch + assert). Seeds the binding via SQL (`E2ETenantDb`). | spec.md §US-5 (L356) | P-01 | `KpiConfig_cascades_show_on_dashboard_off_when_kpi_deactivated` | pass |
| ORG-E2E-01 | Organization Settings (US-6) | Shows current values when the section opens (Name populated; Industry + logo controls rendered) | spec.md §US-6 (L419) | P-01 | `OrganizationSettings_shows_current_values_when_user_opens_section` | pass — drives unified `/settings` |
| ORG-E2E-02 | Organization Settings (US-6) | Blocks save when name is empty (field aria-invalid, no persist) | spec.md §US-6 (L420) | P-01 | `OrganizationSettings_blocks_save_when_name_is_empty` | pass |
| ORG-E2E-03 | Organization Settings (US-6) | Uploads a PNG logo when the file is valid (success toast) — REAL write | spec.md §US-6 (L421) | P-01 | `OrganizationSettings_uploads_png_logo_when_file_is_valid` | pass |
| ORG-E2E-04 | Organization Settings (US-6) | Rejects a PDF logo (error toast) | spec.md §US-6 (L422) | P-01 | `OrganizationSettings_rejects_pdf_logo` | pass |
| ORG-E2E-05 | Organization Settings (US-6) | Sanitises an SVG logo whose payload contains script/event handlers (info toast) | spec.md §US-6 (L423) | P-01 | `OrganizationSettings_sanitises_svg_logo_when_payload_contains_script_or_event_handlers` | pass |
| ORG-E2E-06 | Organization Settings (US-6) | Rejects an unparseable SVG (error toast; exact code pinned by integration lane) | spec.md §US-6 (L424) | P-01 | `OrganizationSettings_rejects_unparseable_svg_with_logo_svg_unsafe_content` | pass |
| ORG-E2E-07 | Organization Settings (US-6) | Industry dropdown lists the canonical six values | spec.md §US-6 (L425) | P-01 | `OrganizationSettings_industry_dropdown_lists_canonical_six_values` | pass |
| ORG-E2E-08 | Organization Settings (US-6) | Redirects to /login when the user is signed out | spec.md §US-6 (L426) | _(none)_ | `OrganizationSettings_redirects_to_login_when_user_is_signed_out` | pass |
| ORG-E2E-09 | Organization Settings (US-6) | Renders the form read-only for a persona without edit rights | spec.md §US-6 (L427) | P-02 | `OrganizationSettings_renders_form_read_only_for_persona_without_edit_rights` | not covered — no seeded persona holds TenantConfiguration WITHOUT Manage (all viewers can edit); server-side gate covered by the backend integration lane. `Assert.Inconclusive` |
| KPI-E2E-25 | KPI Config (US-7) | Save button is hidden for the Analyst persona opening NPS config (FR-009 / US-7 scenario 1) | spec.md §US-7 (L465) | P-02 | `KpiConfig_hides_save_button_for_analyst` | pass |
| KPI-E2E-26 | KPI Config (US-7) | Activation control is inert for the Analyst (US-7 scenario 1). **Deviation:** the page *disables* the Active control rather than removing it — consistent with the scenario's "every form field is rendered but disabled" clause; test asserts present-and-disabled. | spec.md §US-7 (L466) | P-02 | `KpiConfig_hides_activation_control_for_analyst` | pass |
| KPI-E2E-27 | KPI Config (US-7) | Question Preview + Dashboard gauge preview cards still render for the Analyst (US-7 scenario 2) | spec.md §US-7 (L467) | P-02 | `KpiConfig_renders_preview_cards_for_analyst` | pass |

> `KpiConfig_renders_form_read_only_for_analyst` (KPI-E2E-24) is the single owner of the US-7
> read-only-form scenario, cross-referenced by US-7's E2E block to avoid a duplicate entry.

## Spec-scenario → row verification (T150)

| Spec story | E2E block | Scenarios in spec | Rows present | Authored | Gap |
|------------|-----------|-------------------|--------------|----------|-----|
| US-1 | spec.md L86 | 8 | KPI-E2E-01..08 (+09) | ✅ | — |
| US-2 | spec.md L164 | 15 | KPI-E2E-10..24 | ✅ | — |
| US-3 | spec.md L229 | 7 | CXI-E2E-01..07 | ✅ | — |
| US-4 | spec.md L295 | 8 | CJS-E2E-01..09 | ✅ | CJS-E2E-03 inconclusive (no UI path; integration-covered); CJS-E2E-09 adds the happy-path save |
| US-5 | spec.md L350 | 3 | KPI-E2E-28..30 | ✅ | — |
| US-6 | spec.md L415 | 9 | ORG-E2E-01..09 | ✅ | ORG-E2E-09 inconclusive (no fixture; integration-covered) |
| US-7 | spec.md L461 | 3 (+1 shared) | KPI-E2E-25..27 (+24) | ✅ | — |

**Result:** every spec.md E2E scenario maps to a row AND an authored test method. **T116** (US-4) and
**T146** (US-6) are complete — `CustomerJourneySettingsTests` (7 pass / 1 inconclusive) and
`OrganizationSettingsTests` (8 pass / 1 inconclusive) run green against the live stack. The two
inconclusive scenarios are structural gaps in the shipped UI / seeded fixtures (documented above), each
covered by the backend integration lane — not silent skips.

---

## `UserManagement/` — M-10 auth + user management

From `specs/001-user-role-management`. Every `[TestMethod]` carries one row; keep the `ID` stable.
The shared harness signs in through the real `/login` → MFA challenge flow.

| ID     | Story | Feature           | Test Method                                             | Status                                   |
| ------ | ----- | ----------------- | ------------------------------------------------------- | ---------------------------------------- |
| AUTH-1 | US1   | Login + MFA       | `Login_creates_session_when_credentials_and_totp_valid` | ✅ passing                               |
| AUTH-2 | US1   | MFA enrolment     | `Login_shows_mfa_enrollment_when_user_has_no_mfa`       | ✅ passing                               |
| AUTH-3 | US1   | MFA challenge     | `Login_shows_error_when_totp_code_invalid`              | ✅ passing                               |
| AUTH-4 | US1   | Password reset    | `PasswordReset_delivers_and_redeems_token`              | 🟡 authored (re-run at checkpoint)       |
| AUTH-5 | US1   | Password reset    | `PasswordReset_rate_limit_blocks_fourth_request`        | ✅ passing                               |
| USR-1  | US2   | User management   | `UserManagement_P01_can_invite_user_and_see_in_list`    | ✅ passing                               |
| USR-2  | US2   | User permissions  | `UserManagement_P01_can_edit_user_permissions`          | ✅ passing                               |
| USR-3  | US2   | CX authority      | `UserManagement_P07_cannot_assign_CX_domain_modules`    | ✅ passing                               |
| PB-1   | US2   | Persona baselines | `PersonaBaseline_P01_can_view_and_modify_baseline`      | ✅ passing                               |
| PB-2   | US2   | Persona baselines | `PersonaBaseline_P03_cannot_access_page`                | ✅ passing                               |
| DS-1   | US3   | Data scope        | `DataScope_P01_can_assign_branch_scope_and_persist`     | ✅ passing                               |
| DS-2   | US3   | Hierarchy scope   | `DataScope_P01_sees_hierarchy_node_picker`              | ✅ passing                               |
| DS-3   | US3   | Custom rules      | `DataScope_P01_can_create_custom_rule`                  | ✅ passing                               |
| DS-4   | US3   | Scope access      | `DataScope_non_admin_cannot_access_scope_page`          | ✅ passing                               |
| DS-5   | US3   | Scope validation  | `DataScope_shows_error_when_value_not_in_definition`    | ✅ passing                               |
| DS-6   | US3   | Scope load error  | `DataScope_shows_load_error_for_unknown_user`           | ✅ passing                               |
| AL-1   | US4   | Audit log view    | `AuditLog_P01_can_view_recent_events`                   | ✅ passing                               |
| AL-2   | US4   | Audit filter      | `AuditLog_P01_can_filter_by_event_type`                 | ✅ passing                               |
| AL-3   | US4   | Read-only         | `AuditLog_P01_cannot_edit_records`                      | ✅ passing                               |
| AL-4   | US4   | Audit access      | `AuditLog_P03_cannot_access_page`                       | ✅ passing                               |

## `CustomerJourneyManagement/` — M-16 journey mapping + journey settings

The tenant-level Customer Journey **settings** (`CustomerJourneySettings`, CJS-E2E-01..09 above, from
`specs/003-kpi-engine-settings`) and the M-16 journey **builder / version / KPI binding** tests below
(from `specs/002-customer-journey-mapping`) all live here — they share the single harness.

- `JourneyBuilderTests` (JOUR-1…JOUR-7) — author happy path (create → stage → touchpoint → activate),
  read-only-persona nav gate (P-03), and builder CRUD/validation (empty state, stage-name-required,
  edit/delete stage, delete touchpoint).
- `KpiScoringTests` (KPI-1…KPI-2) — per-touchpoint KPI binding on `/journeys/:id/scoring`: weight-sum
  validation and the NPS info banner. *(Strategic scoring config is no longer per-journey — its
  save+confirmation moved to `CustomerJourneySettings` / CJS-E2E-09 when scoring went tenant-level.)*
- `PersonaVersionTests` (PV-1…PV-5) — persona lifecycle, Active-only binding selector, the
  P-01-vs-P-02 authority split.
- `JourneyVersionTests` (PV-6…PV-8) — version publish + read-only snapshot, P-01-only publish gate.
- `DetectionRulesTests` (DET-1…DET-3) — journey-level threshold save round-trip, stage-level override
  persistence, unmeasured-touchpoint callout.

> Fixtures for all TenantApp-harness rows are seeded by the host's `Development/DevDataSeeder.cs`
> (`appsettings.local.json.example` documents each persona key). `ConfigGuard` fails the run up front
> if a required auth fixture is missing.


## `IntegrationHub/` — M-13 Integration Hub

From `specs/006-integration-hub`. **US1 (service channels) and US2 (the parameter catalogue) are
authored**; the remaining stories add their own files to this folder as they land
(`IntegrationMonitoringTests`, `RequestLogsTests`, `ParameterMappingTests`, …), sharing the same
harness.

- `ServiceChannelTests` (M13-E2E-01…06) — SCR-03 list + SCR-04 create/edit, covering every scenario
  in spec.md US1's E2E block, for both owning personas.
- `ParameterCatalogueTests` (M13-E2E-07…13) — SCR-05 list + the SCR-06 drawer that opens over it
  (no route of its own), covering every scenario in spec.md US2's E2E block.

| ID | Story | Feature | Scenario | Spec ref | Persona | Test method | Status |
|----|-------|---------|----------|----------|---------|-------------|--------|
| M13-E2E-01 | US1 | SCR-04 channel ID | ID sanitises live as typed and caps at 19 chars | AC-S4-01 / VR-F04 | P-01 | `ServiceChannel_sanitizes_id_live_as_typed_and_caps_at_19_chars` | ✅ passing |
| M13-E2E-02 | US1 | SCR-04 ID lock | ID field is read-only once the channel served its first 2xx | AC-S4-02 / BR-05 | P-01 | `ServiceChannel_locks_id_field_after_first_successful_request` | ✅ passing |
| M13-E2E-03 | US1 | SCR-04 contract | Required is offerable only while Supported is on, and clears with it | AC-S4-03 / FR-S4-04 | P-01 | `ServiceChannel_required_toggle_disables_when_supported_is_off` | ✅ passing |
| M13-E2E-04 | US1 | SCR-04 validation | Duplicate EN name (case-insensitive) is blocked inline | VR-F02 / VR-F04 | P-01 | `ServiceChannel_blocks_save_on_duplicate_name_or_id` | ✅ passing |
| M13-E2E-05 | US1 | SCR-03 no delete | No delete affordance exists on the list or the editor | BR-07 / FR-S3-02 | P-01 | `ServiceChannel_list_shows_no_delete_action_anywhere` | ✅ passing |
| M13-E2E-06 | US1 | SCR-03/04 read-only | P-07 sees the screens with every write control hidden | BR-24 / FR-GBL-05 | P-07 | `ServiceChannel_it_admin_sees_read_only_view` | ✅ passing |
| M13-E2E-07 | US2 | SCR-06 type config | Range card and List panel swap as the data type changes | AC-S6-01 | P-01 | `Parameters_type_switch_between_range_and_list_shows_correct_panel` | ✅ passing |
| M13-E2E-08 | US2 | SCR-06 API field | Auto-suggests `snake_case` from the EN name, stays editable pre-lock | AC-S6-02 | P-01 | `Parameters_api_field_auto_suggests_from_english_name` | ✅ passing |
| M13-E2E-09 | US2 | SCR-06 uniqueness | Duplicate API field is blocked inline, incl. against a **disabled** row | AC-S6-03 / VR-F06 | P-01 | `Parameters_blocks_save_on_duplicate_api_field_including_disabled` | ✅ passing |
| M13-E2E-10 | US2 | SCR-05 filters | Origin tab ∧ type filter combine, and the tab counts stay global | AC-S5-01 / FR-S5-01 | P-01 | `Parameters_origin_and_type_filters_combine_with_AND` | ✅ passing |
| M13-E2E-11 | US2 | SCR-05 disable guard | Disabling a referenced parameter shows Dialog D-6 before anything changes | AC-S5-02 / BR-10 | P-01 | `Parameters_disable_shows_impact_warning_when_referenced` | ✅ passing |
| M13-E2E-12 | US2 | SCR-05/06 built-ins | No delete anywhere; a built-in's API field and data type are read-only | BR-09 / `[PO-G27]` | P-01 | `Parameters_builtin_row_has_no_delete_action_and_locked_api_field` | ✅ passing |
| M13-E2E-13 | US2 | SCR-06 Range rule | Min ≥ Max is blocked inline; correcting the pair clears the block | VR-F07 | P-01 | `Parameters_range_validation_blocks_min_greater_than_max` | ✅ passing |

> **These tests write real rows.** The E2E lane has no transaction rollback and VR-F13 caps a tenant
> at 100 service channels, so every channel a test seeds or creates is torn down in `[TestCleanup]`
> through `E2ETenantDb.DeleteServiceChannelAsync` / `…ByChannelIdAsync`, and names/IDs carry a
> run-unique `HHmmssfff` suffix. Deleting there is fixture hygiene only — BR-07 means the product
> ships **no** DELETE endpoint. The shared `e2e` tenant was already carrying ~73 channels from
> earlier runs when this file landed; see TODO-M13-004.
>
> **M13-E2E-02 / 04 / 06 need `e2e.tenantDb`** (`appsettings.local.json`). BR-05's lock is set only by
> a channel's first 2xx inbound request — a US4 pipeline no console UI can trigger — so the flag is
> seeded directly, the same "one row no UI can create" pattern as the M-06 KPI binding. Without the
> connection string those three report `Assert.Inconclusive` with the reason rather than silently
> passing.
>
> **The same applies to the parameter rows.** VR-F13 caps a tenant at 200 *custom* parameters, so
> `ParameterCatalogueTests` tears down everything it seeds or creates via
> `E2ETenantDb.DeleteParameterAsync` / `…DeleteCustomParameterByApiFieldAsync` (both guarded on
> `origin = 'custom'`, so a buggy test can never remove one of the 23 built-ins). **M13-E2E-09 / 10
> / 11 need `e2e.tenantDb`**: a *disabled* parameter (VR-F06 must bite against one), a known custom
> Range row, and a `channel_parameter_assignments` reference for D-6 are each seeded directly rather
> than built through a UI path another scenario owns.
>
> **Counts race the first paint.** The SCR-05 filter row renders before the list response lands, and
> the origin-tab count pills are deliberately absent until the counts arrive (a loading tab shows its
> label, never a flash of `0`). `GoToListAsync` therefore waits for a digit in the All tab — waiting
> on the search box alone let M13-E2E-10 read `-1` and fail intermittently.
