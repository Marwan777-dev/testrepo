using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Domain.Interfaces;

/// <summary>
/// Data-access service for <see cref="JourneyVersion"/> (tenant-schema, EF-backed over
/// <c>ITenantDbContext</c>). Versions are immutable: written once at publish time and never
/// updated. <c>version_number</c> is sequential per journey, starting at 1. The publish insert
/// commits atomically with its <c>journey.version.published</c> event when the caller wraps it in
/// <c>ITenantDbContext.ExecuteAsync</c>.
/// </summary>
public interface IVersionDataService
{
    /// <summary>Loads one published version by its per-journey number; null when absent.</summary>
    Task<JourneyVersion?> GetByVersionNumberAsync(Guid journeyId, int versionNumber, CancellationToken ct = default);

    /// <summary>Cursor-paginated versions for a journey, newest first (API-04).</summary>
    Task<RepositoryPage<JourneyVersion>> ListByJourneyAsync(
        Guid journeyId,
        int pageSize,
        string? pageToken,
        CancellationToken ct = default);

    /// <summary>
    /// Highest <see cref="JourneyVersion.VersionNumber"/> for a journey, or 0 when none
    /// exist yet. The service increments this to compute the next version number.
    /// </summary>
    Task<int> GetMaxVersionNumberAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>Inserts an immutable version snapshot (tracks + saves; flushes within an ambient transaction).</summary>
    Task CreateAsync(JourneyVersion version, CancellationToken ct = default);
}
