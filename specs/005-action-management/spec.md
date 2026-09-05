# Feature Specification: M-15 Action Management

**Feature Branch**: `005-action-management`

**Created**: 2026-07-22

**Status**: Draft (rev 1.1 — audit remediation applied, 22 Jul 2026)

**Input**: User description: "Generate a complete functional specification based strictly on the attached SRS (SRS-M15-Action-Management-v1_1). Treat the SRS as the single source of truth and do not omit any requirement, business rule, workflow, validation, dependency, exception, or acceptance criterion."

**Source SRS**: `SRS-M15-Action-Management-v1_1 1.md` v1.1 (21 Jul 2026, Final — approved for Speckit)

**Module code**: M-15 (Nabadat VOC Platform, Phase 2)

**Traceability convention** (mirrors SRS §1):
- **[BR]** — explicit stakeholder business requirement (including the 21 Jul 2026 final ruling)
- **[HTML]** — element or behaviour present in the approved HTML prototype v6
- **[Derived from UI]** — behaviour inferred from the prototype, subsequently ratified

---

## Overview

M-15 Action Management closes the *act* stage of the Voice-of-Customer loop at the **initiative** level (as opposed to the individual case level owned by M-14). It lets Customer Experience (CX) teams **define, track, and measure improvement actions** (e.g., a call-center training program) against one or more KPI Targets, computing outcome (Successful / Partially Successful / Unsuccessful) automatically on each Target Date using a **two-anchor measurement model** (score axis anchored at Action Start Date, time axis anchored at Target Start Date = Action End Date + 1 day).

The module ships **three screens** (All Actions list, Add/Edit Action form, Action Details page), a **Settings → Actions** subsection with two tenant-level parameters (X, PAD), and cross-module contracts with M-06 (KPI Engine) and M-07 (Dashboards & Reporting).

**Explicitly out of scope for v1** (SRS §1.2, per stakeholder decision):
- Cloning / duplicating an Action (removed)
- Permanent Action-level deletion (replaced by Archive; Target-level delete remains in scope)
- Editing a Completed Action (Completed is **read-only** per BR-023)
- All user alerting / notifications (postponed **in full** to the M-09 Notifications module; M-15 v1 ships only the in-app toasts and confirmation dialogs of SRS §15)
- Linkage of Actions to journeys (M-16), cases (M-14), or AI recommendations (v1 links Actions to **KPIs only**)
- Audit-trail *viewing* UI (the data requirement is in scope; the viewer screen is not)
- The permissions engine itself (M-10 — interim matrix per SRS §13 applies until refined)
- Implementation of the M-07 trend chart (only M-15's overlay contract is specified)
- Global platform chrome (sidebar navigation, theme toggle — owned by the platform shell)

**Key terms (imported from SRS §1.3 — rev 1.1, for standalone readability):**

| Term | Definition |
|---|---|
| Action | An improvement initiative measured in M-15 against one or more KPI Targets |
| KPI Target | Per-KPI measurement on an Action: KPI + Target Date + Lower/Upper Thresholds; one per KPI per Action |
| Baseline (B) | KPI score auto-captured on the **Action Start Date**; thresholds are deltas over it |
| Current (C) | KPI's live score from M-06 at viewing time |
| Final Score | KPI score on the Target Date; input to outcome evaluation |
| L / U | Lower / Upper Threshold deltas; 0 ≤ L ≤ U ≤ X, U > 0; Lower/Upper Threshold **Point** = Baseline + L / + U |
| Action Start / End Date (D1/D2) | User dates: work begins (baseline snapshot) / work completes (boundary only) |
| Target Start Date (D3) | **System-derived = End + 1 day**; monitoring clock zero; never user-entered |
| Target Date (D4) | Per-Target evaluation date (Successful / Partially Successful / Unsuccessful) |
| Score Progress | (Current − Baseline) ÷ (Upper Threshold Point − Baseline) — raw drives logic, display clamps 0–100 % |
| Time Progress | (Now − Target Start) ÷ (Target Date − Target Start); 0 during the execution phase |
| Lowest-Performing Target | Eligible Target with the lowest **raw** Score Progress (ties: earliest Target Date, then KPI name) |
| X / PAD | Tenant settings: threshold-slider maximum (default 20) / zone-slider track padding (default 3, integer) |
| Archived | Standalone action status, exclusive of the other three; measurement continues; view-only until unarchived |
| Tenant timezone | Platform setting anchoring every day-boundary comparison (BR-022) |
| KPI standardisation | All KPIs are higher-is-better (M-06); e.g. CES asks "how easy", not "how hard" |

---

## User Scenarios & Testing *(mandatory)*

> **Vocabulary reminder (self-review from SRS §1):** Completed (never Expired/Past); **More Details** (never "View"); **Archive/Unarchive** (never Clone, never action-level Delete, never "Deactivate/End early"); **Archived is a standalone status** (exclusive of Planned/Active/Completed) while measurement continues; Completed Actions are **read-only**; no review-cadence functionality exists.

> **Rev 1.1 governance notes (audit remediation):**
> 1. **Priorities (P1/P2/P3) order the build only — every requirement in this specification is in scope for v1.** No FR, filter, or Settings item may be descoped by priority prose.
> 2. **All API endpoints, HTTP status codes, machine error-code strings, response headers (e.g. `X-Nabadat-Stale-Save`), and event transports named in the test-coverage blocks are *proposed technical design*, subject to architecture review** — SRS §17 declares API shapes out of scope. The *behaviours* those tests verify are normative; the shapes are not contractual until ratified. Test class names/paths assume the repository's existing conventions (.NET tests, `frontend/portal/` SPA) — verify before task generation.

### User Story 1 — Create an Action with KPI Targets (Priority: P1)

**Persona**: CX Program Manager.

A CX Program Manager wants to record a new improvement initiative (for example, *"Training of Call Center Agents"*), attach one or more KPI Targets (each with a Target Date, Lower Threshold, and Upper Threshold expressed as **deltas over the Baseline**), and save it. On save the system persists the Action, auto-captures the Baseline from M-06 on the Action Start Date (or from historical scores if retro-dated), and the Action is grouped into its date-computed status tab on SCR-01.

**Why this priority**: The Add/Save flow is the foundational entry point of the whole module — without it, no measurement is possible. This story unlocks the MVP loop.

**Independent Test**: A Program Manager can open `/actions` → click **Add Action** → enter Name, Start Date, End Date, Description (optional), add one KPI Target with a KPI, Target Date, and Upper Threshold > 0 → click **Save action**. On success a toast reads "Action saved" and the user returns to SCR-01 with the new card in the correct tab (Planned / Active / Completed per date-computed status FR-102). The action is persisted, the Baseline captured (when Start ≤ today), and an audit event "action created" is written.

**Acceptance Scenarios**:

1. **AC-2.1** — **Given** a blank Add-Action form, **When** the user clicks **Save action**, **Then** a toast "Action Name is required" appears, focus moves to the Name field, no record is persisted, no navigation occurs. [SRS §7.12]
2. **AC-2.2** — **Given** the threshold slider is at L = 0 and U = 0, **When** the slider renders, **Then** the track is **plain grey**, both flags read text-only "Lower Threshold" / "Upper Threshold" positioned at illustrative 24 % / 76 % positions. [SRS §5.2, §7.12]
3. **AC-2.3** — **Given** U has never been touched, **When** L is dragged from 0 to 4.5, **Then** U becomes 4.5, both flags read "L +4.5" / "U +4.5", and the track shows hard-edged red [0, 4.5] and green [4.5, X] with no yellow band. [SRS §5.2, §7.12]
4. **AC-2.4** — **Given** U was set to 6, **When** L is dragged toward 8, **Then** L stops at 6 (clamped ≤ U). [SRS §5.2, §7.12]
5. **AC-2.5** — **Given** Target 1's KPI select is NPS, **When** Target 2's KPI select opens, **Then** "NPS" is disabled; deleting Target 1 re-enables it. [SRS §7.5, BR-001]
6. **AC-2.6** — **Given** a KPI is selected and Start Date is empty, **When** the score label renders, **Then** it reads "Current Score · {v} — Captured on the action start date as the baseline score"; setting Start Date to yesterday flips it to "Baseline · {v}". [SRS §7.6]
7. **AC-2.8** — **Given** a Target Date equal to the Action End Date, **When** Save is clicked, **Then** VAL-206 blocks with the message "Target Date must be after the Action End Date". [SRS §7.12]
8. **AC-2.9** — **Given** every tenant KPI is already targeted, **When** the form renders, **Then** the **Add KPI Target** button is disabled. [SRS §7.12]
9. **AC-2.13** — **Given** an active Target with L = 0 and U = 0, **When** Save is clicked, **Then** VAL-210 blocks with "Upper Threshold must be greater than zero" (division guard, BR-F3). [SRS §7.12]
10. **W-7 Retro-dated create** — **Given** an Action whose Start and End dates are in the past, **When** it is saved, **Then** Baseline is pulled from M-06 historical score for the Start Date and the Action is born Active or Completed per FR-102 (Completed → immediately read-only, BR-023). [SRS §7.11]

**Unit Test Coverage**:

- **Units under test**:
  - `ThresholdValidator` — enforces VAL-201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211 (all validation rules of SRS §7.8).
  - `ThresholdAutoSyncCalculator` — auto-sync rule: U mirrors L until U is independently touched; L is clamped ≤ U afterwards (BR-004).
  - `BaselineCaptureService` — snapshots M-06 score on Action Start Date (live if Start = today, historical if retro-dated) (BR-B1, BR-B3).
  - `ActionStatusCalculator` — computes date-driven status (Planned = Start > now, Completed = latest Target Date < now, else Active), returns "Archived" when `archived = true` (BR-008, BR-009).
  - `KpiOptionsFilter` — excludes KPIs already chosen by other Targets of the same Action; excludes M-06-deactivated KPIs (BR-001, BR-002).
- **Required cases**:
  - `Validate(new ActionRequest { Name = "" }) → Invalid("Action Name is required")` (VAL-201).
  - `Validate(existingNames = ["Training"], name = "training")` → `Invalid("An action with this name already exists")` (VAL-202, case-insensitive across all statuses incl. Archived).
  - `Validate(startDate = 2026-07-17, endDate = 2026-07-16)` → `Invalid("Action End Date must be on or after the Action Start Date")` (VAL-204).
  - `Validate(actionEndDate = 2026-07-19, targetDate = 2026-07-19)` → `Invalid("Target Date must be after the Action End Date")` (VAL-206).
  - `Validate(activeTargets = [])` → `Invalid("At least one active KPI target is required")` (VAL-207).
  - `Validate(target = { L = 5, U = 0 })` → `Invalid("Upper Threshold must be greater than zero")` (VAL-210).
  - `Validate(targets = [ NPS, NPS ])` → `Invalid` (VAL-211).
  - `AutoSync(L=0, U=0, changed=L→4.5, uTouched=false)` → `(L=4.5, U=4.5, uTouched=false)` (BR-004).
  - `AutoSync(L=0, U=6, changed=L→8, uTouched=true)` → `(L=6, U=6)` (L clamped to ≤ U).
  - `Capture(kpi=NPS, actionStartDate=2026-07-17, current="today"=2026-07-17)` returns live M-06 score for 2026-07-17.
  - `Capture(kpi=NPS, actionStartDate=2026-01-15, current="today"=2026-07-22)` returns historical M-06 score for 2026-01-15 (retro-dated); if M-06 has no score for that date the service raises `NoBaselineScoreException` (ERR-5 trigger).
  - `ComputeStatus(archived=false, startDate=tomorrow, latestTargetDate=next-month, now="today")` → `Planned`.
  - `ComputeStatus(archived=false, startDate=yesterday, latestTargetDate=next-month, now="today")` → `Active`.
  - `ComputeStatus(archived=false, startDate=last-month, latestTargetDate=yesterday, now="today")` → `Completed`.
  - `ComputeStatus(archived=true, /* any dates */)` → `Archived`.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/actions` — create with valid payload → 201, returns action + captured baseline for each Target; writes `action.created` + `baseline.captured` audit events; row in `actions` + `kpi_targets` tables.
  - `POST /api/actions` — create with duplicate name (case-insensitive, incl. an Archived action carrying the name) → 400 with API-05 envelope `{ error: { code: "validation.duplicate_action_name", message: "An action with this name already exists" } }`.
  - `POST /api/actions` — retro-dated Start Date with existing historical M-06 score → 201, baseline captured from history.
  - `POST /api/actions` — retro-dated Start Date but M-06 has no score for that date → 409 with ERR-5 envelope `{ error: { code: "kpi.no_historical_score", message: "No KPI score exists for {date}. Choose a different Action Start Date." } }`.
  - `POST /api/actions` — retro-dated so that on save the Action is born Completed → 201; the response reflects `status = "completed"` and subsequent writes to this action are refused (BR-023).
  - `POST /api/actions` — U = 0 on an active Target → 400 `validation.upper_threshold_required` (VAL-210).
  - `POST /api/actions` — as a non-Program-Manager role → 403 (ERR-3, hidden UI enforcement server-side per SRS §13).
- **What's intentionally NOT covered end-to-end**: pure calculator/validator behaviour listed under Unit Tests above (`ThresholdValidator`, `ThresholdAutoSyncCalculator`, `ActionStatusCalculator`, `KpiOptionsFilter`).

**Scenario Test**: `scenario-test: ActionCreationScenarioTests` — verifies the full happy path from `POST /api/actions` (create) → `GET /api/actions` (list — new action appears in correct tab) → `GET /api/actions/{id}` (detail — Baseline is snapshotted and stored, `target_start_date = action_end_date + 1 day`, `latest_target_date` computed). Spans 3 endpoints, carries Action id + baseline scores between calls, asserts final state: exactly one `action.created` event and one `baseline.captured` event per Target in the audit log.

**E2E Test Coverage** (frontend SPA `frontend/portal/`):

- **User flows under test**: `/actions/new` (Add Action), `/actions/:id/edit` (Edit Action — same screen pre-filled) → `ActionAddEditTests.cs` in `tests/Nabadat.E2ETests/ActionManagement/`.
- **Required scenarios**:
  - `AddAction_saves_and_navigates_to_all_actions_when_valid` — happy path, W-6.
  - `AddAction_blocks_save_with_toast_and_focus_when_name_is_empty` — VAL-201 (AC-2.1).
  - `AddAction_blocks_save_with_toast_when_upper_threshold_is_zero` — VAL-210 (AC-2.13).
  - `AddAction_disables_kpi_option_when_already_selected_in_another_target` — BR-001 (AC-2.5).
  - `AddAction_switches_current_to_baseline_label_when_start_date_is_set_to_past` — SRS §7.6 (AC-2.6).
  - `AddAction_slider_shows_grey_track_and_text_only_flags_when_both_thresholds_are_zero` — SRS §5.2 default state (AC-2.2).
  - `AddAction_slider_hard_zones_recolour_when_lower_threshold_is_dragged` — SRS §5.2 set state (AC-2.3).
  - `AddAction_lower_flag_clamps_to_upper_when_dragged_past_it` — BR-004 (AC-2.4).
  - `AddAction_disables_add_kpi_target_button_when_all_kpis_are_used` — AC-2.9.
  - `AddAction_role_guard_redirects_when_analyst_opens_new_route` — SRS §13.

---

### User Story 2 — Monitor Actions on the All Actions page (Priority: P1)

**Persona**: any user with view access (Program Manager, Analyst, Executive/Viewer).

Users land on `/actions` (SCR-01) to scan the health of every tenant Action across four tabs — **Active, Planned, Completed, Archived**. Active cards immediately spotlight the **Lowest-Performing Target** (the target with the lowest raw unclamped Score Progress among eligible Targets), so managers can identify red-timer initiatives within seconds. Search and filters span all four tabs.

**Why this priority**: SCR-01 is the module's landing page and the primary entry point to every drill-down and every write path. Without it the module has no navigable surface.

**Independent Test**: Seed the tenant with representative Actions (e.g., 2 Active, 3 Planned, 3 Completed, 1 Archived). Open `/actions`. Verify tab counts show `2/3/3/1`, the Active tab is default, cards render newest-created-first, each Active card shows one featured target with the correct timer colour (green/yellow/red) computed from raw Score Progress vs raw Time Progress. Type a search query — the cross-tab hint appears and filtered cards remain in each tab.

**Acceptance Scenarios**:

1. **AC-1.1** — **Given** 9 actions of which 2 compute Active, 3 Planned, 3 Completed and 1 is Archived, **When** SCR-01 renders, **Then** tab counts read 2/3/3/1 and each tab shows only its own cards. [SRS §6.10]
2. **AC-1.2** — **Given** an Active action whose Lowest-Performing Target has raw Score Progress 66.7 % and raw Time Progress 84.2 %, **When** the card renders, **Then** the timer is **red**, ring fill is 84 %, and the meta reads "Score 67% · Time 84% — behind pace". [SRS §6.10]
3. **AC-1.3** — **Given** an Active action whose latest Target Date was yesterday (tenant timezone), **When** SCR-01 renders today, **Then** the action appears in **Completed** with one outcome chip per KPI, a full grey timer, and no Edit affordance anywhere. [SRS §6.10, FR-105, BR-023]
4. **AC-1.4** — **Given** the query "training", **When** typed in search, **Then** only name-matching cards remain visible in every tab (including Archived) and the hint states the cross-tab match count. [SRS §6.10, FR-106, BR-016]
5. **AC-1.7** — **Given** an action with a deactivated Target, **When** its card renders, **Then** the mini-labels row lists that KPI struck through and the featured target is never the deactivated one. [SRS §6.10, BR-010, §3.6]
6. **AC-1.8** — **Given** an Active action whose only Targets are all deactivated, **When** its card renders, **Then** it shows "No active targets to feature" with no slider, the mini labels struck through, and a timer computed against the latest remaining Target Date (grey full ring if none remains) per FR-111. [SRS §6.10]
7. **Empty state per tab** — **Given** zero Actions in a tab, **When** SCR-01 renders that tab, **Then** a full-width empty card reads "No {active/planned/completed/archived} actions." For the three status tabs (not Archived), append guidance "Create one with **Add Action**." (FR-108). [SRS §6.3]
8. **Ordering & pagination** — **Given** more actions than fit in one viewport, **When** the tab renders, **Then** cards are ordered newest-created-first and pagination / infinite-scroll loads the rest (FR-110). [SRS §6.3]

**Unit Test Coverage**:

- **Units under test**:
  - `LowestPerformingTargetSelector` — implements §3.6: raw unclamped Score Progress ordering, eligibility (active, has Baseline, Target Date ≥ Current Date), tie-breaks (earliest Target Date, then KPI name ASC).
  - `ScoreProgressCalculator` — `(Current − Baseline) ÷ (UpperThresholdPoint − Baseline)`, returns raw signed decimal.
  - `TimeProgressCalculator` — `(CurrentDate − TargetStartDate) ÷ (TargetDate − TargetStartDate)`; returns 0 when `CurrentDate ≤ ActionEndDate` (BR-F1); returns raw ratio (can exceed 1).
  - `TimerColourResolver` — maps raw Score vs raw Time onto Green (Score > Time), Yellow (|Score − Time| ≤ 0.005 — BR-015), Red (Score < Time); Grey for Completed / evaluated Targets; Empty for Planned / deactivated (BR-F1, §3.4 table).
  - `DisplayClamper` — clamps display value to [0, 100]% for ring fill and labels while leaving raw values for logic (BR-F2, BR-014).
  - `ActionCardStatusGrouper` — for a set of Actions, groups by date-computed status per FR-102, sinks Archived Actions to the Archived tab per FR-103.
  - `ActionSearchFilter` — case-insensitive substring match on Action Name, applied across all four tabs simultaneously (BR-016).
  - `ZeroEligibleFallback` — FR-111 rule: replaces featured slot with "No active targets to feature" text + timer against latest remaining Target Date.
- **Required cases**:
  - `Select(targets = [{score=0.5}, {score=-0.2}, {score=0.9}])` → target with `score=-0.2` (raw negative wins).
  - `Select(targets = [{score=0.5,date=2026-08-01}, {score=0.5,date=2026-07-30}])` → target with earliest Target Date (2026-07-30).
  - `Select(targets = [{score=0.5,date=D,name="NPS"}, {score=0.5,date=D,name="CSAT"}])` → target named "CSAT" (alphabetical).
  - `Select(targets = [{deactivated=true}, {evaluated=true}, {active,elig}])` → the eligible one; deactivated + evaluated are excluded.
  - `ScoreProgress(current=76, baseline=70, upper=+6)` → `1.0` (raw).
  - `ScoreProgress(current=68, baseline=70, upper=+6)` → `-0.333` (raw negative; regression).
  - `TimeProgress(currentDate=D, targetStart=D+1, targetDate=D+30)` where `D ≤ actionEndDate` → `0` (BR-F1).
  - `TimeProgress(currentDate=targetStart+15, targetStart=D, targetDate=D+30)` → `0.5`.
  - `Colour(score=0.667, time=0.842)` → `Red`.
  - `Colour(score=0.500, time=0.500)` → `Yellow`.
  - `Colour(score=0.502, time=0.500)` → `Yellow` (equality band ±0.005, BR-015).
  - `Colour(score=0.510, time=0.500)` → `Green` (outside equality band).
  - `Clamp(raw=-0.3)` → `0` (display); `Clamp(raw=1.2)` → `100`.
  - `Group(actions, now=today)` — Archived Action never appears in other tabs (BR-009, FR-103).
  - `Match("Training of Call Center Agents", query="training")` → `true`; `Match("Onboarding", query="training")` → `false`.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `GET /api/actions?tab=active` — returns Actions grouped as Active per FR-102, each with the raw computation inputs (baseline, thresholds, dates, current score, normalised index) from which the client computes the featured target per NFR-5, ordered newest-created-first (FR-110), paginated.
  - `GET /api/actions?tab=archived` — returns only Actions with `archived=true` (FR-103); each carries its underlying date-computed shape (Active/Planned/Completed) but the tab is Archived.
  - `GET /api/actions?q=training` — cross-tab search count in the response envelope + per-tab paginated results (FR-106).
  - `GET /api/actions?kpi=NPS&kpi=CSAT&start_from=2026-07-01&start_to=2026-07-31` — KPI multi-select AND date-range filter combines with search across all four tabs (FR-107).
  - `GET /api/actions?tab=active` — returns 200 with empty payload for an empty tab (empty state driven client-side per FR-108).
- **What's intentionally NOT covered end-to-end**: `LowestPerformingTargetSelector`, `ScoreProgressCalculator`, `TimeProgressCalculator`, `TimerColourResolver`, `DisplayClamper`, `ZeroEligibleFallback` — covered by unit tests.

**Scenario Test**: `scenario-test: not-needed — SCR-01 is a single-endpoint list view with no cross-endpoint state carry-over; the calls are independent.`

**E2E Test Coverage** (frontend SPA `frontend/portal/`):

- **User flows under test**: `/actions` (SCR-01 All Actions) → `AllActionsTests.cs` in `tests/Nabadat.E2ETests/ActionManagement/`.
- **Required scenarios**:
  - `AllActions_shows_tab_counts_matching_underlying_status_distribution` — AC-1.1.
  - `AllActions_active_card_shows_red_timer_and_behind_pace_meta_when_score_lags_time` — AC-1.2.
  - `AllActions_active_moves_to_completed_tab_when_latest_target_date_passes` — AC-1.3, FR-105.
  - `AllActions_search_query_filters_cards_across_all_four_tabs_and_shows_cross_tab_hint` — AC-1.4, FR-106.
  - `AllActions_active_card_shows_zero_eligible_fallback_when_all_targets_deactivated` — AC-1.8, FR-111.
  - `AllActions_planned_card_features_kpi_with_lowest_current_score_on_normalised_index` — SRS §6.5, §3.6 Planned fallback.
  - `AllActions_completed_card_shows_outcome_chips_and_no_edit_affordance` — SRS §6.6, BR-023.
  - `AllActions_shows_empty_state_with_add_action_guidance_when_status_tab_is_empty` — FR-108.
  - `AllActions_analyst_role_sees_no_add_action_button_and_no_kebab_edit_archive_items` — SRS §13.
  - `AllActions_pagination_loads_older_cards_when_scrolling_beyond_one_viewport` — FR-110.

---

### User Story 3 — Drill into Action Details (Priority: P1)

**Persona**: any user with view access.

From any card the user opens SCR-03 (`/actions/:id`) — the full breakdown of one Action: identity, status badge (single, exclusive Planned / Active / Completed / **Archived**), the four-date timeline (Action Start · Action End · Target Start (derived, tooltipped) · Latest Target Date), and one row per KPI Target with the complete visualisation set (reference-variant Stepped Zone Slider with L/U reference flags + B/C markers, per-row timer against its own Target Date, side-zone facts).

**Why this priority**: SCR-03 is where users diagnose *why* a card is red — it is the deep read for decision-making. Without SCR-03 the module is decorative.

**Independent Test**: Open a Planned, Active, and Completed action via `/actions/:id`. Verify each variant renders per SRS §8.4–§8.6: Planned rows show a slider with no B flag and an empty timer; Active rows show reference sliders with per-row timers; Completed rows show outcome labels with the C flag inside its outcome zone. The single status badge always reflects Archived when the Action is archived. All values compute live from stored data.

**Acceptance Scenarios**:

1. **AC-3.1** — **Given** an Active action with three unevaluated Targets of differing Target Dates, **When** SCR-03 renders, **Then** each row shows its own timer against its own Target Date, and exactly one row carries the "Lowest performing" badge + 2 px cyan ring highlight — the one with the lowest raw Score Progress among eligible Targets. [SRS §8.9]
2. **AC-3.2** — **Given** a Completed action with outcomes Successful/Partial/Unsuccessful, **When** it renders, **Then** each row's C flag sits inside the matching colour zone, the outcome label matches, timers are full grey, and no Edit/Activate/Delete control exists (BR-023). [SRS §8.9]
3. **AC-3.3** — **Given** a deactivated Target on an Active action, **When** the page renders, **Then** the row is faded (≈50 % opacity + greyscale) with the slider and dates visible, marked "Deactivated", shows **Activate** + **Delete** buttons, and displays "Excluded from results". [SRS §8.9]
4. **AC-3.4** — **Given** an Archived action, **When** SCR-03 renders, **Then** the single status badge reads "Archived", Edit is absent, Unarchive is present, and all values still compute live. [SRS §8.9, BR-009]
5. **AC-3.5** — **Given** the date row, **When** rendered, **Then** Target Start always equals Action End + 1 day and carries the explanatory tooltip "System-derived: Action End Date + 1 day. Monitoring clock starts here." [SRS §8.9, BR-006]
6. **AC-3.6** — **Given** a Target force-deactivated because its KPI was deactivated in M-06, **When** its KPI is still inactive, **Then** the **Activate** button is disabled with the tooltip "KPI is inactive in M-06"; once the KPI is Active again, Activate is enabled. [SRS §8.9, BR-011]
7. **AC-3.7** — **Given** an Active action where one Target's Target Date passed yesterday, **When** SCR-03 renders, **Then** that row shows its outcome label, its C flag inside the outcome zone, "Evaluated {date}", and a full grey timer, while the other rows keep live timers and the evaluated row is never the "Lowest performing" one. [SRS §8.4 (b), §8.9]
8. **AC-3.8** — **Given** a non-archived action on SCR-03, **When** Archive is clicked, **Then** the page refreshes in place with the "Archived" badge alone, Edit disappears, Unarchive appears, and the SCR-01 counts reflect the move. [SRS §8.9, §10.3]

**Unit Test Coverage**:

- **Units under test**:
  - `ActionDetailProjection` — assembles the per-Target row payload (variant selection per §8.4 (a)/(b), §8.5, §8.6, §8.7) from stored Action + Target + live M-06 scores.
  - `OutcomeEvaluator` — implements BR-O1 (Successful: Final ≥ Baseline + U), BR-O2 (Partially Successful: Baseline + L ≤ Final < Baseline + U), BR-O3 (Unsuccessful: Final < Baseline + L); BR-O4 (U = L → binary outcome).
  - `TargetStartDeriver` — `TargetStartDate = ActionEndDate + 1 day` (always; BR-006).
  - `LatestTargetDateCalculator` — `max(target.target_date for target in action.targets)`.
- **Required cases**:
  - `Evaluate(baseline=70, L=+3, U=+6, final=77)` → `Successful` (77 ≥ 76).
  - `Evaluate(baseline=70, L=+3, U=+6, final=74)` → `PartiallySuccessful` (73 ≤ 74 < 76).
  - `Evaluate(baseline=70, L=+3, U=+6, final=71)` → `Unsuccessful` (71 < 73).
  - `Evaluate(baseline=70, L=+3, U=+3, final=73)` → `Successful` (equality: U=L collapses partial band, binary outcome — BR-O4).
  - `Evaluate(baseline=70, L=+3, U=+3, final=72)` → `Unsuccessful`.
  - `Derive(actionEndDate=2026-07-19)` → `TargetStartDate = 2026-07-20`.
  - `LatestTargetDate(targets = [ {2026-08-01}, {2026-09-15}, {2026-07-25} ])` → `2026-09-15`.
  - `Project(action = Active, target = { targetDate < today })` → row variant = **evaluated** (§8.4 (b)) with outcome label + grey timer.
  - `Project(action = Active, target = { targetDate > today, deactivated=false, has Baseline })` → row variant = **active-unevaluated** with reference slider + own timer.
  - `Project(action = Archived, target = { active + eligible })` → underlying variant preserved; header badge = Archived only.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `GET /api/actions/{id}` — returns full Action + Targets with server-authoritative facts (`target_start_date`, `latest_target_date`, per-target `baseline_score`, and `final_score` + `outcome` once evaluated) plus the raw inputs from which the client computes `score_progress`, `time_progress`, and `timer_state` per NFR-5.
  - `GET /api/actions/{id}` for an Archived Action — returns the same shape with `status = "archived"` (single status) while inner computation continues (BR-009).
  - `GET /api/actions/{id}` for a foreign-tenant id → 404 (ERR-6 envelope).
  - `POST /api/actions/{id}/archive` — sets `archived = true`; response reflects Archived status; writes `action.archived` audit event.
  - `POST /api/actions/{id}/unarchive` — clears `archived`; response reflects date-computed status (may be Completed if latest Target Date passed while Archived); writes `action.unarchived` audit event.
  - `POST /api/actions/{id}/archive` while already Archived → 409 (idempotency envelope).
  - `POST /api/actions/{id}/archive` as Analyst/Viewer → 403 (ERR-3).
- **What's intentionally NOT covered end-to-end**: `OutcomeEvaluator`, `TargetStartDeriver`, `LatestTargetDateCalculator`, `ActionDetailProjection` — covered by unit tests.

**Scenario Test**: `scenario-test: ActionArchivalScenarioTests` — Archive→Unarchive round-trip: `POST /archive` → `GET /{id}` (verify Archived badge + measurement continuing) → `POST /unarchive` → `GET /{id}` (verify status recomputed from dates, possibly Completed if latest Target Date passed in between). Spans 4 endpoints, asserts final state: exactly one `action.archived` + one `action.unarchived` event in audit log.

**E2E Test Coverage** (frontend SPA `frontend/portal/`):

- **User flows under test**: `/actions/:id` (SCR-03 Action Details) → `ActionDetailsTests.cs` in `tests/Nabadat.E2ETests/ActionManagement/`.
- **Required scenarios**:
  - `ActionDetails_active_row_shows_reference_slider_with_LU_flags_and_own_timer` — AC-3.1.
  - `ActionDetails_completed_row_shows_C_flag_inside_outcome_zone_and_no_write_controls` — AC-3.2, BR-023.
  - `ActionDetails_deactivated_row_is_faded_and_shows_activate_delete_buttons` — AC-3.3.
  - `ActionDetails_archived_action_shows_single_archived_badge_and_unarchive_button` — AC-3.4.
  - `ActionDetails_target_start_date_equals_action_end_plus_one_and_shows_tooltip` — AC-3.5, BR-006.
  - `ActionDetails_force_deactivated_target_disables_activate_when_kpi_still_inactive` — AC-3.6, BR-011.
  - `ActionDetails_evaluated_target_on_active_action_renders_as_completed_row` — AC-3.7.
  - `ActionDetails_archive_button_refreshes_page_in_place_with_archived_badge` — AC-3.8.
  - `ActionDetails_deep_link_to_edit_for_completed_action_redirects_to_details_with_toast` — NTF-6, BR-023.
  - `ActionDetails_deep_link_to_missing_action_shows_action_not_found_state` — ERR-6.

---

### User Story 4 — Edit a Planned or Active Action (Priority: P2)

**Persona**: CX Program Manager.

The user reopens an existing Planned or Active (non-archived) Action to change its name, dates, description, thresholds, or Target set. Edit mode is the **same SCR-02 layout pre-filled**. Guarded edits protect the measurement model: changing **Action Start Date** on a started Action opens **DLG-2** (re-capture baselines); changing **Action End Date** on a started Action opens **DLG-4** (moves Target Start, recomputes all Time Progress); changing **thresholds mid-monitoring** opens **DLG-3** (recomputes progress / outcomes). Cancel in any dialog reverts the field. All confirmed edits are audit-logged field-level.

**Why this priority**: A high-value editing surface for corrections and re-planning. Not P1 because the module still delivers observable value with create+monitor+detail alone; but any real-world CX team needs to correct dates, extend end dates, adjust thresholds.

**Independent Test**: A Program Manager opens an Active Action via **Edit** on its card → the SCR-02 form loads pre-filled → change **Action Start Date** → DLG-2 fires → **Recalculate & continue** → baseline recaptured from M-06 history for the new date → Save → success toast NTF-2 → SCR-01 → the Action's Score/Time Progress everywhere reflects the new baseline; the audit log has entries for `field_edited (start_date)` and `baseline.recaptured`.

**Acceptance Scenarios**:

1. **AC-2.7** — **Given** an Active action, **When** its Start Date is changed and DLG-2 is confirmed, **Then** Baselines re-snapshot from M-06 history for the new date, Score Progress everywhere recomputes, and an audit event records old/new values. [SRS §7.12]
2. **AC-2.10** — **Given** a deactivated Target, **When** the form renders, **Then** its body is faded and inert, only **Activate** and **Delete** operate, and Save succeeds provided another active Target exists (VAL-207). [SRS §7.12]
3. **AC-2.11** — **Given** a Completed action, **When** any Edit path is attempted (card, details, deep link), **Then** no edit form opens; deep links redirect to SCR-03 with toast NTF-6 "Completed actions are read-only". [SRS §7.12, BR-023, §4.1]
4. **AC-2.12** — **Given** an Active action being edited, **When** the End Date is changed, **Then** DLG-4 appears; confirming moves Target Start to End + 1 and recomputes every Target's Time Progress and timer state. [SRS §7.12, BR-B2]
5. **Threshold edit mid-monitoring** — **Given** an Active action past its Target Start Date, **When** a Target's Upper or Lower Threshold is edited, **Then** DLG-3 appears; confirming recomputes Score Progress and prospective outcome for that Target. [SRS §7.9, §15.2]
6. **Archived edit redirect** — **Given** an Archived Action, **When** a user opens `/actions/:id/edit` directly, **Then** the URL redirects to `/actions/:id` (SCR-03) and toast NTF-6 reads "Unarchive this action to edit it". [SRS §4.1, §15.1]
7. **Concurrent edit** — **Given** two Program Managers editing the same Action simultaneously, **When** the second one Saves, **Then** the write completes (last-write-wins), the audit trail preserves both actors' events, and a stale-save warning is shown (ERR-8). [SRS §14]

**Unit Test Coverage**:

- **Units under test**:
  - `EditGuardResolver` — decides which of DLG-2 / DLG-3 / DLG-4 (or none) is required for a given field change on a given Action status.
  - `BaselineRecaptureService` — on Start Date change of a started Action, re-snapshots Baseline from M-06 history for the new date; raises `NoBaselineScoreException` (ERR-5) if M-06 has none for that date.
  - `EditPermissionResolver` — returns `Allow` only for Planned/Active + non-archived + Program Manager (BR-018, BR-023).
  - `AuditFieldDiff` — for each edited field, emits an `action.field_edited` event with `{ field, old_value, new_value }`.
- **Required cases**:
  - `Resolve(field=StartDate, actionStatus=Active, hasBaseline=true)` → `DLG-2`.
  - `Resolve(field=StartDate, actionStatus=Planned, hasBaseline=false)` → `None` (no baseline yet; SRS §7.9).
  - `Resolve(field=EndDate, actionStatus=Active)` → `DLG-4`.
  - `Resolve(field=UpperThreshold, actionStatus=Active, currentDate > targetStartDate)` → `DLG-3`.
  - `Resolve(field=Name, actionStatus=Active)` → `None`.
  - `EditPermission(status=Completed, role=ProgramManager)` → `Deny(reason="read_only")` (BR-023).
  - `EditPermission(status=Archived, role=ProgramManager)` → `Deny(reason="unarchive_first")`.
  - `EditPermission(status=Active, role=Analyst)` → `Deny(reason="role")` (SRS §13).
  - `Recapture(oldStartDate=2026-07-17, newStartDate=2026-07-10, M06.hasScore(2026-07-10)=true)` → new baseline stored.
  - `Recapture(newStartDate=2026-07-10, M06.hasScore(2026-07-10)=false)` → throws `NoBaselineScoreException` (ERR-5).
  - `Diff(oldAction={name:"X"}, newAction={name:"Y"})` → yields `field_edited(name, "X", "Y")` (one event, field-level).

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `PUT /api/actions/{id}` — happy path edit of an Active Action (name change only) → 200; writes `action.field_edited` audit event; no baseline recapture.
  - `PUT /api/actions/{id}` — change Start Date on a started Action → 200; server re-captures baselines; writes `field_edited(start_date)` + `baseline.recaptured` events; the client is expected to have confirmed DLG-2 first (server does not gate on the dialog — the dialog is UI-only; *proposed — confirm at architecture review*).
  - `PUT /api/actions/{id}` — change End Date on an Active Action → 200; response reflects new `target_start_date = new_end + 1 day`; audit event `field_edited(end_date)`.
  - `PUT /api/actions/{id}` — change Upper Threshold on an active Target mid-monitoring → 200; audit event `field_edited(upper_threshold)` on the target.
  - `PUT /api/actions/{id}` — edit attempt on Completed Action → 409 with `{ error: { code: "action.read_only", message: "Completed actions are read-only" } }` (ERR-11).
  - `PUT /api/actions/{id}` — edit attempt on Archived Action → 409 with `{ error: { code: "action.archived", message: "Unarchive this action to edit it" } }` (ERR-11).
  - `PUT /api/actions/{id}` — concurrent edit: two writes with stale `updated_at` timestamps → both succeed (last-write-wins per ERR-8) but the older client is signalled via `X-Nabadat-Stale-Save: true` response header + audit trail preserves both writes.
  - `PUT /api/actions/{id}` — Start Date change with no historical M-06 score for the new date → 409 with ERR-5 `kpi.no_historical_score`.
  - `PUT /api/actions/{id}` — Analyst role → 403 (ERR-3, SRS §13).
- **What's intentionally NOT covered end-to-end**: `EditGuardResolver`, `EditPermissionResolver`, `AuditFieldDiff` — covered by unit tests.

**Scenario Test**: `scenario-test: ActionEditGuardScenarioTests` — full guarded edit flow: `GET /{id}` (state before) → `PUT /{id}` with `start_date` change → verify baseline recapture in response → `GET /{id}` (state after — Score Progress recomputed) → `GET /audit-events?action_id={id}` (verify both `field_edited(start_date)` and `baseline.recaptured` events present, in order). Spans 4 endpoints, asserts final aggregate side-effect: audit-log invariant "every start-date change is paired with a baseline-recaptured event within the same request".

**E2E Test Coverage** (frontend SPA `frontend/portal/`):

- **User flows under test**: `/actions/:id/edit` (Edit Action) → `ActionAddEditTests.cs` (same file as Story 1; separate `[TestMethod]` block).
- **Required scenarios**:
  - `EditAction_prefills_all_fields_including_deactivated_targets_when_opening_active_action` — SRS §7.9.
  - `EditAction_start_date_change_shows_DLG2_and_recaptures_baseline_on_confirm` — AC-2.7.
  - `EditAction_end_date_change_shows_DLG4_and_moves_target_start` — AC-2.12.
  - `EditAction_threshold_change_mid_monitoring_shows_DLG3` — DLG-3.
  - `EditAction_cancel_in_dialog_reverts_the_field_change` — DLG behaviour, SRS §7.9.
  - `EditAction_deep_link_for_completed_action_redirects_to_details_with_read_only_toast` — AC-2.11, NTF-6.
  - `EditAction_deep_link_for_archived_action_redirects_to_details_with_unarchive_toast` — NTF-6.
  - `EditAction_ERR5_dialog_appears_when_M06_has_no_score_for_new_start_date` — ERR-5.

---

### User Story 5 — Automatic status transitions (Priority: P2)

**Persona**: any user (transitions are system-driven; users observe results).

Non-archived Actions transition **Planned → Active → Completed** automatically as the tenant-timezone day boundary crosses **Action Start Date** and the **latest Target Date**. No user step, no manual button. Once an Action becomes Completed it is **read-only** (BR-023) and its outcome per Target is fixed. Individual Targets on an Active Action can also be evaluated (their own Target Date passes) while later Targets keep the Action Active.

**Why this priority**: Correctness of the lifecycle is what makes the model meaningful, but the transitions themselves have no dedicated screen — they are observed on SCR-01/03. P2 because Users can still get value from a manual read of any snapshot without this automation, but without it, evaluated outcomes never appear.

**Independent Test**: Advance the tenant's server clock (or use `FakeTimeProvider` in integration tests) across each boundary — Start Date, individual Target Dates, latest Target Date — and assert that (a) SCR-01 groups the Action correctly on each render (FR-102/103/105), (b) SCR-03 renders Active-unevaluated → evaluated → all-evaluated row variants correctly (§8.4–§8.5), (c) an `action.status_transitioned` audit event fires on each transition, (d) outcome is computed and stored-in-derivation-form (BR-O6, never as a hardcoded label).

**Acceptance Scenarios**:

1. **FR-105** — **Given** an Active action whose latest Target Date passes, **When** the next render occurs (no user step, no page reload dependency beyond a normal request cycle), **Then** the Action appears in Completed with outcome chips per KPI, a full grey timer, and becomes read-only (BR-023). [SRS §6.10 AC-1.3, §6.3 FR-105]
2. **Evaluated Target on Active Action** — **Given** an Active Action with a Target whose Target Date passed yesterday, **When** SCR-03 renders, **Then** that Target row shows its outcome label + grey timer while other Targets keep live timers, and this row is never the "Lowest performing" one. [SRS §3.5, §8.4 (b), AC-3.7]
3. **Planned → Active** — **Given** a Planned Action whose Action Start Date is today (tenant timezone), **When** SCR-01 renders on or after Start Date at 00:00 tenant-local, **Then** the Action appears in the Active tab with Baseline captured, empty timer (BR-F1: Time Progress = 0 during execution phase), pace-coloured by lift. [SRS §3.8, §10.1, FR-102]
4. **Retro-dated born Completed** — **Given** a newly saved Action whose dates already place `latest_target_date < today`, **When** save completes, **Then** the Action is born Completed and any subsequent edit attempt is blocked (BR-023). [SRS §7.9 W-8, §10.1]
5. **Outcomes derived from data** — **Given** any Completed Action, **When** viewed, **Then** outcomes are computed from stored `{ baseline, L, U, final_score }` — not stored as labels (BR-O6). [SRS §3.5]

**Unit Test Coverage**:

- **Units under test**:
  - `ActionStatusCalculator` (also unit-tested in Story 1; extended here with boundary edge cases).
  - `PerTargetEvaluationCalculator` — for each Target with `target_date < now`, computes and stores `final_score` from M-06's live score AS-OF `target_date` (day-granular, tenant timezone), then evaluates outcome per BR-O1/2/3.
  - `TimezoneDayBoundary` — converts a UTC instant to a day boundary in the tenant's configured timezone (BR-022, NFR-8).
- **Required cases**:
  - `Status(startDate=today, currentTZ="Asia/Riyadh", now="2026-07-22 23:59:59+03:00")` → `Active` (same tenant day).
  - `Status(latestTargetDate=today, now="2026-07-22 23:59:59+03:00")` → `Active` (day granular — Completed not until day passes, BR-D3).
  - `Status(latestTargetDate=today, now="2026-07-23 00:00:00+03:00")` → `Completed`.
  - `Evaluate(target={ target_date=2026-07-21 }, m06.scoreAsOf(2026-07-21)=77, baseline=70, L=3, U=6)` → outcome `Successful` (BR-O1).
  - `DayBoundary(instant="2026-07-22T20:00:00Z", tz="Asia/Riyadh")` → `2026-07-22` (still Wed local; day rolls at 00:00 tz).
  - `DayBoundary(instant="2026-07-22T21:30:00Z", tz="Asia/Riyadh")` → `2026-07-23` (crossed local midnight).

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `GET /api/actions?tab=active` — with tenant TimeProvider advanced past latest Target Date → Action is now in `tab=completed` response (FR-105).
  - `GET /api/actions/{id}` — with server clock advanced past a single Target's Target Date on an otherwise-Active Action → response's `targets[i].outcome` field is populated for that Target; the Action's overall `status` remains "active" (§8.4 (b)).
  - `POST /api/actions` — retro-dated so it computes Completed → response returns `status = "completed"` on creation; subsequent `PUT` → 409 `action.read_only` (BR-023, ERR-11).
- **What's intentionally NOT covered end-to-end**: `PerTargetEvaluationCalculator`, `TimezoneDayBoundary` — covered by unit tests.

**Scenario Test**: `scenario-test: ActionLifecycleScenarioTests` — full lifecycle: `POST /actions` (born Planned) → advance `FakeTimeProvider` past Start Date → `GET /{id}` (now Active, Baseline captured) → advance past first Target Date → `GET /{id}` (Target 1 evaluated, Action still Active) → advance past latest Target Date → `GET /{id}` (Action Completed, all Targets evaluated) → `PUT /{id}` (rejected with `action.read_only`, ERR-11). Spans 5+ endpoints, carries Action id, asserts final aggregate: exactly one `action.status_transitioned(Planned→Active)`, one `action.status_transitioned(Active→Completed)`, and one `outcome.evaluated` event per Target — all in order — with the `PUT` refused as the final assertion.

**E2E Test Coverage**: `e2e-tests: skipped — lifecycle transitions are time-based and best verified through the integration harness's FakeTimeProvider (backend scenario tests above). E2E tests would require tenant-timezone clock manipulation from the browser, which is not exposed by the SPA. The visible surfaces of the transitions (SCR-01 tab counts change, SCR-03 badge changes) are covered by Story 2 (AC-1.3) and Story 3 (AC-3.7).`

---

### User Story 6 — Archive and Unarchive an Action (Priority: P2)

**Persona**: CX Program Manager.

The user Archives a non-archived Action (from any card kebab or the SCR-03 header). The Action enters the **standalone Archived status** — exclusive of Planned/Active/Completed, but **measurement keeps computing normally**: timers, evaluations, and the Planned→Active→Completed transition all continue underneath. The Archive tab shows the card with an Archived pill; the kebab exposes only **Unarchive**. Unarchive returns the Action to its date-computed status tab, editable again if Planned/Active. **Archive requires no confirmation dialog** (non-destructive per BR-009); both operations are audit events.

**Why this priority**: Archive is the module's *soft-delete* substitute (no permanent delete exists per BR-021) and is essential for tenant hygiene. P2 because MVP is usable without it (Actions accumulate but do not break).

**Independent Test**: On SCR-01 Active tab, click an Action's kebab → **Archive**. The card disappears from Active, appears in Archived (single Archived badge, dashed border). Toast NTF-4 reads "Action archived — it keeps running and is available in the Archived tab". Timers on the archived card continue to reflect live Score/Time Progress. Kebab → **Unarchive** returns the card to its date-computed tab; toast NTF-5. An audit event exists for each.

**Acceptance Scenarios**:

1. **AC-1.5** — **Given** an Active action, **When** Archive is clicked, **Then** it appears only in the Archived tab with the single status "Archived", its timer continues to reflect live Score/Time Progress, and an audit event exists. [SRS §6.10]
2. **AC-1.6** — **Given** an archived action whose dates compute Planned, **When** Unarchive is clicked, **Then** it reappears in Planned (dates unchanged) and its primary button is **Edit**. [SRS §6.10]
3. **Archive from SCR-03** — **Given** a non-archived action on SCR-03, **When** Archive is clicked, **Then** the page refreshes in place with the "Archived" badge alone (single status), Edit disappears, Unarchive appears, and SCR-01 counts reflect the move (AC-3.8). [SRS §8.9]
4. **Unarchive lands in Completed if latest Target Date passed while Archived** — **Given** an Archived Action whose latest Target Date passed while it was Archived, **When** Unarchive is clicked, **Then** the Action lands directly in Completed (read-only) per FR-102 + BR-023. [SRS §10.3]
5. **No confirmation on Archive** — Archive fires immediately with no DLG (BR-009); only Delete (Target) and DLG-2/3/4 (mid-lifecycle edits) show confirmations. [SRS §15.2]

**Unit Test Coverage**:

- **Units under test**:
  - `ArchiveStateMachine` — accepts `Archive` from Planned/Active/Completed → Archived; accepts `Unarchive` from Archived → date-computed status; rejects any other transition.
- **Required cases**:
  - `Transition(Planned, Archive)` → `Archived`.
  - `Transition(Active, Archive)` → `Archived`.
  - `Transition(Completed, Archive)` → `Archived`.
  - `Transition(Archived, Archive)` → `throws InvalidTransitionException` (idempotent write refused).
  - `Transition(Archived, Unarchive, dates.compute=Planned)` → `Planned`.
  - `Transition(Archived, Unarchive, dates.compute=Completed)` → `Completed` (read-only per BR-023).
  - `Transition(Active, Unarchive)` → `throws InvalidTransitionException` (unarchive only from Archived).

**Integration Test Coverage**:

- **What gets tested end-to-end**: covered under Story 3's `POST /api/actions/{id}/archive` and `POST /api/actions/{id}/unarchive` cases — see Story 3 for the endpoint contracts. (No duplicate coverage here.)
- **What's intentionally NOT covered end-to-end**: `ArchiveStateMachine` — covered by unit tests.

**Scenario Test**: covered by Story 3's `ActionArchivalScenarioTests`.

**E2E Test Coverage** (frontend SPA `frontend/portal/`):

- **User flows under test**: kebab-driven Archive/Unarchive on SCR-01; header buttons on SCR-03 → covered by `AllActionsTests.cs` (Story 2) and `ActionDetailsTests.cs` (Story 3). Adds these specific scenarios:
  - `AllActions_kebab_archive_moves_card_to_archived_tab_and_fires_NTF4` — AC-1.5.
  - `AllActions_kebab_unarchive_returns_card_to_planned_tab_and_fires_NTF5` — AC-1.6.
  - `ActionDetails_archive_button_shows_no_dialog_and_updates_badge_in_place` — SRS §15.2 (no DLG for Archive), AC-3.8.

---

### User Story 7 — Manage KPI Target lifecycle (Priority: P3)

**Persona**: CX Program Manager.

Within a Planned or Active (non-archived) Action, the user manages individual KPI Targets: **activate**, **deactivate** (manual), **delete** (only when deactivated; behind DLG-1), or observe **force-deactivation** (when M-06 deactivates the underlying KPI — BR-011). Deactivated Targets render faded read-only, are excluded from results / outcome / lowest-performing selection, but are still saved with the Action. Only KPI reactivation in M-06 re-enables **Activate** on a force-deactivated Target.

**Why this priority**: Target-lifecycle is a refinement path used less frequently than the create/monitor loop, but essential for correcting configuration errors and reacting to KPI churn.

**Independent Test**: Open an Active Action's Edit form (or SCR-03) → toggle a Target **Off**. The subsection fades, Delete becomes visible. Toggle back **On** → subsection restored, Delete hidden. Click Delete on a deactivated Target → DLG-1 → confirm → Target removed, remaining Targets renumbered, KPI freed in other selects, toast NTF-3. Deactivate a KPI in M-06 → its Target across all Actions is force-deactivated (audit-logged); the row's **Activate** is disabled with tooltip until M-06 reactivates the KPI.

**Acceptance Scenarios**:

1. **AC-2.10** — **Given** a deactivated Target, **When** the form renders, **Then** its body is faded and inert, only Activate and Delete operate, and Save succeeds provided another active Target exists (VAL-207). [SRS §7.12]
2. **AC-3.3** — **Given** a deactivated Target on an Active action, **When** SCR-03 renders, **Then** the row is faded with slider and dates visible, marked "Deactivated", shows Activate + Delete, and displays "Excluded from results". [SRS §8.9]
3. **AC-3.6** — **Given** a Target force-deactivated because its KPI was deactivated in M-06, **When** its KPI is still inactive, **Then** the Activate button is disabled with tooltip "KPI is inactive in M-06"; once the KPI is Active again, Activate is enabled. [SRS §8.9, BR-011]
4. **Delete confirmation** — **Given** a deactivated Target and **When** Delete is clicked, **Then** DLG-1 appears with title "Delete this KPI target?" / body "The target and its configuration will be removed from this action. This cannot be undone." / buttons Cancel (ghost) + Delete (destructive). Confirming removes the Target; remaining Targets renumber; the KPI returns to other selects; toast NTF-3 "Target removed". [SRS §15.2, §7.7]
5. **No target-lifecycle controls on Completed/Archived** — **Given** a Completed or Archived Action's SCR-03, **When** it renders, **Then** deactivated rows still render faded but Activate + Delete controls are absent (§8.7 last paragraph, BR-023, BR-009). [SRS §8.7]

**Unit Test Coverage**:

- **Units under test**:
  - `TargetLifecycleStateMachine` — states: Active, Deactivated(manual), Deactivated(forced); transitions: Deactivate(manual)/Reactivate/Delete on manual side; ForceDeactivate/AutoReactivate-permission on forced side; forbids delete on Active Target and reactivate on forced Target while KPI is inactive.
  - `KpiForceDeactivationCascade` — on M-06 KPI deactivation event, sets `deactivation_source = 'forced'` and `active = false` on every Target referencing that KPI across all Actions; emits a `target.deactivated` audit event (source attribute `'forced'`, per INT-04 naming) per Target.
- **Required cases**:
  - `Transition(Active, Delete)` → `throws InvalidTransitionException` (must be Deactivated first, BR-012).
  - `Transition(DeactivatedManual, Reactivate)` → `Active`.
  - `Transition(DeactivatedForced, Reactivate, kpiIsActive=false)` → `throws PermissionException("kpi_inactive")` (BR-011).
  - `Transition(DeactivatedForced, Reactivate, kpiIsActive=true)` → `Active`.
  - `Transition(DeactivatedManual, Delete)` → `Deleted`.
  - `Cascade(m06Event.deactivate(kpiId=NPS), targets=[t1{NPS}, t2{NPS}, t3{CSAT}])` → t1 & t2 become forced-deactivated; t3 unchanged; two `target.deactivated (source='forced')` events emitted.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `PATCH /api/actions/{id}/targets/{targetId}` `{ active: false }` — deactivate a Target → 200; row `active=false`, `deactivation_source='manual'`; audit event `target.deactivated`.
  - `PATCH /api/actions/{id}/targets/{targetId}` `{ active: true }` on a manually-deactivated Target → 200; row `active=true`; audit event `target.activated` (INT-04 naming).
  - `PATCH /api/actions/{id}/targets/{targetId}` `{ active: true }` on a forced-deactivated Target while KPI is inactive → 409 `{ error: { code: "target.kpi_inactive", message: "Cannot reactivate — KPI is inactive in M-06" } }`.
  - `DELETE /api/actions/{id}/targets/{targetId}` on a deactivated Target → 200; row removed; other Targets renumbered client-side (server just returns updated list); audit event `target.deleted`.
  - `DELETE /api/actions/{id}/targets/{targetId}` on an active Target → 409 `{ error: { code: "target.must_be_deactivated" } }` (BR-012).
  - `DELETE /api/actions/{id}/targets/{targetId}` when it is the action's **last remaining Target** (any state) → 409 `{ error: { code: "action.requires_target", message: "An action must keep at least one KPI target" } }` *(R-17 — stakeholder-ratified, 22 Jul 2026)*.
  - `POST /api/internal/kpi-deactivation-events` (M-06 → M-15 webhook or shared event bus consumer) with `{ kpi_id: NPS }` → all NPS Targets across all Actions become `deactivation_source='forced'`, `active=false`; N `target.deactivated (source='forced')` audit events emitted (one per affected Target).
- **What's intentionally NOT covered end-to-end**: `TargetLifecycleStateMachine` transitions — covered by unit tests.

**Scenario Test**: `scenario-test: KpiForceDeactivationScenarioTests` — full cascade: seed two Active Actions each targeting NPS + CSAT → M-06 deactivates NPS → verify each Action's NPS Target now `active=false, deactivation_source='forced'` → attempt reactivate → refused with `target.kpi_inactive` → M-06 reactivates NPS → attempt reactivate → succeeds → verify audit log has 2 `target.deactivated (source='forced')` + 2 `target.activated` events (INT-04 naming). Spans 4+ endpoints, asserts final aggregate: no unintended KPI (CSAT) was touched.

**E2E Test Coverage** (frontend SPA `frontend/portal/`):

- **User flows under test**: SCR-02 Target subsection controls; SCR-03 deactivated-row controls → additional `[TestMethod]` blocks in `ActionAddEditTests.cs` and `ActionDetailsTests.cs`.
- **Required scenarios**:
  - `AddEdit_target_toggle_off_fades_body_and_shows_delete_button` — SRS §7.7.
  - `AddEdit_delete_deactivated_target_shows_DLG1_and_removes_row_on_confirm` — SRS §15.2, NTF-3.
  - `AddEdit_delete_deactivated_target_cancel_in_DLG1_leaves_row_unchanged` — DLG-1 cancel.
  - `AddEdit_saves_when_at_least_one_target_active_after_deactivating_others` — AC-2.10.
  - `ActionDetails_deactivated_row_shows_activate_button_enabled_for_manual_deactivation` — AC-3.3.
  - `ActionDetails_deactivated_row_activate_button_disabled_with_tooltip_when_forced_and_kpi_inactive` — AC-3.6.
  - `ActionDetails_deactivated_row_hides_activate_delete_controls_on_completed_action` — §8.7 last paragraph, BR-023.

---

### User Story 8 — Search and filter across all tabs (Priority: P3)

**Persona**: any user with view access.

From SCR-01 the user filters the Action list with (a) case-insensitive substring search on Action Name — **applied across all four tabs simultaneously** (including Archived) with a cross-tab match hint, (b) a **KPI multi-select** filter, and (c) a **Date range** (from–to) filter over the Action Start Date. Filters AND-combine with search across all four tabs. **There is no Status filter and no Created-by filter** (both removed by stakeholder decision, BR-021).

**Why this priority**: Filters are quality-of-life over a fully-usable base module; large tenants benefit but small tenants do not require them for MVP.

**Independent Test**: Seed 20+ Actions across tabs. Type "training" → cards filter in all four tabs, hint shows "N matches across all tabs — switch tabs to see them all"; per-tab counts remain unfiltered totals. Select KPI = NPS + CSAT (multi) → only Actions targeting NPS OR CSAT remain. Set Start Date range 2026-07-01 → 2026-07-31 → only Actions whose Action Start Date falls in that window remain. Clear query → all restored.

**Acceptance Scenarios**:

1. **AC-1.4** — **Given** the query "training", **When** typed in search, **Then** only name-matching cards remain visible in every tab — including Archived — and the hint states the cross-tab match count. [SRS §6.10]
2. **KPI multi-select filter** — **Given** KPI filter = [NPS, CSAT], **When** applied, **Then** only Actions with at least one Target on NPS or CSAT remain across all four tabs (FR-107). [SRS §6.3]
3. **Date range** — **Given** Start Date range "from" = empty and "to" = empty, **When** the form loads, **Then** the "to" field is present (the prototype's "from"-only was a defect — corrected per FR-107); both from and to are date pickers; both are optional but if either is set, filtering applies. [SRS §6.3, FR-107]
4. **No Status filter, no Created-by filter** — **Given** the toolbar renders, **When** users inspect it, **Then** neither a Status dropdown nor a Created-by dropdown is present (BR-021). [SRS §6.3]

**Unit Test Coverage**:

- **Units under test**:
  - `ActionSearchFilter` (also under Story 2 — extended here with filter combinators).
  - `ActionKpiFilter` — includes Action iff `any(target.kpi_id ∈ filter_set)` (BR-016).
  - `ActionDateRangeFilter` — includes Action iff `filter.from ≤ action.action_start_date ≤ filter.to` (either bound optional).
  - `FilterCombinator` — AND-combines search + kpi + date-range across all four tabs (BR-016).
- **Required cases**:
  - `KpiFilter(action.targets=[NPS], filter=[NPS,CSAT])` → `true`.
  - `KpiFilter(action.targets=[FCR], filter=[NPS,CSAT])` → `false`.
  - `KpiFilter(action.targets=[NPS,FCR], filter=[NPS,CSAT])` → `true` (any-match).
  - `DateRange(actionStart=2026-07-15, from=2026-07-01, to=2026-07-31)` → `true`.
  - `DateRange(actionStart=2026-08-01, from=2026-07-01, to=2026-07-31)` → `false`.
  - `DateRange(actionStart=2026-07-15, from=null, to=null)` → `true` (both bounds optional; no filter applied when neither set).
  - `Combine(search="training", kpi=[NPS], dateFrom=null, dateTo=null, actions=[…])` → intersection of all matchers, applied across all four tabs.

**Integration Test Coverage**: covered by Story 2's `GET /api/actions?q=…&kpi=…&start_from=…&start_to=…` — no duplicate coverage.

**Scenario Test**: `scenario-test: not-needed — filter parameters travel on a single GET; no cross-endpoint state.`

**E2E Test Coverage** (frontend SPA `frontend/portal/`):

- **User flows under test**: SCR-01 toolbar → additional `[TestMethod]` blocks in `AllActionsTests.cs`.
- **Required scenarios**:
  - `AllActions_search_hint_line_updates_with_cross_tab_match_count` — FR-106.
  - `AllActions_kpi_multi_select_filter_ANDs_with_search_across_all_tabs` — FR-107, BR-016.
  - `AllActions_date_range_from_and_to_are_both_present_and_optional` — FR-107 (corrects HTML "from-only" defect).
  - `AllActions_toolbar_contains_no_status_dropdown_and_no_created_by_dropdown` — BR-021.

---

### User Story 9 — Configure tenant Settings → Actions (Priority: P3)

**Persona**: CX Program Manager (per interim §13; refined by M-10 later).

Under the platform Settings screen, the user opens the "**Actions**" subsection and adjusts two tenant-wide parameters that shape every Threshold Slider and every Stepped Zone Slider in M-15:
- **SET-1 — Action Target Maximum Upper Threshold (X)**: number, 1 dp, default 20; range `> 0`; **cannot be lowered below the largest U saved in the tenant** (guard).
- **SET-2 — Slider Padding (PAD)**: positive integer, default 3; range `≥ 1`; extends every stepped zone slider's track per SRS §3.7.

Changes apply tenant-wide on next render and are audit-logged.

**Why this priority**: Settings are one-off configuration ceremonies; MVP works entirely at defaults.

**Independent Test**: Navigate to Settings → Actions → change X from 20 to 30 → Save. Every open SCR-02 form's Threshold Slider scale-note now reads "Scale 0–30". Attempt to set X = 5 when the largest saved U in the tenant is 8 → Save is blocked with "Cannot set the maximum below an existing Upper Threshold (8)". Change PAD from 3 to 5 → all Stepped Zone Sliders extend their tracks by 5 points on either side.

**Acceptance Scenarios**:

1. **SET-1 default and range** — **Given** a new tenant, **When** Settings → Actions loads, **Then** X = 20, PAD = 3 (defaults). [SRS §11]
2. **SET-1 guard** — **Given** the tenant has an Action whose largest saved U = 8, **When** X is set to 5 and Save is clicked, **Then** Save is blocked with "Cannot set the maximum below an existing Upper Threshold (8)" and no update occurs. [SRS §11]
3. **SET-1 audit** — **Given** X is changed from 20 to 30, **When** Save succeeds, **Then** an audit event `settings.X_changed` records `{ old: 20, new: 30 }`. [SRS §12.4]
4. **SET-2 default and validation** — **Given** PAD is set to 0 or a non-integer, **When** Save is clicked, **Then** Save is blocked with the range violation ("PAD must be a positive integer ≥ 1"). [SRS §11]
5. **Permissions** — **Given** an Analyst opens Settings → Actions, **When** the page renders, **Then** the fields are hidden or read-only per §13 (Program Manager only until M-10 refines).

**Unit Test Coverage**:

- **Units under test**:
  - `SettingsUpdateValidator` — enforces SET-1 range + guard (`X > 0`, `X ≥ max(U in tenant)`) and SET-2 range (`PAD ≥ 1`, integer).
  - `LargestSavedUpperCalculator` — computes `max(target.upper_threshold)` across all Actions of the tenant (including Archived) for the SET-1 guard.
- **Required cases**:
  - `Validate({ X: 20, PAD: 3 }, largestU=8)` → `Valid`.
  - `Validate({ X: 5, PAD: 3 }, largestU=8)` → `Invalid("Cannot set the maximum below an existing Upper Threshold (8)")`.
  - `Validate({ X: 20, PAD: 0 }, largestU=8)` → `Invalid("PAD must be a positive integer")`.
  - `Validate({ X: 20, PAD: 2.5 }, largestU=8)` → `Invalid("PAD must be a positive integer")` (non-integer).
  - `LargestU(actions=[])` → `0` (empty tenant; X can go to any positive value).
  - `LargestU(actions=[{targets=[{U=6},{U=8}]},{targets=[{U=3}]}])` → `8`.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `GET /api/settings/actions` — returns `{ max_upper_threshold: 20, slider_padding: 3 }` for a fresh tenant (defaults).
  - `PUT /api/settings/actions` `{ max_upper_threshold: 30 }` → 200; audit event `settings.X_changed(20, 30)`.
  - `PUT /api/settings/actions` `{ max_upper_threshold: 5 }` when largest saved U = 8 → 400 `{ error: { code: "settings.x_below_saved_upper", message: "Cannot set the maximum below an existing Upper Threshold (8)" } }`.
  - `PUT /api/settings/actions` `{ slider_padding: 0 }` → 400 `settings.pad_out_of_range`.
  - `PUT /api/settings/actions` as an Analyst → 403 (SRS §13).
- **What's intentionally NOT covered end-to-end**: `SettingsUpdateValidator`, `LargestSavedUpperCalculator` — covered by unit tests.

**Scenario Test**: `scenario-test: not-needed — settings updates are single-endpoint operations with no cross-endpoint state.`

**E2E Test Coverage** (frontend SPA `frontend/portal/`):

- **User flows under test**: `/settings/actions` (the new Actions subsection appended to platform Settings) → `ActionsSettingsTests.cs` in `tests/Nabadat.E2ETests/ActionManagement/`.
- **Required scenarios**:
  - `ActionsSettings_shows_default_values_20_and_3_on_fresh_tenant` — SET-1/SET-2 defaults.
  - `ActionsSettings_save_updates_scale_note_across_open_add_action_forms` — SET-1 apply behaviour.
  - `ActionsSettings_blocks_save_when_X_lower_than_largest_saved_upper` — SET-1 guard.
  - `ActionsSettings_blocks_save_when_PAD_not_positive_integer` — SET-2 validation.
  - `ActionsSettings_analyst_role_sees_fields_read_only_or_hidden` — SRS §13.

---

### User Story 10 — Retro-date an Action for retrospective documentation (Priority: P3)

**Persona**: CX Program Manager.

The user documents an Action that was performed in the past — either during creation (all dates in the past) or via edit (moving Start Date backward). The system captures the Baseline from M-06's **historical** score for that date; if M-06 has no score for the requested date, a blocking ERR-5 dialog appears with the exact copy "No KPI score exists for {date}. Choose a different Action Start Date." No silent fallback.

**Why this priority**: Retro-dating unlocks documenting historical work but is a lower-frequency path than creating live Actions.

**Independent Test**: Create an Action with Start Date = 6 months ago, End Date = 5 months ago, Target Date = today − 1 day. On save the Action is born Completed (BR-023 → immediately read-only), Baselines pulled from M-06 history for the Start Date, outcomes evaluated on the Target Date. Attempt the same with a Start Date for which M-06 has no historical score → ERR-5 blocks save with the exact error dialog.

**Acceptance Scenarios**:

1. **BR-D1 (Retro-dating allowed)** — **Given** an Add-Action form with Start Date = last month, **When** Save is clicked, **Then** the Action is born into its date-computed status with Baselines from M-06 history for last month. [SRS §3.2]
2. **W-7 Born Completed** — **Given** an Add-Action form whose dates already place `latest_target_date < today`, **When** Save is clicked, **Then** the Action is born Completed and immediately read-only (BR-023). [SRS §7.11]
3. **ERR-5 blocking dialog** — **Given** an Add-Action form with Start Date for which M-06 has no historical score, **When** Save is clicked, **Then** a blocking dialog reads "No KPI score exists for {date}. Choose a different Action Start Date." — no partial save, no silent fallback. [SRS §14]

**Unit Test Coverage**: covered by `BaselineCaptureService` in Story 1's Unit Tests (retro-dated case + no-historical-score case).

**Integration Test Coverage**: covered by Story 1's `POST /api/actions` cases (retro-dated happy + retro-dated missing-history 409 with `kpi.no_historical_score`).

**Scenario Test**: `scenario-test: not-needed — the retro-dated path is a single-endpoint POST with all state carried in the request.`

**E2E Test Coverage** (frontend SPA `frontend/portal/`):

- **User flows under test**: `/actions/new` retro-dated flow — additional `[TestMethod]` blocks in `ActionAddEditTests.cs`.
- **Required scenarios**:
  - `AddAction_retro_dated_born_completed_becomes_read_only_after_save` — W-7, BR-023.
  - `AddAction_retro_dated_pulls_historical_baseline_from_M06_and_labels_it_baseline` — SRS §7.6.
  - `AddAction_retro_dated_shows_ERR5_blocking_dialog_when_no_historical_score` — ERR-5.

---

### Edge Cases

Comprehensive edge cases derived from the SRS. Each is testable and traceable.

**Measurement model & timers**:
- **Regression edge case** — a KPI regressed below Baseline (raw Score Progress negative). The Lowest-Performing Target selector MUST rank this Target as the worst; the timer MUST be Red; display is clamped to 0 % (BR-F2, §3.6).
- **Early overshoot** — a KPI is already above the Upper Threshold Point (raw Score Progress > 1). The ring MUST show a full fill; the colour MUST be Green; display is clamped to 100 % (BR-F2).
- **On-pace equality** — raw Score Progress equals raw Time Progress (within ±0.005). The timer MUST be Yellow — deterministic "on pace" band (BR-015).
- **Zero-eligible-targets Active card** — all Targets deactivated or all already evaluated while the Action is still Active. Card shows "No active targets to feature" with no slider and a timer computed against the latest remaining Target Date (grey full ring if none remains) (FR-111).
- **Baseline capture during execution phase** — while `Action Start ≤ Current ≤ Action End`, Time Progress raw is **0** by definition (BR-F1), regardless of the calendar difference between Start and End. Any positive lift → Green, no movement → Yellow, regression → Red.
- **U = L equality** — a valid saved state (BR-O4). Partial band collapses; outcome is binary (Successful iff Final ≥ Baseline + U, else Unsuccessful).
- **Adaptive slider padding when Current is below Baseline** — Track minimum = min(Baseline, Current) − PAD; Track maximum = max(Baseline + U, Current) + PAD (§3.7). C flag never pins to the track edge.

**Status lifecycle**:
- **Day-boundary comparisons** — every phase transition (Planned→Active at Start Date, Active→Completed at latest Target Date, per-Target evaluation on Target Date) uses **day granularity in the tenant's configured timezone** (BR-022). A UTC-only server MUST convert.
- **Retro-dated born Completed** — a newly saved Action whose dates already place `latest_target_date < today` is Completed on save and immediately read-only (BR-023, W-7). Any subsequent write attempt → ERR-11.
- **Unarchive lands directly in Completed** — Archived while the latest Target Date passed. On Unarchive, status recomputes to Completed (read-only) (§10.3).
- **Archived measurement continuity** — Archived Actions **keep computing normally**: timers keep running, per-Target evaluations still fire, transitions still fire underneath (BR-009). Archived is presentation-only.

**Validation & data**:
- **Duplicate name across statuses (incl. Archived)** — case-insensitive uniqueness applies to every action including Archived (VAL-202, R-8).
- **Delete availability** — Delete is shown on **every deactivated Target** (FR-207/FR-308); BR-012 already forbids deleting active Targets, so no extra visibility restriction exists. **Deleting the action's last remaining Target (any state) is refused** with "An action must keep at least one KPI target" — prevents an orphan action with an undefined `latest_target_date` *(R-17 — stakeholder-ratified, 22 Jul 2026)*.
- **All tenant KPIs used** — **Add KPI Target** button is disabled (AC-2.9); one Target per KPI per Action (BR-001).
- **Target Date == Action End Date** — refused (VAL-206 blocks with "Target Date must be after the Action End Date").
- **U typed above X** — clamped to X by the numeric input (VAL-209).
- **Description overflow** — hard cap at 500 chars with live counter (VAL-205); input-limited, no submit error.

**Cross-module & failure modes**:
- **M-06 unreachable on SCR-02** — KPI select disabled with helper "KPI list unavailable — try again"; score label hidden; Save blocked for new Targets (ERR-4).
- **M-06 score missing for a date** — blocking ERR-5 dialog on Save; no silent fallback.
- **M-06 KPI deactivated externally** — cascades to force-deactivate every Target referencing that KPI across all Actions (BR-011). Row's Activate button disabled with tooltip until KPI is Active again.
- **Deep link to missing/foreign-tenant action** — "Action not found" empty state + Back to Actions (ERR-6).
- **Deep link to `/actions/:id/edit` for Completed/Archived** — redirects to SCR-03 with toast NTF-6 (SRS §4.1, BR-023, ERR-11).
- **Concurrent edits** — last-write-wins + full audit trail + stale-save warning (ERR-8). No locking.
- **Network failure on Save** — toast "Connection lost — your changes were not saved"; form state preserved (ERR-9).
- **Live-score refresh failure on cards** — render last known values with stale-data tooltip; never block the page (ERR-10).
- **Archive/unarchive write failure** — toast "Couldn't update the action — try again"; status unchanged (ERR-7).

**Permissions**:
- **Analyst/Viewer bypass attempt** — the write endpoints must return 403 (ERR-3) even if the client sends the request; write controls are **hidden** (not merely disabled) client-side per §13.

**RTL / bilingual**:
- **Arabic names & descriptions** — accepted; VAL-202 uniqueness is Unicode case-insensitive (Arabic has no case, so effectively canonical-form comparison).
- **Threshold slider RTL** — Lower (nearer 0) and Upper (nearer X) MUST remain semantically identical in RTL; visual flag positions flip with the writing direction (design-system RTL rule).

---

## Requirements *(mandatory)*

> Every functional requirement below is traceable to a specific SRS clause. IDs mirror the SRS where possible (**FR-XXX**, **VAL-XXX**, **BR-XXX**, **NFR-X**, **ERR-X**, **DLG-X**, **NTF-X**, **SET-X**).

### Functional Requirements — SCR-01 All Actions

- **FR-101** — The page MUST present exactly four tabs — **Active, Planned, Completed, Archived** — each showing only the actions belonging to it (grouping per FR-102/103). Switching tabs swaps the visible grid; no cross-tab bleed-through. [SRS §6.3]
- **FR-102** — For non-archived actions, tab/status grouping MUST be **computed from dates**, never stored: Planned = `Action Start Date > Current Date`; Completed = `latest Target Date < Current Date`; otherwise Active (day granularity, tenant timezone — BR-022). [SRS §6.3]
- **FR-103** — An Action with `archived = true` MUST appear **only** in the Archived tab (standalone status, exclusive of the others). Measurement computation MUST continue while Archived (BR-009). [SRS §6.3]
- **FR-104** — Each tab label MUST show a count pill of Actions currently in that tab. Counts MUST update immediately after archive/unarchive and after status transitions. [SRS §6.3]
- **FR-105** — An Active Action MUST move to Completed automatically the moment its **latest** Target Date passes (no user step, no page reload dependency beyond next render), becoming read-only (BR-023). [SRS §6.3]
- **FR-106** — **Search** MUST filter cards by case-insensitive substring match on Action Name, applied **across all four tabs simultaneously** (including Archived). While a query is active, a hint line MUST show "**{n} match{es} across all tabs — switch tabs to see them all**"; per-tab counts remain the unfiltered totals. Clearing the query MUST restore all cards. [SRS §6.3, BR-016]
- **FR-107** — **Filters** MUST apply across all four tabs, combined with search by AND: **KPI** (multi-select of the tenant's KPIs — the prototype's single-select was a simplification; multi-select is the requirement) and **Date range** (from–to date pickers over `action_start_date`; both bounds present, both optional). There MUST be **no Status filter** and **no Created-by filter**. [SRS §6.3, BR-021]
- **FR-108** — **Empty state** per tab MUST render a full-width card reading "No {active/planned/completed/archived} actions." For the three status tabs (not Archived), the copy MUST append "Create one with **Add Action**." (design-system guided empty state). [SRS §6.3]
- **FR-109** — **Loading state**: the card grid MUST render skeleton cards until data resolves. [SRS §6.3]
- **FR-110** — The default landing tab MUST be **Active**; card ordering within a tab MUST be **newest-created-first**; lists beyond one viewport MUST paginate / infinite-scroll. [SRS §6.3]
- **FR-111** — **Zero-eligible-targets fallback (Active card)**: if an Active Action has no eligible Target (§3.6 — all deactivated and/or all already evaluated), its card MUST show the name, the KPI mini labels, the text "**No active targets to feature**" in place of the featured KPI row + slider, and a timer computed against the **latest remaining Target Date** (grey full ring if none remains). Footer and kebab unchanged. [SRS §6.3]

### Functional Requirements — SCR-01 Card Specification *(rev 1.1 — makes SRS §6.2/§6.4–§6.7 normative)*

- **FR-112** — **Page header (exact shipped copy)**: H1 "Actions"; subtitle "Improvement initiatives measured against KPI targets. Each card tracks its lowest-performing target — the one that needs attention first."; **Add Action** primary button (plus icon) at the header's inline-end (one primary CTA; visible to Program Manager only per PERM-01). [SRS §6.2]
- **FR-113** — **Active card composition (top → bottom)**: (1) Action Name (H3 ≈15.5 px semibold) · (2) Timer Ring fed by the Lowest-Performing Target — fill = its display Time Progress, colour = its pace state (FR-111 fallback when none) · (3) kebab · (4) featured row = KPI chip of the Lowest-Performing Target + dashed label "Lowest performing target" · (5) Stepped Zone Slider (card variant, FR-501) for that same Target · (6) KPI mini labels row "Targets:" + one mini chip per KPI, deactivated ones struck through (FR-504) · (7) meta line (exact template): "Target date {d MMM yyyy}" · "Score {s}% · Time {t}% — {ahead of pace / on pace / behind pace}" (display-clamped; pace per FR-M10) · (8) footer = **More Details** primary → SCR-03. [SRS §6.4]
- **FR-114** — **Planned card differences**: featured Target = KPI with the lowest current score on M-06's normalised index, labelled "**Lowest current score**"; slider without Baseline flag (zones provisionally anchored at Current, R-2); timer = icon with no progress ring (empty); meta line (exact copy): "Starts {date} · baseline will be captured on the start date"; footer primary = **Edit** → SCR-02 pre-filled; includes the KPI mini labels row. [SRS §6.5]
- **FR-115** — **Completed card differences**: no slider; all assigned KPIs render as outcome chips (coloured dot per outcome; tooltip = full outcome sentence per FR-306); timer = full grey ring, tooltip "Monitoring complete — all target dates have passed"; meta line (exact copy): "Evaluated on each target date · latest {latest Target Date}"; footer primary = **More Details**; no Edit anywhere (BR-023). **Archived card**: renders in its underlying date-computed shape (Active-style with live timer/slider, Planned-style, or Completed-style — measurement continues) plus the dashed Archived badge beside the title; primary = **More Details**; kebab = **Unarchive** only; no Edit. [SRS §6.6, §6.6b]
- **FR-116** — **Card actions matrix (normative — nothing else may render on a card)**:

  | Tab | Primary | Kebab (⋮) items |
  |---|---|---|
  | Active | More Details | Edit · Archive |
  | Planned | Edit | More Details (preview) · Archive |
  | Completed | More Details | Archive |
  | Archived | More Details | Unarchive |

  Prohibited on any card (BR-021): Clone, action-level Delete, "Deactivate/End early", Edit on Completed or Archived. [SRS §6.7]

### Functional Requirements — SCR-02 Add / Edit Action

- **FR-201** — **Edit mode is the same screen pre-filled** — "it's gonna look more like the Add Action view page". Editing MUST be available **only** for Planned and Active, non-archived Actions. Completed Actions MUST be read-only (BR-023); Archived Actions MUST be view-only until unarchived. [SRS §7.1, BR-018]
- **FR-202** — The layout MUST be: back link "← Back to Actions" · H1 "Add Action" or "Edit Action" · subtitle (exact shipped copy): "Define the initiative, then set a measurable target per KPI. The baseline score is captured automatically on the Action Start Date; monitoring begins the day after the Action End Date. Dates may be in the past to document an action retrospectively." · Panel 1 "Action details" (sub-copy: "Name and dates drive the measurement timeline — see the derived Target Start Date on the details page."; Name wide ~2.1fr + Start Date + End Date side-by-side; Description full-width below with live counter) · Panel 2 "KPI Targets" (with header-right "Add KPI Target" button; sub-copy: "Thresholds are points added over the baseline. Reaching the upper threshold on the Target Date means Successful; the lower one, Partially Successful. At least one active target is required.") · footer (Cancel ghost + Save action primary). [SRS §7.2]
- **FR-203** — Action-level fields MUST be exactly: `action_name` (text, required, ≤120, placeholder "e.g. Training of Call Center Agents"), `action_start_date` (date, required, tooltip "Baseline score is captured on this date", past allowed), `action_end_date` (date, required, tooltip "Monitoring starts the day after this date", past allowed, ≥ start), `description` (multiline plain text, optional, ≤500 with live "{n}/500" counter, placeholder "What is this action, who runs it, and what should it improve?"). [SRS §7.3]
- **FR-204** — The KPI Target subsection MUST show: header row `Target {n}` (renumbered on delete) · Delete ghost button (visible only when deactivated) · Active/Activate toggle switch (green when on) · body grid: KPI select · Target Date · Lower Threshold · Upper Threshold · full-width slider block (score label + Threshold Slider + scale note "Scale 0–{X} · maximum configured in Settings → Actions · decimals allowed · drag the flags or type the values"). [SRS §7.4]
- **FR-205** — Target-level fields MUST be exactly: `kpi_id` (select, required, first option "Select"; options = tenant's Active KPIs from M-06; options already chosen in other Targets of this action MUST be disabled — live cross-refresh on every selection), `target_date` (date, required, > `action_end_date`, past allowed), `lower_threshold` (number, step 0.5, 1 dp, required, 0 permitted, `0 ≤ L ≤ U`, label hint "— points over baseline"), `upper_threshold` (number, step 0.5, 1 dp, required, > 0 per VAL-210, `L ≤ U ≤ X`, label hint "— points over baseline"). [SRS §7.5]
- **FR-206** — The **Current-score / Baseline label** MUST render above the slider, hidden until a KPI is selected, and react live to both the KPI select and the Action Start Date field: (a) Start Date empty or > today → "Current Score · {live score from M-06}" + note "Captured on the action start date as the baseline score" (updates live with M-06's live score); (b) Start Date ≤ today → label flips to "Baseline · {score}" + same note (stored Baseline in edit mode, M-06 historical score in retro-dated create). [SRS §7.6]
- **FR-207** — **Target activate / deactivate / delete**: toggling the switch OFF MUST deactivate the Target — subsection body renders faded read-only (~50 % opacity + greyscale, inputs inert); only the Activate switch and the now-visible Delete button remain operable. Toggling back ON MUST restore editability. Manual and forced deactivation both expose Delete. Delete MUST remove the Target after confirmation dialog DLG-1; remaining Targets renumber; the KPI returns to the other selects' available options; toast NTF-3 fires. Deactivated Targets are excluded from results, outcome evaluation, and lowest-performing selection, but still saved with the Action. [SRS §7.7, BR-010]
- **FR-208** — **Validation catalogue (exact messages)**:
  - **VAL-201** — Action Name required (non-blank after trim). Message: "Action Name is required".
  - **VAL-202** — Action Name unique per tenant, case-insensitive, across all statuses **including Archived**. Message: "An action with this name already exists".
  - **VAL-203** — Start and End dates required. Messages: "Action Start Date is required" / "Action End Date is required".
  - **VAL-204** — End ≥ Start. Message: "Action End Date must be on or after the Action Start Date".
  - **VAL-205** — Description ≤ 500 chars, plain text (hard cap; counter shown). Input-limited; no submit error.
  - **VAL-206** — Every Target Date > Action End Date. Message: "Target Date must be after the Action End Date".
  - **VAL-207** — ≥ 1 active Target with a KPI selected. Message: "At least one active KPI target is required".
  - **VAL-208** — KPI required per active Target. Message: "Select a KPI for Target {n}".
  - **VAL-209** — `0 ≤ L ≤ U ≤ X`, ≤ 1 dp. Prevented by the control; typed overshoot clamps.
  - **VAL-210** — U > 0 on every active Target (division guard, BR-F3). Message: "Upper Threshold must be greater than zero".
  - **VAL-211** — One Target per KPI per Action. Prevented — options disabled.
  - Errors MUST surface as **toasts on Save** AND **inline at the offending field**; focus MUST move to the first invalid field. [SRS §7.8]
- **FR-209** — **Edit-mode specifics**: available only for Planned/Active non-archived; deep links for Completed/Archived MUST redirect to SCR-03 with toast NTF-6. All fields pre-filled including deactivated Targets (rendered in their faded state). Guarded edits on started actions: Start Date → DLG-2 (baselines re-snapshot from M-06 history for the new date; all progress recomputes); End Date → DLG-4 (Target Start moves; all Time Progress recomputes); thresholds mid-monitoring → DLG-3. Cancel in any dialog reverts the field. All confirmed edits are audit-logged field-level. Editing may change the computed status via re-computation (FR-102) — including landing in Completed if new dates so dictate. [SRS §7.9]
- **FR-210** — **Buttons**:
  - Add KPI Target (Panel 2 header outline, plus icon) — appends a blank Target; disabled when every tenant KPI is already used.
  - Delete (target header, deactivated only) — opens DLG-1 → remove Target; renumber; KPI freed; toast NTF-3.
  - Active/Activate switch — toggles Target active state (§7.7 visual state).
  - Cancel (footer ghost) — discards changes, navigates SCR-01.
  - Save action (footer primary) — runs VAL-201…211; persists; audit event (create or field-level edit); toast NTF-2; navigates SCR-01; Action grouped per FR-102. On failure: first error toast + inline; stay on page.
  - ← Back to Actions (top) — same as Cancel. [SRS §7.10]

### Functional Requirements — SCR-03 Action Details

- **FR-301** — SCR-03 MUST be a **full page route** at `/actions/:id`, never a dialog. Deep links MUST resolve directly. A deep link to `/actions/:id/edit` for a Completed or Archived Action MUST redirect to `/actions/:id` with toast NTF-6. [SRS §4.1, §8.1]
- **FR-302** — The header block MUST contain: back link "← Back to Actions" · H1 = Action Name · **single** status badge (Active / Planned / Completed / Archived — Archived is standalone and replaces the date-computed badge while set) · Edit button (outline, pencil icon; visible only for Planned/Active non-archived) · Archive button (outline; visible on all non-archived Actions; enters Archived in place; toast NTF-4) · Unarchive button (outline; visible only while Archived; exits to date-computed status in place; toast NTF-5) · muted-paragraph Description · date row: "**Action Start · baseline captured** {date}" · "**Action End** {date}" · "**Target Start (derived)** {date}" (primary colour, tooltip "System-derived: Action End Date + 1 day. Monitoring clock starts here.") · "**Latest Target Date** {max Target Date}". [SRS §8.2]
- **FR-303** — The Action Targets list MUST render one row card per Target under the heading "Action Targets", in a 3-column grid (KPI zone ≈130 px · slider flexes · side zone ≈210 px), collapsing to one column below 940 px. [SRS §8.3]
- **FR-304** — **Row variant — Active unevaluated Target (§8.4 (a))**: KPI zone has the KPI chip + "Lowest performing" badge on the featured Target + 2 px cyan focus ring highlight on the featured row. Slider is the reference variant (§5.1) — B & C bold markers/labels PLUS L/U reference flags with values above the tick numbers. Side zone (end-aligned facts + timer): "**{Target Date}** / Target date" · "**{s}%** / Score progress" (display-clamped) · Timer Ring (per-row: own Target Date). Tooltip "Time {t}% · Score {s}%". [SRS §8.4]
- **FR-305** — **Row variant — Evaluated Target on Active Action (§8.4 (b))**: renders **exactly like a Completed row (§8.5)** — outcome label, C flag inside outcome zone, side zone "**{Target Date}** / Evaluated", full grey timer (tooltip "Monitoring complete") — and MUST be excluded from lowest-performing selection. [SRS §8.4]
- **FR-306** — **Row variant — Completed Action (§8.5)**: KPI chip + outcome label (dot + text). Slider is reference variant with the **C flag landing visually inside its outcome zone** — green for Successful, yellow band for Partially Successful, red for Unsuccessful. Side zone "**{Target Date}** / Evaluated" + full grey timer. Full outcome sentences (tooltips): "Successful — reached or exceeded the upper threshold on the target date" / "Partially successful — reached the lower threshold on the target date" / "Unsuccessful — did not reach the lower threshold on the target date". No Activate/Delete controls anywhere (BR-023). [SRS §8.5]
- **FR-307** — **Row variant — Planned Action (§8.6)**: slider without B flag AND without L/U reference flags (no baseline to anchor them); side zone "**{Target Date}** / Target date" + empty timer (tooltip "Monitoring not started"). [SRS §8.6]
- **FR-308** — **Row variant — deactivated Target (Planned/Active actions only, §8.7)**: entire row faded (~50 % opacity + greyscale) but showing the same full details (slider, dates) frozen. KPI zone adds "Deactivated" badge and two always-operable buttons: **Activate** (outline; disabled with tooltip "KPI is inactive in M-06" while force-deactivated and KPI remains inactive) and **Delete** (ghost; opens DLG-1). Side zone: "**—** / Excluded from results" + empty timer (tooltip "Deactivated — excluded from results"). On Completed or Archived Actions these controls MUST be hidden (read-only / view-only); the faded row still renders. [SRS §8.7]
- **FR-309** — **Buttons**:
  - Edit — Planned/Active non-archived; Program Manager → SCR-02 pre-filled.
  - Archive — non-archived; Program Manager → enter Archived status; audit event; page refreshes in place: badge becomes Archived, Edit hidden, Unarchive shown; toast NTF-4.
  - Unarchive — Archived; Program Manager → exit Archived; status recomputes; audit event; page refreshes in place: date-computed badge restored, Edit restored when Planned/Active; toast NTF-5.
  - Activate (target) — deactivated Target rows on Planned/Active non-archived; enabled per §10.2 → reactivate the Target; audit event; row un-fades; Target re-enters results & lowest-performing pool.
  - Delete (target) — deactivated Target rows on Planned/Active non-archived → DLG-1 → remove; audit event; row disappears; toast NTF-3.
  - ← Back to Actions — always → SCR-01. [SRS §8.8]

### Functional Requirements — Navigation

- **FR-401** — Screen hierarchy & routes: `/actions` (SCR-01), `/actions/new` (SCR-02 blank), `/actions/:id/edit` (SCR-02 pre-filled; Planned/Active non-archived only), `/actions/:id` (SCR-03). SCR-03 is a full page route, never a dialog. Deep links to all four routes MUST resolve directly. A deep link to `/actions/:id/edit` for a Completed or Archived Action MUST redirect to `/actions/:id` with toast NTF-6. [SRS §4.1]
- **FR-402** — Topbar breadcrumb MUST read `Actions / {All Actions | Add Action | Action Details}` — the second segment updates per screen; "Actions" is static module context. [SRS §4.2]
- **FR-403** — Entry / exit points MUST behave exactly per SRS §4.3 (from SCR-01 Add Action → SCR-02 blank; card More Details → SCR-03; Planned card kebab **More Details (preview)** → SCR-03 (planned rendering); card Edit (Planned) → SCR-02 pre-filled; Active card kebab Edit → SCR-02 pre-filled; SCR-02 Back / Cancel / successful Save → SCR-01; SCR-03 Back → SCR-01; SCR-03 Edit → SCR-02 pre-filled; SCR-03 Archive → stays refreshed as Archived; SCR-03 Unarchive → stays refreshed). Browser back MUST behave identically to the "← Back to Actions" link. [SRS §4.3]

### Functional Requirements — Shared UI components

- **FR-501** — **Stepped Zone Slider** (§5.1) MUST have the anatomy specified in SRS §5.1 (top → bottom): (1) optional L/U reference flags (SCR-03 reference variant only), (2) tick numbers close above the track (~6 px gap; step 1 when span ≤ 16, step 2 when span ≤ 32, else step 4; ticks equal to round(Baseline) and round(Current) render bold and slightly larger), (3) 14 px fully-rounded track with hard-edged Red [min→Baseline+L] / Yellow [Baseline+L→Baseline+U] / Green [Baseline+U→max] zones, no gradient blending, (4) B and C markers on the track — B = solid navy vertical bar (light variant in dark mode), C = card-coloured bar with 2 px navy border, ≈5×22 px rounded; tooltips "Baseline {v} — captured on Action Start Date" / "Current {v}"; L/U reference-flag tooltips "Lower threshold point {v} (baseline + {L})" / "Upper threshold point {v} (baseline + {U})" — (5) B/C letter labels below. Variants: Planned (no B), Reference (adds row 1), Card default (rows 2–5 only). States: static, non-interactive, updates on data refresh. Accessible as `role="img"` with an `aria-label` naming KPI, baseline (if any), current value, and zone bounds. Track bounds per §3.7 adaptive padding. [SRS §5.1]
- **FR-502** — **Threshold Slider (SCR-02, per KPI Target)** (§5.2) MUST have the anatomy specified: draggable flags above, tick numbers (0…X step 2), 14 px fully-rounded track with 7×20 px white-bordered stem handles overlapping the track. Lower flag red `--d5`; Upper flag green `--d2`. States: **Default (L=0 AND U=0)** — plain grey track, text-only flags "Lower Threshold" / "Upper Threshold" at illustrative 24 % / 76 % positions. **Set (either ≠ 0)** — flags show "L +{v}" / "U +{v}"; track shows hard-edged Red [0→L] / Yellow [L→U] / Green [U→X] zones, no gradient blending. Two-way binding between fields and flags. Auto-sync rule (BR-004). Constraint `0 ≤ L ≤ U ≤ X`, equality allowed, `U > 0` to save (VAL-210), decimals to 1 dp (inputs step 0.5; drag rounds to 0.1). Keyboard: `role="slider"` with `aria-valuemin/max/now`; ←/→ adjust by 0.5. Pointer: `pointerdown` on flag begins drag; drag ends on `pointerup`. Disabled + faded when Target is deactivated. [SRS §5.2]
- **FR-503** — **Timer Ring** (§5.3) MUST be a 44×44 px component: background ring (muted token), progress arc (radius 15.5, stroke 4.5, rounded caps, drawn from 12 o'clock clockwise, fill = displayed Time Progress), centred 17 px stopwatch icon. Ring starts empty and fills as time passes. Colour state per §3.4 table; icon inherits state colour. Tooltips (exact copy):
  - Active — "Time {t}% of monitoring window elapsed · Score progress {s}%"
  - Planned — "Not started — monitoring begins the day after the Action End Date"
  - Completed — "Monitoring complete — all target dates have passed"
  - Evaluated Target on Active Action — "Monitoring complete"
  - Deactivated Target — "Deactivated — excluded from results"
  All percentages MUST be display-clamped (BR-F2). [SRS §5.3]
- **FR-504** — **Badges, chips & labels** MUST render exactly as SRS §5.4 (status badge, Archived card badge, KPI chip, KPI mini labels, "Lowest performing" badge, "Lowest performing target" / "Lowest current score" label, "Deactivated" badge, outcome label, outcome chip, pace text). Exactly **one** status badge is shown per Action at a time; Archived is standalone and never co-displayed with another status. [SRS §5.4]
- **FR-505** — **Kebab menu, tabs, search, toast, buttons** MUST render per SRS §5.5: kebab (⋮) 34 px icon button per card opens 190 px dropdown, one open at a time, outside-click closes, `aria-haspopup="menu"`; tabs with underline style + per-tab count pills, active tab cyan underline + tinted count; search input with leading magnifier icon and placeholder "Search actions across all tabs…"; toast bottom-centred pill, ~2.6 s auto-dismiss, `role="status"`; buttons primary (cyan, one per screen region) / outline / ghost; disabled at 45 % opacity, not-allowed cursor. [SRS §5.5]

### Functional Requirements — Measurement Model (SRS §3, normative)

- **FR-M01** — **Date anchors**: D1 Action Start Date (user, required, Baseline snapshot); D2 Action End Date (user, required, ≥ D1, boundary only); D3 Target Start Date (system, = D2 + 1 day, clock zero for Time Progress, read-only on SCR-03); D4 Target Date (user, per Target, required, > D2 equivalently ≥ D3, outcome evaluation); D5 Current Date (system, live). [SRS §3.2, BR-D1/D2/D3]
- **FR-M02** — Retro-dating MUST be allowed on D1, D2, D4. A retro-dated Action MUST be born into whatever status its dates compute to (§10.1). Baseline for retro-dated D1 MUST come from M-06 historical scores. [SRS §3.2, BR-D1]
- **FR-M03** — Ordering: `D1 ≤ D2 < D4` for every Target (D3 = D2 + 1 always holds). [SRS §3.2, BR-D2]
- **FR-M04** — Day granularity & timezone: all date comparisons, baseline captures, phase transitions, and outcome evaluations MUST operate at day granularity in the tenant's configured timezone. [SRS §3.2, BR-D3, BR-022]
- **FR-M05** — **Baseline capture (BR-B1)**: on (or retroactively for) the Action Start Date, the system MUST capture `Baseline = KPI score on the Action Start Date` for each KPI Target, from M-06. [SRS §3.3]
- **FR-M06** — **Baseline recapture (BR-B2)**: if the Action Start Date of an already-started Action is edited, the system MUST automatically re-snapshot the Baseline from M-06 history for the new date, after the user confirms warning dialog DLG-2. Editing Action End Date on a started Action moves Target Start Date and recalculates Time Progress everywhere (guarded by DLG-4). Editing thresholds mid-monitoring is guarded by DLG-3. Every such change MUST be written to the audit trail. [SRS §3.3]
- **FR-M07** — **Planned baseline (BR-B3)**: before the Start Date is reached, no Baseline exists; SCR-02 shows the KPI's live Current Score in its place, labelled as the value that will be captured (FR-206); sliders render without a B flag (FR-501). [SRS §3.3]
- **FR-M08** — **Score Progress formula (normative)**: `Score Progress = (Current Score − Baseline Score) ÷ (Upper Threshold Point − Baseline Score)` where `Upper Threshold Point = Baseline + U`. Canonical form used in all UI tooltips and documentation. [SRS §3.4]
- **FR-M09** — **Time Progress formula (normative)**: `Passed Time = Current Date − Target Start Date`; `Full Time = Target Date − Target Start Date`; `Time Progress = Passed Time ÷ Full Time`. [SRS §3.4]
- **FR-M10** — **Timer colour mapping** MUST be exactly per §3.4 table: Score > Time → Green `--d2`; `|Score − Time| ≤ 0.005` → Yellow `--d3` (equality band, BR-015); Score < Time → Red `--d5`; Completed / evaluated → Grey `--nb-stone`, ring full; Not started (Planned) / deactivated → Empty (icon only, no ring fill). [SRS §3.4]
- **FR-M11** — **Execution-phase rule (BR-F1)**: while `D1 ≤ Current Date ≤ D2`, Time Progress raw = 0 by definition. Ring renders empty-fill; colour follows the table (any positive lift → Green, no movement → Yellow, regression → Red). [SRS §3.4]
- **FR-M12** — **Clamp what is drawn, never what is computed (BR-F2)**: raw Score/Time Progress values MUST drive all logic (colour, ranking); **displayed** values MUST clamp to 0–100 % (ring fill, percentage labels). A regressing KPI (raw negative) MUST always show Red; an early overshoot (raw > 100 %) MUST show a full ring and Green. [SRS §3.4]
- **FR-M13** — **Division guard (BR-F3, VAL-210)**: saving a Target with U = 0 MUST be blocked to keep the Score Progress denominator non-zero. [SRS §3.4]
- **FR-M14** — **Outcome evaluation** (per Target on its Target Date): Final ≥ Baseline + U → **Successful** `--d2`; Baseline + L ≤ Final < Baseline + U → **Partially Successful** `--d3`; Final < Baseline + L → **Unsuccessful** `--d5`. U = L is a valid saved state (equality allowed): the Partially-Successful band collapses; outcome is binary Successful/Unsuccessful (BR-O4). Deactivated Targets are never evaluated (BR-O5). Outcomes MUST be computed from stored data (Baseline, L, U, Final Score) — not stored as hardcoded labels (BR-O6). [SRS §3.5]
- **FR-M15** — **Lowest-Performing Target selection** MUST implement §3.6 exactly: definition = eligible Target with the **lowest raw (unclamped) Score Progress**; eligibility = active + has Baseline + Target Date ≥ Current Date; tie-breaks in order = (1) earliest Target Date, (2) KPI name ascending alphabetical. For Planned actions with no Baselines, fallback = the Target whose KPI has the **lowest current score on M-06's normalised 0–100 index**; card labels this "Lowest current score" rather than "Lowest performing target". Zero-eligible fallback per FR-111. [SRS §3.6]
- **FR-M16** — **Adaptive slider padding** (§3.7) MUST implement: for every stepped zone slider, with anchor A (= Baseline; Planned = Current Score provisionally), Track minimum = `min(A, Current) − PAD` and Track maximum = `max(A + U, Current) + PAD`. If Current < Baseline (regression), the red zone extends PAD points below the Current Score, so the C flag stays on the track. If Current > Upper Threshold Point (overshoot), the green zone extends PAD points above the Current Score. Zone boundaries are unchanged by padding (hard edges, no blending). [SRS §3.7]
- **FR-M17** — **Action phase timeline** MUST render per §3.8: (1) Planned — Current < Start; no Baseline; empty timers; C-only sliders. (2) Execution — Start ≤ Current ≤ End; Baseline captured at phase entry; Time Progress = 0 (BR-F1); ring empty; colour by lift. (3) Monitoring — Target Start ≤ Current ≤ latest Target Date; ring fills with Time Progress; colour by Score vs Time; individual Targets whose Target Date passes are evaluated and thereafter render as evaluated rows while later Targets keep running. (4) Completed — Current > latest Target Date (all Targets evaluated); grey full timers; outcomes displayed; Action is read-only (BR-023). Archived can be entered from any of the above and exited back to date-computed status without pausing measurement. [SRS §3.8]

### Cross-Screen Business Rules (SRS §9)

- **BR-001** — One KPI Target per KPI per Action; enforced live by disabling already-chosen KPIs in every other Target's select.
- **BR-002** — Only **Active** KPIs from M-06 are offered in the KPI select.
- **BR-003** — Thresholds are **deltas over the Baseline**, range `0 → X`, decimals allowed (doubles, 1 dp), `U ≥ L` with equality allowed, `U > 0` (VAL-210).
- **BR-004** — Auto-sync: from L's first change off 0 (typed or dragged), U mirrors L until U is independently set; afterwards `L ≤ U` is clamped in both directions.
- **BR-005** — Baseline = KPI score on the **Action Start Date** (recapture per BR-B2).
- **BR-006** — Target Start Date = Action End Date + 1 day, system-derived, displayed read-only, never editable.
- **BR-007** — Date ordering: `Start ≤ End < every Target Date`. Retro-dating permitted on all user dates.
- **BR-008** — For non-archived Actions, status is computed, date-driven: `Planned (Start > now) → Active → Completed (latest Target Date passed)`.
- **BR-009** — **Archived is a standalone status**, exclusive of Planned/Active/Completed, and non-destructive: measurement computation, timers, evaluations, and phase logic continue unchanged while Archived; presentation is view-only in the Archived tab; unarchiving recomputes the status from dates and restores editability where Planned/Active. Archiving requires no confirmation and is available from every other status, on cards and on SCR-03.
- **BR-010** — Deactivated Targets are excluded from: outcome evaluation, results display eligibility, and lowest-performing selection; they render faded read-only with full frozen details wherever shown.
- **BR-011** — M-06 KPI deactivation **force-deactivates** all its Targets across all Actions: faded read-only, deletable, re-activatable **only** once the KPI is Active in M-06 again.
- **BR-012** — Delete exists only at **Target** level, only while the Target is deactivated (manual or forced), only on Planned/Active non-archived Actions, and always behind confirmation DLG-1. No action-level delete.
- **BR-013** — Lowest-performing selection per §3.6 (raw unclamped Score Progress; eligibility excludes deactivated and already-evaluated Targets; tie-breaks: earliest Target Date, then KPI name). Zero-eligible fallback per FR-111.
- **BR-014** — All drawn values clamp to 0–100 % / track bounds; all logic uses raw values (BR-F2), with the §3.7 adaptive padding guaranteeing the C flag stays on-track.
- **BR-015** — Timer equality band: `abs(Score − Time) ≤ 0.005` ⇒ Yellow (deterministic "on pace" rendering).
- **BR-016** — Search: case-insensitive Action-Name substring, all four tabs at once, with cross-tab match hint. Filters (KPI multi-select, Start-Date range from–to) AND-combine with search across all four tabs. No Status filter; no Created-by filter.
- **BR-017** — Every KPI on an Action appears as a mini label on its Active/Planned card; deactivated ones struck through. Completed cards satisfy this via outcome chips.
- **BR-018** — Edit mode = the Add Action layout pre-filled. Editing is available **only** for Planned and Active non-archived Actions; every field is editable there. **Completed Actions are read-only** (BR-023); Archived Actions are view-only until unarchived.
- **BR-019** — All KPIs are higher-is-better (M-06 standardisation); no inverted-KPI handling exists anywhere in M-15.
- **BR-020** — X (max upper threshold, default 20) and PAD (slider padding, default 3, positive integer) are tenant-configurable in Settings → Actions and apply module-wide.
- **BR-021** — Removed features MUST NOT resurface: Clone, action-level Delete, Status filter, **Created-by filter**, "Deactivate/End early", Expired/Past vocabulary, editing of Completed Actions, review-cadence functionality.
- **BR-022** — **Timezone & granularity**: every day-boundary comparison in the module — baseline capture (D1), Target Start derivation (D3), Planned→Active and Active→Completed transitions, outcome evaluation (D4) — operates at day granularity in the tenant's configured timezone.
- **BR-023** — **Completed Actions are read-only**. No edit form, no field changes, no target activate/deactivate/delete. Permitted operations: view (SCR-03), Archive, Unarchive. Consequently evaluated outcomes cannot be rewritten and a Completed Action can never be resurrected to Active by edits.

### Status Lifecycle (SRS §10)

- **FR-L01** — Action statuses table (SRS §10.1):
  | Status | Condition | Tab | Editability |
  |---|---|---|---|
  | Planned | Non-archived; `Start > now` | Planned | Editable |
  | Active | Non-archived; `Start ≤ now ≤ latest Target Date` | Active | Editable |
  | Completed | Non-archived; `latest Target Date < now` | Completed | **Read-only** (BR-023) |
  | Archived | `archived = true` — standalone, overrides the above for presentation | Archived | **View-only**; Unarchive only |
- **FR-L02** — Transitions: Planned → Active → Completed MUST occur automatically as the clock crosses D1 and the latest D4 (tenant timezone). **No manual status transition** exists among these three. Editing dates on Planned/Active Actions can move an Action between Planned/Active/Completed via recomputation (FR-102); once Completed, no edits are possible, so Completed is terminal except for Archive. Any status → Archived via Archive; Archived → date-computed status via Unarchive. Invalid: any other user-initiated status set; an Action is never simultaneously Archived and Planned/Active/Completed.
- **FR-L03** — KPI Target lifecycle (SRS §10.2):
  | State | Entered by | Behaviour | Exit |
  |---|---|---|---|
  | Active | Default on creation / reactivation | Fully editable (while parent is editable); counted in results & lowest-performing; evaluated on its Target Date | Manual deactivate; force-deactivate; evaluation (remains Active but leaves eligibility pool) |
  | Deactivated (manual) | Toggle off (Planned/Active parents) | Faded read-only, full details frozen; excluded per BR-010; Delete available | Activate (always enabled) · Delete |
  | Deactivated (forced) | KPI deactivated in M-06 (BR-011) | Same rendering & exclusions; Delete available | Activate **only when** KPI is Active in M-06 again · Delete |
- **FR-L04** — **Archived status mechanics** (SRS §10.3): `archived: boolean` is the stored representation; when set, the presented status is Archived (standalone — never co-displayed with another status). Setting/clearing is allowed from any status, never pauses measurement computation (timers keep running, targets keep getting evaluated, Completed transition still fires underneath), and controls: tab placement (FR-103), the single Archived badge, view-only presentation, and the Archive/Unarchive controls. Both operations are audit events. On unarchive, the status recomputes from dates — including landing directly in Completed (read-only) if the latest Target Date passed while Archived.

### Settings — Actions Subsection (SRS §11)

- **SET-1** — **Action Target Maximum Upper Threshold (X)** — number, 1 dp, default **20**; range `> 0`; **cannot be lowered below the largest U saved in the tenant** — such an attempt is blocked with "Cannot set the maximum below an existing Upper Threshold ({largest U})". Effect: Threshold Slider scale `0→X`; VAL-209 ceiling; scale-note text.
- **SET-2** — **Slider Padding (PAD)** — positive integer, default **3**; range `≥ 1`. Effect: §3.7 track extension on every Stepped Zone Slider.
- **SET-3** — Changes MUST apply tenant-wide on next render, MUST be audit-logged, and MUST require the settings-administration permission (Program Manager per §13; refined later by M-10).

### Integrations & Cross-Module Contracts

- **INT-01 (M-06 KPI Engine — hard dependency)** — SRS §12.1:
  - Active KPI registry (M-06 → M-15) populates the KPI select (BR-002); prototype set: NPS, CSAT, CES, FCR, VFM, Agent Score, CHS.
  - Live current score (M-06 → M-15) drives C flags, Score Progress, §7.6 label.
  - Normalised 0–100 index (M-06 → M-15) drives the Planned lowest-current-score selection (§3.6).
  - Historical daily score (M-06 → M-15) drives baseline capture & recapture, retro-dating (BR-B1/B2, BR-D1).
  - KPI-deactivation / reactivation events (M-06 → M-15) trigger force-deactivation and re-enable Activate (BR-011).
  - Failure handling: ERR-4 (M-06 unreachable on SCR-02) / ERR-5 (M-06 score missing for a date).
- **INT-02 (M-07 Dashboards & Reporting — forward contract)** — SRS §12.2: M-07's trend-analysis chart SHALL offer an option to overlay Planned / Active / Completed Actions as vertical solid or dashed reference lines on KPI trend lines. M-15 MUST expose per Action: name, status, Start/End/Target-Start/latest-Target dates, and the Archived status (Archived Actions excluded from the overlay by default). Rendering follows the design system's Trend Chart Annotations pattern.
- **INT-03 (M-09 Notifications — postponed in full)** — SRS §12.3: All user alerting for M-15 is postponed to the M-09 Notifications module. M-15 v1 SHIPS NO alerts, emails, or push notifications of any kind — only the in-app toasts and confirmation dialogs of SRS §15. M-15's obligation is limited to emitting its audit events (§12.4), which M-09 may later subscribe to.
- **INT-04 (Audit trail — F-M15-07, data requirement, no UI in scope)** — SRS §12.4: Every event MUST record actor, timestamp, action id, and old→new values where applicable: `action.created`, `action.field_edited` (field-level, incl. dates & thresholds), `baseline.captured`, `baseline.recaptured`, `target.added`, `target.activated`, `target.deactivated` (manual vs forced — captured as an attribute), `target.deleted`, `action.archived`, `action.unarchived`, `action.status_transitioned` (automatic), `outcome.evaluated`, `settings.X_changed`, `settings.PAD_changed`. **Viewing UI is out of scope for M-15 v1**; the data requirement stands.

### Permissions Matrix (SRS §13 — confirmed interim; refined later by M-10)

- **PERM-01** — The following matrix MUST be enforced server-side (ERR-3 on any bypass) and applied client-side by hiding (not merely disabling) write controls for view-only roles:

  | Capability | CX Program Manager | CX Analyst | Executive / Viewer |
  |---|---|---|---|
  | View SCR-01 / SCR-02 (read) / SCR-03 | ✔ | ✔ (SCR-01 / SCR-03 only) | ✔ (SCR-01 / SCR-03 only) |
  | Create action (Add Action) | ✔ | ✖ | ✖ |
  | Edit action (Planned/Active) | ✔ | ✖ | ✖ |
  | Archive / Unarchive | ✔ | ✖ | ✖ |
  | Activate / deactivate / delete targets | ✔ | ✖ | ✖ |
  | Settings → Actions (SET-1/SET-2) | ✔ | ✖ | ✖ |

- **PERM-02** — M-10 MAY later refine roles without contradicting this baseline.

### Error Handling (SRS §14)

- **ERR-1** — Field validation on Save: first failing VAL rule → toast + inline message + focus; no partial save.
- **ERR-2** — Duplicate Action Name: VAL-202 message; save blocked.
- **ERR-3** — Permission denied: standard platform 403 pattern; controls hidden per §13.
- **ERR-4** — M-06 unreachable on SCR-02: KPI select disabled with helper "KPI list unavailable — try again"; score label hidden; Save blocked for new Targets.
- **ERR-5** — M-06 score missing for a date (baseline/recapture): blocking dialog "No KPI score exists for {date}. Choose a different Action Start Date."; no silent fallback.
- **ERR-6** — Deep link to missing/foreign-tenant Action: "Action not found" empty state + Back to Actions.
- **ERR-7** — Archive/unarchive write failure: toast "Couldn't update the action — try again"; status unchanged.
- **ERR-8** — Concurrent edits (two editors): last-write-wins + full audit trail + stale-save warning on version mismatch (no record locking).
- **ERR-9** — Network failure on Save: toast "Connection lost — your changes were not saved"; form state preserved.
- **ERR-10** — Live-score refresh failure on cards: render last known values with a stale-data tooltip; never block the page.
- **ERR-11** — Edit attempt on Completed/Archived (incl. deep link): redirect to SCR-03 + toast NTF-6; server rejects any write.

### Notifications & Dialogs (SRS §15 — in-app UI feedback only; all alerting deferred to M-09)

- **NTF-1** — Save with VAL failure → the VAL message (e.g., "Action Name is required", "At least one active KPI target is required", "Upper Threshold must be greater than zero").
- **NTF-2** — Successful save → "Action saved".
- **NTF-3** — Target deleted → "Target removed".
- **NTF-4** — Archived → "Action archived — it keeps running and is available in the Archived tab".
- **NTF-5** — Unarchived → "Action unarchived — it resumes in its status tab with its original dates".
- **NTF-6** — Edit attempted on Completed/Archived Action → "Completed actions are read-only" / "Unarchive this action to edit it".
- **DLG-1** — Delete a (deactivated) Target — title "Delete this KPI target?" / body "The target and its configuration will be removed from this action. This cannot be undone." — buttons: Cancel (ghost) · Delete (destructive).
- **DLG-2** — Edit Start Date after Baseline exists — title "Recapture baselines?" / body "Changing the Action Start Date re-captures every KPI baseline for the new date and recalculates all progress and outcomes." — buttons: Cancel · Recalculate & continue (primary).
- **DLG-3** — Edit thresholds mid-monitoring — title "Change thresholds?" / body "This changes how progress and outcomes are calculated for this target." — buttons: Cancel · Apply (primary).
- **DLG-4** — Edit End Date on a started Action — title "Move the monitoring start?" / body "Changing the Action End Date moves the Target Start Date (End + 1) and recalculates time progress for every target." — buttons: Cancel · Recalculate & continue (primary).
- Archiving intentionally has **no** dialog (non-destructive, BR-009).

### Non-Functional Requirements (SRS §16)

- **NFR-1 Localisation/RTL** — Arabic-first production (`dir="rtl"` default), full EN/AR parity, native فصحى copy (not literal translation of the English strings in this SRS), logical CSS properties throughout, Latin numerals in LTR spans, IBM Plex Sans Arabic fallback, minimum 14 px Arabic body with relaxed leading. The prototype is the LTR reference.
- **NFR-2 Theming** — Light + navy-tinted dark mode; design-system tokens only (no raw hex); **Two-Palette Rule** — brand cyan/mint for chrome/CTAs/emphasis only; D-scale semantic tokens exclusively for pace, zones, and outcomes, never decoratively.
- **NFR-3 Typography** — Sora headings, Poppins body, tabular numerals for all scores/dates.
- **NFR-4 Accessibility** — WCAG 2.1 AA; visible focus rings; keyboard operation of threshold flags (FR-502), tabs, kebabs; `role="slider"` / `role="img"` / `role="status"` / `aria-haspopup` as specified; colour never the sole signal (labels/letters/tooltips accompany every colour state); contrast ≥ 4.5:1 text, 3:1 components; `prefers-reduced-motion` honoured for all transitions.
- **NFR-5 Performance** — SCR-01 interactive < 2 s with 200 Actions; search/filter feedback < 100 ms; slider drag at 60 fps; derived values computed client-side from delivered data. **Computation locus (rev 1.1):** the server is authoritative for evaluation-time facts (baseline snapshots, `final_score`, `outcome`); live pace values (Score Progress, Time Progress, timer state, lowest-performing selection) are computed client-side from delivered raw inputs.
- **NFR-6 Responsive** — ≥ 1280 px full layout; card grid ≥ 430 px columns; single column and stacked target rows below 940 px; sidebar collapses below 940 px (platform shell).
- **NFR-7 Audit & security** — Every write audit-logged (INT-04); strict tenant isolation; module access via platform auth; server-side enforcement of PERM-01 and BR-023; no PII beyond creator attribution.
- **NFR-8 Time handling** — All day-boundary logic per BR-022 (tenant timezone, day granularity); server is the source of truth for "today".
- **NFR-9 Browser/session** — Last 2 evergreen versions (Chrome, Edge, Firefox, Safari); platform-standard session handling; unsaved-changes state survives transient network loss (ERR-9).

### Dependency & Side-Effect Analysis (SRS §17)

- **Affected modules**: M-06 (5 data flows + 2 events, INT-01), M-07 (overlay contract INT-02), M-09 (audit-event subscription only; all alerting specified there), M-10 (future refinement of §13), platform Settings (new "Actions" subsection), platform audit service, platform tenant-timezone setting (consumed).
- **API changes required**: yes — new M-15 CRUD/read surfaces and the M-06 flows above (shapes out of scope of this spec).
- **Data migration required**: no (new module; no pre-existing data).
- **Integration impact**: M-06 must add/confirm historical-score-by-date and KPI deactivation/reactivation events; M-07 must plan the overlay toggle; none broken.
- **UI changes cascading**: Settings screen gains the Actions subsection; sidebar gains/keeps the Actions item; M-07 chart gains an overlay option.
- **Risks & mitigations**: missing historical scores → ERR-5 blocking dialog; KPI force-deactivation storms → batch the event handling + audit; mid-flight date/threshold edits distorting results → DLG-2/3/4 warnings + field-level audit; X lowered below existing U → blocked by SET-1 guard; edits racing the Completed transition → server re-validates BR-023 at write time (ERR-11).

### Key Entities (SRS Appendix A)

- **Action** — `id · tenant_id · action_name (≤120, unique/tenant across all statuses) · description (≤500, plain text) · action_start_date · action_end_date · archived (bool, default false — presented as the standalone Archived status when true) · created_by (audit attribution only) · created_at · updated_at`. Derived: `target_start_date`, `status` (Planned / Active / Completed / Archived), `latest_target_date`.
- **KPI Target** — `id · action_id · kpi_id (unique per action) · target_date · lower_threshold (0–X, 1 dp) · upper_threshold (L–X, 1 dp, > 0) · active (bool) · deactivation_source (manual / forced / null)`. Captured: `baseline_score`, `baseline_captured_for_date`. Derived: `score_progress` (raw + display-clamped), `time_progress` (raw + display-clamped), `timer_state`, `outcome`.
- **Settings (Actions)** — `max_upper_threshold X (default 20; SET-1 guard) · slider_padding PAD (default 3, positive integer)`.
- **Audit event** — `id · tenant_id · actor · timestamp · action_id · target_id? · event_type · old_value · new_value`. Event-type catalogue enumerated in INT-04.

---

## Success Criteria *(mandatory)*

All criteria are measurable, technology-agnostic, and verifiable against the deployed module.

### Measurable Outcomes

- **SC-001 — Pace awareness in seconds** — A CX Program Manager viewing SCR-01 for the first time can identify behind-pace Actions (red timer) within **≤ 10 seconds** of page load, verified on a seeded tenant of 30 Active Actions *(timing target to be confirmed with stakeholder)*. (Rationale: the Lowest-Performing-Target model exists precisely for at-a-glance triage — SRS §6.1.)
- **SC-002 — Create-an-action time** — A CX Program Manager can create a new Action with one KPI Target and return to SCR-01 in **≤ 90 seconds** from opening SCR-02. Measured on the golden path of Story 1 (Name + dates + one Target + Save) *(timing target to be confirmed with stakeholder)*.
- **SC-003 — Retro-date correctness** — 100 % of Actions created retro-dated with valid M-06 historical scores land in the correct date-computed tab (Active or Completed) on save, with Baselines captured from historical scores — verified via `BaselineCaptureService` unit tests + Story 1 integration tests + Story 10 E2E tests.
- **SC-004 — Automatic transition fidelity** — 100 % of Active Actions whose latest Target Date passes appear in the Completed tab on the first render after the day boundary is crossed (tenant timezone), with all per-Target outcomes computed — verified via Story 5's `ActionLifecycleScenarioTests`.
- **SC-005 — Archive continuity** — 100 % of Archived Actions continue computing their timers, Score/Time Progress, and per-Target outcomes; unarchiving after a passed latest Target Date lands the Action directly in Completed. Verified via Story 6 scenario tests.
- **SC-006 — Read-only Completed** — 0 successful writes to any Completed Action are possible via the API surface, verified by Story 4's `PUT /api/actions/{id}` on Completed → 409 test AND by the deep-link redirect E2E test.
- **SC-007 — Threshold guard** — 0 tenant configurations exist where SET-1 (X) < max saved U anywhere in the tenant, verified by the SET-1 guard integration test.
- **SC-008 — Cross-tab search** — Users searching by Action Name see results with 100 % recall across all four tabs, with per-tab counts unaffected by the query — verified by AC-1.4 E2E test.
- **SC-009 — Lowest-Performing-Target correctness** — For any Active Action with ≥ 2 eligible Targets, the featured Target's raw Score Progress is `min(target.score_progress_raw for target in eligible)` with tie-breaks (earliest Target Date, then KPI name), verified via `LowestPerformingTargetSelector` unit tests.
- **SC-010 — Force-deactivation cascade** — When M-06 deactivates a KPI, 100 % of Targets referencing that KPI across all Actions become `deactivation_source = 'forced'` and `active = false` on the next event processing cycle, with one `target.deactivated (source='forced')` audit event per Target (INT-04 naming) — verified via Story 7's `KpiForceDeactivationScenarioTests`.
- **SC-011 — Audit completeness** — Every write to an Action or Target (create, edit, deactivate, reactivate, delete, archive, unarchive, status transition, outcome evaluation, settings change) emits an audit event with actor + timestamp + action_id + (where applicable) target_id + old/new values — verified via audit-log assertions in every integration and scenario test.
- **SC-012 — Bilingual parity** — SCR-01, SCR-02, and SCR-03 render with full parity in Arabic (RTL) and English (LTR), with 0 physical direction properties in the M-15 codebase (verified per the CLAUDE.md self-review regex `-\[#[0-9a-fA-F]{3,8}\]` returning 0 hits AND no `pl-*`/`pr-*`/`ml-*`/`mr-*` usages in M-15 components).
- **SC-013 — Performance targets** — SCR-01 interactive < 2 s with 200 Actions; search/filter feedback < 100 ms; slider drag at 60 fps (NFR-5), verified via performance regression tests on a seeded tenant.
- **SC-014 — Accessibility** — 0 WCAG 2.1 AA violations in an automated axe scan of SCR-01, SCR-02, and SCR-03 in both LTR and RTL; keyboard-only operation of every write flow is possible (Threshold Slider flags, tabs, kebabs) — verified via NFR-4 E2E tests.

---

## Assumptions

All former assumptions from SRS v1.0 were **ratified by the stakeholder on 21 Jul 2026** and are now binding requirements above. The following are the ratified decisions carried forward from SRS §19 as active assumptions in this spec:

- **R-1** — Interim permissions matrix per PERM-01 applies; M-10 refines later without contradicting this baseline.
- **R-2** — Planned sliders anchor zones **provisionally at the KPI's current score** until Baseline capture (§3.7, FR-M16, FR-501).
- **R-3** — Default landing tab = Active; card ordering = newest-created-first; pagination/infinite scroll beyond one viewport (FR-110).
- **R-4** — Concurrency: last-write-wins + audit + stale-save warning; no locking (ERR-8).
- **R-5** — v1 source linkage = KPIs only (no journeys/cases/AI recommendations).
- **R-6** — No assignee in v1; Created-by filter removed; creator captured for audit only.
- **R-7** — SET-1 guard: X cannot go below the largest saved U.
- **R-8** — PAD positive integer; yellow-timer equality band ±0.005 (BR-015); Description plain text ≤ 500; name uniqueness per tenant across all statuses incl. Archived (VAL-202/205).
- **R-9** — VAL-210: Upper Threshold must be > 0 on active Targets.
- **R-10** — Evaluated Target on an Active Action renders as a Completed-style row, excluded from lowest-performing (§8.4 (b), FR-305).
- **R-11** — Zero-eligible-target Active card fallback ("No active targets to feature") per FR-111.
- **R-12** — End-Date edits on started Actions guarded by DLG-4.
- **R-13** — Completed Actions are read-only (BR-023).
- **R-14** — Archived is a standalone status; Archive added to the SCR-03 header.
- **R-15** — Search & filters span all four tabs.
- **R-16** — Tenant-timezone, day-granularity time handling (BR-022, NFR-8).
- **R-17** *(rev 1.1 — **stakeholder-ratified, 22 Jul 2026**)* — Deleting the action's **last remaining Target** (any state) is refused with "An action must keep at least one KPI target", preventing an orphan action with an undefined `latest_target_date`. Enforced server-side; no additional client-side visibility restriction beyond FR-207/FR-308.

### Dependencies

- **M-06 (KPI Engine)** — hard dependency. Must expose per tenant: (a) Active KPI registry, (b) each KPI's live current score, (c) each KPI's normalised 0–100 index, (d) historical daily scores by date, (e) KPI-deactivation/reactivation events. Missing any of (a)–(e) blocks the corresponding feature per ERR-4/ERR-5 or BR-011.
- **M-07 (Dashboards & Reporting)** — forward contract only (INT-02). M-15's obligation is limited to exposing the Action metadata; M-07 owns the overlay rendering.
- **M-09 (Notifications)** — all user alerting for M-15 is postponed **in full** to M-09; M-15 v1 ships zero alerts/emails/pushes and only the in-app toasts/dialogs of SRS §15.
- **M-10 (User & Role Management)** — future refinement of PERM-01. M-15 v1 hardcodes the three interim roles (Program Manager, Analyst, Viewer) as documented.
- **Platform shell** — sidebar navigation, theme toggle, language toggle, breadcrumb chrome are platform-owned; M-15 owns only the "Actions" active-nav highlight.
- **Platform Settings** — the new "Actions" subsection is appended to the platform Settings screen.
- **Platform audit service** — M-15 emits audit events per INT-04; the viewing UI is out of scope for M-15 v1.
- **Platform tenant-timezone setting** — consumed by every day-boundary comparison per BR-022 / NFR-8.

---

## SRS Coverage Checklist

*Verification that every SRS section and subsection has been processed and represented in this specification. Cross-reference the SRS by section number.*

| SRS Section | Title | Represented in this spec? | Where |
|---|---|---|---|
| §1.1 | Purpose | ✔ | Overview (implicit) |
| §1.2 | Scope (in scope / out of scope) | ✔ | Overview → "Explicitly out of scope for v1" |
| §1.3 | Definitions & Acronyms | ✔ (canonical vocabulary preserved via traceability tags — VOC, KPI, NPS, CSAT, CES, FCR, VFM, CHS, Agent Score, Action, KPI Target, Baseline (B), Current (C), Final Score, L, U, Lower/Upper Threshold Point, Action Start/End Date, Target Start/Date, Score Progress, Time Progress, Lowest-Performing Target, X, PAD, Archived, Tenant, Tenant timezone, Closed-Loop Management) | Key Entities + FR-M-series + Assumptions |
| §1.4 | References | ✔ | Overview → "Source SRS" |
| §2.1 | Product Perspective + F-M15-01..07 cluster mapping | ✔ | Overview + Assumptions → Dependencies (F-M15-02 no-assignee explicitly noted; F-M15-04 KPI-only linkage explicitly noted; F-M15-07 audit trail INT-04) |
| §2.2 | User Classes & Characteristics (Program Manager / Analyst / Viewer) | ✔ | PERM-01 permissions matrix |
| §2.3 | Operating Environment (evergreen browsers, RTL, dark mode) | ✔ | NFR-1, NFR-2, NFR-9 |
| §2.4 | Assumptions & Dependencies (module-level) | ✔ | Assumptions → Dependencies |
| §3.1 | Concept — narrative + worked example (two-anchor model) | ✔ | Overview + FR-M01..M17 |
| §3.2 | Date anchors — normative table (D1..D5) + BR-D1/D2/D3 | ✔ | FR-M01, FR-M02, FR-M03, FR-M04 |
| §3.3 | Baseline capture and recapture (BR-B1/B2/B3) | ✔ | FR-M05, FR-M06, FR-M07 |
| §3.4 | Formulas (Score/Time Progress) + timer colour mapping + BR-F1/F2/F3 | ✔ | FR-M08, FR-M09, FR-M10, FR-M11, FR-M12, FR-M13 |
| §3.5 | Outcome evaluation (BR-O1..O6) | ✔ | FR-M14 |
| §3.6 | Lowest-Performing Target selection + normalisation | ✔ | FR-M15, plus Story 2 Unit Tests |
| §3.7 | Adaptive slider padding | ✔ | FR-M16, plus FR-501 |
| §3.8 | Action phase timeline (Planned / Execution / Monitoring / Completed / Archived) | ✔ | FR-M17 |
| §4.1 | Screen hierarchy & routes | ✔ | FR-401 |
| §4.2 | Breadcrumb | ✔ | FR-402 |
| §4.3 | Entry / exit points | ✔ | FR-403 |
| §4.4 | Platform chrome (out of module scope) | ✔ | Overview → "Explicitly out of scope" + Assumptions → Dependencies |
| §5.1 | Stepped Zone Slider anatomy & variants | ✔ | FR-501 |
| §5.2 | Threshold Slider anatomy & states | ✔ | FR-502 |
| §5.3 | Timer Ring | ✔ | FR-503 |
| §5.4 | Badges, chips & labels | ✔ | FR-504 |
| §5.5 | Kebab menu, tabs, search, toast, buttons | ✔ | FR-505 |
| §6.1 | SCR-01 purpose, actors, objective | ✔ | User Story 2 Overview |
| §6.2 | SCR-01 layout | ✔ | FR-112 (rev 1.1) + FR-101..FR-111 |
| §6.3 | SCR-01 FR-101..FR-111 | ✔ | FR-101 through FR-111 (verbatim) |
| §6.4 | Active card — content spec | ✔ | FR-113 (rev 1.1) + Story 2 + FR-M15 + FR-501 |
| §6.5 | Planned card — differences | ✔ | FR-114 (rev 1.1) + Story 2 (E2E scenarios) + FR-M15 fallback |
| §6.6 | Completed card — differences | ✔ | FR-115 (rev 1.1) + Story 2 (E2E scenarios) + FR-M14 |
| §6.6b | Archived card | ✔ | FR-115 Archived variant (rev 1.1) + Story 6 + FR-103 |
| §6.7 | Card actions matrix | ✔ | FR-116 (rev 1.1, normative table) + BR-021 |
| §6.8 | Buttons — SCR-01 | ✔ | Story 2 buttons + FR-505 + BR-021 |
| §6.9 | Workflows W-1..W-5 | ✔ | Story 2 acceptance scenarios + Story 5 + Story 6 |
| §6.10 | Acceptance criteria AC-1.1..AC-1.8 | ✔ | Story 2 acceptance scenarios (all 8 preserved verbatim) |
| §7.1 | SCR-02 purpose, entry, exit | ✔ | FR-201 |
| §7.2 | SCR-02 layout | ✔ | FR-202 |
| §7.3 | Action-level fields | ✔ | FR-203 |
| §7.4 | KPI Target subsection structure | ✔ | FR-204 |
| §7.5 | Target-level fields | ✔ | FR-205 |
| §7.6 | Current-score / Baseline label | ✔ | FR-206 + Story 1 AC-2.6 |
| §7.7 | Target activate / deactivate / delete | ✔ | FR-207 + Story 7 |
| §7.8 | Validation catalogue VAL-201..VAL-211 | ✔ | FR-208 (verbatim messages) |
| §7.9 | Edit-mode specifics | ✔ | FR-209 + Story 4 |
| §7.10 | Buttons — SCR-02 | ✔ | FR-210 |
| §7.11 | Workflows W-6..W-9 | ✔ | Story 1 + Story 4 + Story 7 + Story 10 |
| §7.12 | Acceptance criteria AC-2.1..AC-2.13 | ✔ | Story 1 acceptance scenarios (all 13 preserved) + Story 4 + Story 7 |
| §8.1 | SCR-03 purpose & entry | ✔ | FR-301 |
| §8.2 | Header block | ✔ | FR-302 |
| §8.3 | Action Targets list — common row grid | ✔ | FR-303 |
| §8.4 | Row variants — Active | ✔ | FR-304 + FR-305 |
| §8.5 | Row variant — Completed | ✔ | FR-306 |
| §8.6 | Row variant — Planned | ✔ | FR-307 |
| §8.7 | Row variant — deactivated Target | ✔ | FR-308 |
| §8.8 | Buttons — SCR-03 | ✔ | FR-309 |
| §8.9 | Acceptance criteria AC-3.1..AC-3.8 | ✔ | Story 3 acceptance scenarios (all 8 preserved) |
| §9 | Cross-Screen Business Rules BR-001..BR-023 | ✔ | Cross-Screen Business Rules section (all 23 preserved verbatim) |
| §10.1 | Action statuses | ✔ | FR-L01 |
| §10.2 | KPI Target lifecycle | ✔ | FR-L03 |
| §10.3 | Archived status mechanics | ✔ | FR-L04 + Story 6 |
| §10.4 | Superseded decisions (traceability) | ✔ | Referenced under BR-021 (removed features) and Assumptions ratifications; superseded decisions are not repeated as active requirements |
| §11 | Settings — Actions subsection (SET-1, SET-2) | ✔ | SET-1, SET-2, SET-3 + Story 9 |
| §12.1 | M-06 integration | ✔ | INT-01 |
| §12.2 | M-07 forward contract | ✔ | INT-02 |
| §12.3 | M-09 postponed | ✔ | INT-03 |
| §12.4 | Audit trail (F-M15-07) | ✔ | INT-04 |
| §13 | Permissions matrix | ✔ | PERM-01, PERM-02 |
| §14 | Error handling ERR-1..ERR-11 | ✔ | ERR-1 through ERR-11 (verbatim behaviours) |
| §15.1 | Toasts NTF-1..NTF-6 | ✔ | NTF-1 through NTF-6 (verbatim copy) |
| §15.2 | Confirmation dialogs DLG-1..DLG-4 | ✔ | DLG-1 through DLG-4 (verbatim copy) |
| §16 | Non-Functional Requirements NFR-1..NFR-9 | ✔ | NFR-1 through NFR-9 |
| §17 | Dependency & Side-Effect Analysis | ✔ | Dependency & Side-Effect Analysis section |
| §18 | Traceability Matrix (cluster level) | ✔ | Preserved via inline traceability tags [BR]/[HTML]/[Derived from UI] + Assumptions → Dependencies |
| §19 | Ratified Decisions Register R-1..R-16 | ✔ | Assumptions (all 16 preserved) |
| §20 | Open Questions | ✔ | "None" — all ratified per §19; no [NEEDS CLARIFICATION] markers in this spec |
| Appendix A | Data Dictionary | ✔ | Key Entities |

**Coverage summary**: **100 % of SRS sections (§1.1 through §20 + Appendix A) are represented in this specification.** No sections require a separate `/speckit-specify` run. No `[NEEDS CLARIFICATION]` markers remain — the source SRS is explicitly Final for Speckit and every ambiguity was ratified by the stakeholder's 21 Jul 2026 ruling (SRS §19). The single auditor-proposed rule (R-17: last-remaining-Target delete refusal) was **stakeholder-ratified on 22 Jul 2026** — zero open items remain.

---

## Traceability Notes

- Every functional requirement in this spec cites the originating SRS section in brackets, e.g. `[SRS §6.3]`.
- Every cross-screen business rule (BR-001..BR-023) is quoted verbatim from SRS §9 to preserve intent.
- Every validation rule (VAL-201..VAL-211) preserves the exact SRS §7.8 error message wording — those strings are shipped copy.
- Every toast (NTF-1..NTF-6) and dialog (DLG-1..DLG-4) preserves the exact SRS §15 copy — these are shipped copy.
- The measurement-model formulas (Score Progress, Time Progress, outcome bands) are the canonical stakeholder-mandated forms per SRS §3.4/§3.5.
- Removed / superseded features are recorded only under BR-021 (as the negation) and SRS §10.4 (kept in the source SRS for traceability); they are NOT restated as active requirements here.

---

**End of specification.**
