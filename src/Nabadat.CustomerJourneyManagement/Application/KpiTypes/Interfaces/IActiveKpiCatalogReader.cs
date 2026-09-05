namespace Nabadat.CustomerJourneyManagement.Application.KpiTypes.Interfaces;

/// <summary>
/// Inbound port: the set of KPIs a touchpoint may bind, the single source feeding
/// <c>GET /api/v1/kpi-types</c>, the weight validator's known-type check, and the
/// <c>kpi_bindings.kpi_id</c> link on save.
///
/// <para><b>Dependency inversion (AD-01).</b> M-06 references M-16 (for <c>IJourneyBindingQuery</c>),
/// so M-16 cannot reference M-06. M-16 owns this port; the host (<c>Nabadat.TenantAdmin</c>, which
/// references both) wires an adapter backed by M-06's <c>IKpiConfigReader</c> so the catalogue shows
/// the tenant's active KPIs from KPI Management. When M-16 runs standalone (e.g. its integration
/// tests), the module's own default reader supplies the platform-standard reference catalogue +
/// <c>kpi_type_definitions</c>, so this port is always resolvable.</para>
/// </summary>
public interface IActiveKpiCatalogReader
{
    /// <summary>Returns the active, bindable KPIs for the current tenant (composite KPIs excluded — they are computed, not measured at a touchpoint).</summary>
    Task<IReadOnlyList<ActiveKpiCatalogEntry>> GetActiveKpisAsync(CancellationToken ct = default);
}
