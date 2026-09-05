# Feature Specification: Customer Journey Mapping Module (M-16)

**Feature Branch**: `[M-16-customer-journey-mapping]`

**Created**: 2026-06-08

**Status**: Draft

**Input**: User description: "Build the Customer Journey Mapping Module (M-16) for the Nabadat VOC platform. This module is the third implementation phase after M-11 and M-10, and it consumes RBAC primitives, tenant isolation, and audit logging from those foundational modules. M-16 owns journey configuration, journey-local touchpoints, KPI config per touchpoint, strategic satisfaction scoring, journey versioning, pain point and happy moment signal definitions, reporting output definitions for M-07, role-based access that extends M-10 RBAC, and Arabic/English localization. It must not include survey builder UI, response collection mechanisms, text analytics algorithms, shared touchpoint libraries, survey-to-journey bindings, score computation execution, dashboard UI rendering, AI-generated journey suggestions, bulk CSV import, or real-time collaborative editing."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create and version a customer journey (Priority: P1)

A product manager defines a new customer journey with stages, journey-local touchpoints, and KPI definitions for each touchpoint, then saves it as a versioned journey.

**Why this priority**: Journey configuration is the core capability of M-16 and is required before any scoring, reporting, or journey-based analytics can work.

**Independent Test**: This can be tested by creating a journey, adding stages and touchpoints, assigning KPIs and weights, and verifying the journey is persisted with version metadata.

**Acceptance Scenarios**:

1. **Given** a tenant user with journey configuration permission, **When** they create a new journey, **Then** the system persists the journey with stages, touchpoints, KPI definitions, and a version identifier.
2. **Given** a saved journey, **When** the user updates the journey structure or KPI weights, **Then** the system stores a new journey version while preserving the prior version.
3. **Given** a journey version, **When** the user requests journey details, **Then** the system returns stage definitions, touchpoint definitions, KPI config, weights, and version metadata.

**Unit Test Coverage**:

- **Units under test**: `JourneyService`, `JourneyVersionFactory`, `TouchpointRepository`, `KpiConfigurationValidator`, `JourneyPersistence`, `AuditRecorder`.
- **Required cases**:
  - `CreateJourney(request)` with valid stages and touchpoints → persists journey and returns `JourneyId`.
  - `UpdateJourneyVersion(journeyId, modifications)` → creates a new version record and keeps the previous version immutable.
  - `GetJourneyVersion(journeyId, version)` → returns the requested version with all touchpoint and KPI config.
  - `ValidateKpiWeights(touchpointConfig)` → rejects if weights are missing or invalid.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/journeys` creates a journey and writes version metadata.
  - `PUT /api/journeys/{id}` stores a new journey version.
  - `GET /api/journeys/{id}` retrieves the active journey definition.
- **What's intentionally NOT covered end-to-end**: M-06 score computation execution, which is consumed by M-16 but implemented in M-06.

**Scenario Test**:

- `scenario-test: JourneyCreationAndVersioning`.

**E2E Test Coverage**:

- `e2e-tests: skipped — this story focuses on backend journey configuration and does not include new tenant portal pages in Phase 1.`

---

### User Story 2 - Configure journey scoring and reporting output (Priority: P1)

A journey author defines the strategic satisfaction scoring model, touchpoint weights, MoT multipliers, confidence thresholds, and report output definitions consumed by M-07.

**Why this priority**: Strategic scoring and reporting definitions are required for M-16 to support journey performance analysis and dashboard consumption.

**Independent Test**: This can be tested by submitting scoring model configuration and verifying that the stored journey includes the configured scoring parameters and report output contract.

**Acceptance Scenarios**:

1. **Given** a journey, **When** the user sets touchpoint weights, MoT multiplier, and confidence thresholds, **Then** the system stores those parameters and associates them with the journey version.
2. **Given** strategic scoring is configured, **When** the downstream consumer requests report definitions, **Then** the system returns the expected report contract for M-07.
3. **Given** invalid scoring parameters, **When** the user submits the model, **Then** the system rejects the request with a validation error.

**Unit Test Coverage**:

- **Units under test**: `ScoringModelService`, `ReportDefinitionService`, `ScoreParameterValidator`, `JourneyRepository`, `AuditRecorder`.
- **Required cases**:
  - `SaveScoringModel(journeyId, scoringParams)` → persists the model on the journey version.
  - `ValidateScoringParameters(...)` with invalid confidence thresholds → returns validation failure.
  - `GetReportDefinition(journeyId)` → returns the report contract consumed by M-07.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/journeys/{id}/scoring` saves scoring configuration.
  - `GET /api/journeys/{id}/reports` returns configured report output metadata.
- **What's intentionally NOT covered end-to-end**: actual score computation by M-06.

**Scenario Test**:

- `scenario-test: ScoringAndReportContractConfiguration`.

**E2E Test Coverage**:

- `e2e-tests: skipped — Phase 1 focuses on backend scoring and reporting contract configuration.`

---

### User Story 3 - Define journey-local touchpoints and KPI configuration (Priority: P2)

A journey author adds touchpoints inside a journey, defines touchpoint-specific KPIs, and optionally reuses persona configuration across journeys.

**Why this priority**: Journey-local touchpoints are the structural units that map customer interactions and attach KPI definitions that drive scoring.

**Independent Test**: This can be tested by creating or updating touchpoints within a journey and verifying that KPI definitions and persona configuration references are persisted.

**Acceptance Scenarios**:

1. **Given** a journey, **When** the user adds a touchpoint, **Then** the system stores the touchpoint as journey-local and associates it with the correct stage.
2. **Given** a touchpoint, **When** the user assigns KPI definitions and weights, **Then** the system persists the KPI configuration under that touchpoint.
3. **Given** persona configuration is defined, **When** a journey author chooses to reuse it, **Then** the journey references the persona configuration without duplicating journey structure.

**Unit Test Coverage**:

- **Units under test**: `TouchpointService`, `KpiConfigurationService`, `PersonaConfigurationRepository`, `JourneyStructureValidator`, `AuditRecorder`.
- **Required cases**:
  - `AddTouchpoint(journeyId, stageId, touchpointData)` → persists journey-local touchpoint.
  - `ConfigureKpis(touchpointId, kpiList)` → persists KPI definitions and weights.
  - `ReusePersonaConfiguration(journeyId, personaConfigId)` → references existing persona configuration.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `POST /api/journeys/{id}/touchpoints` adds a new touchpoint.
  - `PUT /api/touchpoints/{id}/kpis` stores KPI configuration.
  - `POST /api/persona-configurations/{id}/apply` applies reusable persona configuration.
- **What's intentionally NOT covered end-to-end**: shared touchpoint libraries and survey-to-journey binding.

**Scenario Test**:

- `scenario-test: JourneyLocalTouchpointsAndKpiConfiguration`.

**E2E Test Coverage**:

- `e2e-tests: skipped — no frontend page is introduced in this backend-focused phase.`

---

### User Story 4 - Enforce RBAC and audit for journey configuration (Priority: P1)

Journey configuration operations are protected by M-10 RBAC, and every change is recorded in the tenant audit log.

**Why this priority**: Secure access control and audit trails are required for enterprise governance and compliance.

**Independent Test**: This can be tested by exercising journey CRUD operations with authorized and unauthorized tenant users and verifying audit entries are written.

**Acceptance Scenarios**:

1. **Given** a user without journey configuration permission, **When** they attempt to modify a journey, **Then** the system denies access with `403 Forbidden`.
2. **Given** a permitted user modifies a journey, **When** the operation succeeds, **Then** the system writes an immutable audit record containing actor, action, entity, old value, new value, and UTC timestamp.
3. **Given** a tenant user attempts to access journey configuration outside their permitted scope, **When** the request is processed, **Then** the system enforces M-10 RBAC at the boundary and logs the attempt.

**Unit Test Coverage**:

- **Units under test**: `JourneyAuthorizationGuard`, `AuditService`, `JourneyController`, `PermissionEvaluationService`.
- **Required cases**:
  - `AuthorizeJourneyUpdate(userId, journeyId)` with denied permissions → throws `ForbiddenException`.
  - `RecordAuditEntry(...)` for journey changes → persists immutable audit row.
  - `AuthorizeJourneyRead(userId, journeyId)` when out of scope → returns denied.

**Integration Test Coverage**:

- **What gets tested end-to-end**:
  - `PUT /api/journeys/{id}` forbidden for unauthorized users.
  - `POST /api/journeys` audit record written on success.
  - `GET /api/journeys/{id}` enforces RBAC and returns only permitted data.
- **What's intentionally NOT covered end-to-end**: M-10 internal RBAC implementation details beyond the journey boundary.

**Scenario Test**:

- `scenario-test: JourneyConfigurationRbacAndAudit`.

**E2E Test Coverage**:

- `e2e-tests: skipped — journey configuration is backend infrastructure in this phase.`

---

### Edge Cases

- What happens if a journey update attempts to remove a stage that still contains touchpoints? The system must reject the update unless touchpoints are reassigned or deleted first.
- How does the system handle invalid or contradictory scoring parameters (e.g. negative weights, confidence thresholds outside 0-1)? It must validate and reject the configuration.
- What happens when a persona configuration reference is deleted while a journey still depends on it? The system must prevent deletion or cascade a safe update to affected journeys.
- How does the system behave when M-07 requests a report definition for an unpublished or draft journey? It must return only published/active report contracts.
- What happens if a tenant attempts to reuse journey-local touchpoints across journeys? The system must prevent cross-journey reuse in Phase 1.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow tenant users to configure customer journeys composed of stages and journey-local touchpoints.
- **FR-002**: System MUST allow users to define KPI configuration and weights per touchpoint.
- **FR-003**: System MUST support a strategic satisfaction scoring model for touchpoint, stage, and journey-level aggregation.
- **FR-004**: System MUST support test-configurable scoring parameters including weights, MoT multiplier, and confidence thresholds.
- **FR-005**: System MUST preserve prior journey versions when a journey is updated.
- **FR-006**: System MUST support journey-local pain point and happy moment signal definitions, with computation delegated to M-06.
- **FR-007**: System MUST expose reporting output definitions consumable by M-07.
- **FR-008**: System MUST enforce role-based access using M-10 RBAC for all journey configuration operations.
- **FR-009**: System MUST write immutable audit records for journey configuration changes.
- **FR-010**: System MUST support Arabic and English localization, including RTL support for Arabic.
- **FR-011**: System MUST reject survey builder UI, response collection, text analytics, shared/reusable touchpoint libraries, survey-to-journey binding, score execution, dashboard rendering, AI journey suggestions, bulk CSV import, and real-time collaborative editing as out of scope for Phase 1.

### Key Entities

- **Journey**: represents a customer journey with stages, touchpoints, scoring model, version history, and reporting contract.
- **Stage**: a logical phase within a journey containing one or more journey-local touchpoints.
- **Touchpoint**: a journey-local interaction point with KPI configuration and weight settings.
- **KpiConfiguration**: represents KPI definitions, scales, weights, and normalization details for a touchpoint.
- **ScoringModel**: represents the strategic satisfaction model, including MoT multiplier, confidence thresholds, and aggregation parameters.
- **JourneyVersion**: represents an immutable snapshot of a journey configuration at a point in time.
- **ReportDefinition**: represents the journey-level reporting contract consumed by M-07.
- **PersonaConfiguration**: optional reusable persona-driven journey configuration metadata.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tenant users can create, update, and retrieve journey definitions with stages, touchpoints, and KPI configurations.
- **SC-002**: Journey updates create new versions while preserving prior versions.
- **SC-003**: Strategic scoring parameters and report output definitions are persisted and queryable.
- **SC-004**: Unauthorized users are denied journey operations by M-10 RBAC at the journey boundary.
- **SC-005**: Journey configuration changes generate immutable audit records with actor, action, entity, old/new values, and UTC timestamp.
- **SC-006**: Journey configuration metadata supports Arabic and English localization.

## Assumptions

- M-16 consumes RBAC, tenant isolation, and audit logging from M-10 and M-11.
- Score computation is delegated to M-06; M-16 owns only configuration and contract definitions.
- Journey-local touchpoints are not shared across journeys in Phase 1.
- M-07 consumes reporting output definitions from M-16 and renders dashboards separately.
- The tenant portal UI is implemented later; this spec focuses on backend API and configuration services.
