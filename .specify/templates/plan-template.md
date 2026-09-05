# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]

**Primary Dependencies**: [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]

**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]

**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]

**Target Platform**: [e.g., Linux server, iOS 15+, WASM or NEEDS CLARIFICATION]

**Project Type**: [e.g., library/cli/web-service/mobile-app/compiler/desktop-app or NEEDS CLARIFICATION]

**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]

**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]

**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

[Gates determined based on constitution file]
### Frontend Design Gate *(skip if feature has no UI)*

If this feature ships any UI under `frontend/`, the agent MUST treat the following as
binding and confirm each before Phase 0 research:

- [ ] Read the repo-root `CLAUDE.md` end to end (design system, RTL rules, brand palette
      and D1–D5 scale, Component Sourcing Rule, DO / DO NOT lists, brand voice).
- [ ] Reuse existing components from `frontend/src/components/` (`ui/` primitives, `cx/`
      feature components) per the Component Sourcing Rule — never recreate what exists.
- [ ] Brand palette (`nb-*`) is for chrome only; `D1`–`D5` is for KPI status only
      (Two-Palette Rule). Only logical direction utilities (`ps-*`, `ms-*`, `text-start`).
- [ ] Both light AND dark themes verified; both RTL AND LTR verified.

A spec that violates the repo-root `CLAUDE.md` is invalid and must be revised before
tasks are generated.

### Backend Data-Access Gate *(skip if the feature touches no database)*

If this feature reads or writes PostgreSQL, confirm each before Phase 0 research
(constitution **DB-08** / database-constitution **Article 7** — the M-10 reference pattern):

- [ ] **EF Core only** — no raw ADO.NET (`NpgsqlConnection`/`NpgsqlCommand`) or raw-SQL
      escape hatches (`FromSql*`/`ExecuteSql*`) in feature code.
- [ ] Tables added to the module SQL baseline (`_Baseline.sql` / `_ControlPlane.sql`);
      **no EF migrations**. One `IEntityTypeConfiguration<T>` per entity with explicit
      `HasColumnName` + FK relationships.
- [ ] Context interfaces (`ITenantDbContext` / `IControlPlaneDbContext`) in Application,
      concrete contexts in Infrastructure. Multi-write atomicity via
      `ITenantDbContext.ExecuteAsync` — **no unit-of-work type**; no transaction spans both
      databases.
- [ ] Per-aggregate data-access service + port; business services depend on the port
      (the unit-test seam). Time via injected `TimeProvider`.

### Backend Module Structure Gate *(skip if the feature ships no backend module code)*

If this feature adds or extends a backend module, confirm each before Phase 0 research
(constitution **AMENDMENT-009** / architecture-constitution **Article 1A** — the
`Nabadat.UserManagement` reference layout):

- [ ] Module is a single `Nabadat.<DomainName>` library (AMENDMENT-008) with the four
      top-level layer folders **`Api/`, `Application/`, `Domain/`, `Infrastructure/`** —
      no new top-level folder kind.
- [ ] Dependency direction is inward-only: `Api → Application → Domain` and
      `Infrastructure → Application → Domain`; `Domain` references nothing; `Api` and
      `Infrastructure` never reference each other. Wiring lives only in
      `<DomainName>ServiceCollectionExtensions`.
- [ ] Interface placement: EF context ports → `Application/Interfaces/`; sub-domain
      service/data-access ports → `Application/<SubDomain>/Interfaces/`; **published
      cross-module interfaces** → `Domain/Interfaces/`; Api-layer ports → `Api/Interfaces/`.
- [ ] New work adds files into existing layer/sub-domain folders (a new bounded concern =
      a new `Application/<SubDomain>/` folder with its mirror unit-test folder), one type
      per file.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]

# [REQUIRED for a .NET backend module — constitution AMENDMENT-009 / architecture Article 1A]
src/Nabadat.<DomainName>/
├── Nabadat.<DomainName>.csproj
├── <DomainName>ServiceCollectionExtensions.cs   # composition root
├── Api/            # Controllers/ Contracts/ Middleware/ Accessors/ Interfaces/ [Tenancy/]
├── Application/    # Interfaces/ (context ports) + <SubDomain>/ {Service.cs, Interfaces/, Dtos/, Exceptions/}
├── Domain/         # Entities/ ValueObjects/ Interfaces/ (published cross-module interfaces)
└── Infrastructure/ # data-access (when the module owns tables): Persistence/+Configurations/ [ControlPlane/+Configurations/] [Migrations/]
                    # + one <Concern>/ folder per external adapter the module has (module-specific; M-10 has Crypto/ Auth/ Audit/ Notifications/)
tests/
├── Nabadat.<DomainName>.UnitTests/         # mirrors Application/<SubDomain>/ + TestSupport/
└── Nabadat.<DomainName>.IntegrationTests/  # Endpoints/ Services/ Scenarios/ Infrastructure/
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., raw SQL via `FromSql`] | [specific problem] | [why EF Core mapping (DB-08) insufficient] |
