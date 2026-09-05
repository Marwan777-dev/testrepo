using Nabadat.KpiManagement.Application.Kpis.Dtos;

namespace Nabadat.KpiManagement.Application.Kpis.Interfaces;

/// <summary>
/// Published read contract: M-06 → M-01 / M-07 / M-09 (AD-01 / AMENDMENT-006).
/// Read-only access to the KPI catalogue for question authoring (M-01), dashboard rendering
/// (M-07), and alert evaluation (M-09). Consumers depend only on this interface and the DTOs it
/// returns — never on M-06's entities, services, or tables directly.
///
/// <para>It is the published read surface of the KPI aggregate, so the single per-entity service
/// <c>KpiDefinitionService</c> implements it alongside <see cref="IKpiDefinitionService"/> (one
/// service per entity, DB-08) — there is no separate reader class. There are no write methods:
/// M-06's writes go through the internal <c>KpiSaveService</c>, so consumers can read the
/// catalogue but never mutate it.</para>
///
/// <para><b>Skeleton only.</b> The concrete implementation on <c>KpiDefinitionService</c> (reads
/// the four M-06 tables and assembles these DTOs) is delivered by US-2 / US-3 (task T058).</para>
/// </summary>
public interface IKpiConfigReader
{
    /// <summary>Returns all active KPIs for the current tenant, in canonical order.</summary>
    Task<IReadOnlyList<KpiDefinitionDto>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Returns a single KPI's full configuration by id; null if not found.</summary>
    Task<KpiDefinitionDto?> GetByIdAsync(Guid kpiId, CancellationToken ct = default);

    /// <summary>Returns a single KPI's configuration by short name (case-insensitive); null if not found.</summary>
    Task<KpiDefinitionDto?> GetByShortNameAsync(string shortName, CancellationToken ct = default);

    /// <summary>
    /// Returns the CXI score-snapshot member breakdown for M-07 dashboard rendering.
    /// Null when CXI is inactive or has fewer than two members. The <c>CompositeScore</c> is
    /// computed live by M-06 once the score-computation engine ships (out of scope here).
    /// </summary>
    Task<CxiSnapshotDto?> GetCxiSnapshotAsync(CancellationToken ct = default);
}
