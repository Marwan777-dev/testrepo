# Feature Specification: Customer Journey Mapping Module (M-16)

**Feature Branch**: `[M-16-customer-journey-mapping]`

**Created**: 2026-06-08

**Status**: Draft

**Input**: Customer Journey Mapping Module (M-16) SRS. This module builds on M-11 Tenant Administration and M-10 User & Role Management, and defines tenant-scoped journey configuration, journey-local touchpoints, KPI bindings, strategic satisfaction scoring, journey versioning, persona reuse, pain/happy detection, report contract definitions for M-07, and role-based access control that extends M-10’s RBAC.

## Clarifications

### Session 2026-06-08 (third pass)
- Q: How does M-16's `journey_scores` table get populated and when does `journey.score.updated` fire? → Scores are computed **on request**, not event-driven. M-16 exposes a published interface `IJourneyScoreProvider` that consumers call to retrieve journey scores. When called, M-16 delegates score computation to M-06 via M-06's published interface (using journey config from `IJourneyConfigReader`), stores the result in `journey_scores`, publishes `journey.score.updated` to M-17, and returns the result. M-16 does NOT subscribe to `survey.response.submitted`; score freshness is the caller's responsibility.
- Q: Persona lifecycle states → Personas follow the same four-state lifecycle as journeys: `Draft` (initial, created but not bindable to journeys) → `Active` (live, bindable to journeys) ↔ `Inactive` (temporarily suspended, not bindable to new journeys) → `Archived` (terminal, not bindable). Only P-01 may transition persona status. `Archived` is terminal and irreversible. Only `Active` personas appear in the journey persona binding selector.
- Q: DetectionConfig rule structure → Score-threshold based. A `DetectionConfig` holds a journey-level `painThreshold` (touchpoint/stage score at or below this value = pain point) and `happyThreshold` (score at or above this value = happy moment). Thresholds may be overridden per stage or per touchpoint; the most specific override wins. No NLP/sentiment dependency — rules operate entirely on the KPI scores computed by M-06 and returned via `IJourneyScoreProvider`.
- Q: JourneyVersion snapshot contents → Full serialized copy. A `JourneyVersion` snapshot stores a complete, independent copy of the journey configuration at publish time: all stages (with sequence, metadata), all touchpoints (with channels, importance, `isMoT`, `isMandatory`), all KPI bindings (with types and weights), `ScoringConfig`, and `DetectionConfig`. The snapshot is self-contained and fully independent of any subsequent edits to the live journey. Historical version retrieval returns the exact configuration captured at publish time without reconstructing from deltas.
- Q: Concurrent edit conflict strategy → Last-write-wins with stale-edit notification. No version tracking, no `409 Conflict` responses — later saves silently overwrite earlier ones. However, the UI MUST notify any user currently editing a journey when that journey has been updated by another user, surfacing a non-blocking banner or toast (e.g., "This journey was updated by another user — reload to see the latest changes"). The notification does NOT block saving; it is informational only.

### Session 2026-06-08 (second pass)
- Q: Audit ownership — `AuditRecorder` vs M-17 pattern → M-16 uses `M17EventPublisher` to publish all journey configuration events to M-17’s `event_log` in the same database transaction. `AuditRecorder` is removed from M-16’s unit under test lists. FR-015 updated to reference M-17 (not M-11). Rationale: AMENDMENT-006 transferred audit ownership from M-11 to M-17.
- Q: Which personas can manage journey configurations → P-01 (CX Program Manager) has full journey configuration authority including publishing versions and managing personas. P-02 (CX Analyst) can create and edit journeys, stages, touchpoints, KPI bindings, and detection rules but cannot publish journey versions or manage personas — those actions are restricted to P-01. All other personas (P-03..P-08) are read-only or have no journey access. Enforced at the data layer via M-10 RBAC.
- Q: Journey lifecycle state machine → Four states: `Draft` (initial, editable) → `Active` (live and in use) ↔ `Inactive` (temporarily suspended, can be reactivated to `Active`) → `Archived` (permanently decommissioned, read-only). Versioning is separate from status — a `Draft` journey accumulates versions; status controls whether the journey is live. Only P-01 may transition status. `Archived` is terminal; an `Archived` journey cannot return to any other state.
- Q: Frontend scope in Phase 1 → Full frontend + backend in Phase 1. All four user stories include tenant portal pages: Journey Builder (create journey, add stages and touchpoints), KPI & Scoring configuration UI, Persona & Versioning management UI, and Detection Rules configuration UI. E2E coverage added to all user stories; the stale "UI built later" assumption is removed.
- Q: Maximum stages and touchpoints limits and configuration → Default maximum: 20 stages per journey; 30 touchpoints per stage. Limits are configurable per tenant by M-11 (Tenant Administration) at tenant creation time. Exceeding the stage limit must display: "You've reached the maximum of 20 stages. Consider splitting this into multiple journeys." Exceeding the touchpoint limit must display a comparable user-friendly message. All limits are enforced at the data layer as well as in the UI.
- Q: DB-02 / AD-02 compliance for M-16 entities → All 9 M-16 entities (Journey, Stage, Touchpoint, KpiBinding, ScoringConfig, Persona, JourneyVersion, DetectionConfig, ReportContract) reside in the per-tenant schema (`tenant_{slug}`). None are in the control-plane database. No entity carries a `tenantId` column — tenant isolation is enforced solely by the schema boundary per AD-02. **`ScoringConfig` is a per-tenant singleton** (one row per tenant schema, SRS §4.2.9 Q11); it carries no `tenantId` and no `journeyId` — the schema boundary *is* the tenant scope, and a unique index on a constant expression enforces the single row.
- Q: MoT (Moment of Truth) definition → `isMoT: boolean` flag on Touchpoint, set by the journey author. A touchpoint designated as a Moment of Truth receives elevated priority in pain/happy detection and reporting. Not auto-derived from scores.
- Q: Valid KPI types for `KpiBinding.type` → Extended closed list of six platform-standard types: `NPS`, `CSAT`, `CES`, `FCR`, `AgentSatisfaction`, `VFM`. In addition, tenants may define custom KPI types stored in a tenant-scoped KPI type registry. `KpiBinding.type` references either a platform-standard type or a tenant-defined type. Type-aware scoring behavior (e.g., CES is inverted — lower is better) applies to platform-standard types only; custom types use the default scoring direction.
- Q: M-06/M-07 integration contract pattern → M-16 exposes two published synchronous interfaces per AD-01: `IJourneyConfigReader` (consumed by M-06 to retrieve scoring configuration, KPI bindings, and stage/touchpoint structure) and `IReportContractReader` (consumed by M-07 to retrieve report contract metadata). Both are in-process calls within the modular monolith. M-06 and M-07 MUST NOT read M-16 tenant-schema tables directly.
- Q: Journey name uniqueness → Journey names MUST be unique per tenant, case-insensitive. Duplicate name attempts are rejected with a clear validation error. When a journey reaches `Archived` status its name is released and becomes available for reuse by a new journey.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Define a customer journey with stages and touchpoints (Priority: P1)

A tenant journey author creates a new journey, adds stages, and configures journey-local touchpoints with channel, importance, MoT, mandatory flags, and KPI bindings.

**Why this priority**: Journey configuration is the foundational capability for customer journey mapping and all downstream reporting and score analysis.

**Independent Test**: Create a journey, add stages and touchpoints, assign KPI bindings, and verify the persisted journey includes the configured structure and metadata.

**Acceptance Scenarios**:

1. **Given** a P-01 or P-02 user authenticated in the tenant, **When** they create a new journey, **Then** the system saves the journey with name, description, type, persona bindings, and status `Draft`.
2a. **Given** a P-03..P-08 user authenticated in the tenant, **When** they attempt to create or edit a journey, **Then** the operation is rejected with `403 Forbidden` at the data layer.
2b. **Given** a P-01 user, **When** they transition a `Draft` journey to `Active`, or an `Active` journey to `Inactive`, or a `Inactive` journey back to `Active`, or any non-`Archived` journey to `Archived`, **Then** the transition is persisted and a status-change event is published to M-17.
2c. **Given** a P-01 user attempts to transition an `Archived` journey to any other status, **Then** the system rejects the transition with a clear error — `Archived` is terminal.
2. **Given** a saved journey, **When** the user adds stages and touchpoints, **Then** the system persists each stage and journey-local touchpoint with channel, importance, MoT, mandatory, and KPI references.
3. **Given** a touchpoint without KPI bindings, **When** the user views the journey, **Then** the system marks it as unmeasured and excludes it from score calculation in M-06.
4. **Given** the journey includes stages and touchpoints, **When** the user reorders stages, **Then** the change is accepted and stage order is preserved.

**Unit Test Coverage**:

- **Units under test**: `JourneyService`, `StageService`, `TouchpointService`, `ImportantValidation`, `JourneyPersistence`, `M17EventPublisher`.
- **Required cases**:
  - `CreateJourney(request)` with valid journey metadata → persists journey entity and returns `journeyId`.
  - `AddStage(journeyId, stageData)` → persists stage with correct sequence and metadata.
  - `AddTouchpoint(stageId, touchpointData)` → persists journey-local touchpoint and channel set.
  - `GetJourney(journeyId)` → returns journey, stages, touchpoints, and KPI bindings.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/journeys` creates a journey.
  - `POST /api/journeys/{journeyId}/stages` adds stages.
  - `POST /api/stages/{stageId}/touchpoints` adds touchpoints.
- **What's intentionally NOT covered end-to-end**: M-06 score computation, which is consumed by M-16 but implemented in a sibling module.

**Scenario Test**:

- `scenario-test: JourneyDefinitionFlow`.

**E2E Test Coverage**:

- **User flows under test**: P-01 and P-02 users creating a journey, adding stages and touchpoints; status transitions; access denial for non-admin personas.
- **Required scenarios**:
  - P-01 authenticated user navigates to the Journey Builder page, creates a new journey with name, description, and type, and sees it appear in the journey list with status `Draft`.
  - P-01 or P-02 user adds stages and touchpoints to the journey; the builder reflects the updated structure immediately.
  - P-01 user transitions a journey from `Draft` to `Active`; the status badge updates and a confirmation is shown.
  - P-01 user transitions an `Active` journey to `Inactive` and then back to `Active`.
  - P-01 user archives a journey; the journey becomes read-only and the `Archived` state cannot be reversed.
  - P-03 user cannot see the create journey button or access the journey edit page.

---

### User Story 2 - Configure KPI bindings and scoring parameters (Priority: P1)

A journey author assigns KPIs to touchpoints and configures the strategic scoring parameters used by M-06 to compute touchpoint, stage, and journey scores.

**Why this priority**: KPI binding and scoring configuration are required for the journey map to support meaningful CX measurement.

**Independent Test**: Configure KPIs for touchpoints, set KPI weights, and verify the configuration persists and meets validation rules.

**Acceptance Scenarios**:

1. **Given** a P-01 or P-02 user on a touchpoint configuration page, **When** they configure one or more KPIs with weights, **Then** the system stores the KPI list and enforces the 100% weight rule.
2. **Given** a KPI weight total that does not equal 100%, **When** the user saves, **Then** the system rejects the change with a clear inline validation error on the KPI configuration form.
3. **Given** a tenant, **When** a P-01 user sets the tenant-level strategic scoring parameters (α, MOT multiplier, n_floor, flag percentile, rolling-window days), **Then** the system persists the single per-tenant `ScoringConfig` row and exposes it to M-06 via `IScoringConfigStore`. The parameters apply to every journey in the tenant — there are no per-journey overrides.
4. **Given** NPS is selected for a touchpoint, **When** the user saves, **Then** the system surfaces a non-blocking informational warning on the configuration form.

**Unit Test Coverage**:

- **Units under test**: `KpiConfigurationService`, `ScoreParameterService`, `KpiWeightValidator`, `ScoringModelPersistence`, `M17EventPublisher`.
- **Required cases**:
  - `SaveKpiBindings(touchpointId, kpis)` → persists KPI definitions with weights.
  - `ValidateKpiWeights(kpis)` if weights do not sum to 100% → returns validation failure.
  - `SaveScoringParameters(scoringConfig)` → upserts the single per-tenant `ScoringConfig` row (no `journeyId`); validates α ∈ [0,1], MOT ∈ [1.0,2.0], n_floor ≥ 1, flag_percentile ∈ [1,49], rolling_window_days ≥ 7.
  - `AddNpsKpi(touchpointId)` → records informational warning state.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `PUT /api/touchpoints/{touchpointId}/kpis` persists KPI bindings.
  - `PUT /api/v1/tenant/scoring-config` persists the tenant-level scoring configuration (one row per tenant; no journey-level scoring endpoint).
- **What's intentionally NOT covered end-to-end**: the actual scoring execution by M-06.

**Scenario Test**:

- `scenario-test: KpiAndScoringConfiguration`.

**E2E Test Coverage**:

- **User flows under test**: KPI configuration panel on a touchpoint; weight validation error state; scoring parameters form; NPS warning state.
- **Required scenarios**:
  - P-01 or P-02 user opens a touchpoint's KPI configuration panel, adds two KPIs with weights summing to 100%, and saves successfully.
  - P-01 or P-02 user enters KPI weights that do not sum to 100% and sees an inline validation error blocking save.
  - P-01 or P-02 user adds NPS as a KPI and sees a non-blocking informational warning displayed on the form.
  - P-01 or P-02 user configures the journey's strategic scoring parameters and saves; the configuration is reflected in the journey summary.

---

### User Story 3 - Manage personas and journey versioning (Priority: P2)

A tenant user defines optional customer personas, reuses persona configuration across journeys, and publishes journey versions while preserving historical snapshots.

**Why this priority**: Persona reuse and version control keep journey configurations maintainable and reportable over time.

**Independent Test**: Create a persona, bind it to a journey, publish a version, then update the journey and verify a new version is created while the prior version remains immutable.

**Acceptance Scenarios**:

1. **Given** a P-01 user authenticated in the tenant, **When** they create a new persona, **Then** the system stores it with Arabic and English labels and status `Draft`.
1a. **Given** a P-02 user authenticated in the tenant, **When** they attempt to create or change the status of a persona, **Then** the operation is rejected with `403 Forbidden` at the data layer.
1b. **Given** a P-01 user, **When** they transition a persona through its lifecycle (`Draft` → `Active`, `Active` ↔ `Inactive`, any non-`Archived` → `Archived`), **Then** the transition is persisted and a persona status-change event is published to M-17.
1c. **Given** a P-01 user attempts to transition an `Archived` persona to any other status, **Then** the system rejects the transition — `Archived` is terminal.
2. **Given** a P-01 user authenticated in the tenant, **When** they publish a journey version, **Then** the system creates a new immutable version snapshot and preserves the previous version.
2a. **Given** a P-02 user authenticated in the tenant, **When** they attempt to publish a journey version, **Then** the operation is rejected with `403 Forbidden` at the data layer.
3. **Given** a journey version is published, **When** any authorized user requests historical details, **Then** the system returns the exact published configuration at that version.
4. **Given** a persona is in `Draft`, `Inactive`, or `Archived` status, **When** a user attempts to bind it to a journey, **Then** the system prevents the binding. Only `Active` personas may be bound.

**Unit Test Coverage**:

- **Units under test**: `PersonaService`, `JourneyVersionService`, `VersionValidation`, `PersonaBindingService`, `M17EventPublisher`.
- **Required cases**:
  - `CreatePersona(request)` → persists persona with status Draft.
  - `PublishJourneyVersion(journeyId)` → creates new version snapshot.
  - `GetJourneyVersion(journeyId, version)` → returns the full serialized snapshot (stages, touchpoints, KPI bindings, ScoringConfig, DetectionConfig) captured at publish time, unchanged by any subsequent edits.
  - `TransitionPersonaStatus(personaId, newStatus)` → enforces valid transitions and rejects terminal `Archived` → any.
  - `BindPersonaToJourney(journeyId, personaId)` with non-`Active` persona → returns binding rejection error.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/personas` creates a persona.
  - `POST /api/journeys/{journeyId}/publish` creates a new published version.
  - `GET /api/journeys/{journeyId}/versions/{version}` retrieves version details.
- **What's intentionally NOT covered end-to-end**: M-06 score computation triggered by version publishing.

**Scenario Test**:

- `scenario-test: PersonaAndVersionManagement`.

**E2E Test Coverage**:

- **User flows under test**: P-01 user creating and archiving personas; P-01 publishing journey versions; version history viewer; P-02 being denied publish/persona-manage actions.
- **Required scenarios**:
  - P-01 authenticated user navigates to the Personas management page, creates a persona with Arabic and English labels, and sees it in the persona list with status `Draft`.
  - P-01 user transitions a persona from `Draft` to `Active`; it now appears in the journey persona binding selector.
  - P-01 user transitions an `Active` persona to `Inactive`; it disappears from the binding selector immediately.
  - P-01 user archives a persona; the persona no longer appears in the journey persona binding selector and the `Archived` status cannot be reversed.
  - P-02 user cannot see persona status transition controls (hidden or disabled in the UI).
  - P-01 user publishes a journey version; the version history panel shows the new version with a timestamp.
  - P-02 user cannot see or access the "Publish Version" action.
  - Any authorized user can open the version history panel and view an earlier version's configuration in read-only mode.

---

### User Story 4 - Detect pain points, happy moments, and expose report contracts (Priority: P2)

The journey module defines pain point and happy moment detection rules, and provides reporting metadata that M-07 consumes for dashboards.

**Why this priority**: Detection configuration and report contracts turn configured journeys into actionable analytics.

**Independent Test**: Configure detection rules, save them, and verify the module exposes report definitions compatible with dashboard consumption.

**Acceptance Scenarios**:

1. **Given** a P-01 or P-02 user on the journey detection configuration page, **When** they configure pain/happy detection signals, **Then** the system persists detection rules and flags.
2. **Given** configured detection rules, **When** M-07 requests reporting output definitions, **Then** the system returns a report contract that includes journey, stage, and touchpoint score dimensions.
3. **Given** a journey with no KPIs on a touchpoint, **When** pain detection is evaluated, **Then** the system treats that touchpoint as unmeasured and excludes it from detection calculations.

**Unit Test Coverage**:

- **Units under test**: `DetectionConfigService`, `ReportContractService`, `JourneyReportingRepository`, `M17EventPublisher`.
- **Required cases**:
  - `SaveDetectionConfig(journeyId, config)` with `painThreshold=40, happyThreshold=75` → persists thresholds and returns saved config.
  - `SaveDetectionConfig` with stage-level override → stores override; override takes precedence over journey default.
  - `GetReportContract(journeyId)` → returns report metadata.
  - `GetReportContract` for unmeasured touchpoints → excludes those touchpoints appropriately.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `PUT /api/journeys/{journeyId}/detection` persists detection rules.
  - `GET /api/journeys/{journeyId}/reports` returns report contract metadata.
- **What's intentionally NOT covered end-to-end**: M-07 dashboard rendering.

**Scenario Test**:

- `scenario-test: DetectionAndReportContract`.

**E2E Test Coverage**:

- **User flows under test**: P-01 and P-02 users configuring detection rules on the journey configuration page.
- **Required scenarios**:
  - P-01 or P-02 user navigates to the detection configuration section of a journey, sets a journey-level pain threshold and happy threshold, and saves; the configuration is reflected in the journey summary.
  - P-01 or P-02 user sets a stage-level threshold override; the stage card in the journey map reflects the override badge.
  - A touchpoint with no KPI bindings is visually marked as "unmeasured" in the detection configuration view and is excluded from detection score calculation.

---

### Edge Cases

- What happens when two users edit the same journey simultaneously? Last-write-wins — the later save is persisted without a conflict error. The UI notifies any user currently editing that the journey was updated by another user (non-blocking banner/toast). No server-side locking.
- What happens if a journey update removes a stage that still contains touchpoints? The system must reject the update unless touchpoints are deleted or reassigned.
- How does the system handle KPI weights that sum to more or less than 100%? It must block save and return a clear validation message.
- What happens when a journey is archived while bound to active surveys? The system must prohibit archiving and require survey unbinding first.
- How does the module behave when a persona configuration is deleted while it is still bound to journeys? It must prevent deletion or require unbinding first.
- What happens if a user creates or renames a journey to a name already used by another active journey? The system must reject the change with a clear validation error. Names held by `Archived` journeys are considered released and may be reused.
- What if a customer journey contains more than the allowed maximum stages or touchpoints? The system must enforce the configured per-tenant limits (default: 20 stages, 30 touchpoints/stage). The stage limit error message is: "You've reached the maximum of 20 stages. Consider splitting this into multiple journeys." (or the tenant-configured value). Enforcement is at both the data layer and the UI.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow tenant users to define customer journeys with stages, journey-local touchpoints, and persona bindings. Journey names MUST be unique per tenant, case-insensitive. Attempting to create or rename a journey to a name already held by a non-`Archived` journey MUST be rejected with a clear validation error. An `Archived` journey releases its name, making it available for reuse.
- **FR-002**: System MUST allow journey authors to configure touchpoint metadata including channels, importance, `isMoT` (boolean flag designating this touchpoint as a Moment of Truth — set by the journey author, not auto-derived from scores; MoT touchpoints receive elevated priority in pain/happy detection and reporting), mandatory flag, and KPI bindings.
- **FR-003**: System MUST support KPI configuration per touchpoint with weights that sum to 100%. Valid KPI types are: `NPS`, `CSAT`, `CES`, `FCR`, `AgentSatisfaction`, `VFM` (platform-standard), plus any tenant-defined types stored in the tenant's KPI type registry. Platform-standard types carry type-aware scoring behavior known to M-06 (e.g., `CES` is inverted — lower effort is better). Tenant-defined types use default scoring direction. The system MUST reject `KpiBinding` entries that reference an unknown type (not in the platform list and not in the tenant registry).
- **FR-004**: System MUST allow tenants to configure the strategic scoring parameters that M-06 consumes for score computation. Per SRS §4.2.9 / §11.7 (Q11 RESOLVED), `ScoringConfig` is **tenant-scoped — exactly one row per tenant**, NOT per journey. All journeys within a tenant share the same scoring parameters (keeping scoring methodology consistent and cross-journey comparable). The configured parameters are: `alpha` (α blend; β derived as `1 − α`), `mot_multiplier`, `n_floor`, `flag_percentile`, and `rolling_window_days`. The tenant `ScoringConfig` is owned by M-16 and exposed to M-06 via the `IScoringConfigStore` published interface; the per-tenant editing surface is rendered on the Platform Settings → Customer Journey page (M-06/M-11 feature 003) and persisted through M-16's API. M-06 MUST NOT read M-16 tables directly. The journey-level scoring-config endpoints are removed — a single tenant-level `GET/PUT /api/v1/tenant/scoring-config` pair replaces them. Each published `JourneyVersion` snapshots the active `ScoringConfig` values at publish time so historical recomputation uses the parameters that were live for that version (FR-006).
- **FR-017**: System MUST expose a `IJourneyScoreProvider` published interface that computes and returns journey scores on demand. When called, M-16 MUST: (1) call M-06 via M-06's published scoring interface to compute scores using the journey's configuration; (2) persist the result in `journey_scores`; (3) publish a `journey.score.updated` event to M-17. Score computation is synchronous and on-request — M-16 does NOT subscribe to `survey.response.submitted` or any other event to trigger score updates.
- **FR-005**: System MUST support optional persona definitions and reuse of persona configuration across journeys. Personas follow a four-state lifecycle: `Draft` → `Active` ↔ `Inactive` → `Archived`. Only `Active` personas may be bound to journeys. Only P-01 may transition persona status. `Archived` is terminal. All persona status transitions MUST be published as events to M-17.
- **FR-006**: System MUST preserve prior published journey versions when a journey is updated. Each published version MUST be stored as a full serialized copy of the journey configuration at that point in time — including all stages, touchpoints, KPI bindings, `ScoringConfig`, and `DetectionConfig`. The snapshot MUST be immutable and self-contained; subsequent edits to the live journey MUST NOT alter any published version.
- **FR-016**: System MUST enforce a four-state journey lifecycle: `Draft` → `Active` ↔ `Inactive` → `Archived`. Only P-01 may transition journey status. The `Archived` state is terminal and irreversible. All status transitions MUST be published as events to M-17.
- **FR-007**: System MUST support pain point and happy moment detection configuration for journeys using score thresholds. A journey's `DetectionConfig` MUST define a `painThreshold` (score at or below this value classifies a touchpoint or stage as a pain point) and a `happyThreshold` (score at or above this value classifies it as a happy moment). Thresholds may be overridden per stage or per touchpoint; the most specific override wins. Detection operates on KPI scores computed via `IJourneyScoreProvider` — no NLP or sentiment signal dependency.
- **FR-008**: System MUST provide reporting output definitions consumable by M-07 via the `IReportContractReader` published interface. M-07 MUST NOT read M-16 tables directly.
- **FR-009**: System MUST enforce role-based access for journey configuration using M-10 RBAC. P-01 (CX Program Manager) has full journey configuration authority including publishing versions and managing personas. P-02 (CX Analyst) may create and edit journeys, stages, touchpoints, KPI bindings, and detection rules but MUST NOT publish journey versions or create/archive personas — those actions are P-01-only and MUST be enforced at the data layer. All other personas (P-03..P-08) are read-only or have no journey module access.
- **FR-010**: System MUST treat unmeasured touchpoints as excluded from score computation and detection.
- **FR-011**: System MUST support Arabic and English content with RTL-aware behavior for Arabic.
- **FR-012**: System MUST enforce per-tenant maximum limits on journey stages and touchpoints. Default limits: 20 stages per journey; 30 touchpoints per stage. Limits are configured per tenant by M-11 at tenant creation and are readable by M-16 via M-11's published interface. Exceeding the stage limit MUST display: "You've reached the maximum of 20 stages. Consider splitting this into multiple journeys." (substituting the tenant-configured value). Exceeding the touchpoint limit MUST display a comparable user-friendly message. Enforcement applies at both the data layer and the UI.
- **FR-013**: System MUST prevent deletion of personas and journeys that are still referenced by active bindings.
- **FR-018**: System MUST use last-write-wins for concurrent journey edits — no version tracking or `409 Conflict` responses. The UI MUST notify any user currently viewing or editing a journey when that journey has been updated by another user since they loaded it. The notification MUST be non-blocking (banner or toast) and MUST NOT prevent saving. No server-side locking is required.
- **FR-014**: System MUST provide clear validation errors for invalid configuration changes.
- **FR-015**: System MUST preserve tenant isolation and publish all journey configuration change events to M-17's `event_log` via `M17EventPublisher` in the same database transaction as the triggering action. M-17 derives all audit records from these events. M-16 MUST NOT write directly to `audit_log` or maintain its own audit table. All M-16 entities reside in the per-tenant schema (`tenant_{slug}`) with no `tenantId` columns; tenant isolation is enforced by the schema boundary per AD-02/DB-02.

### Key Entities

- **Journey** *(tenant schema, no tenantId)*: Represents a customer journey with stages, touchpoints, persona bindings, scoring parameters, version history, and report contract definitions. Key attributes: `name` (unique per tenant, case-insensitive; released on `Archived`), `description`, `type`, `status`. Status lifecycle: `Draft` (initial, editable by P-01 and P-02) → `Active` (live) ↔ `Inactive` (temporarily suspended) → `Archived` (terminal, read-only; name released for reuse). Status transitions are P-01-only. `Archived` is irreversible.
- **Stage** *(tenant schema, no tenantId)*: Represents a phase in a journey containing touchpoints, customer goal, expected emotion, duration, and sequence behavior.
- **Touchpoint** *(tenant schema, no tenantId)*: Represents a journey-local interaction point with channels, importance ratings, `isMoT: boolean` (Moment of Truth flag, author-set — elevates this touchpoint in pain/happy detection and reporting), `isMandatory: boolean`, KPI bindings, and descriptions.
- **KpiBinding** *(tenant schema, no tenantId)*: Represents an assigned KPI on a touchpoint. Key attributes: `type` (one of the six platform-standard types — `NPS`, `CSAT`, `CES`, `FCR`, `AgentSatisfaction`, `VFM` — or a reference to a tenant-defined KPI type), `weight` (percentage, all bindings on a touchpoint must sum to 100%), and scoring direction inherited from the type (`CES` is inverted; all others are default ascending).
- **ScoringConfig** *(tenant schema, no tenantId)*: Represents the **tenant-level** strategic satisfaction scoring parameters consumed by M-06 (SRS §4.2.9 / §11.7). **Exactly one row per tenant** — singleton, enforced by a unique index on a constant expression — NOT per journey; all journeys in the tenant share it. Key attributes: `alpha` (numeric(4,3), α ∈ [0,1]; β = 1 − α), `mot_multiplier` (numeric(3,1) ∈ [1.0,2.0]), `n_floor` (int ≥ 1), `flag_percentile` (int ∈ [1,49]), `rolling_window_days` (int ≥ 7), plus `updated_by`/`updated_at` audit columns. The earlier per-journey `model_type` / `stage_weight_mode` / `normalization_params` shape is removed — the scoring formulas are owned and implemented by M-06 (SRS §11) and are not tenant-configurable per journey. Edited via the Platform Settings → Customer Journey surface (feature 003) and read by M-06 through `IScoringConfigStore`. Snapshotted into each `JourneyVersion` at publish time.
- **Persona** *(tenant schema, no tenantId)*: Represents an optional customer archetype with localized labels, descriptions, and reusable journey bindings. Four-state lifecycle mirroring journey status: `Draft` (initial, not bindable) → `Active` (live, bindable to journeys) ↔ `Inactive` (temporarily suspended, not bindable to new journeys) → `Archived` (terminal, not bindable; irreversible). Only P-01 may transition persona status. Only `Active` personas appear in the journey persona binding selector.
- **JourneyVersion** *(tenant schema, no tenantId)*: Represents an immutable published snapshot of journey configuration. Contains a full serialized copy of: all stages (sequence + metadata), all touchpoints (channels, importance, `isMoT`, `isMandatory`), all KPI bindings (type + weight), `ScoringConfig`, and `DetectionConfig` — captured at publish time. Key attributes: `versionNumber` (sequential integer per journey), `publishedAt`, `publishedBy`, `snapshotPayload` (serialized journey tree). Self-contained; not affected by subsequent edits to the live journey.
- **DetectionConfig** *(tenant schema, no tenantId)*: Represents score-threshold detection rules for a journey. Key attributes: `journeyId`, `painThreshold` (score ≤ this = pain point, journey-level default), `happyThreshold` (score ≥ this = happy moment, journey-level default). Stage and touchpoint-level threshold overrides are stored as child records; the most specific override wins. No NLP dependency — detection operates on KPI scores from `IJourneyScoreProvider`.
- **ReportContract** *(tenant schema, no tenantId)*: Represents the metadata that M-07 consumes to render journey dashboards and reports.
- **KpiTypeDefinition** *(tenant schema, no tenantId)*: Represents a tenant-defined custom KPI type, stored in the tenant's KPI type registry. Key attributes: `typeKey` (unique identifier within the tenant), `labelAr`, `labelEn`, `scoringDirection` (ascending or descending; default ascending). Platform-standard types (`NPS`, `CSAT`, `CES`, `FCR`, `AgentSatisfaction`, `VFM`) are not stored here — they are built into the platform.
- **JourneyScore** *(tenant schema, no tenantId)*: Represents the most recently computed score snapshot for a journey, produced by a call to `IJourneyScoreProvider`. Key attributes: `journeyId`, `computedAt`, `touchpointScores`, `stageScores`, `journeyScore`. Updated on every call to `IJourneyScoreProvider`; consumers requesting scores trigger a fresh computation and update this record.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Journey authors can define and save a journey with stages, touchpoints, and KPI bindings in a single session.
- **SC-002**: KPI configurations that do not sum to 100% are rejected with a clear validation error.
- **SC-003**: Published journey updates create a new version while preserving the previous published configuration.
- **SC-004**: Report contract metadata is available for every active journey and can be retrieved by M-07.
- **SC-005**: Persona reuse and journey binding behavior work for at least one persona per tenant.
- **SC-006**: Journey configuration supports Arabic and English labels and text, including RTL-aware rendering expectations.

## Assumptions

- M-10 and M-11 are available and provide RBAC and tenant isolation capabilities. M-17 provides the audit/event log infrastructure; M-16 publishes events to M-17 and does not own audit records directly.
- M-11 (Tenant Administration) stores per-tenant configuration values for M-16, including the maximum stages-per-journey and touchpoints-per-stage limits (defaults: 20 and 30 respectively). M-16 reads these limits via M-11's published interface at runtime.
- M-06 is responsible for applying scoring, normalization, and response-volume adjustments based on M-16 configuration. M-06 accesses M-16 configuration exclusively via the `IJourneyConfigReader` published interface (in-process, per AD-01). M-16 calls M-06's published scoring interface when `IJourneyScoreProvider` is invoked by a consumer; scores are computed on-request, not event-driven.
- M-07 consumes journey report contract definitions from M-16 exclusively via the `IReportContractReader` published interface (in-process, per AD-01) and renders dashboards separately.
- Journey-local touchpoints are not shared across journeys in Phase 1.
- The tenant portal frontend ships in Phase 1 alongside the backend API. Pages included in Phase 1: Journey Builder (create/edit journey, stages, touchpoints), KPI & Scoring Configuration, Persona Management, Version History viewer, Detection Rules configuration.
