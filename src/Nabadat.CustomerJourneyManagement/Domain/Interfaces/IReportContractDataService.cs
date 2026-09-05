using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Domain.Interfaces;

/// <summary>
/// Data-access service for <see cref="ReportContract"/> (one row per journey, tenant-schema,
/// EF-backed over <c>ITenantDbContext</c>). The contract payload is rebuilt after any write to
/// <c>stages</c>, <c>touchpoints</c>, <c>kpi_bindings</c>, or <c>detection_configs</c>; the upsert
/// runs inside that caller's <c>ITenantDbContext.ExecuteAsync</c>. M-07 reads it through the
/// published <c>IReportContractReader</c>.
/// </summary>
public interface IReportContractDataService
{
    /// <summary>Loads the report contract (incl. its JSONB payload); null when none exists yet.</summary>
    Task<ReportContract?> GetByJourneyAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>
    /// Loads every report contract whose owning journey is currently <c>Active</c> (tenant-schema),
    /// ordered by journey id for a deterministic result. Backs
    /// <c>IReportContractReader.GetActiveReportContractsAsync</c>; empty when no active journey has a
    /// built contract yet.
    /// </summary>
    Task<IReadOnlyList<ReportContract>> ListByActiveJourneysAsync(CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates the contract (<c>report_contracts</c> is UNIQUE per journey). Runs inside
    /// the caller's <c>ITenantDbContext.ExecuteAsync</c> so the contract is rebuilt in the SAME
    /// unit-of-work as the configuration write that triggered it (FR-015).
    /// </summary>
    Task UpsertAsync(ReportContract contract, CancellationToken ct = default);
}
