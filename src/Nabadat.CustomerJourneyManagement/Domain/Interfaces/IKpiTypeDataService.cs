using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Domain.Interfaces;

/// <summary>
/// Data-access service for tenant-defined <see cref="KpiTypeDefinition"/> rows (tenant-schema,
/// EF-backed over <c>ITenantDbContext</c>). The six platform-standard KPI types (NPS, CSAT, CES,
/// FCR, AgentSatisfaction, VFM) are built into the platform and NOT stored here. Used by the KPI
/// weight validator to resolve non-standard <c>kpi_type</c> values.
/// </summary>
public interface IKpiTypeDataService
{
    /// <summary>Loads a tenant-defined KPI type by its unique key; null when undefined.</summary>
    Task<KpiTypeDefinition?> GetByKeyAsync(string typeKey, CancellationToken ct = default);

    /// <summary>
    /// True when a tenant-defined KPI type already uses <paramref name="typeKey"/>; backs
    /// the <c>kpi_type.key_conflict</c> 409 on create.
    /// </summary>
    Task<bool> ExistsByKeyAsync(string typeKey, CancellationToken ct = default);

    /// <summary>All tenant-defined KPI types for the current tenant.</summary>
    Task<IReadOnlyList<KpiTypeDefinition>> ListAsync(CancellationToken ct = default);

    /// <summary>Inserts a new tenant-defined KPI type (tracks + saves).</summary>
    Task CreateAsync(KpiTypeDefinition definition, CancellationToken ct = default);
}
