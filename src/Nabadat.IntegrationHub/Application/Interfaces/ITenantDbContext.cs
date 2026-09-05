using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Nabadat.IntegrationHub.Domain.Entities;

namespace Nabadat.IntegrationHub.Application.Interfaces;

/// <summary>
/// Application-owned abstraction of the per-tenant EF context (implemented by <c>TenantDbContext</c> in
/// Infrastructure — T011). M-13's per-aggregate services depend on this interface, not the concrete
/// context, so they live in the Application layer while the EF context and entity mappings stay in
/// Infrastructure (DB-08 rules 3–4 / AMENDMENT-007, mirroring the M-01 / M-06 reference).
///
/// <para>Exposes the eight M-13-owned tenant-schema <see cref="DbSet{TEntity}"/>s plus the shared M-17
/// <see cref="EventLogs"/>, <see cref="SaveChangesAsync"/>, and <see cref="ExecuteAsync(Func{Task},
/// CancellationToken)"/> — the single multi-write transaction boundary. There is no repository layer
/// and no unit-of-work type: the context <b>is</b> the unit of work.</para>
///
/// <para>The multi-write flows that need that boundary: creating an <see cref="Integration"/> together
/// with its first <see cref="Credential"/>; revoking the current credential and generating its
/// replacement (BR-16); saving a <see cref="ServiceChannel"/> with its
/// <see cref="ChannelParameterAssignment"/> contract rows; the all-or-nothing mapping import and
/// replace-all (VR-F09); and every write that appends its M-17 audit row so the change and its
/// <see cref="EventLog"/> commit or roll back together.</para>
/// </summary>
public interface ITenantDbContext
{
    DbSet<ServiceChannel> ServiceChannels { get; }

    DbSet<Parameter> Parameters { get; }

    DbSet<ChannelParameterAssignment> ChannelParameterAssignments { get; }

    DbSet<ParameterMapping> ParameterMappings { get; }

    DbSet<UnmappedValueOccurrence> UnmappedValueOccurrences { get; }

    DbSet<Integration> Integrations { get; }

    DbSet<Credential> Credentials { get; }

    /// <summary>Append-only, DB-04 monthly-partitioned — insert and read only, never update or delete.</summary>
    DbSet<IntegrationRequestLog> IntegrationRequestLogs { get; }

    /// <summary>M-17's shared <c>event_log</c>; M-13 appends configuration-change rows in the same unit of work as the change.</summary>
    DbSet<EventLog> EventLogs { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside one transaction, then commits — rolling back if it throws.
    /// Services invoked inside persist themselves; because the transaction is open those saves only
    /// flush, and this single commit makes them all atomic. Single-write operations don't need this —
    /// their own save is already atomic.
    /// </summary>
    Task ExecuteAsync(Func<Task> work, CancellationToken ct = default);

    /// <summary>As <see cref="ExecuteAsync(Func{Task}, CancellationToken)"/> but returning the work's result.</summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken ct = default);
}
