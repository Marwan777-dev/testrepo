# M-16 Customer Journey Mapping — Persistence (EF Core migration)

This module's data access was migrated from **hand-written Npgsql / ADO.NET** to **EF Core over two
application-layer context ports**, mirroring the reference module `Nabadat.UserManagement` (M-10) and
the platform's architecture-constitution Article 1A.

The change is **persistence-only**. Every controller route, application service behaviour, validation
rule, error code, M-17 event, and the cursor-pagination contract is unchanged — verified by the full
existing unit suite (80 tests) and integration suite (31 tests, Testcontainers Postgres) staying green.

---

## What was replaced

| Before (raw Npgsql / ADO.NET)                                   | After (EF Core)                                                                 |
| --------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| `TenantSchemaRepository` base (opened its own `NpgsqlConnection`, hand-rolled `search_path`) | Two **context ports** in `Application/Interfaces/`: `ITenantDbContext`, `IControlPlaneDbContext` |
| One `*Repository` per entity in `Infrastructure/Persistence/` writing literal SQL strings | One **data-access service** per entity in `Application/<SubDomain>/` over `ITenantDbContext` |
| `ITransactionRunner` / `TransactionRunner` unit-of-work passing an ambient `NpgsqlTransaction` into every write | **Single transaction boundary** — `ITenantDbContext.ExecuteAsync(...)`; the `DbContext` *is* the unit of work (DB-08) |
| `M17EventPublisher` writing `event_log` via raw `INSERT` on the caller's `NpgsqlTransaction` | EF `M17EventPublisher` — maps `CustomerJourneyManagementEvent` → `EventLog` entity, tracks it on the shared context, saves |
| `NpgsqlDataReader` row→entity `Map(...)` methods + `INSERT … ON CONFLICT` upserts | EF entity configurations (`Infrastructure/Persistence/Configurations/`) + change-tracking + load-or-add upserts |

The **SQL baseline still owns the schema.** `Migrations/001_customer_journey_baseline.sql` is unchanged and remains
the single source of truth for the 13 tenant tables (+ M-17's `event_log`). EF Core **owns no
migrations** — it only *maps* onto the baseline via `HasColumnName` per property, exactly like M-10.

### Per-entity data-access services

`I<Entity>Repository` → `I<Entity>DataService` (contracts in `Domain/Interfaces/`), each implemented by
an EF service in the matching `Application/<SubDomain>/` folder:

| Entity / table                              | Data-access service (`Application/…`)             |
| ------------------------------------------- | ------------------------------------------------- |
| `journeys`                                  | `Journeys/JourneyDataService`                     |
| `stages`                                    | `Stages/StageDataService`                         |
| `touchpoints` (+ `kpi_bindings` replace)    | `Touchpoints/TouchpointDataService`               |
| `personas` (+ `journey_persona_bindings`)   | `Personas/PersonaDataService`                     |
| `kpi_type_definitions`                      | `KpiTypes/KpiTypeDataService`                     |
| `scoring_configs`                           | `Scoring/ScoringConfigDataService`                |
| `journey_scores`                            | `Scores/JourneyScoreDataService`                  |
| `detection_configs` + `detection_threshold_overrides` | `Detection/DetectionDataService`        |
| `report_contracts`                          | `Reports/ReportContractDataService`               |
| `journey_versions`                          | `Versioning/VersionDataService`                   |
| journey-tree snapshot read                  | `Versioning/JourneySnapshotBuilder`               |
| journey config read (M-06)                  | `Scoring/JourneyConfigReaderService`              |

---

## The two context ports

```
Application/Interfaces/ITenantDbContext.cs        ← the per-tenant data plane (all 13 tables + event_log)
Application/Interfaces/IControlPlaneDbContext.cs  ← the global control plane
Infrastructure/Persistence/TenantDbContext.cs     ← implements ITenantDbContext
Infrastructure/ControlPlane/ControlPlaneDbContext.cs ← implements IControlPlaneDbContext
```

- **`ITenantDbContext`** exposes the tenant-schema `DbSet`s, `SaveChangesAsync`, and the `ExecuteAsync`
  transaction boundary. Data-access services depend on this port (not the concrete context), so they
  live in the Application layer while the EF context + mappings stay in Infrastructure.
- **`IControlPlaneDbContext`** is present for **convention parity with M-10** (two context ports). M-16
  owns **no control-plane tables today** — all M-16 data is tenant-scoped (DB-02/AD-02) — so this
  context maps nothing and issues no queries; it is the seam for any future M-16 control-plane table.
  A control-plane write would be its own `SaveChangesAsync`, never atomic with a tenant write (DB-08).

### Per-tenant schema selection

`TenantDbContext` reuses M-10's `TenantSchemaConnectionInterceptor` (aliased in
`CustomerJourneyManagementServiceCollectionExtensions`), which issues `SET search_path TO "tenant_{slug}"` per connection open
(AD-02 / DB-01) by reading the scoped `ICurrentTenant`. All tenants share one connection string and one
Npgsql pool. In single-tenant mode the slug is empty and the interceptor no-ops onto the host's default
schema. M-16 owns **no** tenant resolution of its own — it composes M-10's.

---

## The single transaction boundary (no more unit-of-work abstraction)

The old `ITransactionRunner.RunAsync((tx, ct) => …)` is gone. The `DbContext` is the unit of work:

```csharp
// A multi-step write (business change + its M-17 audit row) is atomic via one ExecuteAsync.
await _db.ExecuteAsync(async () =>
{
    await _journeys.CreateAsync(journey, ct);                 // tracks + saves (flushes in the tx)
    await _events.PublishAsync(CustomerJourneyManagementEvent.JourneyCreated(...), ct); // tracks the event row + saves
}, ct);                                                       // one COMMIT makes both atomic (FR-015)
```

- `ExecuteAsync` opens one transaction, runs the work, calls `SaveChangesAsync`, and commits — rolling
  back on any exception. Inner `SaveChangesAsync` calls (inside each data-access method) only **flush**
  while the transaction is open; the single commit makes them atomic.
- **Single-write** operations (e.g. persona bind/unbind, a KPI-type insert) don't need `ExecuteAsync` —
  the data-access method's own `SaveChangesAsync` is already atomic.

### Data-access conventions

- **Reads** use `AsNoTracking()` (stateless, like the old repositories).
- **Writes**: `Add` / `Update` / `ExecuteDeleteAsync`, then `SaveChangesAsync`.
- **Upserts** (one-row-per-journey tables: `scoring_configs`, `detection_configs`, `journey_scores`,
  `report_contracts`) are **load-or-add**: find the existing row → copy mutable fields (preserving the
  original id/`created_at`) or `Add` a new one. This reproduces the old `INSERT … ON CONFLICT DO UPDATE`.
- **Full-replace** (`kpi_bindings`, `detection_threshold_overrides`) = `ExecuteDeleteAsync` + `AddRange`,
  inside the caller's `ExecuteAsync` so the touchpoint/config never transiently holds a partial set.
- **`jsonb` columns** (`snapshot_payload`, `contract_payload`, `normalization_params`, `stage_scores`,
  `touchpoint_scores`, `old_value`/`new_value`) map to `string?`/`string` with `HasColumnType("jsonb")`;
  `numeric(5,2)` and `text[]` (`channels`) are mapped explicitly.
- **Keyset pagination** (journeys, journey versions) preserves the opaque Base64 cursor and the
  `(created_at, journey_id)` / `version_number` ordering, expressed as EF `Where`/`OrderBy`.
- **Relationship reads** use `.Any(...)` subqueries (not LINQ `Join`) for reliable Npgsql translation.

---

## Build & test

```powershell
# Per-task gate — compile + unit tests (no Docker):
dotnet test tests/Nabadat.CustomerJourneyManagement.UnitTests        # 80 passed, 1 skipped

# Per-story checkpoint — integration (Testcontainers Postgres; Docker must be running):
dotnet test tests/Nabadat.CustomerJourneyManagement.IntegrationTests # 31 passed, 5 skipped (by-design persona-auth skips)
```

> If the dev server (`Nabadat.TenantAdmin.exe`) is running it locks the build DLLs — stop it first:
> `Get-Process Nabadat.TenantAdmin -ErrorAction SilentlyContinue | Stop-Process -Force`.
