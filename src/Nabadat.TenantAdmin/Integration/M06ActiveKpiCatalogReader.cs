using Nabadat.CustomerJourneyManagement.Application.KpiTypes;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.Domain.ValueObjects;

namespace Nabadat.TenantAdmin.Integration;

/// <summary>
/// Host adapter that completes the M-06 ↔ M-16 KPI integration (Feature 003). It implements M-16's
/// <see cref="IActiveKpiCatalogReader"/> port using M-06's published <see cref="IKpiConfigReader"/>,
/// so the touchpoint KPI catalogue (<c>GET /api/v1/kpi-types</c>), the weight validator's known-type
/// check, and the <c>kpi_bindings.kpi_id</c> link on save are all driven by the tenant's active
/// KPI-Management KPIs.
///
/// <para>Lives in the host because only it references both modules — M-06 references M-16 (for
/// <c>IJourneyBindingQuery</c>), so M-16 cannot reference M-06. Registered in <c>Program.cs</c> as a
/// replacement for M-16's standalone default reader.</para>
///
/// <para>Mapping: each active, <b>non-composite</b> KPI becomes a catalogue entry keyed by its Short
/// Name. Composite KPIs (CXI) are excluded — they are computed from member KPIs, not measured at a
/// touchpoint. <c>IsPlatformStandard</c> follows the KPI's type. M-06 stores a single
/// <c>FullName</c>, so the curated bilingual labels and scoring direction of the known platform
/// standards are reused (matched by Short Name); custom KPIs fall back to their full name and the
/// default <c>Ascending</c> direction.</para>
/// </summary>
public sealed class M06ActiveKpiCatalogReader : IActiveKpiCatalogReader
{
    private static readonly IReadOnlyDictionary<string, PlatformKpiTypeInfo> StandardLabelsByKey =
        KpiTypeService.PlatformStandardCatalog.ToDictionary(type => type.TypeKey, StringComparer.OrdinalIgnoreCase);

    private readonly IKpiConfigReader _kpis;

    public M06ActiveKpiCatalogReader(IKpiConfigReader kpis) => _kpis = kpis;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveKpiCatalogEntry>> GetActiveKpisAsync(CancellationToken ct = default)
    {
        var active = await _kpis.GetActiveAsync(ct);

        return active
            .Where(kpi => !kpi.IsComposite)
            .Select(kpi =>
            {
                var isPlatformStandard = kpi.KpiType == KpiType.Standard;

                if (StandardLabelsByKey.TryGetValue(kpi.ShortName, out var curated))
                {
                    return new ActiveKpiCatalogEntry(
                        kpi.Id, kpi.ShortName, curated.LabelAr, curated.LabelEn, curated.ScoringDirection, isPlatformStandard);
                }

                return new ActiveKpiCatalogEntry(
                    kpi.Id, kpi.ShortName, kpi.FullName, kpi.FullName, "Ascending", isPlatformStandard);
            })
            .ToList();
    }
}
