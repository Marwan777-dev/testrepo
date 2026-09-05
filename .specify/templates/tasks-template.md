---

description: "Task list template for feature implementation"
---

# Tasks: [FEATURE NAME]

**Input**: Design documents from `/specs/[###-feature-name]/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Per CLAUDE.md "Unit Test Policy", **unit tests are MANDATORY for every backend-bearing user story** (skippable only with an explicit `unit-tests: skipped — <reason>` in spec.md). Each backend story emits a **Unit Tests (write FIRST, must FAIL)** subsection + a **Red Checkpoint** before its implementation tasks, and **Integration & API / Scenario** tests at the per-story checkpoint. **Page-bearing frontend stories** emit an **E2E (Browser) Tests** subsection *after* the page tasks (NO Red Checkpoint), then a **Click-through Parity** subsection carrying one task the frontend developer runs manually. Frontend-only stories are exempt from the unit/red-checkpoint subsections. Contract tests stay feature-driven.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/`, `tests/` at repository root
- **Web app**: `backend/src/`, `frontend/src/`
- **Mobile**: `api/src/`, `ios/src/` or `android/src/`
- Paths shown below assume single project - adjust based on plan.md structure

## Test Generation Rules (BINDING — see CLAUDE.md "Unit Test Policy" + "E2E Test Policy")

- **Unit tests are MANDATORY for every backend-bearing user story.** A story skips only via an explicit `unit-tests: skipped — <reason>` in spec.md. Frontend-only stories skip the Unit Tests / Red Checkpoint subsections.
- **Red Checkpoint is MANDATORY** between the Unit Tests and Implementation subsections of each backend story: run the unit project, verify a valid red state (compile error if the type doesn't exist yet, else assertion failure) — or a **green baseline** on a retrofit where the code already exists — and commit the baseline before any implementation task. Non-parallel. Skipping it is a defect.
- **Test projects are split by kind — never combined.** xUnit v3 layout: `tests/<Project>.UnitTests/<Feature>/<Type>Tests.cs` (pure logic, no Docker) and `tests/<Project>.IntegrationTests/{Api,Integration,Scenarios}/<Feature>/…` (Testcontainers DB + in-process HTTP harness). Contract tests (optional) → `tests/<Project>.ContractTests/`.
- **Integration & API tests are MANDATORY per story when spec.md's Integration Test Coverage lists covered scenarios** — run at the **per-story checkpoint**, NOT between implementation tasks (they need Docker). Add a **Scenario test** when spec.md declares `scenario-test: <Name>ScenarioTests`.
- **Frontend E2E (browser) tests are enforced for page-bearing stories.** A story that ships pages/routes in a frontend SPA workspace MUST get an **E2E (Browser) Tests** subsection (`tests/<Workspace>.E2ETests/<Feature>Tests.cs`; MSTest + Playwright; matrix in `tests/<Workspace>.E2ETests/COVERAGE.md`), UNLESS spec.md declares `e2e-tests: skipped — <reason>`. E2E tasks are emitted **after** the implementation tasks (pages must exist first) and carry **NO Red Checkpoint**. Authored with the `e2e-testing` skill.
- **Click-through parity is a TASK, not an automatic step.** Every page-bearing frontend story ALSO gets a **Click-through Parity for User Story X 🎨** subsection holding exactly one task: run `/clickthrough-parity <feature> phase <N>` over that story's routes and triage the report. It is emitted **after** the story's E2E subsection and Checkpoint, because the audit is only meaningful once the pages are green. **It is assigned to the frontend developer and run by hand** — deliberately not fired automatically, so the report lands when someone is ready to act on it. Two hard preconditions the task text must repeat: the implementation was **click-through-blind** (a ported page makes the run void, not clean), and the click-through checkout + a signed-in product dev stack are both up. The module-wide audit is a separate task in the final Polish phase.
- **Test conventions** (CLAUDE.md): xUnit v3, FluentAssertions 6.12, NSubstitute 5, `FakeTimeProvider` for any `TimeProvider`; one test class per file; naming `<Subject>_<expected>_when_<condition>`. Test-project framework + package versions come from the production project's detected `<TargetFramework>` per the Testing Policy version map — not hard-coded here.
- If a backend story has neither coverage blocks nor a skip declaration in spec.md, **halt and report a spec defect** — fix the spec first.

<!--
  ============================================================================
  IMPORTANT: The tasks below are SAMPLE TASKS for illustration purposes only.

  The /speckit-tasks command MUST replace these with actual tasks based on:
  - User stories from spec.md (with their priorities P1, P2, P3...)
  - Feature requirements from plan.md
  - Entities from data-model.md
  - Endpoints from contracts/

  Tasks MUST be organized by user story so each story can be:
  - Implemented independently
  - Tested independently
  - Delivered as an MVP increment

  DO NOT keep these sample tasks in the generated tasks.md file.
  ============================================================================
-->

## Frontend Task Rule *(applies when the feature ships any UI under `frontend/`)*

Before any UI task, the agent MUST read the repo-root `CLAUDE.md` end to end (design
system, tokens, RTL, D1–D5, DO / DO NOT) and follow the Component Sourcing Rule —
search `frontend/src/components/` (`ui/`, `cx/`) and reuse existing components before
building anything new. UI tasks land under `frontend/src/`.

## Backend Module Folder Structure Rule *(applies to every backend story that adds code to a module)*

File paths in tasks MUST land in the canonical module layout — constitution **AMENDMENT-009** / architecture-constitution **Article 1A** (the `Nabadat.UserManagement` reference). Within `src/Nabadat.<DomainName>/`:

- **Controllers / request-response DTOs / middleware** → `Api/Controllers/`, `Api/Contracts/`, `Api/Middleware/`.
- **Use-case + per-aggregate data-access services** → `Application/<SubDomain>/` (`<Name>Service.cs`); their **ports** → `Application/<SubDomain>/Interfaces/`; use-case DTOs/exceptions → `Application/<SubDomain>/{Dtos,Exceptions}/`. EF context ports → `Application/Interfaces/`.
- **Entities / value objects / published cross-module interfaces** → `Domain/Entities/`, `Domain/ValueObjects/`, `Domain/Interfaces/`.
- **DbContexts + `IEntityTypeConfiguration<T>` + converters + `_Baseline.sql`** → `Infrastructure/Persistence/`(+`Configurations/`), and `Infrastructure/ControlPlane/` + `Infrastructure/Migrations/` only when the module has control-plane tables / owns tables. Every **other** adapter goes in a `Infrastructure/<Concern>/` folder named for the external concern it wraps — module-specific, not a fixed list (M-10 happens to have `Crypto/`, `Auth/`, `Audit/`, `Notifications/`).
- A **new bounded concern** = a new `Application/<SubDomain>/` folder + its mirror `tests/Nabadat.<DomainName>.UnitTests/<SubDomain>/`. One type per file; never a technical-kind bucket at the module root. A new top-level folder kind needs a constitution amendment — halt and report.

## Backend Data-Access Task Rule *(applies to every backend story that reads or writes the database)*

Persistence follows constitution **DB-08** / database-constitution **Article 7** (EF Core; the **M-10** reference pattern). When a story touches data, emit tasks in this shape — **never** raw-SQL repositories, an `IUnitOfWork` type, or EF migrations:

- **Entity + mapping** — a Domain entity (one type per file) and its `IEntityTypeConfiguration<T>` in `Infrastructure/.../Configurations/`, explicit `HasColumnName` per property + FK relationships. The table is added to the module's `_Baseline.sql` / `_ControlPlane.sql` (DB-05 mechanism) — **not** an EF migration.
- **Context wiring** *(foundational, once per module)* — `ITenantDbContext` / `IControlPlaneDbContext` in `Application/Interfaces/` (DbSets + `SaveChangesAsync` + tenant `ExecuteAsync`); concrete contexts + DI in `Infrastructure/`. Reuse across stories; do not re-create.
- **Data-access service** — `<Aggregate>Service`/`Store` + port in `Application/<Domain>/Interfaces/`, depending on the context interface; write methods self-persist. This port is what the business service and its unit tests depend on (the mock seam).
- **Business service** — depends on the data-access port(s); wraps multi-write atomicity in `ITenantDbContext.ExecuteAsync`; takes the clock via injected `TimeProvider`.

A no-DB-on-the-unit-lane corollary: unit tests mock the data-access port (and a fake/recording `ITenantDbContext` when a transaction is asserted); real Postgres lives only in the IntegrationTests lane.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create project structure per implementation plan
- [ ] T002 Initialize [language] project with [framework] dependencies
- [ ] T003 [P] Configure linting and formatting tools

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

Examples of foundational tasks (adjust based on your project):

- [ ] T004 Add the story's tables to the module SQL baseline (`_Baseline.sql` / `_ControlPlane.sql`) and wire the EF Core contexts (`ITenantDbContext` / `IControlPlaneDbContext` + entity configs + DI) per DB-08 — **no EF migrations** (see Backend Data-Access Task Rule). First story in a module also creates `tests/Nabadat.<DomainName>.IntegrationTests/Infrastructure/<DomainName>ApplicationFactory.cs` (meaningful project name per constitution **AMENDMENT-008** — NOT `Nabadat.Platform.M{NN}`; e.g. the M-10 reference module is `Nabadat.UserManagement.IntegrationTests` / `UserManagementApplicationFactory`).
- [ ] T005 [P] Implement authentication/authorization framework
- [ ] T006 [P] Setup API routing and middleware structure
- [ ] T007 Create base models/entities that all stories depend on
- [ ] T008 Configure error handling and logging infrastructure
- [ ] T009 Setup environment configuration management

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - [Title] (Priority: P1) 🎯 MVP

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Unit Tests for User Story 1 (REQUIRED for backend-bearing stories — write FIRST, must FAIL before implementation) ⚠️

> Skip ONLY if spec.md US1 declares `unit-tests: skipped — <reason>`, or US1 is frontend-only (delete this subsection). xUnit v3 + FluentAssertions 6.12 + NSubstitute 5; inject `FakeTimeProvider` wherever production code takes a `TimeProvider`. Naming: `<Subject>_<expected>_when_<condition>`.

- [ ] T010 [P] [US1] Unit tests for `<Type>` in tests/<Project>.UnitTests/<Feature>/<Type>Tests.cs — cover the Required cases from spec.md Unit Test Coverage.

### Red Checkpoint for User Story 1 (MANDATORY — gate between tests and implementation) 🔴

- [ ] T011 [US1] Run `dotnet test tests/<Project>.UnitTests`. **Valid red states**: compile error (production type doesn't exist yet) OR assertion failure (once the type is stubbed). "No tests found" / a pass against a `null`-returning stub are NOT valid. **Retrofit caveat**: if the code already exists, this is a **green baseline** — note it. Non-parallel; must precede any implementation task. Paste the transcript, then commit the baseline.

### Implementation for User Story 1

- [ ] T012 [P] [US1] Create [Entity1] in src/[location]/[Entity1].cs
- [ ] T013 [P] [US1] Create [Entity2] in src/[location]/[Entity2].cs
- [ ] T014 [US1] Implement [Service] in src/[location]/[Service].cs (depends on T012, T013)
- [ ] T015 [US1] Implement [endpoint/feature] in src/[location]/[file].cs
- [ ] T016 [US1] Add validation and error handling

### Integration & API / Scenario Tests for User Story 1 (run at the per-story checkpoint) 🐳

> When spec.md Integration Test Coverage lists endpoints/paths. In-process HTTP harness + Testcontainers DB via the shared `ApplicationFactory` fixture (create it in the first story that needs it).

- [ ] T017 [P] [US1] API test for `<METHOD> <route>` in tests/<Project>.IntegrationTests/Api/<Feature>/<Name>EndpointTests.cs — status + body + DB side-effect + event.
- [ ] T018 [P] [US1] *(when spec declares `scenario-test: <Name>ScenarioTests`)* Scenario test in tests/<Project>.IntegrationTests/Scenarios/<Feature>/<Name>ScenarioTests.cs — walks the Independent Test; asserts final state-of-world.

### E2E (Browser) Tests for User Story 1 (page-bearing frontend stories — authored AFTER the pages, run at the checkpoint, NO Red Checkpoint) 🎭

> Emit ONLY when US1 ships pages/routes in a frontend SPA and spec.md US1 does NOT declare `e2e-tests: skipped`. Author with the `e2e-testing` skill.

- [ ] T019 [P] [US1] E2E tests for `<Flow>` in tests/<Workspace>.E2ETests/<Feature>Tests.cs — cover the Required scenarios from spec.md (happy path, validation/error, auth redirect, empty state). Inherit `E2ETestBase`.

**Build gate (MANDATORY before declaring the checkpoint reached)**: `dotnet test tests/<Project>.UnitTests` → 0 failures/compile errors (any skipped test must cite a `unit-tests: skipped` declaration); `dotnet test tests/<Project>.IntegrationTests` → 0 failures (Docker up; skip if the story has no integration tests). If this story ships UI: the SPA build script (e.g. `npm run build`) green AND, unless `e2e-tests: skipped`, `dotnet test tests/<Workspace>.E2ETests --filter "FullyQualifiedName~<Feature>Tests"` green (stack up + `E2E_BASE_URL` set). Failing backend or E2E tests block the checkpoint exactly like compile errors.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently (unit green; integration/scenario + E2E green at the checkpoint)

### Click-through Parity for User Story 1 (page-bearing frontend stories — run AFTER the E2E checkpoint is green) 🎨

> Emit ONLY when US1 ships pages/routes in a frontend SPA. **Owner: the frontend developer, run manually** — this is not an automatic post-implementation step, so the defect list arrives when they are ready to triage it. Placed after the Checkpoint above because a parity audit of pages that aren't green yet reports noise.
>
> **Preconditions:** (1) the implementation was **click-through-blind** — if any page was ported or copied from the click-through the run is VOID, not clean, and is reported `NOT AUDITED`; (2) the click-through checkout is served and the product dev stack is up and signed in (paths in `.claude/skills/clickthrough-parity/reference.json`).

- [ ] T0xx [US1] Run `/clickthrough-parity <feature> phase <N>` over US1's routes and triage the report — the click-through is the source of truth. Expect real defects (mostly copy, placeholders and layout chrome the spec does not describe). Apply the ones the frontend lead accepts with `--fix`; take every **Needs discussion** item (presence / placement / a deliberate business divergence) to the design owner instead — `--fix` must never touch those. Record the route's result in `.claude/skills/clickthrough-parity/route-map.md`.

---

## Phase 4: User Story 2 - [Title] (Priority: P2)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Unit Tests for User Story 2 (REQUIRED for backend-bearing; write FIRST) ⚠️ + Red Checkpoint 🔴

> Same pattern as US1: one `[P]` unit-test task per unit in tests/<Project>.UnitTests/<Feature>/, then a non-parallel Red Checkpoint (`dotnet test tests/<Project>.UnitTests`, valid red / green-baseline-on-retrofit, commit) before implementation. Skip if `unit-tests: skipped` or frontend-only.

- [ ] T0xx [P] [US2] Unit tests for `<Type>` in tests/<Project>.UnitTests/<Feature>/<Type>Tests.cs — cover spec.md Required cases.
- [ ] T0xxR [US2] Red Checkpoint — run the unit project, verify red (or green baseline), commit.

### Implementation for User Story 2

- [ ] T020 [P] [US2] Create [Entity] model in src/models/[entity].py
- [ ] T021 [US2] Implement [Service] in src/services/[service].py
- [ ] T022 [US2] Implement [endpoint/feature] in src/[location]/[file].py
- [ ] T023 [US2] Integrate with User Story 1 components (if needed)

### Integration & API / Scenario Tests for User Story 2 (run at the per-story checkpoint) 🐳

> When spec.md Integration Test Coverage lists endpoints/paths. Reuse the shared `ApplicationFactory` fixture.

- [ ] T0xx [P] [US2] API test for `<METHOD> <route>` in tests/<Project>.IntegrationTests/Api/<Feature>/<Name>EndpointTests.cs.
- [ ] T0xx [P] [US2] *(when spec declares `scenario-test: <Name>ScenarioTests`)* Scenario test in tests/<Project>.IntegrationTests/Scenarios/<Feature>/<Name>ScenarioTests.cs.

### E2E (Browser) Tests for User Story 2 (page-bearing frontend stories — after the pages, at the checkpoint, NO Red Checkpoint) 🎭

> Emit only when US2 ships pages/routes in a frontend SPA workspace and spec.md US2 does NOT declare `e2e-tests: skipped`. Author with the `e2e-testing` skill.

- [ ] T0xx [P] [US2] E2E tests for `<Flow>` in tests/<Workspace>.E2ETests/<Feature>Tests.cs — cover the Required scenarios from spec.md US2; update `COVERAGE.md`.

**Build gate (MANDATORY)**: `dotnet test tests/<Project>.UnitTests` AND `dotnet test tests/<Project>.IntegrationTests` (if present) both green; if this story ships UI, the SPA build script green AND, unless `e2e-tests: skipped`, `dotnet test tests/<Workspace>.E2ETests --filter "FullyQualifiedName~<Feature>Tests"` green (stack up + `E2E_BASE_URL` set).

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

### Click-through Parity for User Story 2 (page-bearing frontend stories — run AFTER the E2E checkpoint is green) 🎨

> Emit ONLY when US2 ships pages/routes in a frontend SPA. **Owner: the frontend developer, run manually** — this is not an automatic post-implementation step, so the defect list arrives when they are ready to triage it. Placed after the Checkpoint above because a parity audit of pages that aren't green yet reports noise.
>
> **Preconditions:** (1) the implementation was **click-through-blind** — if any page was ported or copied from the click-through the run is VOID, not clean, and is reported `NOT AUDITED`; (2) the click-through checkout is served and the product dev stack is up and signed in (paths in `.claude/skills/clickthrough-parity/reference.json`).

- [ ] T0xx [US2] Run `/clickthrough-parity <feature> phase <N>` over US2's routes and triage the report — the click-through is the source of truth. Expect real defects (mostly copy, placeholders and layout chrome the spec does not describe). Apply the ones the frontend lead accepts with `--fix`; take every **Needs discussion** item (presence / placement / a deliberate business divergence) to the design owner instead — `--fix` must never touch those. Record the route's result in `.claude/skills/clickthrough-parity/route-map.md`.

---

## Phase 5: User Story 3 - [Title] (Priority: P3)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Unit Tests for User Story 3 (REQUIRED for backend-bearing; write FIRST) ⚠️ + Red Checkpoint 🔴

> Same pattern as US1/US2. Skip if `unit-tests: skipped` or frontend-only. Page-bearing frontend stories add an E2E (Browser) Tests subsection after implementation instead (no Red Checkpoint).

- [ ] T0xx [P] [US3] Unit tests for `<Type>` in tests/<Project>.UnitTests/<Feature>/<Type>Tests.cs — cover spec.md Required cases.
- [ ] T0xxR [US3] Red Checkpoint — run the unit project, verify red (or green baseline), commit.

### Implementation for User Story 3

- [ ] T026 [P] [US3] Create [Entity] model in src/models/[entity].py
- [ ] T027 [US3] Implement [Service] in src/services/[service].py
- [ ] T028 [US3] Implement [endpoint/feature] in src/[location]/[file].py

### Integration & API / Scenario Tests for User Story 3 (run at the per-story checkpoint) 🐳

- [ ] T0xx [P] [US3] API test for `<METHOD> <route>` in tests/<Project>.IntegrationTests/Api/<Feature>/<Name>EndpointTests.cs.
- [ ] T0xx [P] [US3] *(when spec declares `scenario-test: <Name>ScenarioTests`)* Scenario test in tests/<Project>.IntegrationTests/Scenarios/<Feature>/<Name>ScenarioTests.cs.

### E2E (Browser) Tests for User Story 3 (page-bearing frontend stories — after the pages, at the checkpoint, NO Red Checkpoint) 🎭

> Emit only when US3 ships pages/routes in a frontend SPA workspace and spec.md US3 does NOT declare `e2e-tests: skipped`. Author with the `e2e-testing` skill.

- [ ] T0xx [P] [US3] E2E tests for `<Flow>` in tests/<Workspace>.E2ETests/<Feature>Tests.cs — cover the Required scenarios from spec.md US3; update `COVERAGE.md`.

**Build gate (MANDATORY)**: full-solution `dotnet test` green; if any UI was shipped, the SPA build script green AND, for each page-bearing story without an `e2e-tests: skipped` declaration, `dotnet test tests/<Workspace>.E2ETests` green (stack up + `E2E_BASE_URL` set).

**Checkpoint**: All user stories should now be independently functional

### Click-through Parity for User Story 3 (page-bearing frontend stories — run AFTER the E2E checkpoint is green) 🎨

> Emit ONLY when US3 ships pages/routes in a frontend SPA. **Owner: the frontend developer, run manually** — this is not an automatic post-implementation step, so the defect list arrives when they are ready to triage it. Placed after the Checkpoint above because a parity audit of pages that aren't green yet reports noise.
>
> **Preconditions:** (1) the implementation was **click-through-blind** — if any page was ported or copied from the click-through the run is VOID, not clean, and is reported `NOT AUDITED`; (2) the click-through checkout is served and the product dev stack is up and signed in (paths in `.claude/skills/clickthrough-parity/reference.json`).

- [ ] T0xx [US3] Run `/clickthrough-parity <feature> phase <N>` over US3's routes and triage the report — the click-through is the source of truth. Expect real defects (mostly copy, placeholders and layout chrome the spec does not describe). Apply the ones the frontend lead accepts with `--fix`; take every **Needs discussion** item (presence / placement / a deliberate business divergence) to the design owner instead — `--fix` must never touch those. Record the route's result in `.claude/skills/clickthrough-parity/route-map.md`.

---

[Add more user story phases as needed, following the same pattern]

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] TXXX [P] Documentation updates in docs/
- [ ] TXXX Code cleanup and refactoring
- [ ] TXXX Performance optimization across all stories
- [ ] TXXX [P] Additional unit tests (if requested) in tests/unit/
- [ ] TXXX Security hardening
- [ ] TXXX Run quickstart.md validation
- [ ] TXXX **Full-module click-through parity audit — run before the module is pushed / shipped.** `/clickthrough-parity <feature>` with a **bare feature and NO phase**, which widens the scope to every page-bearing route of the module in one pass. This is not a repeat of the per-story runs: only whole-module scope can see the cross-page **placement** differences (the same control sitting on a different page than the design), because those need the module's full page map. Triage the report; `--fix` what the frontend lead accepts, escalate the Needs-discussion list. *(Emit only for modules that ship pages.)*

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - May integrate with US1 but should be independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - May integrate with US1/US2 but should be independently testable

### Within Each User Story

- Unit tests for backend stories MUST be written and FAIL (Red Checkpoint) before implementation; integration/scenario + E2E run at the per-story checkpoint
- Click-through parity runs **after** that checkpoint is green, as its own assigned task — never before, and never automatically
- Models before services
- Services before endpoints
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within Phase 2)
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- All tests for a user story marked [P] can run in parallel
- Models within a story marked [P] can run in parallel
- Different user stories can be worked on in parallel by different team members

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together (if tests requested):
Task: "Contract test for [endpoint] in tests/contract/test_[name].py"
Task: "Integration test for [user journey] in tests/integration/test_[name].py"

# Launch all models for User Story 1 together:
Task: "Create [Entity1] model in src/models/[entity1].py"
Task: "Create [Entity2] model in src/models/[entity2].py"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
