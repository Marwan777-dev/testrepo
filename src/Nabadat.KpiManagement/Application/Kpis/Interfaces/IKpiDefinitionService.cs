using Nabadat.KpiManagement.Application.Kpis.Dtos;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Kpis.Interfaces;

// KpiTypeFilter lives in the parent Application.Kpis namespace; KpiCataloguePage in its Dtos child.

/// <summary>
/// The single per-entity service for <see cref="KpiDefinition"/> (DB-08 / AMENDMENT-007 — one
/// <c>&lt;Aggregate&gt;Service</c> holding CRUD + business logic over <see cref="Interfaces.ITenantDbContext"/>,
/// NOT a repository and NOT a separate <c>*DataService</c>). This port is the unit-test mock seam
/// (CLAUDE.md / the M-10 reference). Higher-level orchestration (validation, multi-table save,
/// audit emission) composes this service inside <c>KpiSaveService</c>.
/// </summary>
public interface IKpiDefinitionService
{
    /// <summary>Loads a KPI definition by id; null if absent. Tracked = false (read path).</summary>
    Task<KpiDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Loads a KPI definition by Short Name (case-insensitive); null if absent.</summary>
    Task<KpiDefinition?> GetByShortNameAsync(string shortName, CancellationToken ct = default);

    /// <summary>All KPI definitions for the tenant (caller applies filtering/ordering).</summary>
    Task<IReadOnlyList<KpiDefinition>> ListAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns one cursor-paginated page of the catalogue (US-1): applies the type/active/search
    /// filter and canonical ordering via <see cref="KpiCatalogueQuery"/>, then slices by
    /// <paramref name="cursor"/> + <paramref name="limit"/> (research.md R7 / R8).
    /// </summary>
    Task<KpiCataloguePage> ListCatalogueAsync(
        KpiTypeFilter type,
        bool activeOnly,
        string? search,
        string? cursor,
        int limit,
        CancellationToken ct = default);

    /// <summary>True when a KPI with the given Short Name (case-insensitive) exists, optionally excluding one id.</summary>
    Task<bool> ExistsByShortNameAsync(string shortName, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>Inserts a new KPI definition (flushes; commits with the surrounding transaction when wrapped).</summary>
    Task AddAsync(KpiDefinition definition, CancellationToken ct = default);

    /// <summary>Updates an existing KPI definition.</summary>
    Task UpdateAsync(KpiDefinition definition, CancellationToken ct = default);
}
