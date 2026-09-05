# Specification Quality Checklist: M-13 Integration Hub

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-27
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
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

## Testing-Flow Checks (CLAUDE.md "Unit Test Policy" + "E2E Test Policy")

- [x] Every backend-bearing user story (US1–US10) carries a populated **Unit Test Coverage** block naming concrete units and required cases with literal input → literal expected output/exception, OR an explicit `unit-tests: skipped — <reason>` (none needed — every story has testable units).
- [x] Every backend-bearing story carries a populated **Integration Test Coverage** block (endpoint/service-path level) — all ten stories have HTTP/DB/event side-effects, so none carry a skip declaration.
- [x] Every story carries a **Scenario Test** line — either `scenario-test: <Name>ScenarioTests` (US3, US4, US6, US7) or `scenario-test: not-needed — <reason>` (US1, US2, US5, US8, US9, US10), each with a concrete justification.
- [x] Every page-bearing frontend story (US1, US2, US3, US5, US6, US7, US8, US9, US10) carries an **E2E Test Coverage** block with named `[TestMethod]`-style scenarios. US4 (Feature 0, explicitly headless per the SRS) carries `e2e-tests: skipped — <reason>` instead, correctly justified.
- [x] Coverage-block units trace to concrete, plausible types (e.g. `ChannelIdSanitizer`, `RequestValidationPipeline`, `MappingResolver`) rather than vague descriptions.
- [x] `scenario-test` declarations match the presence/absence of a described multi-step scenario walk (US3/US4/US6/US7 each carry ≥3-call sequences with a final aggregate assertion; the `not-needed` stories are genuinely single-endpoint or already covered cross-story).

## Notes

- Spec derives from an already-ratified, "Implementation-ready" SRS (`SRS-M13-Integration-Hub-v1_1.md`, "Open Questions: None") — zero `[NEEDS CLARIFICATION]` markers were needed, consistent with the source document's own completeness claim. **Provenance caveat**: spec.md's header and changelog notes cite `SRS-M13-Integration-Hub-v1_3.md` and `M13-Integration-Hub-Prototype-v0.5-Ratified.html` as sources, but only the v1.1 SRS file exists in the repo — the v1.2/v1.3 decisions (case-insensitive name uniqueness, P-01 log exclusivity, G-25…G-28) are recorded only in spec.md's own "Rev 1.1"/"Rev 1.2" changelog, with no separate SRS file backing them. Flagged during the 2026-07-27 clarification session; the user chose not to resolve it at that time ("ignore the file name and continue").
- The referenced prototype file (`M13-Integration-Hub-Prototype-v0.5-Ratified.html`) is not present in the repository; this is recorded as a non-blocking assumption in spec.md (every `[UI]`-tagged behaviour affecting functional behaviour is already captured verbatim in the spec; purely visual details are intentionally excluded per the preserve/omit rule).
- One design-time decision remains intentionally left open for `/speckit-plan` (per spec-template's WHAT/HOW separation): the exact endpoint shape for the parameter-disable impact-warning response (US2's Integration Test Coverage notes two equally valid server-response shapes — a pure wire-protocol choice with no behavioral difference). Does not block story independence or testability.
- **2026-07-27 clarification session (round 1, standard taxonomy scan)** — 4 gaps resolved and integrated into spec.md: survey-link-expiry rejection is out of M-13's API scope entirely (edge case + FR-F0-08 area); OAuth scopes extended to all 5 scenarios (BR-26, new `survey-definitions:read`/`survey-embed:read`); tenant capacity-guardrail-exceeded behavior specified as a console-side validation error (new VR-F13); mapping source-value uniqueness and read-time resolution (VR-F08, F0.5) made explicitly case-insensitive, matching VR-F01/F04's convention. The inactive-*integration* rejection code (previously an open item) was independently already resolved to `401 E-1401` before this session, per the Status Lifecycle table. The SRS/prototype-version provenance question (v1.3/v0.5 cited, only v1.1 exists in repo) was asked but declined by the user — remains Outstanding, low urgency.
- **2026-07-27 clarification session (round 2, deep architectural-tension pass, user-directed)** — 3 more gaps found and resolved, all integrated into spec.md: (1) SCN-04 iFrame auth was in tension with FR-F0-02's blanket header-auth rule (browsers can't attach custom headers to `<iframe>`) — resolved as a two-step flow: M-13's authenticated call returns a short-lived embed URL, which the browser loads from a separate, unauthenticated/origin-checked-only M-03 rendering endpoint (FR-F0-01, FR-F0-08, CMC-02, `AllowedOriginsWhitelistStore`); (2) M-04's discretionary rejection of an already-`202`'d SCN-05 response was undefined — resolved as an unconditional-save guarantee: M-04 must never silently drop a payload that passed M-13's own validation (CMC-03, new **SC-016**); (3) the BR-18/F0.7 idempotency retention window was undefined — resolved as *no* fixed window: a sufficiently late retry may be processed as a new request, an accepted limitation not a defect (FR-F0-07, BR-18, new Edge Cases bullet). Combined with round 1, this feature has now had **8 questions asked** across today's clarify sessions (7 answered/integrated, 1 declined) — beyond the nominal 5-per-session cap, but each additional question in round 2 was explicitly user-directed (re-invoking the same custom analysis prompt) rather than self-initiated.
- All items pass — no spec updates were required to any checkbox state as a result of the clarification sessions (all were already passing; the sessions added detail, they didn't fix a failing item).
