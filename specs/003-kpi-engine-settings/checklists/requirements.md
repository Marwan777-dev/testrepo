# Specification Quality Checklist: CX Metrics & KPI Engine (M-06) + Platform Settings

**Purpose**: Validate specification completeness and quality before proceeding to planning

**Created**: 2026-06-21

**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
  - *Note*: API routes appear as a **named surface** (`GET /api/v1/kpis`, etc.) under "API Endpoints (informative)". These are platform-contract anchors required by API-01 / API-05 in the constitution and by the SRS, not implementation choices — the request/response shapes are deferred to `contracts/` in `/plan`. No language, framework, ORM, or library is named.
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Per-Story Test Coverage Blocks (CLAUDE.md Unit + E2E Test Policies)

- [x] **US1** — backend-bearing + page-bearing: Unit Test Coverage populated; Integration Test Coverage populated; Scenario Test declared (`not-needed — single GET round-trip`); E2E Test Coverage populated.
- [x] **US2** — backend-bearing + page-bearing: Unit + Integration coverage populated; Scenario Test `KpiCreateThenEditScenarioTests`; E2E coverage populated.
- [x] **US3** — backend-bearing + page-bearing: Unit + Integration coverage populated; Scenario Test `CxiConfiguresAndRebalancesScenarioTests`; E2E coverage populated.
- [x] **US4** — backend-bearing + page-bearing: Unit + Integration coverage populated; Scenario Test `ScoringConfigEditAndPersistScenarioTests`; E2E coverage populated.
- [x] **US5** — backend-bearing + page-touching (no new route): Unit + Integration coverage populated; Scenario Test `KpiDeactivationCascadeScenarioTests`; E2E Test Coverage populated — three new `[TestMethod]`s appended to US-2's `KpiConfigTests.cs` (added via post-analyze remediation D4 / AMENDMENT-008 cleanup; the dialog is a UI variant of US-2's form, not a new page).
- [x] **US6** — backend-bearing + page-bearing: Unit + Integration coverage populated; Scenario Test `not-needed`; E2E coverage populated.
- [x] **US7** — page-bearing permission variant: `unit-tests: skipped` with explicit justification (no new business logic; persona variant only); Integration coverage populated for permission gating; Scenario Test `not-needed`; E2E coverage populated.

### Concreteness Warnings (soft check)

- *None.* Every Required-case bullet under every populated Unit Test Coverage block carries literal inputs and literal expected outputs (sample values, error codes, member tuples). No bullets read "validator rejects invalid input" or similar abstract statements.

## Notes

- All hard SRS facts (eight standard KPIs and their canonical order, NPS field locks, TOP n Box scale-points clarification, perspective independence, CXI member breakdown contract, ScoringConfig defaults including `n_floor=100`, Industry list sourced from M-11, P-02 read-only access) are encoded verbatim in the spec.
- The five v1 Settings sections (Organization, Customer Journey, Notifications, Branding Theme, Localization Defaults) are listed on the landing page per FR-S1; only Organization and Customer Journey are functionally specified here. The other three sections are explicitly noted as out of scope of this feature in the Assumptions block.
- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`.
