# Feature Specification: Survey & Form Builder (M-01)

**Feature Branch**: `004-survey-form-builder`

**Created**: 2026-07-12

**Status**: Draft

**Input**: User description: "@SRS-M0_2.MD Generate a complete functional specification based strictly on the attached SRS. Treat the SRS as the single source of truth and do not omit any requirement, business rule, workflow, validation, dependency, exception, or acceptance criterion."

**Source SRS**: [SRS-M0_2.MD](../../SRS-M0_2.MD) v6.0 (Draft) — prototype-aligned, screen-by-screen.

**Traceability**: Every FR-x.y / BR-x.y identifier from the SRS is preserved verbatim in the [Functional Requirements](#functional-requirements) and [Business Rules](#business-rules) tables so a downstream `/speckit-plan` can map spec → SRS 1:1.

---

## Overview

Nabadat's **Survey & Form Builder (module M-01)** is the authoring surface of the Voice-of-Customer platform. CX Program Managers, Survey Administrators and analysts use it to compose surveys (sections → standalone questions and Questions Sets), bind KPI questions to journey touchpoints, tailor appearance, translate content, preview across channels, drive the approval/publishing workflow, and consume the per-survey report and analytics that other modules feed.

M-01 owns the **authoring** and the **lifecycle** of a survey; delivery (M-02), response collection (M-04), NLP (M-05), KPI computation (M-06), dashboards (M-07), alerts (M-09), RBAC primitives (M-10), tenant admin & branding (M-11), and journey/stage/touchpoint definitions (M-16) remain external. This spec captures every requirement of the SRS as a system behaviour, without prescribing implementation.

---

## Clarifications

### Session 2026-07-14

- Q: Publish-gate content invariants (Draft/Pending-review → Active) — FR-2.3 lets a survey exist with zero sections/questions at edit time, but the spec was silent on Publish. → A: **Publish requires ≥1 section AND ≥1 question total** (Option A). Any transition to Active on a survey with `sections_count = 0` OR `questions_count = 0` (sum of standalone + Questions Set members across all sections) is rejected by the API (409 `publish.requires_content`) and the Change-status / Publish action is disabled in the UI with a tooltip stating the requirement. This applies to every entry into Active (Draft → Publish by P-01, Draft → Publish via "Publish own surveys" grant, Pending-review → Publish). Codified as **BR-1.7**; see the Status Transition Matrix (content gate note), Error Handling & Notifications (Publish-gate error payload), and the Independent Test for US1 (already assumes ≥1 question at Active).
- Q: When an Active survey is Returned to Draft (BR-1.5), edited, then re-Published, what happens to responses collected during the previous Active period? (Q6 re-opened) → A: **Destructive edit — warn + purge + invalidate in-flight sessions** (Option D, new to this spec). When P-01 initiates Return-to-Draft on an Active (or Paused) survey with the intent to edit, a **blocking alert** warns that all prior responses will be permanently deleted; on confirmation, M-01 (a) **hard-deletes every response** attached to the survey — live in-period responses **and** any M-07 post-expiry store rows for this survey — (b) **invalidates every open, in-flight respondent session** so no late submit can land, and (c) transitions the survey to Draft with a **zero response count**. When the survey is later re-Published, its report/analytics start from an empty response set. The Survey entity therefore needs **no `version` field** — there is never more than one Active period's worth of responses in the survey's history. Codified as **BR-1.6**; see the Status Transition Matrix (destructive marker on `Active → Return to Draft`), Edge Cases (new bullet), and Error Handling & Notifications (new blocking confirmation modal).

### Session 2026-07-13 (second pass)

- Q: Reviewer notification fanout on Submit-for-Review (FR-15.2) → A: **Broadcast to every user holding the review/publish permission** (default: every P-01 in the tenant) (Option A). M-01 emits a single fan-out event to M-09; whichever qualifying reviewer acts first performs the Publish or Return-to-draft. There is no reviewer-assignment field on Survey and no first-claim lock. Individual notification lifecycle (read/dismiss/dedupe) is M-09's concern.
- Q: Scope of P-03's "(own drafts)" edit right in the Permissions table (§Permissions & Roles) → A: **Team-owned — any P-03 in the tenant can edit any Draft authored by any other P-03** (Option A). "Own drafts" is read at the **tenant-team level**, not at the individual-author level: P-03 is treated as a collaborative Survey-Administrator role whose members share editing rights across all P-03-authored Drafts. **Guardrails**: (i) every edit is still audited to the individual acting P-03 (BR-1.2); (ii) the "Publish own surveys" M-10 grant remains **per-individual** — it applies only to surveys the granted user personally authored (not to any P-03's draft), so team-editing does not become team-publishing; (iii) concurrent edits are resolved by the ETag conflict flow from Q1 (a stale ETag returns 412 and the UI surfaces a conflict dialog). P-01 retains full edit rights over any Draft.

### Session 2026-07-13

- Q: Autosave cadence for the builder & Settings screen (NFR-5, Q1) → A: **Explicit Save only + unsaved-changes guard on navigation** (Option C). No autosave; edits persist only when the author clicks Save; navigating away with unsaved edits shows a blocking confirmation. Concurrency handled via optimistic locking (`If-Match` ETag) on write endpoints when a second editor is possible (e.g., P-01 editing a Pending-review survey submitted by P-03).
- Q: Post-expiry response retention in the M-07 store (BR-3.1, Q2) → A: **Indefinite retention, subject to tenant-level data-retention policy in M-11** (Option A). M-01 sets no independent retention window; late responses live in the M-07 post-expiry store until the tenant's M-11 retention policy purges them.
- Q: Sanitiser allowlist for the welcome / thank-you rich-text `</> HTML` source toggle (FR-3.2, Q3) → A: **Full HTML5 subset except `<script>`, DOM event-handler attributes (`on*`), and `javascript:` URLs**; sanitisation runs at server ingress on every save (Option A). `<iframe>` remains disallowed by default; if a tenant needs embeds, a follow-up permission grant is required — this spec does not enable them. A battle-tested sanitiser (e.g. equivalent to the OWASP Java HTML Sanitizer / DOMPurify ruleset) is mandatory; the allowlist is auditable and versioned so that any expansion is a deliberate change.
- Q: Template ↔ instantiated survey relationship on template delete (FR-6.3 / FR-7.4) → A: **Snapshot copy, no link** (Option B). Instantiation copies all template data (settings, appearance, sections/sets/questions, KPI links, journey/stage/touchpoint bindings) into a new, independent Survey row; the Survey stores no foreign-key reference back to the Template. Deleting (or editing) a customized template therefore leaves already-instantiated surveys untouched. There is no cascade and no orphan-reference risk.
- Q: When does a flip of the tenant-level `post-expiry feedback collection` setting take effect for already-Active / already-expired surveys? (BR-3.1) → A: **Evaluated per response at M-04** (Option A). The setting is a **live** tenant policy: M-04 reads its current value at the moment each incoming post-expiry response is received; there is no snapshot on the Survey row. Flipping the setting therefore takes effect **immediately** for every survey across the tenant — the previously-Active/expired surveys included. In-flight responses that cross the expiry boundary remain rejected regardless of the setting (BR-3.1, unchanged).

---

## User Scenarios & Testing *(mandatory)*

> Every user story below is a self-contained slice of the module. Each is independently deployable — an author using only US1 already has a functioning single-page single-section survey; every subsequent story layers on. Priorities are ordered by "smallest viable surface first" (US1 = MVP; US8/US9 are read-only consumers of aggregates owned by other modules).

### User Story 1 — Author, save and publish a basic survey (Priority: P1)

A **CX Program Manager (P-01)** or **Survey Administrator (P-03)** opens the **Survey Library** (F1), starts a new survey via the build-method chooser (F5), completes the **Survey Settings** screen (F3) — English name, optional journey binding, welcome/thank-you messages, layout, active period, appearance (F4) — enters the **Builder** (F8), adds at least one **section** with standalone questions (all seven answer types + KPI metric type, each with the correct sub-type), saves, and (via the approval workflow of US2 or the self-publish grant) sets the survey **Active** so distribution rules in M-02 can begin sending it.

**Why this priority**: Without this surface the module produces nothing. Every other story assumes a saved survey exists.

**Independent Test**: A tester with P-01 credentials creates a survey named "Post-visit satisfaction", picks the "Branch Visit" journey, adds one Scale question and one KPI (CSAT) question bound to a Stage → Touchpoint, sets the survey Active, and verifies that (a) the library lists the survey with the right Type/Journey/Status, (b) the row-click deep-link opens Survey Settings pre-filled, and (c) an audit-log entry is written to M-11.

**Acceptance Scenarios**:

1. **Given** the Survey Library is open, **When** the manager clicks **Add Survey**, **Then** the F5 build-method chooser appears **first** with three paths, and the F3 Survey Settings screen is shown only **after** a build method is picked — **From scratch** → Settings → Builder; **From a template** → Template Picker → Settings → Builder; **Build with AI** → AI flow → Settings → Builder (see [Survey Creation Flow](#survey-creation-flow-authoritative)).
2. **Given** an empty Survey Settings screen, **When** the manager fills only the English name and clicks Continue, **Then** the survey is saved as Draft with survey_type = "Seasonal / Relational" (no journey bound).
3. **Given** the settings screen with a bound journey chosen, **When** the manager saves, **Then** survey_type is set to "Transactional" automatically and the KPI-question stage/touchpoint dropdowns are seeded from that journey.
4. **Given** the builder with layout = "One page per section", **When** the manager switches the layout to "One question per page", **Then** a warning is shown that the layout may disturb the respondent experience.
5. **Given** any question card, **When** the manager toggles **Show comments field**, **Then** the card shows the "Comments field" badge and the response payload will carry that comment forward to M-05.
6. **Given** a Text or Paragraph input question, **When** the manager toggles **Apply sentiment analysis**, **Then** the question is flagged for M-05 sentiment scoring; the toggle has no effect and is not shown for non-text types.
7. **Given** a KPI question in a Transactional survey, **When** the manager sets Bound journey ON, picks a Stage and Touchpoint, **Then** the card shows the "KPI · Stage → Touchpoint" badge and the binding is persisted.
8. **Given** a KPI question, **When** the manager sets Bound journey OFF, **Then** Stage and Touchpoint are disabled and no touchpoint binding is stored (BR-8.2).
9. **Given** the library, **When** the manager clicks a survey row outside the action controls, **Then** Survey Settings opens for that survey pre-filled (FR-1.5); row action buttons / overflow items never trigger the row navigation (FR-1.6).
10. **Given** an Active survey with ≥ 1 connected distribution rule, **When** the manager selects **Paused**, **Then** a blocking confirmation shows the exact rule count and explains that the rules are preserved but stop sending until reactivation (FR-1.10 / FR-1.12).
11. **Given** an Active survey with 0 rules, **When** Paused is selected, **Then** the status changes immediately with no rules alert.
12. **Given** an **Archived** survey, **When** the manager opens Change status, **Then** only **Unarchive → Draft** is offered — Active/Paused are not (FR-1.14 / BR-1.3).
13. **Given** any list action, **When** the manager performs it, **Then** an audit-log entry is written to M-11 attributed to the actor with a timestamp (BR-1.2).
14. **Given** the creation flow **before** the first Continue, **When** the manager clicks **Cancel**, **Then** no Survey row is persisted and the Library reopens; **and given** the manager already pressed **Continue** once, **When** they later Cancel, **Then** the already-persisted Draft remains listed (FR-5.5, FR-5.6).
15. **Given** any step after the chooser, **When** the manager clicks **Back**, **Then** the previous step reopens with previously entered data intact (FR-5.6).
16. **Given** a KPI question with Bound journey ON and a Stage chosen but no Touchpoint, **When** the manager saves, **Then** the binding is valid and persisted at journey/stage level — Touchpoint is optional (FR-8.4).
17. **Given** a KPI question already bound to a Touchpoint, **When** the manager changes the KPI, **Then** the Touchpoint is kept if still valid for the new KPI + Journey + Stage and cleared otherwise (BR-8.5).

**Unit Test Coverage** *(backend-bearing)*:

- **Units under test**: `SurveyValidator`, `SurveyTypeSyncService` (journey↔type invariant), `StatusTransitionPolicy`, `RulesCountProjection`, `AuditWriter` port, `QuestionValidator` (per-type + sub-type invariants), `KpiBindingValidator`, `CommentFieldFlagPolicy`, `SentimentFlagPolicy`.
- **Required cases**:
  - `SurveyValidator.Validate(new SurveyDraft { NameEn = "" }) → Invalid("survey.name_en.required")`.
  - `SurveyValidator.Validate(new SurveyDraft { NameEn = "Post-visit", BoundJourney = null }) → Valid, SurveyType = "SeasonalRelational"`.
  - `SurveyTypeSyncService.OnBoundJourneyChanged(survey, journeyId) → survey.SurveyType = "Transactional"` (BR-3.3).
  - `SurveyTypeSyncService.OnBoundJourneyChanged(survey, null) → survey.SurveyType = "SeasonalRelational"`.
  - `StatusTransitionPolicy.Allowed(current: "Archived", next: "Active") → false`; `Allowed(current: "Archived", next: "Draft") → true` (BR-1.3, FR-1.14).
  - `StatusTransitionPolicy.Allowed(current: "Draft", next: "Active") → false when survey has unpublished pending review` (Section 3.15 lock).
  - `KpiBindingValidator.Validate(kpi: "CSAT", boundJourneyOn: true, stage: null, touchpoint: "TP-1") → Invalid("kpi.touchpoint.requires_stage")`.
  - `KpiBindingValidator.Validate(kpi: "CSAT", boundJourneyOn: false, stage: "S1", touchpoint: "T1") → Warn+Strip("kpi.binding_ignored_when_bound_journey_off")` (BR-8.2).
  - `KpiBindingValidator.Validate(kpi: "CSAT", boundJourneyOn: true, stage: "S1", touchpoint: null) → Valid` (Touchpoint optional, FR-8.4).
  - `KpiBindingChangePolicy.OnKpiChanged(question, newKpi) → retains Touchpoint if valid for new KPI+Journey+Stage, else clears; clears Stage if invalid for new KPI` (BR-8.5).
  - `QuestionValidator.Validate(type: Scale, subType: null) → Invalid("question.subtype.required")` (FR-8.8).
  - `QuestionValidator.Validate(type: Scale, subType: "slider", sliderSteps: 0) → Invalid("scale.slider.steps.min")`.
  - `SentimentFlagPolicy.Apply(question: { type: SingleSelect, sentiment: true }) → Warn("sentiment.ignored_for_non_text")` (FR-8.11).
  - `CommentFieldFlagPolicy.Apply(question: { comments: true }) → HasCommentField = true, CommentRequired = false, CommentMaxLength = 200, CommentLabel = "Comments" (translatable), CommentTravelsToNlp = true` (FR-8.9).

**Integration Test Coverage** *(backend-bearing)*:

- **What gets tested end-to-end**:
  - `POST /api/surveys` creates a draft, returns 201, persists status = Draft; `POST /api/surveys/{id}/status {"to":"Active"}` transitions Draft → Active only when caller holds review permission (or grant), and the rules-count projection is refreshed.
  - `POST /api/surveys/{id}/status {"to":"Paused"}` returns 409 + `pause.requires_rules_confirmation` payload when `rules_count > 0` and `?confirm=false`; returns 200 when `?confirm=true`, and emits `survey.paused` event.
  - `POST /api/surveys/{id}/status {"to":"Active"}` from Archived returns 409 (`archived.only_unarchive_allowed`); `{"to":"Draft"}` from Archived returns 200 (unarchive).
  - `GET /api/surveys` filters by Type/Status/Journey combine (AND) with the search term over English name (and tag on Templates tab); "no results" state returned when empty.
  - `POST /api/surveys/{id}/questions` with a KPI question containing `{stage: null, touchpoint: "T1"}` returns 400 (`kpi.touchpoint.requires_stage`).
  - Row click deep-link route `GET /api/surveys/{id}` returns settings payload used by the F3 screen.
- **What's intentionally NOT covered end-to-end**: validator pure-logic cases enumerated above — verified by `SurveyValidatorTests` / `QuestionValidatorTests` / `KpiBindingValidatorTests`.

**Scenario Test**:

- `scenario-test: SurveyLifecycleFromDraftToActiveScenarioTests` — walks the full journey: **create Draft → add section → add question with KPI binding → set Active (as P-01) → verify Active count in library + audit-log entries emitted (`survey.created`, `survey.status.changed`)**.

**E2E Test Coverage** *(page-bearing frontend)*:

- **User flows under test**: `SurveyLibraryTests`, `SurveyBuildMethodTests`, `SurveySettingsTests`, `SurveyAppearanceTests`, `SurveyBuilderTests` (five test classes under `tests/Nabadat.E2ETests/SurveyBuilder/`).
- **Required scenarios**:
  - Library — happy path: filters + row click open Settings; unauthorized user (P-02) sees read-only badges only.
  - Library — pause confirmation modal renders exact rule count and blocks until Confirm.
  - Library — Archived row shows only "Unarchive" in the status menu.
  - Build method — chooser precedes Settings for new; bypassed on edit/clone.
  - Settings — required-field validation for English name; layout warning modal appears on "one question per page" switch.
  - Appearance — Inherited mode locks all controls; Customize unlocks; live preview updates within 100 ms of a change.
  - Builder — palette shows the 7 answer types in the specified order + KPI under "Metric"; drag-and-drop moves questions between sections; sentiment toggle hidden for non-text types.
  - Auth redirect — signed-out user hitting `/surveys` is redirected to `/login`; P-02 hitting builder controls sees them disabled with an aria-label.

---

### User Story 2 — Approval & publishing workflow (Priority: P1)

A **Survey Administrator (P-03)** finishes a Draft, submits it for review. The survey enters **Pending review**, becomes read-only for P-03, and a notification (via M-09) is delivered to the **CX Program Manager (P-01)** who is deep-linked to the survey's Settings screen. P-01 either **Publishes** (status → Active, ready for M-02 rules) or **Returns to draft** with remarks. An M-10 grant ("Publish own surveys") lets qualified P-03 users skip the review step for their own surveys.

**Why this priority**: The workflow is the governance gate — without it, no P-03 survey can reach customers even after US1 is complete. It is P1 because it directly gates the "Active" transition.

**Independent Test**: A P-03 tester creates a Draft, submits it → verifies the survey enters Pending review, editors become read-only, an M-09 notification is emitted, the deep link opens the Settings screen for a P-01 tester who publishes → status becomes Active, audit log carries submit + publish entries with actor, timestamp, remarks.

**Acceptance Scenarios**:

1. **Given** a Draft owned by P-03, **When** P-03 submits, **Then** status → Pending review, edit controls are disabled and a "Pending review" banner is shown to P-03 (FR-15.1, BR-15.1).
2. **Given** a survey enters Pending review, **When** the transition completes, **Then** M-09 fires a notification to all users holding the review/publish permission and the notification deep-links to the survey's F3 Settings screen (FR-15.2).
3. **Given** a survey in Pending review, **When** M-02 rules would fire, **Then** they do not send it and it collects zero responses (FR-15.4).
4. **Given** P-01 opens the notification, **When** they click **Publish** in Settings, **Then** status → Active and the audit log records the actor, timestamp and any remarks (FR-15.3, FR-15.6).
5. **Given** P-01 clicks **Return to draft** with remarks "Fix Arabic name", **Then** status → Draft, P-03 regains edit rights, and the remarks are visible in the audit log (FR-15.3).
6. **Given** P-03 holds the "Publish own surveys" grant, **When** they save a Draft, **Then** a **Publish** action is available directly to them, the submit-for-review step is skipped and no reviewer notification is emitted (FR-15.5, BR-15.2, scenario "Self-publish grant").
7. **Given** a survey in Pending review, **When** P-01 opens it, **Then** P-01 can edit before publishing (BR-15.1); P-03 remains locked.
8. **Given** a Draft owned by P-01, **When** P-01 publishes it directly, **Then** status → Active without a Pending-review step (Status Transition Matrix, "Draft → Publish").
9. **Given** an Active survey, **When** P-01 chooses to edit its content, **Then** the platform first requires a **Return to Draft** (BR-1.5); the survey is not editable while Active.

**Unit Test Coverage** *(backend-bearing)*:

- **Units under test**: `ApprovalStateMachine`, `EditLockPolicy`, `PublishAuthorizationService`, `ReviewNotificationBuilder`, `AuditEventFactory`.
- **Required cases**:
  - `ApprovalStateMachine.Submit(draft, actorRole: "P-03") → transitions Draft → PendingReview and returns SubmitOutcome { NotificationTo = <reviewers>, EditLockOwner = "P-03" }`.
  - `ApprovalStateMachine.Publish(pendingReview, actorRole: "P-03", grant: null) → Forbidden`.
  - `ApprovalStateMachine.Publish(pendingReview, actorRole: "P-03", grant: "PublishOwnSurveys", ownerId: sameAsActor) → Active` (FR-15.5).
  - `ApprovalStateMachine.Publish(pendingReview, actorRole: "P-01") → Active`.
  - `ApprovalStateMachine.ReturnToDraft(pendingReview, actorRole: "P-01", remarks: "Fix Arabic") → Draft, RemarksPersisted = true`.
  - `EditLockPolicy.CanEdit(user: P-03, survey: { status: PendingReview, submittedBy: P-03 }) → false` (BR-15.1).
  - `EditLockPolicy.CanEdit(user: P-01, survey: { status: PendingReview }) → true` (BR-15.1).
  - `PublishAuthorizationService.Authorize(actor, survey) → Forbidden` for `P-03` without grant on their own draft in `Draft` state (must submit first).

**Integration Test Coverage** *(backend-bearing)*:

- **What gets tested end-to-end**:
  - `POST /api/surveys/{id}/submit` transitions Draft → PendingReview, `event_log` gets `survey.submitted_for_review`, and an M-09 notification event is emitted.
  - `POST /api/surveys/{id}/publish` returns 403 when caller is P-03 without the grant on their own draft.
  - `POST /api/surveys/{id}/publish` succeeds for P-01 and status becomes Active.
  - `POST /api/surveys/{id}/return-to-draft` with `{ remarks: "..." }` returns 200, status → Draft, remarks in audit log.
  - `PUT /api/surveys/{id}` returns 403 for P-03 on their own survey while in PendingReview.
- **What's intentionally NOT covered end-to-end**: state-machine pure-logic — verified by `ApprovalStateMachineTests`.

**Scenario Test**:

- `scenario-test: SurveyApprovalWorkflowScenarioTests` — walks: **P-03 saves Draft → P-03 submits → M-09 notification emitted to P-01 → P-01 lands on Settings deep-link → P-01 publishes → survey Active + audit trail complete**. Also exercises the self-publish-grant variant.

**E2E Test Coverage** *(page-bearing frontend)*:

- **User flows under test**: `SurveyApprovalTests.cs` under `tests/Nabadat.E2ETests/SurveyBuilder/`.
- **Required scenarios**:
  - Standard review (SRS "Key scenarios"): P-03 submits → sees Pending banner → P-01 receives notification → deep-links to Settings → publishes → status becomes Active in the library.
  - Locked while pending: P-03 opens the survey, editors are read-only, banner visible.
  - Self-publish grant: with the grant, P-03 sees Publish directly and completes the flow without a reviewer notification.
  - Return-to-draft: P-01 returns with remarks; P-03 gets back to Draft and remarks appear in the audit view.

---

### User Story 3 — Sections + rotating Questions Sets with low-response ordering (Priority: P2)

A survey author structures a large question bank into **sections**, each holding standalone questions and one or more **Questions Sets** (rotating pools). Each Set defines a **selection mode** (Random / Prioritize low-response) and a **per-sending count** — the render/collection layer (M-02/M-04) later serves only that subset to each respondent. When "Prioritize low-response" is in effect, the platform surfaces the survey-wide lowest-response section first, so coverage evens out over time.

**Why this priority**: Sections and standalone questions are enough for an MVP (US1). Questions Sets and low-response ordering are the next-most-important structural feature; every larger bank needs them, but a smaller survey can ship without.

**Independent Test**: A tester creates a survey with two sections; adds a Questions Set of 10 questions to section 1 with `selection_mode = "low_response"` and `count = 3`; verifies the set stores the config and the library structure view (F2) shows "shows 3 of 10". A rendered-selection test (in a fake M-02/M-04 harness) walks the low-response algorithm and asserts the section order matches FR-10.4.

**Acceptance Scenarios**:

1. **Given** a survey, **When** the author opens the library structure view (F2), **Then** it lists sections, standalone question counts and Questions Sets with "shows ‹k› of ‹n›" and the selection mode (FR-2.1).
2. **Given** a survey with one section, **When** the author deletes the last section, **Then** the deletion succeeds (after the appropriate confirmation) and the survey is left with no sections (FR-2.3).
3. **Given** a Questions Set with `count = 5` in a set of 10, **When** the config is saved, **Then** the persisted selection configuration is retrievable by M-02/M-04 at send time (FR-10.3).
4. **Given** three sections with Prioritize low-response enabled, **When** the low-response ordering algorithm runs at send time, **Then** the section holding the survey-wide lowest-response question is presented first, the next-lowest section second, and so on (FR-10.4).
5. **Given** a Questions Set, **When** the author edits its settings from either F2 (library structure) or the builder set menu, **Then** both entry points persist the same underlying record (FR-10.2).
6. **Given** the builder, **When** the author drags a question from section A into a Questions Set inside section B, **Then** the move persists including its new section_id / set_id / order (FR-8.2).
7. **Given** a Questions Set, **When** the author sets `count` > number of questions in the set, **Then** the value is rejected (`count ≤ set size`).
8. **Given** a section with 3 standalone questions and a set of 5, **When** M-04 selects for a respondent with `count = 2`, **Then** all 3 standalone questions plus 2 sampled set questions are served (BR-8.4).
9. **Given** a non-empty section, **When** the author deletes it, **Then** a destructive confirmation lists the standalone questions and Questions Sets that will be deleted, and deletion happens only on explicit confirmation (FR-2.5).
10. **Given** a non-empty Questions Set, **When** the author deletes it, **Then** a destructive confirmation lists the questions that will be deleted (FR-2.6).
11. **Given** a question referenced as a routing target, **When** it is deleted, **Then** routes pointing to it reset to the next-question default (FR-2.7) and its translations are removed in every locale (FR-2.8).

**Unit Test Coverage** *(backend-bearing)*:

- **Units under test**: `SectionValidator`, `QuestionsSetValidator`, `LowResponseOrderingService`, `SectionDeletionGuard`, `QuestionMoveService`.
- **Required cases**:
  - `SectionDeletionGuard.CanDelete(survey.Sections.Count == 1) → true` (last section is deletable, FR-2.3).
  - `SectionDeletionService.Delete(nonEmptySection, confirmed: false) → Blocked("section.delete.requires_confirmation")`; `Delete(nonEmptySection, confirmed: true) → cascades all standalone questions and sets` (FR-2.5).
  - `QuestionDeletionService.Delete(q) → resets inbound routing targets to next-question default (FR-2.7) and purges all-locale translations (FR-2.8)`.
  - `QuestionsSetValidator.Validate(new Set { Count = 6, Questions.Count = 5 }) → Invalid("questionsset.count.exceeds_size")`.
  - `LowResponseOrderingService.OrderSections(sections, responseCounts) → [<section with lowest question first>, …]` for a fixture with three sections whose lowest-response questions are (7, 4, 12) → order = [section2 (4), section1 (7), section3 (12)] (FR-10.4).
  - `LowResponseOrderingService.WithinSet.PickCandidates(set, count: 3, responseCounts) → 3 least-answered eligible questions`.
  - `QuestionMoveService.Move(from: sectionA, to: setB, order: 2) → persistsAllFields(section_id, set_id, order)`.

**Integration Test Coverage** *(backend-bearing)*:

- **What gets tested end-to-end**:
  - `POST /api/surveys/{id}/sections` creates a section; `DELETE …/sections/{sid}` deletes any section including the last (200), cascading its questions and sets after confirmation.
  - `POST /api/surveys/{id}/sections/{sid}/sets` creates a Questions Set; `PATCH …/sets/{setId}` updates title/mode/count.
  - `POST /api/surveys/{id}/questions/{qid}/move` moves a question across section/set boundaries; order is compact and unique per parent.
  - `GET /api/surveys/{id}/render-plan?respondentId=…` (server-side selection endpoint feeding M-02/M-04) returns the correct low-response order for a fixture with three sections.
- **What's intentionally NOT covered end-to-end**: pure-algorithm cases — `LowResponseOrderingServiceTests`.

**Scenario Test**:

- `scenario-test: QuestionsSetLowResponseOrderingScenarioTests` — walks: **create survey → add 3 sections each with a Set → seed response counts via test fixture → request render-plan → assert survey-wide-lowest section is served first**.

**E2E Test Coverage** *(page-bearing frontend)*:

- **User flows under test**: `SectionsAndSetsTests.cs` under `tests/Nabadat.E2ETests/SurveyBuilder/`.
- **Required scenarios**:
  - F2 structure view lists sections + sets with live "shows k of n"; Add section / Add set / Delete work.
  - Delete last section succeeds after confirmation; deleting a non-empty section shows a destructive confirmation listing the cascaded questions and sets.
  - Builder drag-and-drop reorders sections, sets and questions; order persists on reload.
  - Auth redirect for signed-out user; empty state when a new section has no questions.

---

### User Story 4 — Answer routing / skip logic (Priority: P2)

A survey author enables **Question routing** from the builder header. Because routing advances one question at a time it is available **only** with the "One question per page" layout; enabling it disables (and locks) question shuffling. Eligible question types (Single select, Scale — **except the Slider sub-type**, Yes/No, KPI) that are **standalone** (not inside a Questions Set, FR-9.5) expose a routing editor; each answer is mapped to a next standalone question or "End survey" (default = the next question in order).

**Why this priority**: Routing is a widely used but not-strictly-necessary feature. Without it a survey can still ship as a flat sequence.

**Independent Test**: A tester enables the one-question-per-page layout, toggles Question routing on, confirms the shuffle-disabled prompt, opens the routing editor on a KPI question, sets "Score = 1" → "End survey", saves; a preview run answering "1" jumps directly to the thank-you screen.

**Acceptance Scenarios**:

1. **Given** any layout other than "one question per page", **When** the author tries to enable routing, **Then** the toggle is disabled with a tooltip explaining the layout requirement (FR-9.1).
2. **Given** the correct layout, **When** the author enables routing, **Then** a confirmation modal appears stating that shuffling will be disabled; on confirm, shuffle is turned off and locked (FR-9.1, key scenario "Enable + confirm").
3. **Given** routing is on, **When** the author switches layout back away from one-question-per-page, **Then** routing is turned off automatically (FR-9.1).
4. **Given** routing is on, **When** the author disables it, **Then** shuffle becomes available again (FR-9.1).
5. **Given** an eligible question card, **When** routing is on, **Then** the card exposes an answer-routing button next to delete; when at least one route is set, a "Routing set" badge is rendered (FR-9.2).
6. **Given** the routing editor for a Single select with 4 options, **When** it opens, **Then** it lists 4 rows ("Answer 1: ‹label›"…), each Go-to defaulting to the immediately following question and offering every subsequent question + "End survey" (FR-9.3).
7. **Given** a saved routing map, **When** the editor is reopened, **Then** previously chosen targets are restored (FR-9.4).
8. **Given** a KPI question routed such that Score 1 → End survey, **When** a respondent selects 1 in preview, **Then** the survey ends immediately (SRS key scenario "Route an answer").
9. **Given** an eligible question **inside a Questions Set**, **When** routing is on, **Then** it exposes no routing control and is not offered as a routing target (FR-9.5).
10. **Given** a Scale question in the **Slider** sub-type, **When** routing is on, **Then** it is not routable (Question Type Catalogue).

**Unit Test Coverage** *(backend-bearing)*:

- **Units under test**: `RoutingEligibilityService`, `RoutingConflictDetector`, `RoutingDefaultTargeter`, `LayoutRoutingCoupler` (mutual-exclusion enforcement).
- **Required cases**:
  - `RoutingEligibilityService.IsEligible(question: { type: MultiSelect }) → false`.
  - `RoutingEligibilityService.IsEligible(question: { type: Scale }) → true`.
  - `RoutingEligibilityService.IsEligible(question: { type: Scale, subType: "slider" }) → false` (Question Type Catalogue).
  - `RoutingEligibilityService.IsEligible(question: { type: SingleSelect, inSet: true }) → false` (FR-9.5).
  - `LayoutRoutingCoupler.OnLayoutChanged(survey, next: "single_page") → survey.RoutingOn = false`.
  - `LayoutRoutingCoupler.OnRoutingEnabled(survey) → survey.ShuffleOn = false, survey.ShuffleLocked = true`.
  - `RoutingConflictDetector.Detect(routes) → CycleDetected` when a route points back to a prior question.
  - `RoutingDefaultTargeter.Default(question, nextInOrder) → nextInOrder.Id`.

**Integration Test Coverage** *(backend-bearing)*:

- **What gets tested end-to-end**:
  - `POST /api/surveys/{id}/routing` returns 409 when layout ≠ `question`.
  - `POST /api/surveys/{id}/routing` toggling on returns 200 + `shuffleLocked=true` in the response payload.
  - `PUT /api/surveys/{id}/questions/{qid}/routing` persists the per-answer map; `GET` returns it verbatim.
- **What's intentionally NOT covered end-to-end**: eligibility/coupling pure-logic — `RoutingEligibilityServiceTests`, `LayoutRoutingCouplerTests`.

**Scenario Test**:

- `scenario-test: not-needed — Independent Test is a single toggle + a single per-answer save; each is covered by its own endpoint test in EndpointTests.`

**E2E Test Coverage**:

- **User flows under test**: `RoutingTests.cs` under `tests/Nabadat.E2ETests/SurveyBuilder/`.
- **Required scenarios**:
  - Toggle disabled unless layout = one-question-per-page (tooltip visible).
  - Confirmation modal appears; Cancel returns to previous state; Confirm disables/locks shuffle.
  - Routing editor lists one row per answer with correct default target ("next question").
  - "Routing set" badge appears on cards with a saved map.
  - Preview run for the "Score 1 → End survey" scenario ends the survey when 1 is chosen.

---

### User Story 5 — Templates: built-in library and tenant-authored (Priority: P2)

Authors can start a new survey from a **template** (F6) or save an existing survey as a **customized template** (F7). Built-in templates are platform-curated per sector and locked; customized templates are the tenant's own with editable tags. A template stores **all** of the source survey's data — collection behaviour, appearance, welcome/thank-you messages, sections/sets/questions, KPI links **and** journey/stage/touchpoint bindings — and **copies all of it** when a new survey is created from it.

**Why this priority**: Templates accelerate authoring but are not required for a first survey to exist.

**Independent Test**: A P-01 tester saves a survey as a template, verifies (a) the template appears in the Templates tab, (b) its "Use this template" opens the builder with the same questions, settings **and** journey/stage/touchpoint bindings copied, and (c) editing the template does not affect the source survey.

**Acceptance Scenarios**:

1. **Given** the Templates tab, **When** it opens, **Then** customized templates are listed first, then built-in cards; built-in cards show sector chips + padlock; customized cards show tag chips (FR-6.1).
2. **Given** the Templates tab, **When** the manager types "onboarding", **Then** templates whose name or tags contain "onboarding" remain listed (FR-6.2, key scenario "Tag search").
3. **Given** a built-in template, **When** the manager clicks Edit, **Then** the platform surfaces a "cannot edit built-in templates" notice (FR-7.1).
4. **Given** a customized template, **When** the manager clicks Edit questions, **Then** the builder opens in template context with its questions loaded (FR-7.2).
5. **Given** a survey with a bound journey, **When** the manager saves it as a template, **Then** the template snapshot includes **all** data — settings, appearance, messages, sections, sets, questions, KPI links **and** journey/stage/touchpoint bindings (FR-7.4).
6. **Given** a template, **When** the manager clicks **Use this template**, **Then** a new survey is created pre-loaded with **all** of the template's data, **including** its journey/stage/touchpoint bindings, copied as-is (FR-6.3).
7. **Given** the template picker, **When** the manager clicks **Preview**, **Then** the template's survey can be previewed without creating a survey (FR-6.4).
8. **Given** the template authoring form, **When** it is shown, **Then** Class and Primary sector inputs are **not** shown as authoring fields (they persist only as list filter facets for built-in templates) (FR-7.3).

**Unit Test Coverage** *(backend-bearing)*:

- **Units under test**: `TemplateSnapshotBuilder`, `TemplateAuthorizationService`, `TemplateSearchIndexer`, `TemplateInstantiator`.
- **Required cases**:
  - `TemplateSnapshotBuilder.Build(survey) → snapshot includes {journeyId, stageId, touchpointId} on every question` (copy-all).
  - `TemplateAuthorizationService.CanEdit(template: { class: "BuiltIn" }, actor: P-01) → false` (FR-7.1).
  - `TemplateSearchIndexer.Match(term: "onboarding", template: { name: "Onboarding pulse", tags: [] }) → true`; `Match("onboarding", { name: "Post-visit", tags: ["Onboarding"] }) → true` (FR-1.2).
  - `TemplateInstantiator.CreateSurveyFrom(template) → survey with same settings, questions, appearance **and** journey/stage/touchpoint bindings`.

**Integration Test Coverage** *(backend-bearing)*:

- **What gets tested end-to-end**:
  - `POST /api/templates` from a survey returns 201 with all data captured (bindings included).
  - `POST /api/templates/{tid}/instantiate` returns a new survey id whose questions and journey/stage/touchpoint bindings match the template exactly.
  - `PATCH /api/templates/{tid}` returns 403 on a built-in template.
  - `GET /api/templates?search=onboarding` matches name or tag.

**Scenario Test**:

- `scenario-test: TemplateCreateAndInstantiateScenarioTests` — walks: **P-01 creates survey with journey binding → saves as template → instantiates new survey → asserts settings/appearance/questions AND journey/stage/touchpoint bindings all carried**.

**E2E Test Coverage**:

- **User flows under test**: `TemplatesTests.cs`.
- **Required scenarios**:
  - Template picker orders customized-first; built-in cards show padlock + sector chips.
  - Tag search filters correctly.
  - Preview opens without creating a survey.
  - Edit disabled for built-in with a notice.

---

### User Story 6 — Translate workspace (Priority: P2)

An author (or a specialist localiser) opens the **Translate workspace** to complete the survey's Arabic name and all localisable strings (welcome / thank-you, question text and descriptions, option labels, scale labels, reason items). Arabic renders full RTL; source and target sit side by side.

**Why this priority**: Bilingual output is a platform differentiator but a survey can ship English-only and be usable — so translation is P2, not P1.

**Independent Test**: A tester opens the Translate workspace on a survey; enters Arabic values for the name, welcome, one option label and one scale label; the preview renders these RTL and the report's localised string uses the Arabic value.

**Acceptance Scenarios**:

1. **Given** the workspace, **When** it opens, **Then** it exposes every localisable string: survey Arabic name, welcome & thank-you, question text/description, option labels, scale labels, reason items (FR-11.1).
2. **Given** English is authored on the build form, **When** the workspace opens, **Then** English is shown as source and Arabic (and any other locale) is authored here (FR-11.2).
3. **Given** the workspace, **When** the target locale is Arabic, **Then** the target column renders full RTL (FR-11.3).
4. **Given** the Survey Settings screen, **When** the author saves without an Arabic name, **Then** the save succeeds — only English is required (BR-3.2).

**Unit Test Coverage** *(backend-bearing)*:

- **Units under test**: `TranslationBundleBuilder`, `LocaleFallbackPolicy`, `TranslatableStringExtractor`.
- **Required cases**:
  - `TranslatableStringExtractor.Extract(survey) → bundle` with keys covering: nameEn/nameAr, welcome, thanks, per-question text/description/options/scale-labels/reason-items.
  - `LocaleFallbackPolicy.Resolve(bundle, locale: "ar", key: "welcome") → English fallback when Arabic missing` (implied by "translations may be completed later", BR-3.2).

**Integration Test Coverage** *(backend-bearing)*:

- **What gets tested end-to-end**:
  - `GET /api/surveys/{id}/translations?locale=ar` returns the bundle; missing keys resolve to English.
  - `PUT /api/surveys/{id}/translations/ar` persists the Arabic values and echoes them on the next GET.

**Scenario Test**:

- `scenario-test: not-needed — the workspace is a single-endpoint round-trip.`

**E2E Test Coverage**:

- **User flows under test**: `TranslateTests.cs`.
- **Required scenarios**:
  - Workspace lists every localisable string with side-by-side source/target.
  - Arabic column renders RTL (`dir="rtl"` verified).
  - Save without Arabic name is allowed (English-only proceed).

---

### User Story 7 — Multi-channel preview (Priority: P2)

Before publishing, an author previews the themed survey across delivery channels — Mobile web, Desktop web (default), WhatsApp and Email. Welcome/thank-you render live from the Settings editors and pagination follows the configured Question layout.

**Why this priority**: Preview catches layout/wording issues but does not gate publishing at the module level — governance is US2.

**Independent Test**: A tester opens the preview on a saved survey; the Desktop web frame is active by default; switching to WhatsApp re-renders the frame with the WhatsApp chrome; changing layout to "one question per page" is reflected in the preview pagination.

**Acceptance Scenarios**:

1. **Given** the preview, **When** it opens, **Then** the active channel is Desktop web (FR-12.1).
2. **Given** a Mobile / WhatsApp / Email tab, **When** the manager switches, **Then** the preview re-renders in the correct channel frame (FR-12.1).
3. **Given** a survey with welcome and thank-you HTML, **When** the preview starts, **Then** the welcome renders before any question and the thank-you renders after submit (FR-12.2).
4. **Given** any of the four Question-layout modes, **When** the preview renders, **Then** pagination mirrors that mode (FR-12.3).
5. **Given** a multi-section survey, **When** the preview and the rendered answer page render, **Then** each section's title heads its block of questions (FR-12.4).

**Unit Test Coverage**:

- `unit-tests: skipped — preview is a client-side renderer of persisted survey state and asserts only rendered pagination + channel chrome, which is exercised end-to-end in the E2E lane.`

**Integration Test Coverage**:

- `integration-tests: skipped — no server-owned behaviour beyond GET /api/surveys/{id} which is already covered in US1.`

**Scenario Test**:

- `scenario-test: not-needed — single-page render surface.`

**E2E Test Coverage**:

- **User flows under test**: `PreviewTests.cs`.
- **Required scenarios**:
  - Default channel = Desktop web.
  - Switch Mobile / WhatsApp / Email → chrome changes; content survives.
  - Layout change reflects in pagination (four modes verified).
  - Empty state when a survey has no questions.

---

### User Story 8 — Survey Report (Priority: P3)

A CX Program Manager opens a survey's **Report** and sees metric cards (Responses, Completion rate, Median time, Touchpoints), KPI gauges (CSAT/NPS/CES) with target markers and period delta, a period filter (Last 1 day / 7 days / month / 3-6-9 months / year / custom), and per-question result views chosen by type. The report reflects only responses collected **within** the survey's active period; late responses live in the M-07 post-expiry store.

**Why this priority**: Reporting depends on M-04 / M-05 / M-06 producing data; the module surfaces it but computes almost nothing itself. P3 reflects that it is consumer-side.

**Independent Test**: A tester loads the report on a survey seeded with responses; changes period to "Last 7 days" and asserts (a) the response count in the metric card matches the fixture, (b) CSAT gauge shows the average of the survey's CSAT questions, (c) each per-question card renders the correct visual per type per FR-13.3, (d) responses collected after the active period do not appear.

**Acceptance Scenarios**:

1. **Given** the report, **When** it opens, **Then** it shows metric cards Responses / Completion rate / Median time / Touchpoints; Touchpoints = count of bound touchpoints across the journey's stages (F13 Fields & behaviour).
2. **Given** the period filter, **When** the manager switches window, **Then** KPI gauges, per-question values and counts update accordingly (FR-13.1).
3. **Given** two CSAT questions with scores 81% and 76%, **When** the headline CSAT gauge renders, **Then** it shows the average — 78.5% — and updates if either question changes (FR-13.2).
4. **Given** a KPI question, **When** its per-question card renders, **Then** it shows a bar distribution + a KPI gauge with the response-count label top-right (FR-13.3).
5. **Given** a Single-select / Yes-No question, **When** rendered, **Then** a distribution donut with a legend is shown (FR-13.3).
6. **Given** a Multi-select question, **When** rendered, **Then** a bar chart shows each option's count and % of respondents; the base (respondents) is stated with the chart and percentages **may total > 100%** (FR-13.3, FR-13.5).
7. **Given** a Scale question, **When** rendered, **Then** an aggregate gauge plus a style visual is shown: a face for Faces, filled stars for Stars, no side chart for Labels (FR-13.3).
8. **Given** a Text/Paragraph question, **When** rendered, **Then** a table of individual verbatim responses (each with channel + submission time) is shown — the latest few by default, with a **"show more"** control revealing up to the last 100 (FR-13.3, FR-13.7).
9. **Given** a Number/Date/Time question, **When** rendered, **Then** a value-distribution line is shown; numeric additionally shows the average (FR-13.3).
10. **Given** the report, **When** completion time is queried, **Then** the median-time metric is always available because completion time is recorded automatically for every response (FR-13.4, FR-3.5, `record_time` invariant in §4.1).
11. **Given** the survey's active period has elapsed, **When** additional responses arrive, **Then** they do not appear in the live report; they are available in the M-07 post-expiry store (FR-13.6, BR-3.1).

**Unit Test Coverage** *(backend-bearing)*:

- **Units under test**: `HeadlineCsatCalculator` (composite average), `PeriodResolver`, `PerQuestionViewSelector`, `ResponseWindowFilter`, `VerbatimSampler`.
- **Required cases**:
  - `HeadlineCsatCalculator.Compute([81m, 76m]) → 78.5m`; `Compute([]) → null`.
  - `PeriodResolver.Resolve("last_7_days", now) → { From: now.AddDays(-7), To: now }`.
  - `PerQuestionViewSelector.Pick(type: MultiSelect) → BarWithCountsAndPct`.
  - `PerQuestionViewSelector.Pick(type: Scale, subType: Labels) → GaugeOnly` (no side chart for Labels).
  - `ResponseWindowFilter.Include(response, activePeriod) → false when response.SubmittedAt > survey.SentAt + activePeriod` (FR-13.6).
  - `VerbatimSampler.Sample(responses, limit: 100) → newest-first up to 100` (FR-13.7).

**Integration Test Coverage** *(backend-bearing)*:

- **What gets tested end-to-end**:
  - `GET /api/surveys/{id}/report?period=last_7_days` returns metric cards, KPI gauges, and per-question payloads.
  - Late responses (submitted post-expiry, marked in a seeded fixture) are excluded from `/report` but present at `GET /api/surveys/{id}/post-expiry-responses` (M-07 endpoint contract point; M-01 boundary only).

**Scenario Test**:

- `scenario-test: not-needed — the report is a read model; single /report call per test class covers the aggregate.`

**E2E Test Coverage**:

- **User flows under test**: `ReportTests.cs`.
- **Required scenarios**:
  - Period filter switches all cards and charts.
  - Multi-select bars display "N respondents" base line and allow totals > 100%.
  - Verbatim responses list — "show more" reveals up to 100.
  - Median time is always visible.
  - Empty state when no responses exist.

---

### User Story 9 — Survey Analytics (Priority: P3)

Analytics explains **reach and drop-off**: sent → opened → started → finished, per-channel completion rates, and a responses-trend line. Every headline number carries an up/down deviation indicator (▲ green / ▼ red) computed against the previous period of equal length. When a survey is new and no prior period exists, deviations are suppressed rather than shown as 0.

**Why this priority**: Same as US8 — the module surfaces aggregates owned by M-04 / M-06 / M-07.

**Independent Test**: A tester loads Analytics with a fixture where the previous 7 days had 100 sends → 50 finished (50%) and the current 7 days had 200 sends → 120 finished (60%); asserts the funnel counts, per-stage % of Sent, stage-to-stage conversion chips, and a **▲ +10 pp** delta on the Overall Completion Rate; a brand-new survey shows no deltas at all.

**Acceptance Scenarios**:

1. **Given** the Analytics screen, **When** it opens, **Then** it offers period options identical to the report (Last 1 day / 7d / month / 3-6-9 months / year / custom) plus a daily/weekly/monthly granularity segment (FR-14.1).
2. **Given** a period change, **When** it fires, **Then** all numbers, deltas, funnel bars, channel bars and the trend line recompute (FR-14.1).
3. **Given** the funnel, **When** rendered, **Then** Sent, Opened, Started, Finished are shown with absolute counts, % of Sent, stage-to-stage conversion chips and an overall completion rate (FR-14.2).
4. **Given** any headline number, **When** rendered, **Then** a ▲/▼ delta vs the previous equal-length period is displayed — value in % for counts and in percentage points for rates (FR-14.3).
5. **Given** a per-channel breakdown, **When** rendered, **Then** each channel shows Sent + completion rate + deviation vs previous period (FR-14.4).
6. **Given** a new survey with no previous-period data, **When** the screen renders, **Then** deviation indicators are suppressed (not shown as 0) (FR-14.5).

**Unit Test Coverage** *(backend-bearing)*:

- **Units under test**: `FunnelCalculator`, `PeriodDeltaCalculator`, `ChannelBreakdownCalculator`, `TrendGranularityResolver`.
- **Required cases**:
  - `FunnelCalculator.Compute({ Sent: 200, Opened: 160, Started: 130, Finished: 120 }) → { OpenedPct: 80m, StartedPct: 65m, FinishedPct: 60m, OpenedToSent: 80m, StartedToOpened: 81.25m, FinishedToStarted: 92.31m }` (rounding rules preserved).
  - `PeriodDeltaCalculator.Delta(current: 60m, prior: 50m, kind: "rate") → +10pp`; `Delta(current: 200, prior: 100, kind: "count") → +100%`.
  - `PeriodDeltaCalculator.Delta(current: X, prior: null) → null` (FR-14.5).
  - `TrendGranularityResolver.Resolve(period: "last_7_days") → "daily"`; `Resolve("last_year") → "monthly"`.

**Integration Test Coverage** *(backend-bearing)*:

- **What gets tested end-to-end**:
  - `GET /api/surveys/{id}/analytics?period=last_7_days&granularity=daily` returns the four funnel counts, all deltas, channel bars and the daily trend series.
  - `GET .../analytics?period=last_1_day` (new survey) returns deltas as null.

**Scenario Test**:

- `scenario-test: not-needed — read-only aggregate endpoint covered by a single test.`

**E2E Test Coverage**:

- **User flows under test**: `AnalyticsTests.cs`.
- **Required scenarios**:
  - Period + granularity switches recompute every card, bar and line within 100 ms.
  - Deltas render with correct ▲ green / ▼ red glyph and % vs pp units.
  - New survey suppresses deltas (no glyphs, no 0%).
  - Empty state / permission redirect.

---

### Edge Cases

- **Last section deletion** — the only section **can** be deleted, leaving the survey with no sections (FR-2.3). Non-empty deletions require a destructive confirmation (FR-2.5 / FR-2.6).
- **Deleting a routed-to question** — inbound routes reset to the next-question default (FR-2.7).
- **Deleting a question with translations** — all locale strings for it are purged immediately (FR-2.8).
- **Archived → Active** — blocked by BR-1.3 / FR-1.14. Only Unarchive → Draft is offered; the normal transitions then apply.
- **Active survey edit** — not editable in place; must be Returned to Draft first (BR-1.5). This return is **destructive** (BR-1.6): a blocking confirmation warns that all responses will be permanently deleted; on confirm, every Response (live + M-07 post-expiry store) is hard-deleted and every in-flight respondent session is invalidated. Report/analytics restart from zero once the survey re-Publishes.
- **In-flight respondent session invalidation on Return-to-Draft** — a respondent mid-survey (opened the survey URL but not yet submitted) at the moment BR-1.6 fires cannot submit: their next submit attempt is rejected with the configured expiry-style message. The respondent is not notified in-page pre-submit; the rejection surfaces only on submit. This is deliberate — P-01 is warned to edit responsibly.
- **Pending review in filters vs builder** — Pending review is a Library status-filter value but is never selectable in the builder status control (FR-1.3, FR-8.12).
- **Pause with 0 rules** — no confirmation modal shown; status changes immediately (SRS scenario "Pause without rules").
- **Reactivate a Paused survey with rules** — connected rules resume sending automatically without reconfiguration (FR-1.11, key scenario "Reactivate").
- **Layout switch off "one question per page" while routing is on** — routing is auto-disabled (FR-9.1); the shuffle-lock is released.
- **Routing on a question type not in the eligible list** — the routing button is not shown; toggling routing on does not add it.
- **Routing source/target inside a Questions Set** — not allowed; set questions are excluded as routing sources and targets, and a Scale Slider sub-type is not routable (FR-9.5, Question Type Catalogue).
- **KPI question with Bound journey OFF** — Stage and Touchpoint are disabled and any prior values are stripped from the payload (BR-8.2).
- **KPI question without a Touchpoint** — valid; the question may bind at journey or stage level only (FR-8.4).
- **KPI change with an incompatible Touchpoint** — the Touchpoint is cleared; a still-valid Touchpoint is retained (BR-8.5).
- **Matrix KPI: extra row beyond seeded perspectives** — counts toward the KPI overall but is not a new perspective (BR-8.3) — perspectives are M-06-owned.
- **Sentiment flag on a non-text question** — the flag is inert; the UI hides it for non-text types (FR-8.11).
- **Comments field** — available on every question type, OFF by default, always optional (never required), default label "Comments" (translatable), max 200 chars; the comment travels with the response to M-05 (FR-8.9).
- **Rich-text HTML source toggle** — allows raw HTML editing of welcome / thank-you. Every save is sanitised against a **Full-HTML5-minus-unsafe** allowlist: `<script>`, DOM event-handler attributes (`on*`), and `javascript:` URLs are stripped; `<iframe>` remains disallowed by default. Resolved by Clarifications § Session 2026-07-13 (Q3); see FR-3.2.
- **Empty active period** — the survey does not auto-expire (FR-3.4).
- **Post-expiry late arrivals** — with post-expiry collection **ON**, retained in the M-07 store and never counted in the live report; with it **OFF**, rejected with the configured expiry message. An in-flight response that crosses the expiry boundary is rejected either way (BR-3.1, BR-3.4, FR-13.6).
- **Tenant flips `post-expiry feedback collection` while surveys are Active / expired** — M-04 evaluates the setting **live per incoming response** (BR-3.1, Q5). Flipping OFF → ON begins accepting late responses into M-07 immediately across every already-expired survey in the tenant; flipping ON → OFF begins rejecting late responses immediately. No Survey row is rewritten. Historical decisions already made on prior responses are not retroactively re-evaluated.
- **Before survey start** — the survey is not collectable until it is Active/sent (BR-3.4).
- **Redirect with a delay of 0 s** — redirect happens immediately after submit; behaviour identical to "redirect_after_s = null" for that purpose.
- **Analytics with no prior-period data** — deltas suppressed rather than shown as 0 (FR-14.5).
- **Multi-select report totals** — sum of option percentages may exceed 100% because a respondent may choose several options; the respondent-base count is stated with the chart (FR-13.5).
- **Templates on edit** — built-in are locked; attempting to edit surfaces a notice (FR-7.1).
- **Templates on instantiate** — all data, including journey/stage/touchpoint bindings, is copied as-is (FR-6.3, FR-7.4).
- **Text/Paragraph verbatim table** — capped at the last 100 received responses via "show more" (FR-13.3, FR-13.7).
- **Pending review edit attempt by submitter** — every editor is read-only with a "Pending review" banner (SRS scenario "Locked while pending").

---

## Requirements *(mandatory)*

### Functional Requirements

> Every SRS FR / BR is reproduced below with its original ID for traceability. Requirements added by the clarification round — **FR-2.4–2.8, FR-5.5–5.6, FR-8.12, FR-9.5, FR-12.4** and **BR-1.4–1.5, BR-3.4, BR-8.5** — follow the same numbering convention and are marked in context; the four **(authoritative)** matrices above them (Creation Flow, Status Transition, Question Type Catalogue, Active Period & Expiry) are the deterministic source of truth for those areas.

#### F1 — Survey Library

| ID | Requirement |
|---|---|
| FR-1.1 | The two tabs display live counts of surveys and templates and switch the listing without a page reload. |
| FR-1.2 | Surveys and templates are listed by their English name only (Arabic is in Translate workspace, not shown in lists). Search filters in real time by English name; on the Templates tab matches are also evaluated against each template's tags. |
| FR-1.3 | Type, Status and Journey dropdowns filter the list; filters combine (AND) with search and with one another, and clearing a filter restores its rows. The **Status filter offers Draft, Pending review, Active, Paused and Archived**. When the combined filters match nothing, the list shows an explicit "no results" state rather than a blank table. |
| FR-1.4 | Each row exposes quick-access icons for Preview, Survey report and Analytics, plus an overflow menu (Edit, Change status, Clone, Sections, Archive). |
| FR-1.5 | Clicking anywhere on a survey row (outside the action controls) opens that survey's Survey Settings screen. |
| FR-1.6 | Row action controls and the overflow menu stop propagation so they never trigger the row-level navigation. |
| FR-1.7 | Status is shown as a coloured dot + label. The survey's theme mode is still stored (inherited/customized) but is no longer surfaced as a list column. |
| FR-1.8 | Clone creates a "Copy of — ‹name›" draft and continues directly into the builder with the cloned questions. |
| FR-1.9 | A distribution rule sends its survey only while the survey status is Active; Draft, Pending review, Paused and Archived surveys are never sent by rules. |
| FR-1.10 | When the user changes an Active survey with ≥ 1 connected rule to Paused, a blocking confirmation alert is shown stating the number of connected rules and that pausing stops them from sending the survey until reactivation; the rules themselves are preserved. |
| FR-1.11 | Confirming pauses the survey and suspends rule-driven sending; cancelling leaves the status unchanged. Reactivating the survey resumes the connected rules without reconfiguration. |
| FR-1.12 | The status-change dialog states the rule behaviour: only Active surveys collect responses and are sent by rules; Paused keeps configuration and rules but stops sending and new responses. |
| FR-1.13 | In addition to pausing at survey level, the related module (M-02 Channel Management) must provide the ability to pause an individual distribution rule from the rule's side: a paused rule stops sending its survey while the survey itself remains Active and continues to be sent by its other active rules; reactivating the rule resumes sending without reconfiguration. |
| FR-1.14 | The Change-status action does not offer Active/Paused for an Archived survey; it offers only Unarchive → Draft. Unarchiving sets the status to Draft and re-enables editing; the survey does not resume collection or rule-driven sending until it is explicitly set Active again. |

#### F2 — Sections & Questions Sets (library structure view)

| ID | Requirement |
|---|---|
| FR-2.1 | The structure view lists the survey's sections, and under each section its standalone question count and its Questions Sets, each with a live "shows ‹k› of ‹n›" summary and selection mode. |
| FR-2.2 | From this view a manager can add a section, add a Questions Set to a section, open a Questions Set's settings (F10), and delete a section or a set. |
| FR-2.3 | The **last remaining section can be deleted**; a survey may be left with **no sections** (an empty survey). Deleting any section follows the confirmation rules in FR-2.4–FR-2.6. |
| FR-2.4 | Deleting an **empty** section requires a **standard confirmation**, after which the section is deleted. |
| FR-2.5 | Deleting a **non-empty** section shows a **destructive confirmation** that explicitly states all contained standalone questions **and** Questions Sets will also be deleted; deletion proceeds only after explicit confirmation. |
| FR-2.6 | Deleting a **non-empty Questions Set** shows a **destructive confirmation** that all questions inside the set will be deleted; deletion proceeds only after explicit confirmation. |
| FR-2.7 | When a question is deleted (individually or by cascade), any **routing references targeting it are reset to the default routing (the next question in order)**, and any routing configured on the deleted question is removed. |
| FR-2.8 | When a question is deleted, its **translations in every locale are deleted immediately and the deletion is persisted**. |

#### F3 — Survey Settings

| ID | Requirement |
|---|---|
| FR-3.1 | Survey name is English-only here; Arabic and all other translations are entered in the Translate workspace (F11). |
| FR-3.2 | Welcome and Thank-you are full rich-text editors supporting bold/italic/underline, heading, bulleted list and link, plus a `</> HTML` source toggle to edit raw HTML. **Every save is sanitised at server ingress against a Full-HTML5-minus-unsafe allowlist**: `<script>`, DOM event-handler attributes (`on*` — e.g. `onclick`, `onerror`, `onload`), and any `javascript:` (or equivalent script-executing) URL scheme are stripped; `<iframe>` remains disallowed by default. The sanitiser rules are auditable and versioned; any allowlist expansion is a deliberate, tracked change (Clarifications § Session 2026-07-13, Q3). |
| FR-3.3 | Question layout controls pagination independently of sections and Questions Sets. Selecting "one question per page" or "a set number per page" first warns the author that the layout may disturb the respondent experience. Answer routing is only available with the one-question-per-page layout (see F9). |
| FR-3.4 | The survey's active period is a duration in **days and hours** measured from the moment it is sent (see [Active Period & Expiry Lifecycle](#active-period--expiry-lifecycle-authoritative)). While within it an Active survey accepts responses; on elapse, collection behaviour depends on the tenant-level **post-expiry feedback collection** setting — **OFF** ⇒ new responses rejected with the configured expiry message; **ON** ⇒ responses accepted but stored separately in M-07 and excluded from the live report, and any response started before but submitted after expiry is rejected. An empty active period means the survey does not auto-expire. |
| FR-3.5 | Shuffle exposes a mode selector (Random · Prioritize low-response); enabling routing (F9) disables shuffle, and routing is available only with the one-question-per-page layout. Completion time is recorded automatically for every response (there is no author toggle), so the report's median-time metric is always available. |
| FR-3.6 | For a new survey the build-method chooser (F5) is shown before the settings screen; the settings screen is then reachable from a library row click, from the build flow, and from a Survey settings button inside the builder. When editing, Continue returns to the builder rather than the build-method chooser. |

#### F4 — Appearance & live preview

| ID | Requirement |
|---|---|
| FR-4.1 | Inherited mode renders all controls read-only/locked; Customize unlocks every token and applies changes live. |
| FR-4.2 | The live preview is pinned to the right and remains visible while the controls list scrolls; it offers a Desktop and a Mobile device frame. |
| FR-4.3 | The preview reflects the current theme (inherited or customized) and the survey's welcome message, updating instantly on any change. |
| FR-4.4 | The chosen appearance (inherited, or the full set of customized theme tokens) is part of the survey's saved settings and is included when the survey is saved as a template (F7), so a template reproduces branding as well as questions. |

#### F5 — Build method

| ID | Requirement |
|---|---|
| FR-5.1 | Presents three paths: From scratch, From a template, and Build with AI. |
| FR-5.2 | Each build method shares the same **Settings → Builder** tail and differs only in the seed step: **From scratch** → Survey Settings → Builder (empty); **From a template** → Template Picker (F6) → Survey Settings → Builder (seeded from the template); **Build with AI** → AI assisted flow → Survey Settings → Builder (seeded from the AI draft). The Survey Settings screen (F3) is always entered **before** the Builder (F8). |
| FR-5.3 | This step is shown only when creating a new survey; editing or cloning bypasses it and opens the builder directly. |
| FR-5.4 | For a new survey the build-method chooser is the first step and precedes the Survey Settings screen; the selected method (scratch / template / AI) determines the seed content before settings are configured. |
| FR-5.5 | Persistence: no Survey row exists during the chooser, template-picker or AI steps; the Draft Survey is persisted on the first successful **Continue** out of Survey Settings (valid English name). Re-entering the flow creates no duplicate draft. |
| FR-5.6 | **Back** returns to the immediately preceding step, preserving the current session's entered data. **Cancel** abandons creation and returns to the Survey Library: if no draft was persisted, nothing is saved; if a draft was already persisted, it remains in the Library unchanged. |

#### F6 — Choose a template

| ID | Requirement |
|---|---|
| FR-6.1 | Templates are sorted customized-first; built-in cards show sector chips + padlock, customized cards show tag chips. |
| FR-6.2 | Search matches template name and tags; Type and Sector filters apply. |
| FR-6.3 | "Use this template" starts a new survey seeded with **all** of the template's data — collection behaviour, appearance, messages, sections/sets/questions, KPI links **and** journey/stage/touchpoint bindings — copied as-is. |
| FR-6.4 | From the template picker a manager can preview a template's survey before choosing it, without creating a survey. |

#### F7 — Template authoring

| ID | Requirement |
|---|---|
| FR-7.1 | Built-in templates cannot be edited (attempting to edit surfaces a notice); only customized templates are editable. |
| FR-7.2 | Editing a customized template exposes Edit questions, opening the builder in template context with its questions loaded. |
| FR-7.3 | Class and Primary-sector inputs are not part of the authoring form; they persist as filter facets for built-in templates. |
| FR-7.4 | Saving a survey as a template captures **all** of its data — collection behaviour, appearance, welcome/thank-you messages, sections/Questions Sets, KPI links **and** journey/stage/touchpoint bindings; instantiating the template later copies all of it into the new survey. |

#### F8 — Build Survey (the builder)

| ID | Requirement |
|---|---|
| FR-8.1 | The palette lists the question types in the specified order and offers the KPI type under a separate Metric heading. |
| FR-8.2 | Sections, Questions Sets and questions all support drag-and-drop reordering (questions within and across sections and sets); order persists and drives presentation. |
| FR-8.3 | The settings panel shows only the controls relevant to the selected question's type. |
| FR-8.4 | A KPI question's binding is **layered** under Bound journey ON: it may bind at **journey**, **stage** or **touchpoint** level. **Touchpoint is optional** — the question is valid bound to the journey/stage alone. **Stage is required before a Touchpoint** may be chosen. The Touchpoint list is filtered by **KPI + Journey + Stage** (only touchpoints carrying the selected KPI in that journey/stage are offered). Stage and Touchpoint default to None; with Bound journey OFF both are disabled and no binding is stored (BR-8.2). |
| FR-8.5 | Scale offers a Slider display with lower/higher limits and step count; Single select offers List/Dropdown display; Input Field offers a Paragraph (long-text) type. |
| FR-8.6 | In matrix KPI mode, rows are seeded from the selected KPI's perspectives and remain individually renamable/removable. |
| FR-8.7 | The builder header exposes Survey settings, status, Translate, Preview, Save as template, Save survey and the Question-routing toggle. |
| FR-8.8 | Every question exposes a sub-type whose available options are determined by its question type; changing the sub-type changes how the question is rendered/collected without changing its type. |
| FR-8.9 | **Every question type** may enable a **comment field**: an optional free-text box shown under the question after it is answered. It is **OFF by default**, is always **optional** (there is no option to make it required), has a **default label "Comments"** that is **translatable** (F11), and accepts at most **200 characters**. When enabled, the comment is collected alongside the answer and forwarded to M-05 with the response. |
| FR-8.10 | Questions Sets are created and managed from the section's menu, not from a question's settings; a set is added to a section, and questions are added into a set. A question's settings panel does not create sections or sets. |
| FR-8.11 | A Text or Paragraph question may be flagged with Apply sentiment analysis; when set, its answers are routed to M-05 for sentiment scoring (positive / neutral / negative) in addition to being stored and shown as verbatim responses in the report. The flag is off by default and has no effect on non-text question types. |
| FR-8.12 | The builder's status control never lists **Pending review** (that state is entered only via *Submit for review*, never chosen); it offers only the statuses reachable by a valid transition from the survey's current status per the Status Transition Matrix (BR-1.4). |

#### F9 — Answer routing (skip logic)

| ID | Requirement |
|---|---|
| FR-9.1 | Routing is available only with the one-question-per-page layout; enabling it requires confirmation and then disables (and locks) question shuffling, and switching the layout away from one-question-per-page turns routing off. Disabling routing re-enables shuffle. |
| FR-9.2 | When routing is on, an answer-routing control appears beside the delete control on eligible question cards, and cards with routing configured show a "Routing set" badge. |
| FR-9.3 | The routing editor lists each answer of the question with a target selector containing every subsequent **standalone** question plus "End survey"; the default is the immediately following question. Questions inside Questions Sets are not offered as targets (FR-9.5). |
| FR-9.4 | Saving persists the per-answer targets on the question; the editor can be reopened to adjust them. |
| FR-9.5 | **Routing × Questions Sets.** Because a Questions Set serves a nondeterministic per-respondent subset, questions **inside a Questions Set are not eligible as routing sources and cannot be selected as routing targets**. Routing may be configured only on **standalone** eligible questions, and a target may only be another standalone question or **End survey**. A Questions Set is delivered as a contiguous block at its position; after its selected subset is answered, delivery resumes at the next standalone question (or the routed target of the last standalone question before the set). |

#### F10 — Questions Set settings & low-response ordering

| ID | Requirement |
|---|---|
| FR-10.1 | Each Questions Set stores a title, description, a selection mode (Random / Prioritize low-response) and a per-sending count (number of its questions each respondent receives). |
| FR-10.2 | Set settings are editable both from the builder (the set's menu) and from the library structure view (F2). |
| FR-10.3 | M-01 stores the selection configuration and ordering policy; the render/collection layer (M-02/M-04) executes the actual per-respondent selection at send time. |
| FR-10.4 | Low-response ordering algorithm. When Prioritize low-response is in effect, the platform first determines, within each Questions Set, the set's lowest-response question(s) among those eligible to be sent; it compares that against the response counts of the section's standalone questions to obtain the section's lowest-response question. It then compares each section's lowest-response question across the whole survey: the section holding the survey-wide lowest is presented first, the next-lowest section second, and so on. |

#### F11 — Translate workspace

| ID | Requirement |
|---|---|
| FR-11.1 | The Translate workspace holds every localizable string: survey Arabic name, welcome & thank-you messages, **section titles**, question text/description, option labels, scale labels, reason items and the **per-question comment-field label**. |
| FR-11.2 | English is authored on the build form; Arabic (and any other language) is completed here. |
| FR-11.3 | Arabic renders full RTL; the workspace shows source and target side by side. |

#### F12 — Multi-channel preview

| ID | Requirement |
|---|---|
| FR-12.1 | The preview opens on the Desktop web channel by default and offers Mobile, WhatsApp and Email. |
| FR-12.2 | The welcome message is shown before questions and the thank-you message after submit, live from the Details editors. |
| FR-12.3 | Preview pagination mirrors the configured Question layout. |
| FR-12.4 | The rendered survey (respondent answer page) **and** the multi-channel preview display each **section's title** as a heading above that section's questions; standalone questions and Questions Sets appear under their section title. The section title is translatable (F11). |

#### F13 — Survey Report

| ID | Requirement |
|---|---|
| FR-13.1 | A period filter (1 day / 7d / month / 3 / 6 / 9 months / year / custom) drives the report; KPI gauges, per-question values and counts update accordingly. |
| FR-13.2 | The headline CSAT gauge is computed as the average of the survey's CSAT questions and updates if those change. |
| FR-13.3 | Per-question results are rendered per question type: KPI → bar distribution + KPI gauge (top-right label = response count); single-select and Yes/No → distribution donut with legend; multi-select → bar chart with counts and %; scale → aggregate gauge + a visualisation in the question's display style (a face for Faces, filled stars for Stars, no side chart for Labels); text/paragraph → table of individual verbatim responses (channel + submission time) — latest few by default, with "show more" up to the last 100; number/date/time → value-distribution line (numeric additionally shows average). Frequent-word, theme and sentiment analysis are provided by M-05. |
| FR-13.4 | Median completion time is always available because completion time is recorded automatically for every response (there is no author toggle). |
| FR-13.5 | Multi-select results are reported as counts and percentages of respondents, and the percentages can total more than 100% because a respondent may choose several options; the base (number of respondents) is stated with the chart. |
| FR-13.6 | The report reflects only responses collected within the survey's active period; responses received after expiry are excluded from the live report and are available in the M-07 post-expiry store instead. |
| FR-13.7 | Text/paragraph questions surface a sample of recent verbatim responses (with channel and submission time) directly in the report — latest few by default, expandable to the last 100 received responses — giving managers immediate qualitative signal; full-text listing, frequent-word, theme and sentiment analysis are owned by M-05. |

#### F14 — Analytics

| ID | Requirement |
|---|---|
| FR-14.1 | Analytics offers the same period options as the report (including Last 1 day) plus a granularity segment appropriate to the period; all numbers, deltas and charts recompute on period change. |
| FR-14.2 | The funnel shows Sent/Opened/Started/Finished with per-stage absolute counts, % of Sent, stage-to-stage conversion chips, and an overall completion rate. |
| FR-14.3 | Every headline number (completion rate, each funnel stage, each channel) displays an up/down deviation indicator computed against the previous period of equal length: ▲ (positive, green) or ▼ (negative, red), with the value in % (counts) or percentage points (rates). |
| FR-14.4 | A channel breakdown and a responses trend chart update with the selected period/granularity. |
| FR-14.5 | When no previous-period data exists (new survey), deviation indicators are suppressed rather than showing a misleading 0. |

#### F15 — Survey approval & publishing workflow

| ID | Requirement |
|---|---|
| FR-15.1 | A survey created by P-03 is saved as Draft; submitting it moves it to Pending review and locks it against edits by the submitter (read-only view remains available). |
| FR-15.2 | On entering Pending review, M-09 sends a notification to **every user holding the review/publish permission** in the tenant (default: every P-01) — **broadcast fan-out**, not assignment; the notification deep-links to the survey's Survey Settings screen. Whichever qualifying reviewer acts first (Publish or Return to draft) performs the review; there is no reviewer-assignment field on Survey and no first-claim lock at the M-01 layer. Individual notification lifecycle (read / dismiss / dedupe once one reviewer acts) is M-09's concern (Clarifications § Session 2026-07-13 second pass, Q7). |
| FR-15.3 | From Survey Settings, the reviewer can Publish (status → Active) or Return to draft (status → Draft, editable again by P-03), optionally with remarks recorded in the audit log. |
| FR-15.4 | A survey in Pending review cannot be distributed, is not sent by rules, and does not collect responses. |
| FR-15.5 | M-10 exposes a grantable permission "Publish own surveys" for P-03. When granted, the P-03 user may publish surveys they authored directly and the submit-for-review step is skipped. The grant is tenant-configurable per user/role. |
| FR-15.6 | All submit, publish, return and permission-grant events are written to the M-11 audit log with actor, timestamp and remarks. |

### Business Rules

| ID | Business rule |
|---|---|
| BR-1.1 | Only Active surveys collect responses and are sent by distribution rules; Paused keeps configuration and rules but stops sending and new responses; Draft and Pending review never collect; Archived is read-only. |
| BR-1.2 | Every list action is scoped to the current tenant and written to the M-11 audit log. |
| BR-1.3 | Archived is a terminal, read-only state. The only status change available to an archived survey is Unarchive, which restores it as a Draft; a survey cannot move directly from Archived to Active or Paused. From the restored Draft the normal transitions apply before it can collect again. |
| BR-1.4 | Survey status changes follow the **[Survey Status Transition Matrix](#survey-status-transition-matrix-authoritative)** exactly; the status/actions UI and the API expose **only** the transitions valid from the current status and permitted to the acting role. No other transition is possible. |
| BR-1.5 | An **Active survey is not directly editable** — its structure, questions, routing and settings are locked while Active. To modify it, an authorised user (P-01) must first **Return it to Draft** (an explicit, audited action that stops collection and rule-driven sending per BR-1.1). **This transition is destructive** per BR-1.6: it purges every response already collected for the survey and invalidates in-flight respondent sessions; the user is warned by a blocking confirmation before the purge runs. Once in Draft, the survey re-enters the publish path to become Active again, starting from a zero response count. The same destructive-return rule applies to Paused → Draft. |
| BR-1.7 | **Publish-gate content invariants.** A transition to **Active** — from Draft (by P-01), from Draft via the "Publish own surveys" grant (P-03 on personally-authored surveys), or from Pending review — MUST be **rejected** when the survey has **zero sections** OR **zero total questions** (the sum of standalone questions plus Questions-Set members across all sections). The API returns 409 with `publish.requires_content` and an error payload listing which invariant failed (`missing_sections` / `missing_questions`). The UI disables the Publish and the Change-status → Active controls with a tooltip stating the requirement. Reactivating a **Paused** survey is not gated by this rule (its content already passed the invariant when it first became Active, and Pause does not change content). Resolved by Clarifications § Session 2026-07-14 (Q9). |
| BR-1.6 | **Return-to-Draft-to-edit is a destructive action.** When P-01 initiates a Return-to-Draft transition on a survey in **Active** or **Paused** (the two statuses where responses may already have been collected), a **blocking confirmation** MUST be presented stating that all prior responses will be **permanently deleted**. On confirmation: (a) every Response attached to the survey is **hard-deleted** from both the live report store **and** the M-07 post-expiry store (Q6, Session 2026-07-14) — no soft-delete, no archive; (b) every **open, in-flight respondent session** for the survey is **invalidated** — subsequent submit attempts by those respondents are rejected with the configured expiry message; (c) the survey transitions to Draft with a **response count of zero**, `rules_count` preserved, `bound_journey`/settings preserved, edit lock lifted. The delete is atomic with the status change: either both succeed or neither does. A single M-11 audit-log entry records the actor, timestamp, previous status, and the number of responses purged. Because the response history never survives a Return-to-Draft, the Survey entity carries **no `version` field**: there is at most one Active period's worth of responses at any time. Cancelling the confirmation leaves the survey unchanged (still Active/Paused). Pending-review → Draft (`Return to draft` in the approval workflow, FR-15.3) is **not destructive** — no responses can have been collected in Pending review (FR-15.4), so no confirmation is required and no purge happens. |
| BR-3.1 | When a survey's active period elapses it stops appearing in the live survey report. If the tenant-level **post-expiry feedback collection** setting is **ON**, responses that still arrive are accepted and retained in a dedicated post-expiry response store page in M-07 (they never feed the live report); if it is **OFF**, such responses are rejected and the respondent sees the configured expiry state/message. A response started before but submitted after expiry is rejected regardless of the setting. **The `post-expiry feedback collection` setting is evaluated live per incoming response by M-04**, not snapshotted onto the Survey row (Clarifications § Session 2026-07-13, Q5): flipping the tenant setting takes effect immediately for every survey across the tenant, including already-Active and already-expired surveys. **Retention of post-expiry responses is indefinite from M-01's perspective and is governed by the tenant's data-retention policy in M-11** (Clarifications § Session 2026-07-13, Q2); M-01 sets no independent retention window. |
| BR-3.2 | Only the English name is required to proceed; translations may be completed later. |
| BR-3.3 | Journey binding is optional. A survey with a bound journey is Transactional (its KPI questions can bind to that journey's stages/touchpoints); a survey with no bound journey is Seasonal / Relational and its KPI questions are journey-independent. The survey type is kept consistent with the journey binding automatically. |
| BR-3.4 | A survey is collectable only while **Active and within its active period**. Before the survey has started (not yet Active/sent) it is not available for response collection; before-start attempts are refused. |
| BR-7.1 | **Templates are snapshots, not parents.** Instantiating a template (FR-6.3) copies **all** of its data — settings, appearance, welcome/thank-you, sections / Questions Sets / questions, KPI links **and** journey/stage/touchpoint bindings — into a new, independent Survey row; the Survey persists **no reference** back to the source Template. Consequently: (a) editing a customized template does **not** propagate to already-instantiated surveys; (b) **deleting a customized template deletes only the template** — every survey previously instantiated from it remains fully intact and independently editable; (c) there is no cascade delete and no orphan-reference risk. Resolved by Clarifications § Session 2026-07-13 (Q4). |
| BR-8.1 | Switching a question's type preserves compatible settings and seeds sensible defaults for the new type. |
| BR-8.2 | A KPI question with Bound journey off contributes no touchpoint binding. |
| BR-8.3 | Adding a row to a matrix KPI question adds a metric that counts toward the KPI's overall score like any other question measuring that KPI, but it is not a new perspective: perspectives are defined in KPI Management (M-06) and only defined perspectives receive per-perspective calculations. The added row therefore contributes to the KPI as a whole without producing perspective-level results. |
| BR-8.4 | A Questions Set presents each respondent with a subset of its questions — up to its configured per-sending count — chosen by its selection mode (Random or Prioritize low-response). Standalone questions in a section are always shown; only the set's pool is sampled. |
| BR-8.5 | When a KPI question's **KPI changes**, the currently-selected **Touchpoint is retained if it is still valid** for the new KPI + Journey + Stage combination and **cleared if not**; a Stage not valid for the new KPI is likewise cleared. A KPI question remains valid with **no Touchpoint** (journey/stage-level binding). |
| BR-15.1 | The edit lock applies to the survey structure, questions, routing and details while Pending review; the reviewer (P-01) may edit before publishing. |
| BR-15.2 | Publishing requires either the reviewer permission (P-01) or the M-10 "Publish own surveys" grant (P-03, own surveys only). |
| BR-15.3 | Statuses interact with distribution rules per Section 3.1: only Active surveys are sent by rules. |

### Survey Creation Flow (authoritative)

For a **new** survey the interaction order is fixed and its tail is identical across all three build methods. The build-method chooser (F5) is always first; the **Survey Settings** screen (F3) is always entered **before** the **Builder** (F8):

| Build method | Ordered steps |
|---|---|
| From scratch | Add Survey → **Choose build method** → Survey Settings → Builder (empty) |
| From a template | Add Survey → **Choose build method** → Template Picker (F6) → Survey Settings → Builder (seeded from the template) |
| Build with AI | Add Survey → **Choose build method** → AI assisted flow → Survey Settings → Builder (seeded from the AI draft) |

- **Persistence** — no Survey row exists during the chooser / template-picker / AI steps. The Draft Survey is persisted on the first successful **Continue** out of Survey Settings (valid English name present, US1 AS2). Re-entering the flow does not create duplicate drafts (FR-5.5).
- **Back** — returns to the immediately preceding step, preserving data entered in the current session (Builder → Settings → seed step → chooser) (FR-5.6).
- **Cancel** — abandons creation and returns to the Survey Library; nothing is saved if no draft was persisted, otherwise the already-persisted Draft remains unchanged (FR-5.6).
- **Editing / cloning** bypasses the chooser and opens the Builder directly (FR-5.3); Settings is reachable via the *Survey settings* button in the Builder (FR-3.6).

### Survey Status Transition Matrix (authoritative)

All survey status changes MUST follow this matrix; the status/actions UI and API expose **only** the transitions valid from the current status and permitted to the acting role (BR-1.4). No other transitions exist.

| Current status | Action | Result | Permitted role(s) |
|---|---|---|---|
| Draft | Submit for Review | Pending Review | P-03 (own draft), P-01 |
| Draft | Publish | Active | P-01; P-03 with "Publish own surveys" (own draft) |
| Pending Review | Publish | Active | P-01; P-03 with "Publish own surveys" (own draft) |
| Pending Review | Return to Draft | Draft | P-01 |
| Active | Return to Draft (to edit) **⚠ destructive** | Draft | P-01 |
| Active | Pause | Paused | P-01 |
| Paused | Reactivate | Active | P-01 |
| Paused | Return to Draft (to edit) **⚠ destructive** | Draft | P-01 |
| Draft / Active / Paused | Archive | Archived | P-01 |
| Archived | Unarchive | Draft | P-01 |

Additional authoritative rules:

- **Pending Review is a filterable status** in the Survey Library status filter (FR-1.3).
- **Pending Review is never offered in the Builder status dropdown** — it is entered only via *Submit for review*, never chosen (FR-8.12).
- **P-01 may edit a survey while it is Pending Review** (BR-15.1); the submitting P-03 is edit-locked.
- **An Active (or Paused) survey is not directly editable** (BR-1.5): it must first be **Returned to Draft** by P-01 — which stops collection and rule-driven sending (BR-1.1). This return is **destructive** (BR-1.6, Q6, Session 2026-07-14): P-01 sees a blocking confirmation warning that all prior responses will be permanently deleted; on confirm, every Response for the survey is hard-deleted (live + M-07 post-expiry store) and every in-flight respondent session is invalidated. The survey then re-enters the publish path with a zero response count.
- **Pending-review → Draft** (`Return to draft` in the approval workflow, FR-15.3) is **NOT destructive** — no responses can have been collected in Pending review (FR-15.4).
- **Publish (→ Active) is content-gated** by BR-1.7 (Q9): a survey with zero sections OR zero questions cannot become Active — the API returns 409 `publish.requires_content` and the UI disables the action. Applies to every Draft/Pending-review → Active path. Paused → Active (Reactivate) is **not** gated, because Pause does not remove content.
- Archived is terminal except **Unarchive → Draft** (BR-1.3).

### Question Type Catalogue (authoritative)

M-01 offers exactly **seven answer question types plus the KPI metric type** (8 total). This table is the single source of truth for per-type capabilities; any earlier "ten question types" phrasing elsewhere is superseded.

| Question type | Sub-types / display modes | Routing | Sentiment | Comments | KPI-capable |
|---|---|---|---|---|---|
| Scale | Labels / Stars / Smileys / Slider | Yes — **except Slider** | No | Yes | No |
| Input Field | Text / Paragraph / Number / Date / Time / Date-Time / Month | No | **Text & Paragraph only** | Yes | No |
| Single Select | List / Dropdown | Yes | No | Yes | No |
| Multi-select | — | No | No | Yes | No |
| Yes/No (Boolean) | Editable labels | Yes | No | Yes | No |
| Single-select Matrix | Custom columns / KPI scale | No | No | Yes | Conditional (KPI-scale mode) |
| Ranking | — | No | No | Yes | No |
| KPI | KPI-defined representation | Yes | No | Yes | Yes |

- **Routing eligibility** = the "Routing = Yes" rows only, and only when the question is **standalone** (not inside a Questions Set — FR-9.5); a **Scale in the Slider sub-type is not routable**.
- **Sentiment** applies only to Input Field **Text/Paragraph** (FR-8.11).
- **Comments** are available on **every** type (FR-8.9).
- **KPI-capable** = the KPI type; a **Matrix** reflects a KPI only in its **KPI-scale** sub-type (BR-8.3).

### Active Period & Expiry Lifecycle (authoritative)

A survey collects only while it is **Active and within its active period**. "Start" is the moment the survey is sent/activated; the **active period** (FR-3.4) is a duration in days/hours measured from that moment. **Post-expiry feedback collection** is a **tenant-level** setting (owned in tenant Settings / M-11) that M-01 reads and M-04 enforces.

| Lifecycle phase | Behaviour |
|---|---|
| Before start (survey not yet Active/sent) | The survey is **not available** for response collection; attempts are refused (BR-3.4). |
| During the active period (Active, not expired) | The survey **accepts responses** normally; these feed the live report. |
| After expiry — post-expiry collection **OFF** | New responses are **rejected**; the respondent is shown the configured **expiry state/message**; nothing is stored. |
| After expiry — post-expiry collection **ON** | The respondent **may submit**; the response is **stored separately** in the M-07 post-expiry store and **does not contribute to normal survey reporting**. A response **started before expiry but submitted after expiry is rejected** (in-flight responses must be submitted within the active period). |

The `post-expiry feedback collection` value is **evaluated live** by M-04 at the moment each incoming response is received (BR-3.1, Clarifications § Session 2026-07-13, Q5); it is **not snapshotted** onto the Survey row. Flipping the tenant setting takes effect immediately for every survey across the tenant, including surveys that are already Active or already expired.

- An **empty active period** means the survey does not auto-expire (FR-3.4).
- The live report and analytics reflect only in-period responses (FR-13.6); post-expiry responses (collection ON) live only in the M-07 store (BR-3.1).

### Field Definitions & Validation

Field-level rules extracted from every SRS "Fields & labels" table, preserved verbatim so downstream design can wire label copy, default values and validation without re-reading the SRS.

#### Survey Settings fields (F3)

| Field | Type | Required | Default | Rules |
|---|---|---|---|---|
| `name_en` | Text | Yes | — | English survey name; placeholder "e.g. Post-disbursement satisfaction". Arabic name is authored in the Translate workspace only. |
| `description` | Textarea | No | empty | Internal note — "What this survey measures and when it is sent." |
| `survey_type` | Enum | Auto | Seasonal / Relational | Values: Transactional · Seasonal / Relational. Kept in sync with Bound journey (BR-3.3). |
| `bound_journey` | Ref → M-16 Journey | No | None | Options: None or a journey (e.g. Personal Loan Application · Account Onboarding · Branch Visit). Drives available stages/touchpoints for KPI questions. Empty ⇒ Seasonal / Relational; set ⇒ Transactional. |
| `welcome_html` | Rich text | No | empty | Editor supports Bold/Italic/Underline/Heading/List/Link + `</> HTML` source toggle. Shown before any question. |
| `thanks_html` | Rich text | No | empty | Same editor; shown after submit; optional redirect. |
| `redirect_url` | Text (URL) | No | empty | Optional post-submit redirect URL. |
| `redirect_after_s` | Number (seconds) | No | 0 | Delay before redirect. |
| `layout` | Enum | Yes | `section` (One page per section) | Values: `single` (All questions on one page) · `section` · `question` (One question per page) · `count` (A set number per page). Choosing `question` or `count` warns the author. |
| `active_period` | Duration `{days, hours}` | No | null | How long the survey keeps collecting after being sent. Empty ⇒ does not auto-expire. |
| `record_time` | Bool | System | always true | Completion time is always recorded (no author toggle). |
| `shuffle` | Bool | No | false | Enables shuffle. |
| `shuffle_mode` | Enum | No | `random` | Values: `random` · `low_response`. Mutually exclusive with `routing_on`. |
| `routing_on` | Bool | No | false | Requires `layout = question`. Enabling disables & locks shuffle. |

#### Appearance fields (F4)

| Field | Type | Default | Rules |
|---|---|---|---|
| Theme mode | Radio | Inherited | Values: Use Tenant Design Guidelines · Customize this survey. Inherited locks all tokens. |
| Survey logo | File | — | Upload or clear. |
| Primary colour | Colour | Inherited | — |
| Text colour | Colour | Inherited | — |
| Button radius | Number/px | Inherited | — |
| Button border colour | Colour | Inherited | — |
| Button text colour | Colour | Inherited | — |
| Header show logo | Bool | true | — |
| Header show title | Bool | true | — |
| Header alignment | Enum | start | Options: start · center · end. |
| Footer text | Text | — | Placed beside Background top-right in the panel. |
| Background type | Enum | Solid | Solid · Gradient · Image · Pattern. |
| Background gradient stops / angle | — | — | Only when Background = Gradient. |
| Background image | File | — | Only when Background = Image. |
| Background opacity | Number 0–100 | 100 | — |
| Advanced — status colours | Colour set | Inherited | — |
| Advanced — surfaces | Colour set | Inherited | Background/card/border. |
| Advanced — typography | Font set | Inherited | Heading / body fonts. |
| Advanced — layout | Number set | Inherited | Card radius, progress-bar style. |
| Live preview device | Toggle | Desktop | Desktop / Mobile. |

#### Question fields (F8 common + type-specific)

| Field | Type | Default | Rules |
|---|---|---|---|
| Question text | Text | — | Required. |
| Description | Textarea | — | Optional helper text. |
| Question type | Enum | Scale | Values: Scale · Input Field · Single select · Multi-select · Yes/No (Boolean) · Single-select matrix · Ranking · KPI. |
| Question sub-type | Enum (type-dependent) | — | Options change with type: Single select → List / Dropdown; Scale → Labels / Stars / Smileys / Slider; Input Field → Text / Paragraph / Number / Date / Time / Date-and-Time / Month; Matrix → Custom columns / KPI scale. |
| Show comments field | Toggle | Off | Available on **every** question type. Adds an **optional** free-text comment box (never required) under the question after answering. |
| Comment field label | Text (translatable) | "Comments" | Shown above the comment box; localisable in the Translate workspace (F11). |
| Comment max length | Number | 200 | Maximum characters accepted in a comment. |
| Required question | Toggle | Off | Must be answered before submitting. |
| KPI | Enum | — | Only for KPI question type. Values from active KPI catalogue (CSAT, NPS, CES, FCR, VFM, Agent Score, CHS). |
| Perspective | Enum | — | Optional; options change with the KPI. |
| Bound journey (per question) | Toggle | On | Reflects the KPI on the survey's bound journey. Off disables Stage and Touchpoint. |
| Stage | Enum | None | Options from the bound journey; **required before a Touchpoint may be set**. |
| Touchpoint | Enum (optional) | None | Enabled only after a Stage is chosen; filtered by **KPI + Journey + Stage**. On KPI change, retained if still valid else cleared (BR-8.5). A KPI question is valid without a Touchpoint. |
| Allow N/A | Toggle | Off | Adds a not-applicable choice to the scale (KPI). |
| Reason follow-up | Toggle + list | Off | Show a reason prompt based on score; multi/single select; reason items; optional "Other". Always initialised on KPI questions to prevent the settings-render defect noted in Batch H. |
| Scale — point count | Number 2–10 | 5 | Non-slider only. |
| Scale — per-point labels | Text list | — | Non-slider only; optional. |
| Slider lower / higher | Number | 0 / 10 | Slider only. |
| Slider steps | Number ≥ 1 | 1 | Intermediate values auto-computed. |
| Input field type | Enum | Text | Text / Paragraph / Number / Date / Time / Date-and-Time / Month. |
| Apply sentiment analysis | Toggle | Off | Text / Paragraph only. Flags answers for M-05 sentiment scoring. |
| Single-select options | Text list | — | At least 2. |
| Yes/No labels | Text × 2 | Yes / No | Editable. |
| Matrix mode | Enum | Custom columns | Custom columns · KPI scale. |
| Matrix rows (KPI mode) | list | Seeded from KPI perspectives | Editable/removable; extra rows count toward KPI overall but are not new perspectives (BR-8.3). |
| Matrix columns (Custom mode) | Text list | — | Authored. |
| Routing map | `{answerKey → target}` | Default = next question | Only when `routing_on` and question is eligible; target is a question id or `__end`. |

#### Questions Set fields (F10)

| Field | Type | Default | Rules |
|---|---|---|---|
| `title` | Text | Required | Shown as sub-window header. |
| `description` | Textarea | — | Optional. |
| `selection_mode` | Enum | `random` | Values: `random` · `low_response`. |
| `count` | Number | 1 | Questions per sending; must be ≤ set size. |
| `order` | Number | auto | Position within its section. |

#### Section fields

| Field | Type | Default | Rules |
|---|---|---|---|
| `name` | Text | Required | Section title. |
| `description` | Text | — | Optional. |
| `order` | Number | auto | Position within the survey (drag-and-drop). |

#### Template fields (F7)

| Field | Type | Rules |
|---|---|---|
| Name (EN / AR) | Text | English name required; Arabic optional for localization. |
| Description | Textarea | What the template is for. |
| Tags | Chip list | Power the search; appear on customized cards. |
| Class / Primary sector | — | Not part of authoring form; persist only as filter facets for built-in templates (FR-7.3). |

### Error Handling & Notifications

- **Blocking confirmation modals**:
  - Pause with rules (FR-1.10): "Pause survey?" — warns "This survey has N distribution rules connected… Rules only send a survey while it is Active — pausing will stop these rules from sending it until the survey is reactivated. The rules themselves are kept and are not deleted." Actions: Cancel / Pause survey.
  - Enable routing (FR-9.1, F9 fields & labels): "Enable question routing?" — Cancel / Enable routing. On confirm, shuffle is turned off and locked.
  - Change to "One question per page" or "A set number per page" layout (FR-3.3): warns the layout may disturb the respondent experience before it is applied.
  - **Return to Draft to edit an Active or Paused survey** (BR-1.5, BR-1.6, Q6): "Return this survey to Draft to edit?" — warns "**All N responses collected for this survey will be permanently deleted, including any responses in the post-expiry store. Anyone currently mid-survey will not be able to submit. This cannot be undone.**" The current response count `N` MUST be shown in the message. Actions: **Cancel** / **Return to Draft & delete responses**. On confirm, M-01 runs the atomic transition (purge + status change + in-flight invalidation) and writes one M-11 audit entry recording actor, timestamp, previous status, and the count of purged responses.
  - **Publish blocked by empty survey** (BR-1.7, Q9): non-modal — the Publish / Change-status → Active control is disabled with a tooltip stating "Add at least one section and one question before publishing." Attempting the transition via the API returns 409 with `publish.requires_content` and an error payload naming the failing invariant (`missing_sections` / `missing_questions`). Adding content clears the block; no confirmation dialog is required (this is a validation gate, not a destructive action).
  - Delete a customized template (BR-7.1): standard destructive confirmation; already-instantiated surveys are unaffected (no cascade). — this is a standard-destructive, not blocking, and is included here for completeness.

- **Notifications (via M-09)**:
  - When a survey enters Pending review, a notification is fired to every user holding the review/publish permission (P-01 by default). The notification deep-links to the survey's Survey Settings screen (FR-15.2).

- **Audit log entries (via M-11)** — every write on the following surfaces MUST be recorded with actor, timestamp, remarks and tenant id:
  - Create / edit / clone / archive / unarchive / status change on a survey (BR-1.2).
  - Add / edit / delete of a section or Questions Set.
  - Add / edit / delete of a question, KPI binding, routing map.
  - Save / edit / delete of a template.
  - Save of a translation bundle.
  - Submit / publish / return-to-draft / permission-grant events on the approval workflow (FR-15.6).

- **API-05 error envelope** (per project convention) — every non-2xx response follows `{ error: { code, message, request_id, tenant_id } }`.
- **Permission enforcement** — enforced at both UI (controls hidden/disabled) and API (403) levels; every attempt is auditable.

### Non-Functional Requirements

Verbatim from SRS §6, extended with project-standard constraints from CLAUDE.md.

| ID | Requirement |
|---|---|
| NFR-1 | **Performance** — list and builder load within 1.5 s on standard tenant volumes; live preview and configuration updates render within ~100 ms of a change. |
| NFR-2 | **Localization** — full EN/AR parity and RTL for every screen, control, tooltip, message and preview. Arabic copy is authored natively (فصحى) — never translated from English. |
| NFR-3 | **Accessibility** — WCAG 2.1 AA. Colour is never the sole signal (badges/labels accompany status and bands). All controls are keyboard-reachable. Icon-only buttons have aria-labels. Error messages use `role="alert"`. |
| NFR-4 | **Isolation & audit** — strict tenant isolation; all create/edit/status/delete actions audit-logged to M-11 with actor / timestamp / remarks / tenant id. |
| NFR-5 | **Resilience** — **explicit Save** persistence (no autosave): edits to the builder / Survey Settings / Appearance / Translate workspace persist only on the author's Save action. Any navigation (route change, tab close, browser back) with unsaved edits shows a blocking unsaved-changes confirmation. To resolve concurrent edits (e.g. P-01 editing a Pending-review survey submitted by P-03), all write endpoints implement optimistic locking via `If-Match: <etag>`; a stale ETag returns 412 and the UI surfaces a conflict dialog. Resolved by [Clarifications § Session 2026-07-13](#clarifications) (Q1). |
| NFR-6 | **Forward compatibility** — the data model and API contracts are designed to add channels, question types and languages by configuration, without new columns or code branches. |
| NFR-7 | **Themeability** — appearance tokens follow the design-system multi-tenant theming rules; a tenant theme reskins the surface with zero code change. Only semantic (D1–D5) tokens keep constant meaning across tenants. |
| NFR-8 | **Design system fidelity** — every UI element sourced through the shadcn / `@base-ui/react` component library per the Component Sourcing Rule; no raw hex classes; logical properties only (`ps-*`, `me-*`); no physical `pl-*` / `ml-*`. |

### Permissions & Roles

Verbatim from SRS §5 with column semantics preserved.

| Action | P-01 CX PM | P-03 Survey Admin | P-02 Analyst | P-06 Exec |
|---|---|---|---|---|
| View library / preview | ✓ | ✓ | ✓ | ✓ |
| Create / edit draft survey, sections, Questions Sets, questions | ✓ | ✓ (any P-03-authored Draft in the tenant — team-owned per Q8) | — | — |
| Submit survey for review | — | ✓ (any P-03-authored Draft in the tenant) | — | — |
| Review / publish / return to draft | ✓ | ✓ only with "Publish own surveys" grant (own, personally authored surveys) | — | — |
| Edit a survey in Pending review | ✓ | — | — | — |
| Configure KPI binding & routing | ✓ | ✓ (any P-03-authored Draft in the tenant) | — | — |
| Edit appearance / customize theme | ✓ | ✓ (any P-03-authored Draft in the tenant) | — | — |
| Author customized templates | ✓ | ✓ | — | — |
| Change status (pause / reactivate / archive / unarchive / return Active or Pending-review to Draft) | ✓ | — | — | — |
| View report & analytics | ✓ | ✓ | ✓ | ✓ (summary) |

- The **"Publish own surveys"** grant is an M-10 permission option configurable per user/role at tenant level; without it P-03's surveys must pass P-01 review (§3.15). **The grant is per-individual, not per-team** (Clarifications § Session 2026-07-13 second pass, Q8): a P-03 with the grant may publish only surveys they *personally authored* (their `created_by`), not another P-03's Draft — team-editing of Drafts does not become team-publishing.
- **Draft ownership is team-scoped for P-03** (Q8): any P-03 in the tenant may edit / configure / submit any Draft authored by any other P-03. P-01 retains full edit rights. Every edit is audited to the individual acting user (BR-1.2). Concurrent P-03 edits are resolved by the ETag conflict flow (Q1) — a stale ETag returns 412 and the UI surfaces a conflict dialog.
- Permissions are enforced at both UI (controls hidden/disabled) and API (403) levels; every change is attributed in the M-11 audit log.

### Module Interactions (dependencies)

| Consumer / Provider | Direction | Contract |
|---|---|---|
| M-02 Channel Management | M-01 → M-02 | M-01 exposes survey definitions and per-set selection config; M-02 sends surveys only while status = Active (BR-1.1, FR-1.9). |
| M-04 Response Collection | M-01 → M-04 | M-04 attaches responses to the published survey (there is no `survey.version` — Q6 resolves this by purging responses on Return-to-Draft, so the survey never carries a mixed-version history) and enforces the **active-period lifecycle**: refuses before start; accepts in-period; after expiry applies the tenant post-expiry setting **evaluated live at receipt** (OFF ⇒ reject + expiry message; ON ⇒ accept into the M-07 store, excluded from the live report); rejects any response started before but submitted after expiry (BR-3.1, BR-3.4, FR-13.6). No per-survey snapshot of the post-expiry setting exists — flipping the tenant setting is effective immediately for every survey (Clarifications § Session 2026-07-13, Q5). **On a BR-1.6 Return-to-Draft**, M-04 invalidates every open, in-flight respondent session for the survey (in-flight sessions are addressable by `(survey_id, respondent_id)`); subsequent submit attempts return the expiry-style rejection (Clarifications § Session 2026-07-14, Q6). |
| M-05 NLP | M-01 → M-05 | Text/Paragraph answers and per-question comments are routed to M-05; the sentiment flag opts a Text/Paragraph question into scoring (FR-8.11). |
| M-06 CX Metrics & KPI Engine | Bidirectional | M-01 reads the active KPI catalogue (scale, representation, normalisation, calculation method); M-06 computes scores from responses. |
| M-07 Dashboards & Reporting | M-01 → M-07 | Report + Analytics surfaces consume M-06/M-04 aggregates. M-07 hosts the post-expiry response store page. |
| M-09 Alerts | M-01 → M-09 | Approval-workflow notifications are dispatched via M-09 (FR-15.2). |
| M-10 RBAC | M-01 ← M-10 | Roles & permissions are managed in M-10; "Publish own surveys" grant is defined there (FR-15.5). |
| M-11 Tenant Admin | M-01 ← M-11 | Branding defaults (Tenant Design Guidelines) and the audit log ledger are provided by M-11. |
| M-16 Customer Journey Mapping | M-01 ← M-16 | Journeys, stages and touchpoints are read by M-01 for KPI question binding (FR-8.4). |

### Key Entities

M-01 owns the following entities (standard audit fields — `id`, `tenant_id`, `created_at/by`, `updated_at/by` — apply to every entity):

- **Survey** — `name_en` (required), `description`, `survey_type` (enum: Transactional · Seasonal / Relational, derived from `bound_journey`), `bound_journey` (nullable ref → M-16 Journey), `status` (enum: Draft · Pending review · Active · Paused · Archived), `rules_count` (derived, owned by M-02), `submitted_by`/`reviewed_by` (ref → user), `theme_mode` (enum: inherited · customized), `welcome_html`, `thanks_html`, `redirect_url`, `redirect_after_s`, `layout` (enum: `single` · `section` · `question` · `count`, + `questions_per_page` when `count`), `active_period` (`{days, hours}`, nullable), `record_time` (bool, always true), `shuffle` (bool), `shuffle_mode` (enum: `random` · `low_response`), `routing_on` (bool). Constraint: `routing_on == true` requires `layout == "question"` and disables/locks `shuffle`.
- **Section** — `name`, `description`, `order`, `questions[]` (list → Question), `sets[]` (list → Questions Set).
- **Questions Set** — `title`, `description`, `selection_mode` (enum: `random` · `low_response`), `count` (≤ number of questions in the set), `order`, `questions[]`.
- **Question** — `type` (enum: Scale · Input Field · Single select · Multi-select · Yes/No (Boolean) · Single-select matrix · Ranking · KPI), `subtype` (type-dependent enum), `text`, `description`, `required` (bool), `comments` (bool, default false — any type), `comment_label` (string, default "Comments", translatable), `sentiment` (bool — Text/Paragraph only), `section_id`, `set_id` (nullable — standalone if null), `order`. Comment answers are capped at **200 characters**. Type-specific extensions per §4.3 field table above.
- **Theme** — object holding per-survey tokens (colours, fonts, radius, header/footer, background) when customized; else `inherited` and the tokens resolve from Tenant Design Guidelines (M-11).
- **Template** — `class` (enum: Built-in · Customized), `name_en`, `name_ar`, `description`, `tags[]`, `sectors[]` (built-in only), **full settings snapshot** (collection behaviour, appearance, welcome/thanks, sections/sets), `questions[]` **including their journey/stage/touchpoint bindings**. Instantiation copies all data as-is into a new Survey (FR-7.4, FR-6.3). **Instantiated surveys hold no foreign-key reference back to the template** (BR-7.1): editing or deleting a customized template does not propagate to (or cascade into) already-instantiated surveys.
- **Translation** — per-locale bundle of localizable strings (Arabic name, welcome/thanks, **section titles**, question/option/scale/reason strings and **per-question comment-field labels**). Arabic renders RTL.

### API Surface (behavioural summary)

The precise route shapes are for `/speckit-plan` to design; this section captures the endpoints implied by the SRS.

| Verb + Route | Purpose | SRS anchor |
|---|---|---|
| `GET /api/surveys` | Library listing with filters (Type, Status, Journey) + search over English name | F1 |
| `GET /api/surveys/{id}` | Survey Settings payload | F3 |
| `POST /api/surveys` | Create draft survey | F3 / F5 |
| `PUT /api/surveys/{id}` | Update settings | F3 |
| `POST /api/surveys/{id}/clone` | Clone with "Copy of — ‹name›" prefix | FR-1.8 |
| `POST /api/surveys/{id}/status` | Status transitions with rules-count confirmation payload | F1 |
| `POST /api/surveys/{id}/submit` | Draft → Pending review | F15 |
| `POST /api/surveys/{id}/publish` | Pending review → Active | F15 |
| `POST /api/surveys/{id}/return-to-draft` | Pending review → Draft with remarks | F15 |
| `POST /api/surveys/{id}/sections` / `PATCH …/sections/{sid}` / `DELETE …/sections/{sid}` | Section CRUD | F2 / F8 |
| `POST /api/surveys/{id}/sections/{sid}/sets` etc. | Questions Set CRUD | F2 / F10 |
| `POST /api/surveys/{id}/questions` etc. | Question CRUD | F8 |
| `POST /api/surveys/{id}/questions/{qid}/move` | Cross-section / cross-set move | FR-8.2 |
| `PUT /api/surveys/{id}/questions/{qid}/routing` | Per-question routing map | F9 |
| `POST /api/surveys/{id}/routing` | Enable/disable routing (mutually exclusive with shuffle) | F9 |
| `GET / PUT /api/surveys/{id}/translations/{locale}` | Translate workspace | F11 |
| `GET /api/surveys/{id}/preview?channel=…` | Multi-channel preview | F12 |
| `GET /api/surveys/{id}/report?period=…` | Survey Report data | F13 |
| `GET /api/surveys/{id}/analytics?period=…&granularity=…` | Analytics data | F14 |
| `GET /api/templates` / `POST` / `PATCH` / `DELETE` / `POST /{tid}/instantiate` | Template CRUD + instantiate | F6 / F7 |
| `GET /api/surveys/{id}/render-plan?respondentId=…` | Server-side low-response selection for M-02/M-04 | FR-10.4 |

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** — **Time to first Active survey**: an authorised P-01 can go from "empty tenant" to an Active survey with at least one section, one KPI question and one route in **under 15 minutes**, verified in an unassisted usability walkthrough.
- **SC-002** — **Library load time**: the Survey Library and the builder each open in **under 1.5 s** on standard tenant volumes (NFR-1).
- **SC-003** — **Preview responsiveness**: the live preview and any single configuration change render within **~100 ms** of the change (NFR-1).
- **SC-004** — **Localisation completeness**: every user-facing string reachable in the module has both an English source and an Arabic target; the Translate workspace reports **100 % coverage** for a survey before it can be marked "translated" (author advisory, not a publishing gate).
- **SC-005** — **Approval-lag minimisation**: 90 % of surveys submitted for review are actioned (publish or return-to-draft) within **1 business day**, measured by the M-11 audit log timestamps.
- **SC-006** — **Rule-pause miscommunication**: 0 surveys are paused unintentionally (Active surveys with rules where the confirmation modal was not shown) — traced from telemetry that logs whether the modal was invoked on every Active → Paused transition (FR-1.10).
- **SC-007** — **Report / analytics accuracy**: for a fixture-seeded survey, headline CSAT equals the arithmetic mean of contributing question values to within 0.01 (FR-13.2); funnel deltas equal the mathematical difference in percentage points vs the prior period (FR-14.3).
- **SC-008** — **Accessibility conformance**: automated WCAG 2.1 AA scan passes on every page with **0 critical / 0 serious** issues; every icon-only button has a labelled name (NFR-3).
- **SC-009** — **Post-expiry integrity**: a survey with an active period of 3 days receives 0 late responses in its live report; with post-expiry collection **ON** late responses appear in the M-07 store within 60 s of arrival, and with it **OFF** they are rejected with the expiry message; a response started before but submitted after expiry is rejected in both cases (FR-13.6, BR-3.1, BR-3.4).
- **SC-010** — **Template fidelity**: a survey saved as a template and re-instantiated produces a new survey whose question count, question types, appearance tokens, welcome/thank-you HTML **and journey/stage/touchpoint bindings** all match the source exactly (FR-7.4, FR-6.3).

---

## Assumptions

Reasonable defaults chosen where the SRS does not specify. Each is a decision the `/speckit-plan` step may revisit.

- **English is the source locale**; Arabic is the mandatory secondary; other locales are optional (SRS §7 and FR-11.2 imply the two-language pair; the workspace supports "and any other language" — this spec treats third locales as forward-compatible NFR-6).
- **KPI catalogue** is provided by M-06 and read-through; M-01 does not cache scale definitions and re-queries when a survey opens the builder.
- **Journey binding options** (Personal Loan Application · Account Onboarding · Branch Visit) shown in the SRS are illustrative fixtures; the real list is served by M-16 per tenant.
- **Send-time selection** for Questions Sets is executed by M-02/M-04 using the config stored by M-01 (FR-10.3). M-01 exposes the `render-plan` endpoint as the seam.
- **Post-expiry retention** — **indefinite** in the M-07 store from M-01's perspective; the tenant-level data-retention policy in M-11 governs purge. M-01 sets no independent retention window. Resolved by Clarifications § Session 2026-07-13 (Q2); see BR-3.1.
- **Persistence model** — **no autosave**; edits persist only on explicit Save, with an unsaved-changes navigation guard and optimistic ETag locking on write endpoints. The SRS's "autosave-friendly drafts" phrasing (NFR-5) is interpreted as "safe against accidental data loss via the navigation guard", not as an autosave cadence. Resolved by Clarifications § Session 2026-07-13 (Q1); see NFR-5.
- **Rich-text HTML sanitiser allowlist** — **Full HTML5 subset minus `<script>`, event-handler attributes (`on*`) and `javascript:` URLs**; `<iframe>` disallowed by default. A battle-tested sanitiser runs on every save at server ingress. Resolved by Clarifications § Session 2026-07-13 (Q3); see FR-3.2.
- **AI build path (F5)** — the AI-assisted flow is delegated to the platform's AI orchestration layer; M-01 only exposes the entry point and consumes generated content.
- **Redirect delay of 0 s** is treated as "redirect immediately after submit" (equivalent to null for user-perceived behaviour); the field remains distinct so telemetry can distinguish "author explicitly set 0" from "author left it blank".
- **Cloned surveys and template instantiation both copy all data**, including journey/stage/touchpoint bindings (FR-1.8, FR-6.3, FR-7.4).
- **Response cap for the verbatim table** — the F13 "show more" reveals up to the **last 100** received responses. The full text listing lives in M-05 (SRS Batch H change).

---

## Dependencies

- **M-02 Channel Management** (delivery, distribution rules; pause-a-rule surface).
- **M-04 Response Collection** (attaches responses; enforces active-period expiry).
- **M-05 NLP** (sentiment on Text/Paragraph + comments; frequent-word/theme analytics owned there).
- **M-06 CX Metrics & KPI Engine** (KPI catalogue read-through + score computation).
- **M-07 Dashboards & Reporting** (post-expiry response store; reporting internals).
- **M-09 Alerts** (approval-workflow notifications).
- **M-10 RBAC** ("Publish own surveys" grant + role primitives).
- **M-11 Tenant Admin** (Tenant Design Guidelines, audit log).
- **M-16 Customer Journey Mapping** (journeys / stages / touchpoints).

---

## Clarifications & Open Questions

All nine clarifications raised across `/speckit-clarify` sessions (Q1–Q5 first pass; Q7–Q8 second pass; Q6 re-opened + Q9 third pass) are now **RESOLVED**. Answers are integrated in the sections cited below.

### Q1 — Autosave cadence for the builder & Settings screen — **RESOLVED**

**Resolved (Clarifications § Session 2026-07-13)**: **Option C — Explicit Save only + unsaved-changes navigation guard**. No autosave (debounced or on-blur) — edits persist only when the author clicks Save; navigating away with pending edits shows a blocking confirmation. Optimistic locking (`If-Match: ETag`) applies on all write endpoints to resolve concurrent edits when a second editor is possible (e.g. P-01 editing a Pending-review survey submitted by P-03); a stale ETag returns 412 and the UI surfaces a conflict dialog. See NFR-5.

### Q2 — Post-expiry response retention — **RESOLVED**

**Resolved (Clarifications § Session 2026-07-13)**: **Option A — Indefinite retention, subject to the tenant-level data-retention policy in M-11**. M-01 sets no independent retention window; late responses live in the M-07 post-expiry store until purged by the tenant's M-11 policy. See BR-3.1.

### Q3 — Rich-text HTML source-toggle sanitiser allowlist — **RESOLVED**

**Resolved (Clarifications § Session 2026-07-13)**: **Option A — Full HTML5 subset minus `<script>`, DOM event-handler attributes (`on*`) and `javascript:` URLs**, sanitised at server ingress on every save. `<iframe>` remains disallowed by default; a follow-up tenant permission would be required to enable it. The sanitiser must be battle-tested (OWASP-equivalent) and its allowlist is auditable and versioned so any expansion is a deliberate, tracked change. See FR-3.2.

### Q4 — Template ↔ instantiated survey relationship — **RESOLVED**

**Resolved (Clarifications § Session 2026-07-13)**: **Option B — Snapshot copy, no link**. Instantiation copies all template data into a new, independent Survey row; the Survey stores no foreign-key reference back to the Template. Deleting (or editing) a customized template therefore leaves already-instantiated surveys untouched. Codified as **BR-7.1**.

### Q5 — Tenant `post-expiry feedback collection` setting — flip semantics — **RESOLVED**

**Resolved (Clarifications § Session 2026-07-13)**: **Option A — Evaluated live per response at M-04**. The setting is a live tenant policy: M-04 reads its current value at the moment each incoming post-expiry response is received; there is no snapshot on the Survey row. Flipping the setting takes effect immediately for every survey across the tenant. In-flight responses crossing the expiry boundary remain rejected regardless of the setting. See BR-3.1 and the Active Period & Expiry Lifecycle table.

### Q6 — Survey versioning on Return-to-Draft → edit → re-Publish — **RESOLVED**

**Resolved (Clarifications § Session 2026-07-14)**: **Option D — Destructive edit: warn + purge + invalidate in-flight sessions** (new to this spec; not among the A/B/C options previewed in the second pass). When P-01 initiates Return-to-Draft on an Active or Paused survey, a blocking confirmation warns that all prior responses will be permanently deleted (including any M-07 post-expiry rows for the survey). On confirm, every Response is hard-deleted, every open in-flight respondent session is invalidated, and the survey transitions to Draft with a zero response count — atomically. The Survey entity therefore needs **no `version` field**: at most one Active period's worth of responses ever exists in the survey's history. Codified as **BR-1.6**; see the Status Transition Matrix (destructive markers on Active → Draft and Paused → Draft), Edge Cases (invalidation-of-in-flight bullet), Error Handling & Notifications (blocking modal spec), and the Module Interactions M-04 row (in-flight session invalidation contract).

### Q7 — Reviewer notification fanout on Submit-for-Review — **RESOLVED**

**Resolved (Clarifications § Session 2026-07-13 second pass)**: **Option A — Broadcast to every user holding the review/publish permission** (default: every P-01 in the tenant). M-01 emits a single fan-out event to M-09; whichever qualifying reviewer acts first performs the Publish or Return-to-draft. No `reviewer_id` field on Survey; no first-claim lock at M-01. Individual notification lifecycle (read / dismiss / dedupe) is M-09's concern. See FR-15.2.

### Q8 — Scope of P-03's "(own drafts)" edit right — **RESOLVED**

**Resolved (Clarifications § Session 2026-07-13 second pass)**: **Option A — Team-owned**. Any P-03 in the tenant may edit / configure / submit any Draft authored by any other P-03. **Guardrails**: (i) every edit is audited to the individual acting user (BR-1.2); (ii) the "Publish own surveys" M-10 grant remains **per-individual** (applies only to surveys the granted user personally authored — team-editing does not become team-publishing); (iii) concurrent P-03 edits are resolved by the ETag conflict flow from Q1 (412 + conflict dialog). P-01 retains full edit rights. See Permissions & Roles.

### Q9 — Publish-gate content invariants — **RESOLVED**

**Resolved (Clarifications § Session 2026-07-14)**: **Option A — Publish requires ≥1 section AND ≥1 question total**. Any transition to Active from Draft or Pending review on a survey with `sections_count = 0` OR total `questions_count = 0` is rejected with 409 `publish.requires_content` and an error payload naming the failing invariant. The Publish / Change-status → Active controls are also disabled in the UI with a tooltip. Reactivating a Paused survey is **not** gated (Pause does not remove content). Codified as **BR-1.7**; see the Status Transition Matrix additional-rules bullet and the Publish-gate error entry in Error Handling & Notifications.

---

## SRS Coverage Checklist

Every SRS heading is enumerated below with an explicit status. Any row that says "Partially represented" or "Requires a separate spec run" was **not** silently omitted — its residual gap is called out.

### Front-matter, headers, definitions

| SRS section | Represented in spec | Notes |
|---|---|---|
| Front-matter table (version / author / audience / companion prototype) | ✅ Header of this spec | Companion prototype `m01-survey-screens.html` cited. |
| Contents | ✅ Consumed implicitly | Not reproduced as it is a TOC. |
| 1. Introduction | ✅ Overview + Assumptions | |
| 1.1 Purpose & audience | ✅ Overview | |
| 1.2 Scope | ✅ Overview + Module Interactions | |
| 1.3 Definitions | ✅ Field Definitions & Validation + Assumptions | VOC / KPI / Touchpoint / Section / Questions Set / Routing / Theme / Template / Active period / Seasonal / Comment field. |
| 2. Overall description | ✅ | |
| 2.1 Module interactions | ✅ Module Interactions section | All 8 modules mapped. |
| 2.2 Operating environment | ✅ NFR-1, NFR-2, NFR-3 | Latest two browser versions, RTL, isolation. |
| 2.3 Primary actors | ✅ Permissions & Roles | P-01, P-02, P-03, P-06. |

### 3. System features (F1–F15)

| SRS section | Represented in spec | Notes |
|---|---|---|
| 3.1 F1 — Survey Library | ✅ US1 + FR-1.1..FR-1.14 + BR-1.1..BR-1.5 | All rows preserved; status transition matrix + edit-lock added (BR-1.4, BR-1.5). |
| 3.2 F2 — Sections & Questions Sets | ✅ US3 + FR-2.1..FR-2.8 | Cascade-deletion + routing-reset + translation-purge added (FR-2.4–2.8). |
| 3.3 F3 — Survey Settings | ✅ US1 + FR-3.1..FR-3.6 + BR-3.1..BR-3.4 + Field Definitions | Active-period lifecycle added (BR-3.4). |
| 3.4 F4 — Appearance & live preview | ✅ US1 + FR-4.1..FR-4.4 + Field Definitions | |
| 3.5 F5 — Build method | ✅ US1 + FR-5.1..FR-5.6 | Creation flow, persistence, Back/Cancel added (FR-5.5–5.6). |
| 3.6 F6 — Choose a template | ✅ US5 + FR-6.1..FR-6.4 | |
| 3.7 F7 — Template authoring | ✅ US5 + FR-7.1..FR-7.4 | |
| 3.8 F8 — Build Survey (the builder) | ✅ US1 + FR-8.1..FR-8.12 + BR-8.1..BR-8.5 + Field Definitions | KPI binding model (FR-8.4, BR-8.5), builder status dropdown (FR-8.12), Question Type Catalogue all added. |
| 3.9 F9 — Answer routing (skip logic) | ✅ US4 + FR-9.1..FR-9.5 | Routing × Questions Sets compatibility added (FR-9.5). |
| 3.10 F10 — Questions Set settings & low-response ordering | ✅ US3 + FR-10.1..FR-10.4 | Algorithm preserved verbatim in FR-10.4. |
| 3.11 F11 — Translate workspace | ✅ US6 + FR-11.1..FR-11.3 | |
| 3.12 F12 — Multi-channel preview | ✅ US7 + FR-12.1..FR-12.4 | Section-title on answer page + preview added (FR-12.4). |
| 3.13 F13 — Survey Report | ✅ US8 + FR-13.1..FR-13.7 | Every per-question view mapped. |
| 3.14 F14 — Analytics | ✅ US9 + FR-14.1..FR-14.5 | Numbers-and-calculations table preserved. |
| 3.15 F15 — Approval & publishing workflow | ✅ US2 + FR-15.1..FR-15.6 + BR-15.1..BR-15.3 | |

### 4. Data model

| SRS section | Represented in spec | Notes |
|---|---|---|
| 4.1 Survey | ✅ Key Entities → Survey + Field Definitions | Every column present. |
| 4.2 Section & Questions Set | ✅ Key Entities | |
| 4.3 Question (+ type-specific) | ✅ Key Entities → Question + Field Definitions | |
| 4.4 Theme, Template, Translation | ✅ Key Entities | |

### 5–8. Cross-cutting

| SRS section | Represented in spec | Notes |
|---|---|---|
| 5. Permissions (RBAC — extends M-10) | ✅ Permissions & Roles | Full table preserved incl. grant behaviour. |
| 6. Non-functional requirements | ✅ NFR-1..NFR-8 | Extended with NFR-7 (theming) and NFR-8 (design system) from CLAUDE.md; original 5 preserved. |
| 7. Localization & RTL | ✅ NFR-2 + US6 | |
| 8. Glossary | ✅ Field Definitions / Assumptions / Overview | All glossary terms are cited in-context. |

### 9. Change log — prototype iterations reflected

The Change log is historical context; every current-state rule it documents is captured in the FR / BR tables above. The log's row-level content is not itself a requirement, so it is not reproduced as normative rows here.

| SRS batch | Represented in spec (via current-state FR/BR) | Notes |
|---|---|---|
| Batch A — foundations | ✅ Implicit in the current baseline | No standalone bullets — sub-headings were introduced but bullet content is present under the F1..F15 sections. |
| Batch B — builder, routing, report | ✅ FR-8.x / FR-9.x / FR-13.x | |
| Batch C — placement & polish | ✅ F4 preview placement + F8 header | |
| Batch D — report maths & library UX | ✅ FR-13.2 + F1 rows | |
| Batch E — routing & navigation | ✅ FR-9.1 + FR-3.6 | |
| Batch F — approval, rules & analytics deltas | ✅ FR-15.x + FR-1.9..1.14 + FR-14.3 | |
| Batch G — sections & sets, seasonal binding, active-period, report types | ✅ FR-2.x + FR-10.x + BR-3.x + FR-8.8 + FR-8.9 + FR-13.3 | |
| Batch H — verbatim sampling & sentiment flag (current) | ✅ FR-13.3 + FR-13.7 + FR-8.11 + KPI-question reason-object init defect fix noted in Field Definitions | |

### Coverage summary

- **Fully represented**: every FR (1.1–1.14, 2.1–2.8, 3.1–3.6, 4.1–4.4, 5.1–5.6, 6.1–6.4, 7.1–7.4, 8.1–8.12, 9.1–9.5, 10.1–10.4, 11.1–11.3, 12.1–12.4, 13.1–13.7, 14.1–14.5, 15.1–15.6) and every BR (1.1–1.7, 3.1–3.4, 7.1, 8.1–8.5, 15.1–15.3). Clarification-derived additions (across revisions): FR-2.4–2.8, FR-5.5–5.6, FR-8.12, FR-9.5, FR-12.4; BR-1.4–1.5, **BR-1.6 (Q6, destructive Return-to-Draft-to-edit)**, **BR-1.7 (Q9, Publish-gate content invariants: ≥1 section + ≥1 question)**, BR-3.4, BR-7.1 (Q4, template snapshot-no-link), BR-8.5. `/speckit-clarify` § Session 2026-07-13 resolved Q1 (autosave → explicit Save + ETag), Q2 (post-expiry retention → tenant policy), Q3 (HTML sanitiser → Full-HTML5-minus-unsafe), Q4 (template deletion → snapshot no-link), Q5 (post-expiry setting flip → live evaluation). § Session 2026-07-13 second pass resolved Q7 (reviewer notification → broadcast to all reviewers, FR-15.2) and Q8 (P-03 "own drafts" scope → team-owned, Permissions & Roles). § Session 2026-07-14 resolved **Q6 (survey versioning → Option D: destructive edit, purge on RtD; no `version` field on Survey)** and **Q9 (Publish-gate content invariants → ≥1 section + ≥1 question)**. **All clarifications resolved — spec is `/speckit-plan`-ready.**
- **Partially represented**: none. Every SRS section either appears as a normative requirement or (for the historical change log) is superseded by the current-state FRs.
- **Requires a separate `/speckit-specify` run**: none. The SRS is a single self-contained module (M-01). Downstream modules (M-02 · M-04 · M-05 · M-06 · M-07 · M-09 · M-10 · M-11 · M-16) are already in the Dependencies section as external contracts and belong to their own separate spec-kit features.
