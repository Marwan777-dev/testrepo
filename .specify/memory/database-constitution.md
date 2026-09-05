# Nabadat Platform — Database Constitution

**Project:** Nabadat — multi-tenant Voice-of-Customer / CX SaaS platform.
**Source:** HLD Chapter 3 "Data Storage and Database Design", reconciled to `constitution.md` (router, v1.6.0) — schema-per-tenant model, M-17 event/audit ownership, canonical table names.
**Status:** Governing principles. Every schema migration and data-access implementation MUST uphold these rules. A change that contradicts an article requires an explicit, recorded amendment.

---

## Article 1 — Core Storage Principles

1. **PostgreSQL is the single source of truth.** PostgreSQL is the system of record for all operational data, configuration, audit events, and the event log. No other store is authoritative. (Verified by **GP-01**.)
2. **Three stores, three roles** — never substituted for one another:
   - **PostgreSQL 16+** — operational data, ACID transactions (authoritative, Zone 3).
   - **Elasticsearch 8+** (OpenSearch on-premises) — derived analytics, dashboards, full-text search (non-authoritative, Zone 3, read-side only).
   - **Shared file storage** — binary attachments (authoritative for binaries, Zone 2).
3. **Elasticsearch is always rebuildable.** It holds only data derived from PostgreSQL and MUST be reconstructable from PostgreSQL at any time; a total ES loss is a recoverable latency event, never data loss.
4. **Binaries never live in the relational database.** Attachment bytes live on the file system; PostgreSQL holds only the reference (path, content type, size, AV-scan status).
5. **Data stays in the tenant's designated jurisdiction.** All stores, backups, and replicas run in the region designated for the tenant (`DEPLOYMENT_REGION` / the tenant's `jurisdiction`). No copy of a tenant's data is held outside its designated jurisdiction. Residency is enforced at the infrastructure/provisioning layer, not by runtime routing code. (See router T-04.)
6. **Encryption at rest is universal.** Every store encrypts at rest with AES-256 at the storage layer. Selected high-sensitivity fields and all attachments receive an additional layer of application-layer envelope encryption under the customer-owned CMK in the cloud-provider KMS. No key material is held at rest inside Nabadat. (Verified by **GP-02**.)

---

## Article 2 — Tenant Isolation and Module Ownership

1. **Schema-per-tenant is the only isolation boundary.** Every tenant has a dedicated PostgreSQL schema named `tenant_{slug}` (e.g. `tenant_acme`). There are **no** `tenant_id` columns in tenant tables, **no** row-level-security policies, and **no** shared query paths between tenants. Connection pools are per-tenant and pre-configured to the correct schema. (Router AD-02; verified by **GP-04**.)
2. **Modules own their tables in code, not by schema.** There are no per-module database schemas. Each module owns a defined set of tables (the owned-table lists in `constitution.md` Section 3) and writes only to those. A module MUST NOT read or write another module's tables directly.
3. **Cross-module data access is sanctioned three ways only:** a synchronous call through the owning module's **published interface**; consuming a domain event from **M-17**; or reading an **Elasticsearch** index refreshed from the owning module's data. Module isolation is enforced in **code and code review** — not by schema grants.
4. **Global control-plane data is separate.** Control-plane / cross-tenant tables (e.g. M-18 Commercial & Metering, M-19 Billing Operations) live in the **global control-plane database**, not in any `tenant_{slug}` schema. These tables may use explicit `tenant_id` FK columns referencing `tenants.id`; the no-`tenant_id` rule applies only inside per-tenant schemas.

---

## Article 3 — PostgreSQL: System of Record

### 3.1 Topology and High Availability
- A **primary** with a **hot streaming replica**, both in Zone 3 on PostgreSQL 16+.
- Database servers are reachable only from the application layer (Zone 2). No path from the edge or external systems.
- Default HA posture is supervised (manual) failover per runbook, to avoid split-brain. Automatic failover is supported and enabled per deployment if required.

### 3.2 Replication
- The primary streams WAL to the standby continuously on port 5432.
- **Asynchronous replication is the default** (sub-second lag). Synchronous mode is a documented operational change, not the default.
- Replication lag is monitored; lag above threshold raises an operator alert.

### 3.3 Connection Pooling
- Application servers never connect directly to PostgreSQL. **PgBouncer** runs as a sidecar in **transaction-pooling mode**; pools are **per-tenant**, pre-configured to the tenant schema.
- Application code avoids features incompatible with transaction pooling (session temp tables, cross-statement advisory locks, certain prepared-statement configs).

### 3.4 Schema and Table Organization
- **One schema per tenant** (`tenant_{slug}`); no per-module schemas. Tables inside the tenant schema are owned by modules in code per the canonical owned-table lists. Cross-cutting tables `event_log` and `audit_log` are owned by **M-17**.
- **Primary keys:** every table uses a **UUID or integer** primary key. Composite keys involving a tenant identifier are forbidden (tenancy is the schema, not a column).
- **Date partitioning (monthly)** is required for high-volume tables: `responses`, `delivery_log`, `audit_log`, `notification_log`, `event_log`. Partitions are pre-created by a scheduled job; retention is enforced by detaching old partitions.
- **Indexing follows access patterns:** primary keys, intra-table foreign keys, and dashboard/report columns are indexed. Full-text and large-scale aggregation are served by Elasticsearch, never by PostgreSQL.
- **Configuration is versioned:** configuration changes create a new version row (`version`, `valid_from`, `valid_to`, `actor`, `change_reference`); prior versions are retained so historical data is interpretable against the configuration then in force.
- **Append-only tables:** `audit_log`, `event_log`, and case state/assignment history. No `UPDATE`/`DELETE` in normal operation; corrections are appended as new rows referencing the prior row.

---

## Article 4 — Data Model Principles

1. **Foreign keys within a module's tables; identifiers across modules.** Referential integrity uses FKs only among a module's own tables. Cross-module references use the target's identifier, never a FK, so one module's data lifecycle is not coupled to another's. Cross-module integrity is maintained by the application layer and the event log.
2. **Version where history matters.** Configuration is always versioned. Operational data is versioned where history has analytical or regulatory value (survey versions, case history); other data is mutable in place.
3. **Identifiers and time.** Primary keys are UUID or integer (no tenant-composite keys). Every record carries `created_at` / `updated_at` in **UTC**; presentation applies the user's time zone (configurable; `Asia/Riyadh` is an overridable default).
4. **Soft delete vs. hard delete, per entity:**
   - **Soft delete** where the entity must remain referenced historically (deactivated users, archived surveys) — row retained, flag set.
   - **Hard delete** where data must not remain readable — PDPL right-to-erasure (contact record and channel identifiers removed; historical responses retained but anonymized by breaking linkage) and operational secrets. Every deletion is audited without retaining the deleted data. (Verified by **GP-03**.)
5. **Open-text and anonymization.** Open-text response content lives with the response (`responses`); NLP-derived data lives in its own tables (`sentiment_results`, `themes`, `keywords`) keyed by identifier. On erasure the response is retained and unlinked from the contact. Fully automatic open-text anonymization at scale is not provided; an optional manual review-and-redact step covers residual cases.
6. **Columns by default, JSON where justified.** Structured columns are the default; `jsonb` is used narrowly (e.g. event payloads), validated at the application layer at publish time.

---

## Article 5 — Elasticsearch: Analytics and Search

1. **Derived and non-authoritative.** Every document derives from a committed PostgreSQL row; ES never holds data absent from PostgreSQL. PostgreSQL is never queried for dashboard/reporting use cases (router AD-04).
2. **Per-tenant indices, fixed naming.** Only `tenant_{tenantId}_responses` and `tenant_{tenantId}_analytics` are valid per-tenant patterns (plus the platform-wide `platform_billing_analytics` owned by M-19). No other naming is permitted.
3. **Eventual consistency is accepted.** Dashboards reflect state within a few seconds (refresh target typically under 3s). Guarantees: no phantom data, no silent loss (events replay after a worker crash), idempotent indexing by entity identifier.
4. **Authorization is enforced by the application, not Elasticsearch.** Every query is built in `nabadat-api` with the user's permission and data scope applied as filter clauses before it leaves the API layer. ES has no per-user authorization and is reachable only from the application tier over **HTTPS on port 9200**. (Verified by **GP-04**.)

---

## Article 6 — File Storage

1. **Antivirus before persistence.** Every upload is quarantined and scanned (ClamAV, Zone 2) before any database reference is created. Clean → persisted; infected → deleted and rejected with a security event; scan-failed → quarantined, rejected as retryable, operator alerted.
2. **Type and size enforcement at the API boundary.** Defaults: permitted types `pdf, doc, docx, xls, xlsx, png, jpg, jpeg`; max 10 MB per attachment; max 50 MB and 5 attachments per response/case. Validated by both extension and magic-byte sniffing. Over-limit uploads are rejected before any write.
3. **Two encryption layers.** Storage-layer AES-256 plus application-layer envelope encryption under the customer CMK; the file on disk is ciphertext. The customer can disable/rotate the CMK at any time. (Verified by **GP-02**.)

---

## Article 7 — Data-Access Implementation (EF Core)

The **M-10 module (project `Nabadat.UserManagement`) is the reference implementation**; every future backend module follows it. Router rule: `constitution.md` **DB-08**. (Added by AMENDMENT-007.) The folders these data-access types live in (`Application/Interfaces/`, `Infrastructure/Persistence/` + `Configurations/`, `Infrastructure/ControlPlane/`, `Infrastructure/Migrations/`, per-aggregate services under `Application/<SubDomain>/`) are fixed by architecture-constitution **Article 1A** (Canonical Module Folder Structure) and router **AMENDMENT-009**.

1. **EF Core only — no raw SQL in feature code.** All reads and writes go through EF Core (Npgsql provider). `NpgsqlConnection`/`NpgsqlCommand`, Dapper, and the EF raw-SQL escape hatches (`FromSql*`, `ExecuteSqlRaw*`, `Database.ExecuteSqlAsync`) MUST NOT appear in module feature code. Hand-written SQL is permitted only inside baseline/migration scripts and one-off operational tooling.

2. **Two DbContexts; cross-database atomicity is forbidden.** `TenantDbContext` binds to the resolved `tenant_{slug}` schema; `ControlPlaneDbContext` binds to the global control-plane database. A single transaction or `SaveChanges` MUST NOT span both (Article 2.4 / DB-02). When a flow must write to both, split into two saves and bridge durability with an M-17 event / outbox row — never a distributed transaction. *(Incident of record: a tenant-context transaction wrapping a control-plane write failed with PostgreSQL `42P01`.)*

3. **Context interfaces live in the Application layer.** `ITenantDbContext` and `IControlPlaneDbContext` (`src/<Module>/Application/Interfaces/`) expose the `DbSet<>`s, `SaveChangesAsync`, and — on the tenant context only — the transaction boundary `ExecuteAsync(Func<Task> work)` / `ExecuteAsync<T>(Func<Task<T>> work)`. The concrete `TenantDbContext` / `ControlPlaneDbContext` and the `IEntityTypeConfiguration<T>` classes live in `Infrastructure/`. Application and Domain code reference only the interfaces; the one accepted coupling is that the interface surfaces EF's `DbSet<T>` (the *IApplicationDbContext* pattern), nothing deeper.

4. **One transaction boundary; no unit-of-work type.** Multi-write atomicity is `ITenantDbContext.ExecuteAsync(...)`: it opens one transaction, runs the delegate, calls `SaveChangesAsync`, commits, and rolls back on any throw. There is deliberately **no** `IUnitOfWork`/`ITenantUnitOfWork` abstraction — the context interface *is* the unit of work. Data-access write methods call `SaveChangesAsync` themselves ("self-persist"); intermediate saves inside an `ExecuteAsync` merely flush, and the surrounding transaction governs the final commit/rollback.

5. **Per-aggregate data-access service layer.** Each table/aggregate is fronted by a data-access service — `<Aggregate>Service`, or `<Aggregate>Store` where the name would collide with a business service — placed in the Application layer under the owning domain folder, with its port interface in `<Domain>/Interfaces/`. It depends only on the context interface. **Application/business services depend on these data-access ports, not on the context directly** — this is the unit-test seam: unit tests substitute the port (and, where a transaction is asserted, a fake/recording `ITenantDbContext` whose `ExecuteAsync` runs the delegate). No Testcontainers in the unit lane.

6. **SQL baseline owns the schema; EF maps onto it.** The module's `_Baseline.sql` (tenant) and `_ControlPlane.sql` (control-plane) own every table, column, index, and constraint, applied by the DB-05 migration mechanism (and by Testcontainers in the integration lane). **EF Core does not generate or apply migrations.** Mapping rules: one `IEntityTypeConfiguration<T>` per entity; an explicit `HasColumnName(...)` for every property (no naming-convention/snake-case package); FK relationships declared in the configs so EF orders dependent inserts; array / `jsonb` value converters in a shared converters file.

7. **Time is injected.** Data-access and business code that needs the clock takes `System.TimeProvider` by constructor injection (`GetUtcNow()`), never `DateTime.UtcNow` (Unit Test Policy rule 8; tests inject `FakeTimeProvider`).

8. **Test alignment.** Unit tests mock the data-access port interfaces and, when a transaction is asserted, the context interface; integration tests exercise the real stack through the module's `tests/Nabadat.<DomainName>.IntegrationTests/Infrastructure/<DomainName>ApplicationFactory.cs` (Testcontainers Postgres + `_Baseline.sql` + in-process `WebApplicationFactory`). The project is named with a meaningful domain name per `constitution.md` **AMENDMENT-008** (`Nabadat.<DomainName>`, not `Nabadat.Platform.M{NN}`; e.g. the M-10 reference module is `Nabadat.UserManagement.IntegrationTests` / `UserManagementApplicationFactory`). See CLAUDE.md "Unit Test Policy".

---

## Article 8 — Governance and Amendment

Concrete defaults (pool sizes, retention windows, file limits, partition cadence) are tunable per deployment without amending the principles above. Any change that violates an article — making ES authoritative, a `tenant_id` column inside a tenant schema, row-level security, a cross-module direct table read, storing data outside the tenant's jurisdiction, or removing an encryption layer — requires an explicit, recorded amendment.
