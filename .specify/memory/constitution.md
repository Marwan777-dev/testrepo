# Nabadat Platform Constitution

**Version**: `1.12.0` | **Ratified**: `2026-05-06` | **Last Amended**: `2026-06-22`

---

## When to Read What

Read this file first for every task. Load the additional file(s) below when your task touches that area.

| Your task involves…                                               | Read                                                                                 |
| ----------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| Module structure, cross-module calls, event publishing            | [`architecture-constitution.md`](architecture-constitution.md)                       |
| Schema design, migrations, queries, Elasticsearch indices         | [`database-constitution.md`](database-constitution.md)                               |
| API design, versioning, auth headers, pagination, error envelope  | [`APIs-constitution.md`](APIs-constitution.md)                                       |
| Auth, JWT, permissions, RBAC, audit events                        | [`security-constitution.md`](security-constitution.md)                               |
| Error codes, HTTP status mapping, dead-letter, idempotency        | [`error-handling-constitution.md`](error-handling-constitution.md)                   |
| External integrations (AD, SMTP, KMS, NLP, SMS)                   | [`integrations-constitution.md`](integrations-constitution.md)                       |
| Performance-sensitive paths, bottleneck patterns, latency targets | [`scalability-performance-constitution.md`](scalability-performance-constitution.md) |

---

## 1. Platform Identity

Nabadat is a multi-tenant, multi-language SaaS platform for Voice of Customer (VOC) and Customer Experience (CX) management.

**Architecture pattern**: Modular monolith with synchronous event logging via M-17.

**Technology stack**

| Layer                  | Technology                                                |
| ---------------------- | --------------------------------------------------------- |
| Backend                | C# / .NET 10, ASP.NET Core                                |
| Primary database       | PostgreSQL 16+, schema-per-tenant isolation               |
| Analytics engine       | Elasticsearch 8+ (OpenSearch for on-premises deployments) |
| Admin frontend         | React                                                     |
| Survey renderer        | React / Preact (lightweight)                              |
| SaaS deployment        | Kubernetes on a cloud provider                            |
| On-premises deployment | Docker Compose on client infrastructure                   |

**No caching layer.** Redis is not in the stack. In-memory caching of analytics data is not permitted. All read-side analytics execute directly against Elasticsearch. (The only permitted in-process cache is the KMS data-key cache; see scalability-performance-constitution.md Article 3.)

---

## 1A. Core Governing Principles (GP-01 – GP-05)

These five principles are binding on every spec. New specs MUST reference GP-01–GP-05 explicitly in their Constitution Check section. Any spec storing personal data MUST satisfy GP-03.

### GP-01 — Single Source of Truth

PostgreSQL is the authoritative system of record. Elasticsearch is a derived, read-side projection that MUST be fully rebuildable from PostgreSQL at any time. No store other than PostgreSQL is authoritative.
_Pass condition:_ rebuild all ES indices from PostgreSQL → query results identical to the pre-drop snapshot.

### GP-02 — Customer-Controlled Encryption

High-sensitivity fields and attachments are envelope-encrypted under the customer-owned CMK in the cloud-provider KMS. The CMK never leaves KMS; the platform never holds key material at rest. Revoking the CMK renders the protected data permanently unreadable.
_Pass condition:_ revoke the CMK → decrypt any envelope-encrypted field → permanent failure.

### GP-03 — Right to Erasure

An erasure request clears the subject's data across all stores within SLA, and restoring a backup does not resurface erased data.
_Pass condition:_ erasure request → all stores cleaned within SLA; a subsequent backup restore does not resurface erased data.

### GP-04 — Tenant / Scope Isolation

No tenant — and no branch/scope within a tenant — can access another's data, even in error or edge-case states. Denied attempts are audited.
_Pass condition:_ Branch X user queries Branch Y data → empty/403; the attempt appears in the M-17 `audit_log`.

### GP-05 — Constitution Compliance Gate

Every plan passes the Constitution Check before implementation begins.
_Pass condition:_ the Constitution Check in `plan-template.md` passes before implementation begins.

> Note: principles previously numbered GP-02 (network zone segregation), GP-04 (Riyadh-region residency), and GP-05 (per-module-schema isolation) were removed by AMENDMENT-006; the surviving principles were renumbered to GP-01–GP-05. Network/zone context now lives in architecture-constitution.md Article 5; residency is governed by T-04; module isolation by AD-01 and the database constitution.

---

## 2. Architecture Decisions

All decisions in this section are **locked and non-negotiable**. A spec that contradicts any decision below is **invalid** and MUST be rewritten before it can be accepted.

### AD-01 — Modular Monolith

All modules run in the **same process**. There are no microservices, no service mesh, and no distributed tracing between services.

Rules:

- Synchronous cross-module calls are permitted **only through a module's published interface**. A module exposes a published interface; consumers depend on that interface only.
- No module may reference another module's concrete types, internal classes, or tables directly.
- Cross-module side effects are recorded as domain events via **M-17 (Event Log)** — the preferred pattern for work that need not block the caller.
- Direct cross-module **database** access is **forbidden**.

### AD-02 — Schema-Per-Tenant Isolation

Every tenant has a dedicated PostgreSQL schema named `tenant_{slug}` (e.g., `tenant_acme`).

Rules:

- There are **no** `tenant_id` columns in any tenant table.
- There are **no** row-level security policies.
- There are **no** shared query paths between tenants.
- The schema boundary is the **only** tenant-isolation mechanism.
- Connection pools are per-tenant and pre-configured to the correct schema.

### AD-03 — No Caching Layer

Redis is not in the stack and MUST NOT be introduced.

Rules:

- In-memory caching of analytics data is **not permitted**.
- All dashboard and reporting queries MUST execute against Elasticsearch.
- The sole exception is the in-process KMS data-key cache (key material only, never business data).

### AD-04 — Elasticsearch for All Read-Side Analytics

PostgreSQL is the write store (source of truth). Elasticsearch is the read store for all analytics, dashboards, aggregations, and full-text search.

Rules:

- No module may query PostgreSQL for dashboard or reporting use cases.
- The two per-tenant Elasticsearch indices are `tenant_{tenantId}_responses` and `tenant_{tenantId}_analytics`.
- No other per-tenant index naming pattern is valid.

### AD-05 — Single Codebase, Two Deployment Modes

All code MUST work in both SaaS mode and on-premises mode.

Rules:

- Deployment mode is controlled **exclusively** by environment flags (see Section 10).
- No code branching by deployment mode is permitted.
- The three controlling flags are `ENABLE_MULTI_TENANT`, `ENABLE_BILLING`, and `ENABLE_TENANT_MGMT`.

### AD-06 — Phase 2 Tables Provisioned at Phase 1

Tables owned by Phase 2 modules (`M-12`, `M-13`, `M-14`, `M-15`) are provisioned as **empty tables** during Phase 1 tenant creation.

Rules:

- No migration is required at Phase 2 activation.
- All specs MUST respect this constraint.
- The full list of reservation tables is documented in Database Rule DB-06 (Section 6).

### AD-07 — Tenant Context Is Immutable Per Request

Tenant context is resolved **once** at the API gateway and injected into the request pipeline.

Rules:

- No downstream module may change or override tenant context after it is resolved.
- Specs MUST NOT include logic that modifies tenant context post-resolution.
- Tenant resolution MUST occur in an HTTP request filter that derives the tenant from the request's subdomain, loads the tenant metadata (`tenantId`, `tenantName`, `tenantDomain`, etc.), and stores it in a request-scoped thread-local tenant context object.

---

## 3. Module Registry

All specs MUST use these canonical module IDs and names. Divergence is a defect.

### Phase 1 Modules

| ID     | Name                                | Owned Tables                                                                                                                                                                                                                                                                                                                                                                             |
| ------ | ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `M-01` | Survey and Form Builder             | `surveys`, `questions`, `question_bank`, `survey_versions`, `survey_templates`                                                                                                                                                                                                                                                                                                           |
| `M-02` | Channel Management and Distribution | `channels`, `campaigns`, `distribution_configs`, `delivery_log`                                                                                                                                                                                                                                                                                                                          |
| `M-03` | Audience and Contact Management     | `contacts`, `segments`, `contact_lists`, `import_jobs`                                                                                                                                                                                                                                                                                                                                   |
| `M-04` | Response Collection Engine          | `responses`, `response_tokens`, `partial_responses`                                                                                                                                                                                                                                                                                                                                      |
| `M-05` | NLP and Text Analytics              | `sentiment_results`, `themes`, `keywords`, `nlp_jobs`                                                                                                                                                                                                                                                                                                                                    |
| `M-06` | CX Metrics and KPI Engine           | `kpi_definitions`, `metric_configs`, `metric_values`, `thresholds`                                                                                                                                                                                                                                                                                                                       |
| `M-07` | Dashboards and Reporting            | `dashboard_configs`, `report_templates`, `report_jobs`, `report_exports`                                                                                                                                                                                                                                                                                                                 |
| `M-08` | Closed-Loop Case Management         | `cases`, `case_history`, `escalation_chains`, `sla_configs`                                                                                                                                                                                                                                                                                                                              |
| `M-09` | Notifications and Alerts Engine     | `notification_templates`, `notification_log`, `delivery_status`, `alert_rules`                                                                                                                                                                                                                                                                                                           |
| `M-10` | User and Role Management            | `users`, `roles`, `permissions`, `user_groups`, `role_assignments`                                                                                                                                                                                                                                                                                                                       |
| `M-11` | Tenant Administration               | `settings`, `branding_configs`, `language_configs`                                                                                                                                                                                                                                                                                                                                       |
| `M-16` | Journey Management                  | `journeys`, `stages`, `touchpoints`, `kpi_bindings`, `scoring_configs`, `personas`, `journey_persona_bindings`, `journey_versions`, `detection_configs`, `detection_threshold_overrides`, `report_contracts`, `kpi_type_definitions`, `journey_scores`                                                                                                                                   |
| `M-17` | Event Log & Audit                   | `event_log`, `audit_log`                                                                                                                                                                                                                                                                                                                                                                 |
| `M-18` | Commercial & Metering               | `commercial_plans`, `plan_versions`, `plan_version_names`, `plan_currency_variants`, `meter_dimensions`, `depletion_thresholds`, `plan_assignments`, `credit_ledger_entries`, `pending_grant_requests`, `metering_queue`, `meter_event_idempotency_keys`, `depletion_threshold_events`, `tech_grant_caps`                                                                                |
| `M-19` | Billing Operations                  | `invoices`, `invoice_line_items`, `invoice_tax_lines`, `credit_notes`, `tax_jurisdictions`, `tax_rate_versions`, `payment_providers`, `payments`, `refunds`, `dunning_rule_sets`, `dunning_records`, `dunning_step_records`, `dunning_step_schedules`, `reconciliation_imports`, `reconciliation_discrepancies`, `unallocated_credits`, `qbs_legal_entities`, `invoice_number_sequences` |

> Audit ownership note (AMENDMENT-006): the `audit_log` table is owned by **M-17**, which reads domain events from `event_log` and writes append-only, tamper-evident audit entries. (Previously assigned to M-11.)

### Phase 2 Modules (tables provisioned empty at Phase 1 tenant creation)

| ID     | Name                                | Owned Tables                                                          |
| ------ | ----------------------------------- | --------------------------------------------------------------------- |
| `M-12` | AI Intelligence and Recommendations | `ai_recommendations`, `anomaly_events`, `trend_snapshots`             |
| `M-13` | Integration Hub                     | `api_keys`, `webhook_configs`, `connector_configs`, `integration_log` |
| `M-14` | Survey Logic and Targeting Engine   | `branching_rules`, `targeting_rules`, `ab_test_configs`               |
| `M-15` | Action Management                   | `action_plans`, `action_assignments`, `action_progress`               |

**Module reference rules**: always use the canonical module ID; always reference the correct owning module when consuming a capability; a spec for module X MUST NOT include logic that belongs to module Y.

---

## 4. Event Catalogue

Specs MUST only record events from this catalogue by calling M-17 synchronously. New event types require a constitution amendment. Downstream modules read events from M-17 by querying `event_log` by `event_type`.

| Event                                                                                             | Source Module | Downstream Modules (read from M-17)                                       |
| ------------------------------------------------------------------------------------------------- | ------------- | ------------------------------------------------------------------------- |
| `survey.published`                                                                                | `M-01`        | —                                                                         |
| `survey.archived`                                                                                 | `M-01`        | —                                                                         |
| `campaign.dispatched`                                                                             | `M-02`        | —                                                                         |
| `delivery.confirmed`                                                                              | `M-02`        | —                                                                         |
| `delivery.failed`                                                                                 | `M-02`        | —                                                                         |
| `segment.updated`                                                                                 | `M-03`        | —                                                                         |
| `survey.response.submitted`                                                                       | `M-04`        | `M-05`, `M-06`, `M-07`, `M-03`, `M-11`, `M-12` (Ph2), `M-14` (Ph2)        |
| `survey.response.partial`                                                                         | `M-04`        | —                                                                         |
| `campaign.completed`                                                                              | `M-04`        | —                                                                         |
| `sentiment.analyzed`                                                                              | `M-05`        | `M-06`, `M-12` (Ph2)                                                      |
| `metric.threshold.breached`                                                                       | `M-06`        | `M-09`, `M-12` (Ph2), `M-15` (Ph2)                                        |
| `report.generated`                                                                                | `M-07`        | —                                                                         |
| `case.created`                                                                                    | `M-08`        | `M-09`                                                                    |
| `case.resolved`                                                                                   | `M-08`        | —                                                                         |
| `case.escalated`                                                                                  | `M-08`        | `M-09`                                                                    |
| `sla.breached`                                                                                    | `M-08`        | `M-09`                                                                    |
| `rule.alert.triggered`                                                                            | `M-09`        | `M-08`                                                                    |
| `notification.sent`                                                                               | `M-09`        | —                                                                         |
| `notification.failed`                                                                             | `M-09`        | —                                                                         |
| `user.created`                                                                                    | `M-10`        | —                                                                         |
| `role.assigned`                                                                                   | `M-10`        | —                                                                         |
| `settings.changed`                                                                                | `M-11`        | —                                                                         |
| `tenant.provisioned`                                                                              | `M-11`        | —                                                                         |
| `journey.created`                                                                                 | `M-16`        | —                                                                         |
| `journey.updated`                                                                                 | `M-16`        | —                                                                         |
| `journey.status.changed`                                                                          | `M-16`        | —                                                                         |
| `journey.stage.added`                                                                             | `M-16`        | —                                                                         |
| `journey.stage.removed`                                                                           | `M-16`        | —                                                                         |
| `journey.touchpoint.added`                                                                        | `M-16`        | —                                                                         |
| `journey.touchpoint.removed`                                                                      | `M-16`        | —                                                                         |
| `journey.kpi_bindings.updated`                                                                    | `M-16`        | —                                                                         |
| `journey.scoring_config.updated`                                                                  | `M-16`        | —                                                                         |
| `journey.detection_config.updated`                                                                | `M-16`        | —                                                                         |
| `journey.version.published`                                                                       | `M-16`        | —                                                                         |
| `journey.score.updated`                                                                           | `M-16`        | —                                                                         |
| `persona.created`                                                                                 | `M-16`        | —                                                                         |
| `persona.updated`                                                                                 | `M-16`        | —                                                                         |
| `persona.status.changed`                                                                          | `M-16`        | —                                                                         |
| `trend.anomaly.detected`                                                                          | `M-12`        | —                                                                         |
| `ai.recommendation.created`                                                                       | `M-12`        | `M-15` (Ph2)                                                              |
| `webhook.dispatched`                                                                              | `M-13`        | —                                                                         |
| `integration.synced`                                                                              | `M-13`        | —                                                                         |
| `action.created`                                                                                  | `M-15`        | —                                                                         |
| `action.completed`                                                                                | `M-15`        | —                                                                         |
| `plan.*` (created/published/deprecated/restored/retired/assigned/assignment.changed)              | `M-18`        | `M-11` (audit)                                                            |
| `credit.grant.*` (issued/pending/approved/rejected), `credit.correction.issued`                   | `M-18`        | `M-09` (pending → notify Financial), `M-11` (audit)                       |
| `meter.depletion.*` (warning/throttle/suspension), `meter.period.closed`                          | `M-18`        | `M-09`, Feature 1 (suspend), `M-19` (period.closed → invoice)             |
| `billing.invoice.*`, `billing.payment.*`, `billing.dunning.*`, `billing.provider.health_degraded` | `M-19`        | `M-09` (delivery/alerts), Feature 1 (throttle/suspension), `M-11` (audit) |

(Full per-event downstream detail for M-18/M-19 is retained in AMENDMENT-002 and AMENDMENT-004.)

---

## 5. Cross-Cutting Concern Ownership

### Audit Log — owned by `M-17`

`M-17` records domain events to `event_log` and writes append-only, tamper-evident entries to `audit_log`.

Rules:

- No other module writes `audit_log` entries directly.
- Every spec MUST record the correct event via M-17 so it is audited.
- A spec that writes directly to `audit_log` from a non-`M-17` module is **invalid**.

### Notification Delivery — owned by `M-09`

All email, SMS, WhatsApp, push, and in-app notifications to **platform users** route through `M-09`. No other module dispatches notifications directly. (`M-02` handles customer-facing survey distribution to respondents — a distinct concern.)

### Permission Enforcement — owned by `M-10`

Permission checks are enforced at the API layer via middleware using the role context from `M-10`. No business module implements its own permission check; specs declare the required permission scope per endpoint.

### Language and RTL — owned by `M-11`

Language configuration (supported languages, RTL flag, locale) is managed in `M-11` and consumed as a tenant-context property. Specs read language from tenant context; they do not resolve it independently.

### Data Residency — owned by infrastructure / `M-11` at provisioning time

The `jurisdiction` field in the Global Tenant Configuration Database is set at onboarding and is **immutable**. No runtime code reads or reroutes based on `jurisdiction`; residency is enforced at the infrastructure provisioning layer. Any spec that adds runtime jurisdiction routing logic is **invalid**.

### AI Output Advisory Constraint — Tenet T-06 (no exceptions)

All AI-generated outputs are stored as **advisory fields**. No spec may auto-execute an action based solely on an AI output; every AI-triggered action requires a human-configured rule or human-initiated step. No exceptions; cannot be overridden by tenant configuration.

---

## 6. Database Spec Rules

### DB-01 — Schema Naming

Tenant schemas are named `tenant_{slug}`. The global control-plane database is a **separate** PostgreSQL database, not a schema within the tenant cluster.

### DB-02 — No `tenant_id` Columns in Tenant Tables

Tenant tables MUST NOT have a `tenant_id` column. Isolation is at the schema level (AD-02). (Global control-plane tables — M-18/M-19 — are exempt and DO use `tenant_id` FKs.)

### DB-03 — Primary Keys

All tenant tables use `UUID` or `integer` primary keys. Composite keys involving tenant identifiers are **forbidden**.

### DB-04 — Date Partitioning

High-volume tables MUST be partitioned by date (monthly): `responses`, `delivery_log`, `audit_log`, `notification_log`, `event_log`. Specs for these MUST include a partition strategy.

### DB-05 — Migration Atomicity

Every migration MUST be applicable to all tenant schemas atomically; failure for any tenant rolls back across all tenants. Migration specs document rollback behaviour.

### DB-06 — Phase 2 Table Reservation

These tables MUST be in the Phase 1 baseline migration as **empty tables with correct structure**: `ai_recommendations`, `anomaly_events`, `trend_snapshots`, `branching_rules`, `targeting_rules`, `ab_test_configs`, `action_plans`, `action_assignments`, `action_progress`, `webhook_configs`, `connector_configs`.

### DB-07 — Elasticsearch Index Naming

Valid patterns only:

| Pattern                       | Scope         | Owner          | Purpose                                                                                          |
| ----------------------------- | ------------- | -------------- | ------------------------------------------------------------------------------------------------ |
| `tenant_{tenantId}_responses` | Per-tenant    | `M-04`, `M-05` | Survey response pipeline, NLP results                                                            |
| `tenant_{tenantId}_analytics` | Per-tenant    | `M-06`, `M-07` | CX metrics, KPI dashboards, reporting                                                            |
| `platform_billing_analytics`  | Platform-wide | `M-19`         | QBS-internal billing aggregates (single shared index, aggregated non-PII only; only M-19 writes) |

### DB-08 — Data-Access Implementation (EF Core)

All persistence is **EF Core** (Npgsql provider); the **M-10 module is the reference implementation**. Raw ADO.NET (`NpgsqlConnection`/`NpgsqlCommand`, Dapper) and EF raw-SQL escape hatches (`FromSql*`, `ExecuteSql*`, `ExecuteSqlRaw`) are **forbidden in module feature code** (hand-written SQL lives only in baseline/migration scripts and ops tooling).

1. **Two contexts, never crossed.** `TenantDbContext` (per-tenant schema) and `ControlPlaneDbContext` (control-plane DB). One `SaveChanges`/transaction MUST NOT span both databases (DB-02); split the writes and bridge durability with an M-17 event/outbox — never a distributed transaction.
2. **Context abstractions live in Application.** `ITenantDbContext` / `IControlPlaneDbContext` (in `Application/Interfaces/`) expose the `DbSet<>`s, `SaveChangesAsync`, and — tenant only — the transaction boundary `ExecuteAsync(Func<Task>)` / `ExecuteAsync<T>`. Concrete contexts + one `IEntityTypeConfiguration<T>` **per entity** live in Infrastructure. **No separate unit-of-work type** — `ExecuteAsync` is the only multi-write transaction boundary.
3. **Per-aggregate data-access service layer.** Each table/aggregate is fronted by a data-access service (`<Aggregate>Service`, or `<Aggregate>Store` on a name clash) in the Application layer under its domain folder, port in `<Domain>/Interfaces/`, depending only on the context interface. **Business services depend on these ports — that is the unit-test seam** (mock the port; no DB in the unit lane). Write methods self-persist (`SaveChangesAsync`); compose them inside `ITenantDbContext.ExecuteAsync` for atomicity.
4. **SQL baseline owns the schema; EF only maps.** All DDL is in the module's `_Baseline.sql` / `_ControlPlane.sql`, applied by the DB-05 migration mechanism. **EF Core does not generate or apply migrations.** EF maps onto the existing schema with **explicit `HasColumnName` per property** (no naming-convention package) and FK relationships declared in the configs so EF orders dependent inserts.
5. **Time is injected** via `System.TimeProvider` (Unit Test Policy rule 8) — never `DateTime.UtcNow` in tested code.

Full rationale + test alignment: database-constitution **Article 7**.

---

## 7. API Spec Rules

### API-01 — Versioning

All endpoints are versioned (`/api/v1/…`). Unversioned endpoints are not permitted.

### API-02 — Tenant Resolution

Tenant is resolved from the JWT claim (`tenant_id`) or the subdomain. Resolving tenant from a query parameter or body field is **forbidden**.

### API-03 — Permission Declaration

Every endpoint spec MUST declare `required_permission`, `required_scope` (`organisation` | `region` | `branch` | `own`), and `default_personas` (persona IDs from Section 8).

### API-04 — Pagination

All list endpoints MUST use **cursor-based pagination**. Offset-based pagination is not permitted.

### API-05 — Error Response Envelope

All error responses MUST follow:

```json
{
  "error": {
    "code": "string",
    "message": "string",
    "correlation_id": "UUID",
    "tenant_id": "UUID"
  }
}
```

`correlation_id` is the platform-wide trace identifier. `tenant_id` is always present for tenant-scoped requests.

### API-06 — Authentication Headers

JWT bearer → `Authorization: Bearer <token>`; API key → `X-API-Key: <key>`; one-time survey token → `X-Survey-Token: <token>`. No other authentication patterns are permitted.

### API-07 — Webhook Signature

Outbound webhook payloads MUST include `X-Nabadat-Signature` (HMAC-SHA256 of the body). Webhook specs document the signing algorithm.

---

## 8. Persona and Permission Reference

Specs MUST use these persona IDs; do not invent roles.

| ID     | Persona                      | Default API Permission Scope                                                                      |
| ------ | ---------------------------- | ------------------------------------------------------------------------------------------------- |
| `P-01` | CX Program Manager           | Full access; only persona that can create users and modify tenant configuration                   |
| `P-02` | CX Analyst                   | Read/write KPI definitions; read-only analytics at full org scope; no user management             |
| `P-03` | Survey Administrator         | Read/write survey builder, channel & audience management; no analytics admin                      |
| `P-04` | Operational Manager          | Read dashboards scoped to org unit; write case management; no survey building                     |
| `P-05` | Frontline Performer          | Read own cases only; no broader dashboard access                                                  |
| `P-06` | Executive Sponsor            | Read-only org-level dashboards; no write access                                                   |
| `P-07` | Tenant IT Administrator      | User management and SSO configuration only; no CX data                                            |
| `P-08` | Platform Administrator (QBS) | Platform control plane (`M-11`) only; cannot access tenant data schemas; all actions audit-logged |

---

## 9. NLP Service Contract

All specs that call the NLP service (`M-05` and the `M-12` infrastructure shell) MUST use this contract exclusively.

**Endpoint**: `POST /analyze` — request `{ "text": "string", "language": "ar | en | auto" }`; response `{ "sentiment": "positive|neutral|negative", "confidence": float, "themes": [string], "keywords": [string], "detected_language": string, "detected_dialect": "msa|gulf|levantine|egyptian|en|null" }`.

The implementation is environment-specific (on-prem: CAMeLBERT; SaaS: provider NLP) and selected by `NLP_ENDPOINT`. Specs MUST call this contract, never a specific NLP provider directly.

---

## 10. Deployment and Environment Rules

Feature flag names are **fixed**.

| Flag                  | SaaS value                     | On-prem value                    | Effect when `false`                                                                                                                                                                                       |
| --------------------- | ------------------------------ | -------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ENABLE_MULTI_TENANT` | `true`                         | `false`                          | Single-tenant mode; no tenant resolution from subdomain; single schema                                                                                                                                    |
| `ENABLE_BILLING`      | `true`                         | `false`                          | Billing, plan tiers, quota enforcement disabled                                                                                                                                                           |
| `ENABLE_TENANT_MGMT`  | `true`                         | `false`                          | Tenant onboarding UI and lifecycle management disabled                                                                                                                                                    |
| `ENABLE_LOCAL_AUTH`   | `false`                        | `false`                          | Local-password auth disabled; users authenticate via Customer AD. Set `true` only when AD is unavailable; revert once AD is operational. See APIs-constitution.md §3.2 and security-constitution.md §2.1. |
| `NLP_ENDPOINT`        | URL of NLP service             | URL of local CAMeLBERT container | N/A — always required                                                                                                                                                                                     |
| `DEPLOYMENT_REGION`   | the tenant's designated region | `client-local`                   | Drives file-storage and DB provisioning region                                                                                                                                                            |

---

## 11. Platform Tenets

- **T-01 — Multi-Language by Design.** Every spec handling user-facing text supports N languages; Arabic (Gulf, Levantine, Egyptian, MSA) and English required at Phase 1; language from tenant context; RTL is a Phase 1 requirement for Arabic.
- **T-02 — Channel Agnostic.** Survey delivery routes through the `M-02` channel-adapter pattern; engine/pipeline/reporting specs carry zero channel-specific code.
- **T-03 — Configuration over Code.** Any spec requiring a code deployment to change a tenant's KPIs, workflows, notification rules, thresholds, branding, or survey logic is **invalid**.
- **T-04 — Data Residency by Architecture.** No runtime jurisdiction-routing logic; residency is enforced at provisioning; the `jurisdiction` field is read-only after provisioning.
- **T-05 — Tenant Isolation Without Exception.** No code path through which one tenant's request could access another tenant's data, even in error states.
- **T-06 — AI Assists, Humans Decide.** AI outputs are advisory; no auto-execution on AI output alone. No exceptions.
- **T-07 — Industry Flexibility Without Custom Code.** KPI definitions, metric configs, and survey templates are data, not code.
- **T-08 — Forward-Compatible Foundation.** Stable UUID/integer keys, canonical event types (Section 4), Phase 2 reservation strategy (AD-06, DB-06); no pattern requiring a breaking migration to activate Phase 2.

---

## 12. Constitution Self-Enforcement Rules

1. A spec that contradicts any rule here is **invalid** and MUST be rewritten. The constitution takes precedence over individual specs.
2. A question not answered here is flagged for a **constitution amendment**, not silently resolved in the spec.
3. All module IDs, table names, event names, persona IDs, GP IDs, and feature-flag names MUST match this document exactly.
4. This constitution is **append-only** after the first freeze, except where an amendment explicitly removes or renumbers a rule (e.g. AMENDMENT-006). Amendments are numbered and appended.

---

**Version**: `1.12.0` | **Ratified**: `2026-05-06` | **Last Amended**: `2026-06-22`

---

## 13. AMENDMENT-001 — Module Registry: M-19 Billing Operations

Adds M-19 to Phase 1 (owned tables listed in Section 3). All M-19 tables reside in the **global control-plane PostgreSQL database** (not per-tenant schemas); they use explicit `tenant_id` FK columns referencing `tenants.id` — permitted because DB-02 applies only to per-tenant schemas.

## 14. AMENDMENT-002 — Event Catalogue: M-19 Billing Events

Adds the `billing.invoice.*`, `billing.payment.*`, `billing.dunning.*`, and `billing.provider.health_degraded` events (source `M-19`). Cross-module contracts: Feature 1 polls M-17 for `billing.dunning.throttle_requested` (portal read-only + block new resources) and `billing.dunning.suspension_requested` (lifecycle suspension, reason `billing-dunning`); M-09 handles `billing.invoice.issued` (deliver PDF) and `billing.dunning.step_escalated` (deliver dunning notification).

## 15. AMENDMENT-003 — Elasticsearch Index Pattern: Platform Billing Analytics

Extends DB-07 with `platform_billing_analytics` (platform-wide, owner `M-19`): a single shared index of aggregated, non-PII billing metrics; only M-19 may write to it. Per-tenant index rules (AD-02) are unaffected.

## 16. AMENDMENT-004 — Module Registry & Event Catalogue: M-18 Commercial & Metering

Adds M-18 to Phase 1 (owned tables in Section 3) in the **global control-plane database** (tables with `tenant_id` FKs reference `tenants.id`), plus the `plan.*`, `credit.grant.*`, `credit.correction.issued`, `meter.depletion.*`, and `meter.period.closed` events. Cross-module contracts: Feature 1 polls `meter.depletion.suspension` (suspend); M-09 handles `credit.grant.pending` and `meter.depletion.warning`; M-19 polls `meter.period.closed` to trigger invoicing.

## 17. AMENDMENT-005 — Core Governing Principles

Introduces Section 1A (Core Governing Principles). As amended by AMENDMENT-006, the set is **GP-01 – GP-05**, defined inline in Section 1A with their pass conditions. New specs (Feature 006 onward) MUST reference GP-01–GP-05 in their Constitution Check; any spec storing personal data MUST satisfy GP-03.

## 18. AMENDMENT-006 — GP Set Reduction, Audit Ownership, Sync Calls, Residency

1. **GP set reduced and renumbered.** The former GP-02 (network zone segregation), GP-04 (Riyadh-region residency), and GP-05 (per-module-schema isolation) are **removed**. Survivors renumbered: old GP-01→GP-01, GP-03→GP-02, GP-06→GP-03, GP-07→GP-04, GP-08→GP-05 (Section 1A).
2. **Audit ownership.** `audit_log` moves from `M-11` to **`M-17`** (Section 3 registry and Section 5).
3. **Cross-module synchronous calls.** AD-01 amended: synchronous cross-module calls are permitted **through a published interface** (previously only M-17 calls were allowed).
4. **Residency genericised.** Saudi-specific anchors removed: residency is governed per-tenant by `jurisdiction` / `DEPLOYMENT_REGION`; compliance baselines (e.g. NCA ECC) are applied per the tenant's jurisdiction rather than hardcoded.

## 19. AMENDMENT-007 — Data-Access Implementation (EF Core)

Adds **DB-08** (Section 6) and database-constitution **Article 7**: all persistence is EF Core over two Application-layer context interfaces (`ITenantDbContext` / `IControlPlaneDbContext`), fronted by a per-aggregate data-access service layer, with `ITenantDbContext.ExecuteAsync` as the single multi-write transaction boundary (**no unit-of-work type**) and SQL-baseline-owned schema (**no EF migrations**). Ratifies the **M-10** implementation as the reference pattern for every future backend module. The `plan-template` Constitution Check gains a **Backend Data-Access Gate**; `tasks-template` gains a **Backend Data-Access Task Rule** and its Foundational "database schema" task is redefined to mean _baseline DDL + context wiring_, not an EF-migrations framework.

## 20. AMENDMENT-008 — Backend Project Naming: Meaningful Domain Names (not `M{NN}`)

**Effective for every backend module, repo-wide.** Establishes the project/assembly naming scheme that the backend structure review flagged as missing (BR-12 — _"No documented scheme for project/assembly names"_) and supersedes the de-facto `Nabadat.Platform.M{NN}` pattern everywhere.

1. **Meaningful name, not the module ID.** A backend module's class library, root namespace, and test projects MUST use a concise PascalCase domain name in the form `Nabadat.<DomainName>` — derived from the module's canonical Section 3 registry name. The `M{NN}` token MUST NOT appear in any project name, assembly name, root namespace, or type name. _Worked example:_ module **M-10 "User and Role Management"** is named `Nabadat.UserManagement` (project, namespaces, types, and the `Nabadat.UserManagement.UnitTests` / `.IntegrationTests` projects).

2. **Canonical module IDs are unchanged — this governs code-artifact names only.** The module IDs `M-01`…`M-19` remain mandatory and exact (§12.3) in the Section 3 Module Registry, the Section 4 Event Catalogue, API permission declarations (API-03), and all spec/plan/tasks prose. A module is still _identified_ by its `M-NN` ID; it is only _named in code_ by its domain name. Each module records its `<DomainName>` ↔ `M-NN` mapping in its `plan.md` Structure Decision; once chosen the name is stable.

3. **Project family.** For a module with domain name `<DomainName>`:
   - Production library: `src/Nabadat.<DomainName>/Nabadat.<DomainName>.csproj`
   - Unit tests: `tests/Nabadat.<DomainName>.UnitTests/`
   - Integration tests: `tests/Nabadat.<DomainName>.IntegrationTests/`
   - Contract tests (when shipped): `tests/Nabadat.<DomainName>.ContractTests/`
   - The integration fixture is `<DomainName>ApplicationFactory` — this replaces the `M{NN}ApplicationFactory` placeholder used in database-constitution **Article 7.8** and `tasks-template`.

4. **No redundant ID prefix on types or published interfaces.** Types and published interfaces are named by capability inside the domain namespace — e.g. `Nabadat.UserManagement.Application.Events.AuditEventPublisher`, `IUserAuthService` — never `M10Event` / `IM10AuthService`. (Resolves BR-12's redundant-prefix finding for new code.)

5. **Internal layer structure is unchanged.** DB-08 / database-constitution **Article 7** still governs the in-project folder layout (Api / Application / Domain / Infrastructure; context interfaces in `Application/Interfaces/`; per-aggregate data-access services in Application under the domain folder with their port in `<Domain>/Interfaces/`). Only the top-level project/assembly identifier changes.

6. **Reference application.** The platform's first module — **M-10 (User and Role Management)**, the AMENDMENT-007 data-access reference — is named `Nabadat.UserManagement` under this rule (project, root namespace, types, and the `Nabadat.UserManagement.UnitTests` / `.IntegrationTests` projects). It is the worked example of the scheme, not an exception (BR-12 resolved).

## 21. AMENDMENT-009 — Canonical Module Folder Structure

**Effective for every backend module and every feature added to one, repo-wide.** Ratifies the reference module's internal layout as the binding structure so current and future work stays consistent.

1. **The structure is defined in architecture-constitution.md Article 1A.** Every backend module (`Nabadat.<DomainName>`) is organised into the four top-level layer folders **`Api/`, `Application/`, `Domain/`, `Infrastructure/`** with an inward-only dependency direction, plus the fixed sub-folder taxonomy and interface-placement rules in that article. The **M-10 module (`Nabadat.UserManagement`) is the reference implementation**.
2. **Applies to new features in existing modules too** — a feature adds files into the established layer/sub-domain folders; it does not invent a new top-level folder kind. A new sub-domain folder under `Application/` (with its mirror under `Application/<SubDomain>/Interfaces/` and the unit-test project) is the unit of growth.
3. **Enforcement.** The `plan-template` Constitution Check gains a **Backend Module Structure Gate**; `tasks-template` gains a **Module Folder Structure Rule** so generated task file-paths land in the canonical folders. Data-access placement inside `Infrastructure/` continues to be governed by DB-08 / database-constitution **Article 7**; layer-dependency and cross-module-interface rules by architecture-constitution **Articles 1A and 3**.
4. **Deviations require an amendment.** Adding a fifth layer, a technical-kind bucket at the module root, or relocating the fixed interface placements is an architectural change recorded as an amendment — not resolved per-spec (§12.2).

## 22. AMENDMENT-010 — M-16 Event Catalogue & Module Registry (Feature 002)
1. **Event Catalogue (Section 4).** Registers 14 new `M-16`-sourced events (no downstream consumers at Phase 1): `journey.created`, `journey.updated`, `journey.status.changed`, `journey.stage.added`, `journey.stage.removed`, `journey.touchpoint.added`, `journey.touchpoint.removed`, `journey.kpi_bindings.updated`, `journey.scoring_config.updated`, `journey.detection_config.updated`, `journey.version.published`, `persona.created`, `persona.updated`, `persona.status.changed`. The previously registered `journey.score.updated` is retained unchanged.
2. **`survey.response.submitted` downstream correction.** `M-16` is **removed** from the downstream-consumers column of `survey.response.submitted`. M-16 does not subscribe to survey responses; it was listed in error.
3. **Module Registry (Section 3).** M-16's owned-tables entry is corrected from the placeholder list (`journeys`, `touchpoints`, `journey_scores`) to the full set of 13 tenant-schema tables defined in Feature 002 `data-model.md`: `journeys`, `stages`, `touchpoints`, `kpi_bindings`, `scoring_configs`, `personas`, `journey_persona_bindings`, `journey_versions`, `detection_configs`, `detection_threshold_overrides`, `report_contracts`, `kpi_type_definitions`, `journey_scores`.

## 23. AMENDMENT-011 — M-06 Event Source Attribution & Module Registry (Feature 003)

1. **Event Catalogue (Section 4) — `settings.changed` source extension.** The `settings.changed` event row in Section 4 is amended to list **`M-11`, `M-06`** in the Source Module column (previously `M-11` only). `M-06` publishes `settings.changed` when a tenant edits a KPI catalogue / KPI configuration / KPI activation / CXI weights / KPI perspectives. The behavioural semantics of the event are unchanged: payload carries `entity_type` (`kpi` for M-06 emissions; `organization` / `branding` / `language` for M-11 emissions), a per-field `{from, to}` diff, and — for KPI deactivation cascades — a nested `cxi_side_effect` array per Feature 003 `spec.md` FR-026. All downstream consumers continue to read by `event_type` from M-17 without source-filtering.

2. **Module Registry (Section 3) — M-06 owned tables.** M-06's owned-tables entry is corrected from the placeholder list (`kpi_definitions`, `metric_configs`, `metric_values`, `thresholds`) to the actual Feature 003 set: **`kpi_definitions`, `kpi_thresholds`, `kpi_perspectives`, `cxi_weights`**. The renamed `thresholds → kpi_thresholds` reflects the actual table name shipped. The placeholders `metric_configs` and `metric_values` are removed pending the M-06 score-computation engine release, which is out of scope of Feature 003 and will introduce its own owned-tables amendment when it ships.

## 24. AMENDMENT-012 — M-01 Owned Tables & New Events (Feature 004)

1. **Module Registry (Section 3) — M-01 owned tables.** M-01's owned-tables entry is corrected from the placeholder list (`surveys`, `questions`, `question_bank`, `survey_versions`, `survey_templates`) to the actual Feature 004 set (9 tenant-schema tables, per Feature 004 `data-model.md` §2.1–2.9): **`surveys`, `sections`, `questions_sets`, `questions`, `routing_maps`, `themes`, `survey_translations`, `templates`, `template_snapshots`**. `question_bank` is removed from M-01's scope (question-bank / KPI-catalogue concepts are owned by **M-06** per AMENDMENT-011). `survey_versions` is dropped: Q6's destructive Return-to-Draft-to-edit (BR-1.6) means at most one Active period's worth of responses ever exists, so no `version` column/table is needed. There is **no** `question_translations` table — per-question translatable strings live as keys inside `survey_translations` (jsonb), not a separate physical table.

2. **Event Catalogue (Section 4).** Registers **four** new events:

   | Event | Source Module | Downstream Modules |
   |---|---|---|
   | `survey.responses.purged` | `M-04` | `M-05`, `M-06`, `M-07` (each drops derived aggregates for the survey) |
   | `survey.created` | `M-01` | — |
   | `survey.status.changed` | `M-01` | — |
   | `survey.submitted_for_review` | `M-01` | — |

   - `survey.responses.purged` is emitted by **M-04** at the tail of `IResponsePurgeService.PurgeSurveyResponsesAsync(...)`; payload `{ survey_id, purged_response_count, invalidated_session_count, actor_id, correlation_id }`. Introduced to support M-01's BR-1.6 (destructive Return-to-Draft-to-edit, spec.md Q6 Session 2026-07-14).
   - The other three are emitted by **M-01** itself: `survey.created` on `POST /surveys`; `survey.status.changed` on every status transition (Pause/Reactivate/Archive/Unarchive; payload carries `{from, to}`); `survey.submitted_for_review` on Draft → Pending review (feeds M-09's reviewer broadcast, FR-15.2). None have downstream consumers registered at Phase 1 — same pattern as the existing `survey.published` / `survey.archived` rows.

   These four are required by Feature 004's US1/US2 unit, integration and scenario tests (`SurveyLifecycleServiceTests`, `SurveyLifecycleFromDraftToActiveScenarioTests`, `SurveyApprovalWorkflowScenarioTests`, `SurveyLifecycleEndpointTests`) and block tasks T044/T102/T110/T124/T125 from legally emitting them per §12.2 ("a question not answered here is flagged for amendment, not silently resolved in the spec").

3. **Ratification gate.** This amendment is **filed** by Feature 004 task T022; it MUST be **ratified** before BR-1.6's destructive Return-to-Draft path ships to production and before the three M-01-sourced events are emitted. Cross-module coordination for the M-04 `IResponsePurgeService` port and the `survey.responses.purged` emission is tracked in Feature 004's `coordination-log.md` (C-01/C-02/C-06).
