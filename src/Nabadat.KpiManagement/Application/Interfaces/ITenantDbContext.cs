using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Interfaces;

/// <summary>
/// Application-owned abstraction of the per-tenant EF context (implemented by
/// <c>TenantDbContext</c> in Infrastructure). The M-06 per-entity services depend on this
/// interface — not the concrete context — so they live in the Application layer while the EF
/// context and entity mappings stay in Infrastructure (DB-08 / AMENDMENT-007, mirroring the
/// M-16 reference). Exposes the four M-06 tenant-schema <see cref="DbSet{TEntity}"/>s,
/// <see cref="SaveChangesAsync"/>, and <see cref="ExecuteAsync(Func{Task}, CancellationToken)"/>
/// — the single multi-write transaction boundary (no unit-of-work type) that makes a KPI write
/// and its M-17 audit row atomic (data-model.md §8).
/// </summary>
public interface ITenantDbContext
{
    DbSet<KpiDefinition> KpiDefinitions { get; }

    DbSet<KpiThreshold> KpiThresholds { get; }

    DbSet<KpiPerspective> KpiPerspectives { get; }

    DbSet<CxiWeight> CxiWeights { get; }

    /// <summary>The tenant's singleton Organization settings (US-6, data-model.md §2.1).</summary>
    DbSet<OrganizationSettings> OrganizationSettings { get; }

    /// <summary>M-17's shared <c>event_log</c>; M-06 appends <c>settings.changed</c> audit rows here
    /// within the same unit of work as the KPI/settings write (data-model.md §8).</summary>
    DbSet<EventLog> EventLogs { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside one transaction, then commits — rolling back if it
    /// throws. The per-entity services invoked inside persist themselves; because the
    /// transaction is open those saves only flush, and this single commit makes them all atomic
    /// (a KPI write and its M-17 audit row commit or roll back together). Single-write
    /// operations don't need this — the method's own save is already atomic.
    /// </summary>
    Task ExecuteAsync(Func<Task> work, CancellationToken ct = default);

    Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken ct = default);
}
