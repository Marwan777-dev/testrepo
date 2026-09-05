namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// The <c>type</c> query-param filter for <c>GET /api/v1/kpis</c> (contracts/kpi-api.md):
/// <c>All</c> (no type filter), <c>Standard</c>, or <c>Custom</c>. Distinct from
/// <see cref="Domain.ValueObjects.KpiType"/> (which has no "All" member) because the catalogue
/// filter needs the unfiltered case.
/// </summary>
public enum KpiTypeFilter
{
    All,
    Standard,
    Custom,
}
