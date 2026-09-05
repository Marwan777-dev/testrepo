using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Domain.Interfaces;

/// <summary>
/// Data-access service for <see cref="Journey"/> (tenant-schema). Implemented by the EF-backed
/// <c>JourneyDataService</c> over <c>ITenantDbContext</c>; all reads/writes are scoped to the
/// current tenant schema (DB-02/AD-02 — no <c>tenant_id</c> filter needed). Writes track changes on
/// the shared context and save; when the caller wraps them in <c>ITenantDbContext.ExecuteAsync</c>
/// the row and its M-17 event commit atomically (FR-015) — there is no ambient-transaction
/// parameter anymore (the context IS the unit of work).
/// </summary>
public interface IJourneyDataService
{
    /// <summary>Loads a single journey row by id; null when it does not exist.</summary>
    Task<Journey?> GetByIdAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>
    /// Cursor-paginated journey list (API-04), optionally filtered by lifecycle
    /// <paramref name="status"/>. Pass the previous response's cursor in
    /// <paramref name="pageToken"/>; null starts from the first page.
    /// </summary>
    Task<RepositoryPage<Journey>> ListAsync(
        string? status,
        int pageSize,
        string? pageToken,
        CancellationToken ct = default);

    /// <summary>
    /// True when a non-Archived journey already uses <paramref name="name"/>
    /// (case-insensitive). <paramref name="excludeJourneyId"/> skips the journey being
    /// updated so a journey never conflicts with itself. Backs the case-insensitive
    /// partial unique index (<c>idx_journeys_name_ci</c>).
    /// </summary>
    Task<bool> ExistsActiveByNameAsync(
        string name,
        Guid? excludeJourneyId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns just the journey's <c>updated_at</c> timestamp; null when it does not
    /// exist. Backs the lightweight <c>GET /api/v1/journeys/{id}/updated-at</c> poll.
    /// </summary>
    Task<DateTimeOffset?> GetUpdatedAtAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>Inserts a new journey (tracks + saves; flushes within an ambient transaction).</summary>
    Task CreateAsync(Journey journey, CancellationToken ct = default);

    /// <summary>Updates a mutable journey, incl. status (tracks + saves; flushes within an ambient transaction).</summary>
    Task UpdateAsync(Journey journey, CancellationToken ct = default);
}
