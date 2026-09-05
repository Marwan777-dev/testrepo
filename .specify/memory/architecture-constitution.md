# Nabadat Platform — Architecture Constitution

**Project:** Nabadat — multi-tenant Voice-of-Customer / CX SaaS platform.
**Source:** HLD Chapter 2 "System Overview" + Chapter 1.3 module roster, reconciled to `constitution.md` (router, v1.6.0).
**Status:** Foundational architecture principles. Every other layer constitution depends on these articles. A change that contradicts an article is not implemented; the design is revised, or the principle is revisited in a documented amendment — never silently bypassed.

---

## Article 1 — The Architectural Pattern

1. **Modular monolith, containerized, event-logged.** The platform modules share a runtime within the application layer (they are not independent network services); the application and supporting components are packaged as containers. Modules coordinate through two mechanisms only — synchronous calls through published interfaces, and domain events recorded synchronously via **M-17 (Event Log)** — per Article 3.
2. **The module boundary is the architectural unit; the container boundary is the deployment unit.** A module is a bound unit of code with its own responsibilities, its own tables, and its own published interface. A unit that needs a separate runtime is **not** a module — it is a supporting service in its own container.
3. **One application runtime.** All modules run inside the same processes (`nabadat-api`, `nabadat-worker`, `nabadat-scheduler`) on the same application servers.
4. **Deliberate pattern choice.** Microservices were rejected (network boundaries, distributed transactions, per-service tooling exceed the benefit at this scope); a classical monolith was rejected (direct coupling of internal types and tables); the modular monolith with an event log keeps modules decoupled while keeping the synchronous path fast.

---

## Article 1A — Canonical Module Folder Structure

Every backend module is a single class library `Nabadat.<DomainName>` (router AMENDMENT-008) organised into **four layers as top-level folders** with an inward-only dependency direction. The **M-10 module (`Nabadat.UserManagement`) is the reference implementation**; new modules — and new features added inside existing modules — MUST follow this layout. Data-access specifics inside `Infrastructure/` are governed by database-constitution **Article 7** / router **DB-08**. **What is fixed vs. module-specific:** the four layer roots and the `Api/` and `Domain/` sub-folder *kinds* are fixed; the `Application/<SubDomain>/` and `Infrastructure/<Concern>/` folders are named after the module's own bounded concerns and external adapters (the names in the tree below are M-10's, shown as a worked example — not a required set).

```text
src/Nabadat.<DomainName>/
├── Nabadat.<DomainName>.csproj
├── <DomainName>ServiceCollectionExtensions.cs   # composition root: Add<DomainName>Module(...)
├── Api/                       # ASP.NET surface. Depends on Application + Domain (+ AspNetCore). No persistence.
│   ├── Controllers/           # one controller per resource group; thin — delegates to Application services
│   ├── Contracts/             # request/response DTOs (one type per file)
│   ├── Middleware/            # module middleware + its `*Extensions`
│   ├── Accessors/             # request-scoped accessors (the authenticated session, the current tenant)
│   └── Interfaces/            # Api-layer accessor ports (e.g. ICurrentSessionAccessor, ICurrentTenant) — see rule 7
├── Application/               # use-case orchestration. Depends on Domain only.
│   ├── Interfaces/            # EF DbContext ports (ITenantDbContext, IControlPlaneDbContext) + ICurrentTenant
│   └── <SubDomain>/           # one folder per bounded sub-domain (Auth, Users, Permissions, Events, …)
│       ├── <Name>Service.cs   # business + per-aggregate data-access services
│       ├── Interfaces/        # service + data-access ports for this sub-domain (the unit-test seam)
│       ├── Dtos/              # use-case inputs/results (one type per file)
│       └── Exceptions/        # sub-domain exceptions
├── Domain/                    # pure model. References nothing.
│   ├── Entities/              # persistent entities/aggregates (one per file; no tenant_id in tenant tables)
│   ├── ValueObjects/          # value objects + enums (+ their `*Extensions`)
│   └── Interfaces/            # PUBLISHED cross-module interfaces + cross-cutting client ports (audit reader, M-09 client)
└── Infrastructure/            # adapters. Depends on Application + Domain.
    ├── Persistence/           # TenantDbContext + Configurations/  — present when the module owns tenant tables
    ├── ControlPlane/          # ControlPlaneDbContext + Configurations/  — only if the module owns control-plane tables
    ├── Migrations/            # the module's SQL schema scripts (_Baseline.sql [+ _ControlPlane.sql]) — when it owns tables
    └── <Concern>/             # one folder per external/adapter concern the module actually has, named by concern
                               #   (module-specific, open set — the M-10 reference happens to have Crypto/, Auth/, Audit/, Notifications/)
```

Rules (each is binding; a deviation is an amendment, not a judgement call):

1. **Four layer folders are mandatory; the dependency arrow points inward only.** `Api → Application → Domain` and `Infrastructure → Application → Domain`. `Domain` references nothing; `Application` references only `Domain`; `Api` and `Infrastructure` never reference each other. The composition root (`<DomainName>ServiceCollectionExtensions`) is the only place interfaces are wired to Infrastructure concretes.
2. **One sub-domain folder per bounded concern under `Application/`**, each carrying its own `Interfaces/` (and `Dtos/` + `Exceptions/` as needed). A sub-domain folder name SHOULD match a unit-test folder of the same name.
3. **Interface placement is fixed:** EF context ports → `Application/Interfaces/`; sub-domain service/data-access ports → `Application/<SubDomain>/Interfaces/`; **published cross-module interfaces** and cross-cutting client ports → `Domain/Interfaces/`; Api-layer ports → `Api/Interfaces/`.
4. **`Infrastructure/` = data-access folders (fixed, when applicable) + adapter folders (module-specific).** Persistence is confined here: `Persistence/` (`TenantDbContext` + a `Configurations/` sub-folder, one `IEntityTypeConfiguration<T>` per entity + value converters) when the module owns tenant tables; `ControlPlane/` only if it owns control-plane tables; `Migrations/` holds the module's SQL schema scripts (`_Baseline.sql` [+ `_ControlPlane.sql`]) — DDL only, **no EF migrations** (Article 7.6). A module that owns no tables has none of these three. **Every other `Infrastructure/` sub-folder is a module-specific adapter grouping named by the external concern it wraps** — the set is open and per-module, never a fixed checklist (the M-10 reference happens to expose `Crypto/`, `Auth/`, `Audit/`, `Notifications/`). No persistence type appears in `Api` / `Application` / `Domain`.
5. **Folders are by responsibility, one type per file** (project rule) — never a technical-kind bucket dumped at the module root.
6. **Tests mirror the module:** `tests/Nabadat.<DomainName>.UnitTests/<SubDomain>/…` (mirrors `Application/<SubDomain>/`, plus `TestSupport/`); `tests/Nabadat.<DomainName>.IntegrationTests/{Endpoints,Services,Scenarios,Infrastructure}/` (CLAUDE.md "Unit Test Policy" rule 11).
7. **"Context" is reserved for the EF `DbContext`.** Do **not** put *Context* in `Api`- or `Application`-layer type names for request-scoped state — name those for what they hold (`ICurrentTenant`; a current-session accessor such as `ICurrentSessionAccessor` / `CurrentSessionAccessor`, the value object it returns as `CurrentSession` / `SessionPrincipal`). This way an unqualified *Context* in the codebase always means a `DbContext` (`ITenantDbContext` / `IControlPlaneDbContext`), never a request-scoped accessor.

A new top-level folder kind (a fifth layer, or a technical-kind bucket at the module root) is an architectural change requiring an amendment.

---

## Article 2 — The Module Registry

Each module owns a defined area of responsibility and its own tables. A new module is a documented architectural change. Canonical IDs and owned-table lists are maintained in `constitution.md` Section 3; this article must stay in sync with it.

**Phase 1 modules:**

| ID | Module |
| --- | --- |
| M-01 | Survey and Form Builder |
| M-02 | Channel Management and Distribution |
| M-03 | Audience and Contact Management |
| M-04 | Response Collection Engine |
| M-05 | NLP and Text Analytics |
| M-06 | CX Metrics and KPI Engine |
| M-07 | Dashboards and Reporting |
| M-08 | Closed-Loop Case Management |
| M-09 | Notifications and Alerts Engine |
| M-10 | User and Role Management |
| M-11 | Tenant Administration |
| M-16 | Journey Management |
| M-17 | Event Log (owns `event_log` and `audit_log`) |
| M-18 | Commercial & Metering |
| M-19 | Billing Operations |

**Phase 2 modules** (tables provisioned empty at Phase 1; activated without migration):

| ID | Module |
| --- | --- |
| M-12 | AI Intelligence and Recommendations |
| M-13 | Integration Hub |
| M-14 | Survey Logic and Targeting Engine |
| M-15 | Action Management |

---

## Article 3 — Cross-Module Communication

### 3.1 Two communication modes
- **Synchronous (permitted, through a published interface).** Where a caller needs a result before continuing (login, dashboard load, response acknowledgement, report streaming), or needs data from another module to fulfil a request, it may call that module **synchronously through the module's published interface**. No module may reference another module's concrete types, internal classes, or tables directly.
- **Event-driven (preferred for side effects).** For cross-module side effects (NLP processing, metric recalculation, case evaluation, notification dispatch), the producing module records a domain event via **M-17**; interested modules consume it independently. This keeps side effects off the synchronous path and makes them independently retryable. Developers default to this mode unless a synchronous result is genuinely required.

### 3.2 Interface contract rule
All direct cross-module calls go through a defined interface. A module exposes what it is willing to share via an interface; consumers depend on that interface only. No module instantiates another module's concrete classes, references its internal types, or touches its tables directly. (Module data isolation is enforced in code and code review — see the database constitution; it is not a database-schema boundary.)

### 3.3 M-17 for downstream work
When a user-facing request triggers work elsewhere that need not block the response, recording a domain event via M-17 is the required pattern.

---

## Article 4 — Non-Negotiable Architecture Principles

1. **Configuration over code.** Behavior that may differ across tenants (branding, hierarchy, KPI definitions, thresholds, routing, alert rules, templates, survey logic, language, retention, channels, permission profiles) is configuration. A new survey, KPI, alert rule, or dashboard does **not** require a code release.
2. **Multi-language by design.** Language handling is built into the data model, processing pipeline, and rendering engine. Arabic (Gulf, Levantine, Egyptian, MSA) is processed in its written variant; English as English. RTL is full layout mirroring. New languages are added without architectural change. Language is resolved from tenant context, never hardcoded.
3. **Configuration isolation.** Configuration is versioned, audited, and rollback-able independently of operational data.
4. **Isolation at every layer.** Isolation (tenant, data, identity, process, network) is applied at each layer rather than relying on a single boundary.

---

## Article 5 — Zone Model (context)

The platform is reasoned about in four zones; coding decisions must respect their boundaries even though provisioning the zones is an infrastructure concern.

| Zone | Purpose | Reachable from | Coding consequence |
| --- | --- | --- | --- |
| **Zone 1 — Edge / Front-End** | TLS termination, WAF, rate limiting, static assets | Public + tenant internal networks | The front end contains no business logic; it calls the API only. |
| **Zone 2 — Application** | The module runtime, API surface, NLP service, file storage | Zone 1 only | All business logic, validation, authz, and security enforcement live here, server-side. |
| **Zone 3 — Data** | PostgreSQL (primary + replica), Elasticsearch | Zone 2 only | Data stores are never reachable from the edge or end users; the app tier is the only client. |
| **Zone 4 — Management** | Out-of-band operations (jump host, MFA) | Operations only | Not part of any request path; cannot reach user-data paths. |

Security is always enforced **server-side** in Zone 2 — never relied upon at the edge or client.

---

## Article 6 — Governance and Amendment

Any change that violates an article — a direct cross-module table read or concrete-type reference, placing business logic at the edge/client, requiring a code release for a configuration-class change, or breaking tenant/zone isolation — requires an explicit, recorded amendment before implementation.
