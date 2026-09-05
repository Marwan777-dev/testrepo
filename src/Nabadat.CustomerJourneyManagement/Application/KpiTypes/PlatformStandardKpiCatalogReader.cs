using Nabadat.CustomerJourneyManagement.Application.KpiTypes.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.KpiTypes;

/// <summary>
/// The standalone default <see cref="IActiveKpiCatalogReader"/>: M-16's own bindable-KPI catalogue —
/// the six platform-standard reference types (<see cref="KpiTypeService.PlatformStandardCatalog"/>)
/// plus the tenant's <c>kpi_type_definitions</c>. It carries no M-06 <c>kpi_id</c> (those entries map
/// to no KPI-Management row), so bindings saved against it leave <c>kpi_bindings.kpi_id</c> blank —
/// exactly the behaviour before the M-06 integration.
///
/// <para>In the deployed host this is replaced by the M-06-backed adapter (which returns the tenant's
/// active KPI-Management KPIs with their ids); this default keeps the port resolvable wherever M-16
/// runs without M-06 (its integration tests), so those tests are unaffected.</para>
/// </summary>
public sealed class PlatformStandardKpiCatalogReader : IActiveKpiCatalogReader
{
    private readonly IKpiTypeDataService _kpiTypes;

    public PlatformStandardKpiCatalogReader(IKpiTypeDataService kpiTypes) => _kpiTypes = kpiTypes;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveKpiCatalogEntry>> GetActiveKpisAsync(CancellationToken ct = default)
    {
        var tenantDefined = await _kpiTypes.ListAsync(ct);

        var entries = new List<ActiveKpiCatalogEntry>(KpiTypeService.PlatformStandardCatalog.Count + tenantDefined.Count);

        entries.AddRange(KpiTypeService.PlatformStandardCatalog.Select(type => new ActiveKpiCatalogEntry(
            KpiId: null,
            Key: type.TypeKey,
            LabelAr: type.LabelAr,
            LabelEn: type.LabelEn,
            ScoringDirection: type.ScoringDirection,
            IsPlatformStandard: true)));

        entries.AddRange(tenantDefined.Select(definition => new ActiveKpiCatalogEntry(
            KpiId: null,
            Key: definition.TypeKey,
            LabelAr: definition.LabelAr,
            LabelEn: definition.LabelEn,
            ScoringDirection: definition.ScoringDirection,
            IsPlatformStandard: false)));

        return entries;
    }
}
