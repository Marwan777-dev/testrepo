# Specification Quality Checklist: M-15 Action Management

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-22
**Feature**: [spec.md](../spec.md)
**Source SRS**: `SRS-M15-Action-Management-v1_1 1.md` v1.1 (21 Jul 2026, Final — approved for Speckit)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) — the spec references API surfaces at the *contract* level (e.g. `POST /api/actions`) as required by the CLAUDE.md test-policy vocabulary for backend-bearing stories, but does not prescribe language/framework choice.
- [x] Focused on user value and business needs — every FR is traceable to a business goal (pace awareness, retro-documentation, force-cascade correctness, etc.).
- [x] Written for non-technical stakeholders — the Measurement Model is narrated with a worked example; the Two-Palette Rule and RTL rules are stated in outcome terms.
- [x] All mandatory sections completed — User Scenarios & Testing (10 stories), Requirements, Key Entities, Success Criteria, Assumptions, Coverage Checklist all present.

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — the source SRS is explicitly Final; every open question was ratified per SRS §19.
- [x] Requirements are testable and unambiguous — every FR/BR/VAL/NFR/ERR/DLG/NTF cites its SRS section; every measurement rule has an accompanying formula or literal-input Required-case bullet.
- [x] Success criteria are measurable — SC-001 through SC-014 all name a metric (time, count, %, 0-violations, etc.) with a verification method.
- [x] Success criteria are technology-agnostic — SC-013 references NFR-5 targets (< 2 s / < 100 ms / 60 fps) in user-facing terms; no framework/database names appear in Success Criteria.
- [x] All acceptance scenarios are defined — every SRS acceptance criterion (AC-1.1..AC-1.8, AC-2.1..AC-2.13, AC-3.1..AC-3.8) is preserved verbatim under the appropriate user story.
- [x] Edge cases are identified — Edge Cases section covers measurement (regression, overshoot, on-pace equality, zero-eligible-targets, execution-phase Time=0, U=L equality, adaptive padding), status lifecycle (day-boundary, retro-dated born Completed, unarchive-to-Completed, Archived continuity), validation & data (duplicate name across statuses, delete-on-last-active-Target, all-KPIs-used, Target Date == End Date, U above X, description overflow), cross-module (M-06 unreachable, missing historical score, KPI force-deactivation, deep-link cases, concurrent edits, network failure, live-score refresh, archive write failure), permissions (bypass attempt), RTL/bilingual (Arabic names, slider RTL).
- [x] Scope is clearly bounded — Overview → "Explicitly out of scope for v1" lists every stakeholder-removed feature (Clone, action-level Delete, editing Completed, M-09 alerting, journey/case/AI linkage, audit-viewing UI, permissions engine, M-07 chart implementation, platform chrome).
- [x] Dependencies and assumptions identified — Assumptions section lists all 16 ratified decisions (R-1..R-16) verbatim, plus the Dependencies subsection enumerates M-06/M-07/M-09/M-10/platform-shell/Settings/audit-service/tenant-timezone.

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria — every FR is either paired with an SRS acceptance scenario, or is a purely-normative rule (measurement formulas, timer-colour mapping) whose test cases are enumerated under Unit Test Coverage.
- [x] User scenarios cover primary flows — Story 1 (Create), Story 2 (Monitor SCR-01), Story 3 (Details SCR-03), Story 4 (Edit + guards), Story 5 (Auto-transitions), Story 6 (Archive/Unarchive), Story 7 (Target lifecycle + force-cascade), Story 8 (Search/filter), Story 9 (Settings), Story 10 (Retro-date) — collectively cover every SRS workflow (W-1..W-9) and every SRS user class.
- [x] Feature meets measurable outcomes defined in Success Criteria — each SC is anchored to a specific story's tests (unit / integration / scenario / E2E).
- [x] No implementation details leak into specification — API endpoint contracts are stated in terms of HTTP verb + route + payload shape + response envelope (API-05 error envelope per CLAUDE.md), which is the required level for the backend integration test blocks. No framework classes (EF DbContext, React hooks, etc.) are referenced in the spec text.

## CLAUDE.md Testing Policy — story-by-story audit

**Rule**: every backend-bearing story MUST populate a Unit Test Coverage block (or `unit-tests: skipped — <reason>`) AND an Integration Test Coverage block (or `integration-tests: skipped — <reason>`); page-bearing frontend stories MUST populate an E2E Test Coverage block (or `e2e-tests: skipped — <reason>`); a Scenario Test line must be present (either naming the class or declaring `not-needed — <reason>`).

| Story | Unit | Integration | Scenario | E2E |
|---|---|---|---|---|
| US1 Create Action | ✔ populated | ✔ populated | ✔ `ActionCreationScenarioTests` | ✔ populated (`ActionAddEditTests`) |
| US2 Monitor SCR-01 | ✔ populated | ✔ populated | ✔ `not-needed — single-endpoint list` | ✔ populated (`AllActionsTests`) |
| US3 Details SCR-03 | ✔ populated | ✔ populated | ✔ `ActionArchivalScenarioTests` | ✔ populated (`ActionDetailsTests`) |
| US4 Edit Action | ✔ populated | ✔ populated | ✔ `ActionEditGuardScenarioTests` | ✔ populated (`ActionAddEditTests`) |
| US5 Auto-transitions | ✔ populated | ✔ populated | ✔ `ActionLifecycleScenarioTests` | ✔ `e2e-tests: skipped — time-based; SPA does not expose clock manipulation; visible surfaces covered by US2 AC-1.3 and US3 AC-3.7` |
| US6 Archive/Unarchive | ✔ populated | ✔ covered by US3 (referenced) | ✔ covered by US3 | ✔ populated (added scenarios to US2 and US3 files) |
| US7 Target lifecycle | ✔ populated | ✔ populated | ✔ `KpiForceDeactivationScenarioTests` | ✔ populated (added scenarios to US1 and US3 files) |
| US8 Search/filter | ✔ populated | ✔ covered by US2 (referenced) | ✔ `not-needed — single GET, no cross-endpoint state` | ✔ populated (added scenarios to US2 file) |
| US9 Settings | ✔ populated | ✔ populated | ✔ `not-needed — single-endpoint updates` | ✔ populated (`ActionsSettingsTests`) |
| US10 Retro-dating | ✔ covered by US1's `BaselineCaptureService` | ✔ covered by US1's retro-dated cases | ✔ `not-needed — single-endpoint POST` | ✔ populated (added scenarios to US1 file) |

**All stories carry every required test block (populated or explicit `skipped`/`not-needed`).** No backend-bearing story is missing coverage.

**Soft case-concreteness check**: every Required-case bullet in every Unit Test Coverage block is written with a literal input → literal expected output/exception (e.g., `Validate(new ActionRequest { Name = "" }) → Invalid("Action Name is required")`, `ScoreProgress(current=76, baseline=70, upper=+6) → 1.0`, `Colour(score=0.502, time=0.500) → Yellow`). No bullets contain hand-wave-only phrasing like "validator rejects invalid input" — verified.

## Notes

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`. **All items above pass.**
- SRS coverage is verified in the spec's own "SRS Coverage Checklist" table — 100 % of SRS sections §1.1 through §20 + Appendix A are represented.
- The source SRS v1.1 is explicitly Final and closes all open questions per §20 — no clarification round is needed; the spec is ready for `/speckit-plan`.
- **CLAUDE.md compliance**:
  - Testing Policy §7 (Red Checkpoint) applies at `/speckit-implement` time — not at spec time; no action needed here.
  - Testing Policy §10 (Integration tests mandatory for backend stories with HTTP/DB/event side-effects) — every backend-bearing story here has such side-effects (INT-04 audit events, database writes, cross-module KPI cascade); every story except pure-lookup ones populates the block.
  - Testing Policy §11 (Scenario tests for ≥2-endpoint flows / carried state / aggregate side-effects) — US1 (POST → GET → GET), US3 (archive round-trip), US4 (baseline-recapture pairing invariant), US5 (full Planned→Active→Completed lifecycle), US7 (force-deactivation cascade) all qualify and have named scenario-test classes.
  - E2E Test Policy — every page-bearing story targets `tests/Nabadat.E2ETests/ActionManagement/`, grouping tests by module folder per the E2E project structure convention. `SignInAsync` role-guard scenarios are called out under US1, US2, US3, US7, US9.
