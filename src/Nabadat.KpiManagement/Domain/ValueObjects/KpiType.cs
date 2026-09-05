namespace Nabadat.KpiManagement.Domain.ValueObjects;

/// <summary>
/// Classifies a <see cref="Entities.KpiDefinition"/> as a platform-seeded standard KPI or a
/// tenant-authored custom KPI (column <c>kpi_definitions.kpi_type</c>, <c>varchar(16)</c>).
/// <para>
/// Wire/storage form is the exact PascalCase member name (e.g. <c>"Standard"</c>). Entities
/// model the column as <see langword="string"/> per the M-16 reference; this enum is the
/// type-safe twin used by validators, the catalogue query, and the published
/// <c>KpiDefinitionDto</c> (contracts/published-interfaces.md §1).
/// </para>
/// </summary>
public enum KpiType
{
    /// <summary>One of the eight platform-seeded KPIs; calculation method and scale are locked.</summary>
    Standard,

    /// <summary>Tenant-authored KPI; fully configurable within validation rules.</summary>
    Custom,
}
