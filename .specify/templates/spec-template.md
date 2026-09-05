# Feature Specification: [FEATURE NAME]

**Feature Branch**: `[###-feature-name]`

**Created**: [DATE]

**Status**: Draft

**Input**: User description: "$ARGUMENTS"

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.

  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - [Brief Title] (Priority: P1)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently - e.g., "Can be fully tested by [specific action] and delivers [specific value]"]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]
2. **Given** [initial state], **When** [action], **Then** [expected outcome]

**Unit Test Coverage** *(MANDATORY for backend-bearing stories — see CLAUDE.md "Unit Test Policy". Frontend-only stories may delete this block.)*:

- **Units under test**: [List the concrete units — services, validators, calculators, reducers, state machines — whose behaviour this story introduces or changes.]
- **Required cases**: [One bullet per behaviour each unit MUST cover — happy path, validation rule, edge case, error condition. Concrete: literal input → literal expected output/exception.]
- **Skip declaration** *(only if no testable units exist)*: `unit-tests: skipped — <one-sentence justification>`. Absence of this line means coverage is required.

**Integration Test Coverage** *(MANDATORY for backend-bearing stories whose acceptance scenarios have HTTP / DB / event side-effects — apply the qualifier rule)*:

- **What gets tested end-to-end**: [One line per endpoint (`<METHOD> <route>`) or service path covered via the in-process HTTP harness + Testcontainers DB.]
- **What's intentionally NOT covered end-to-end**: [Pure-logic scenarios verified by unit tests alone — name the unit-test class.]
- **Skip declaration**: `integration-tests: skipped — <one-sentence justification>`.

**Scenario Test**:

- `scenario-test: <Name>ScenarioTests` *(when the Independent Test spans ≥2 endpoints, carries state across calls, or asserts an aggregate side-effect)*.
- OR `scenario-test: not-needed — <one-sentence reason>`.

**E2E Test Coverage** *(MANDATORY for stories that ship pages/routes in a frontend SPA workspace — see CLAUDE.md "E2E Test Policy". Delete this block for backend-only stories.)*:

- **User flows under test**: [One per route/page this story adds/changes — becomes a `<Feature>Tests.cs` class in `tests/<Workspace>.E2ETests/`.]
- **Required scenarios**: [Happy path, validation/error state, auth/permission redirect (signed-out → login; role without access), empty state — one bullet per `[TestMethod]`.]
- **Skip declaration**: `e2e-tests: skipped — <one-sentence justification>`.

---

### User Story 2 - [Brief Title] (Priority: P2)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

**Unit Test Coverage** *(MANDATORY for backend-bearing stories — see CLAUDE.md "Unit Test Policy". Frontend-only stories may delete this block.)*:

- **Units under test**: [List units]
- **Required cases**: [Enumerate cases — concrete: literal input → literal expected output/exception]
- **Skip declaration** *(only if no units exist)*: `unit-tests: skipped — <reason>`

**Integration Test Coverage** *(MANDATORY when story has HTTP/DB/event side-effects — see qualifier rule)*:

- **What gets tested end-to-end**: [Endpoints / service paths]
- **What's intentionally NOT covered end-to-end**: [Pure-logic scenarios covered by unit tests alone]
- **Skip declaration** *(only if every scenario is pure-logic)*: `integration-tests: skipped — <reason>`

**Scenario Test**:

- `scenario-test: <Name>ScenarioTests` *(if the Independent Test is multi-step)*
- OR `scenario-test: not-needed — <reason>`

**E2E Test Coverage** *(MANDATORY for stories shipping pages in a frontend SPA workspace; delete for backend-only stories)*:

- **User flows under test**: [Navigable flows / routes — one per page]
- **Required scenarios**: [Happy path, validation/error, auth/permission redirect, empty state — one bullet per `[TestMethod]`]
- **Skip declaration** *(only if no navigable flow)*: `e2e-tests: skipped — <reason>`

---

### User Story 3 - [Brief Title] (Priority: P3)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

**Unit Test Coverage** *(MANDATORY for backend-bearing stories — see CLAUDE.md "Unit Test Policy". Frontend-only stories may delete this block.)*:

- **Units under test**: [List units]
- **Required cases**: [Enumerate cases — concrete: literal input → literal expected output/exception]
- **Skip declaration** *(only if no units exist)*: `unit-tests: skipped — <reason>`

**Integration Test Coverage** *(MANDATORY when story has HTTP/DB/event side-effects — see qualifier rule)*:

- **What gets tested end-to-end**: [Endpoints / service paths]
- **What's intentionally NOT covered end-to-end**: [Pure-logic scenarios covered by unit tests alone]
- **Skip declaration** *(only if every scenario is pure-logic)*: `integration-tests: skipped — <reason>`

**Scenario Test**:

- `scenario-test: <Name>ScenarioTests` *(if the Independent Test is multi-step)*
- OR `scenario-test: not-needed — <reason>`

**E2E Test Coverage** *(MANDATORY for stories shipping pages in a frontend SPA workspace; delete for backend-only stories)*:

- **User flows under test**: [Navigable flows / routes — one per page]
- **Required scenarios**: [Happy path, validation/error, auth/permission redirect, empty state — one bullet per `[TestMethod]`]
- **Skip declaration** *(only if no navigable flow)*: `e2e-tests: skipped — <reason>`

---

[Add more user stories as needed, each with an assigned priority. Every backend-bearing story MUST carry a populated Unit Test Coverage block (or `unit-tests: skipped`) AND an Integration Test Coverage block (or `integration-tests: skipped`); page-bearing frontend stories MUST carry an E2E Test Coverage block (or `e2e-tests: skipped`).]

### Edge Cases

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right edge cases.
-->

- What happens when [boundary condition]?
- How does system handle [error scenario]?

## Requirements *(mandatory)*

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right functional requirements.
-->

### Functional Requirements

- **FR-001**: System MUST [specific capability, e.g., "allow users to create accounts"]
- **FR-002**: System MUST [specific capability, e.g., "validate email addresses"]
- **FR-003**: Users MUST be able to [key interaction, e.g., "reset their password"]
- **FR-004**: System MUST [data requirement, e.g., "persist user preferences"]
- **FR-005**: System MUST [behavior, e.g., "log all security events"]

*Example of marking unclear requirements:*

- **FR-006**: System MUST authenticate users via [NEEDS CLARIFICATION: auth method not specified - email/password, SSO, OAuth?]
- **FR-007**: System MUST retain user data for [NEEDS CLARIFICATION: retention period not specified]

### Key Entities *(include if feature involves data)*

- **[Entity 1]**: [What it represents, key attributes without implementation]
- **[Entity 2]**: [What it represents, relationships to other entities]

## Success Criteria *(mandatory)*

<!--
  ACTION REQUIRED: Define measurable success criteria.
  These must be technology-agnostic and measurable.
-->

### Measurable Outcomes

- **SC-001**: [Measurable metric, e.g., "Users can complete account creation in under 2 minutes"]
- **SC-002**: [Measurable metric, e.g., "System handles 1000 concurrent users without degradation"]
- **SC-003**: [User satisfaction metric, e.g., "90% of users successfully complete primary task on first attempt"]
- **SC-004**: [Business metric, e.g., "Reduce support tickets related to [X] by 50%"]

## Assumptions

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right assumptions based on reasonable defaults
  chosen when the feature description did not specify certain details.
-->

- [Assumption about target users, e.g., "Users have stable internet connectivity"]
- [Assumption about scope boundaries, e.g., "Mobile support is out of scope for v1"]
- [Assumption about data/environment, e.g., "Existing authentication system will be reused"]
- [Dependency on existing system/service, e.g., "Requires access to the existing user profile API"]
