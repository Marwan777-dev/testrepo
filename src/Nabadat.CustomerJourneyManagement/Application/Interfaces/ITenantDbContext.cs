using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Application.Interfaces;

/// <summary>
/// Application-owned abstraction of the per-tenant EF context (implemented by
/// <c>TenantDbContext</c> in Infrastructure). The M-16 data-access services depend on this
/// interface — not the concrete context — so they live in the Application layer while the
/// EF context and entity mappings stay in Infrastructure. Exposes the tenant-schema
/// <see cref="DbSet{TEntity}"/>s, <see cref="SaveChangesAsync"/>, and
/// <see cref="ExecuteAsync(Func{Task}, CancellationToken)"/> — the single transaction
/// boundary that replaces the old <c>ITransactionRunner</c> unit-of-work abstraction and
/// makes a multi-step operation (a business write and its M-17 audit row) atomic (FR-015).
/// </summary>
public interface ITenantDbContext
{
    DbSet<Journey> Journeys { get; }

    DbSet<Stage> Stages { get; }

    DbSet<Touchpoint> Touchpoints { get; }

    DbSet<KpiBinding> KpiBindings { get; }

    DbSet<KpiTypeDefinition> KpiTypeDefinitions { get; }

    DbSet<ScoringConfig> ScoringConfigs { get; }

    DbSet<Persona> Personas { get; }

    DbSet<JourneyPersonaBinding> JourneyPersonaBindings { get; }

    DbSet<JourneyVersion> JourneyVersions { get; }

    DbSet<DetectionConfig> DetectionConfigs { get; }

    DbSet<DetectionThresholdOverride> DetectionThresholdOverrides { get; }

    DbSet<ReportContract> ReportContracts { get; }

    DbSet<JourneyScore> JourneyScores { get; }

    /// <summary>M-17's <c>event_log</c>; M-16 appends audit rows here within the same unit of work.</summary>
    DbSet<EventLog> EventLogs { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside one transaction, then commits — rolling back if it
    /// throws. The data-access methods invoked inside persist themselves; because the
    /// transaction is open those saves only flush, and this single commit makes them all
    /// atomic (a business write and its M-17 audit row commit or roll back together, FR-015).
    /// Single-write operations don't need this — the method's own save is already atomic.
    /// </summary>
    Task ExecuteAsync(Func<Task> work, CancellationToken ct = default);

    Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken ct = default);
}
