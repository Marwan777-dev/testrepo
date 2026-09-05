namespace Nabadat.KpiManagement.Domain.ValueObjects;

/// <summary>
/// How a <see cref="Entities.KpiDefinition"/>'s raw responses are reduced to a score
/// (column <c>kpi_definitions.calculation_method</c>, <c>varchar(32)</c>).
/// <para>
/// Wire/storage form is the exact PascalCase member name (e.g. <c>"WeightedAverage"</c>).
/// Entities model the column as <see langword="string"/> per the M-16 reference; this enum
/// is the type-safe twin used by validators and the published <c>KpiDefinitionDto</c>.
/// <c>NPSStandard</c> and <c>WeightedComposite</c> are reserved for the seeded NPS and CXI
/// KPIs respectively (FR — custom KPIs may not select them).
/// </para>
/// </summary>
public enum CalculationMethod
{
    /// <summary>Weighted average of normalised per-response values.</summary>
    WeightedAverage,

    /// <summary>Share of responses in the top-N boxes of the scale (requires <c>top_n_value</c>).</summary>
    TopNBox,

    /// <summary>Net Promoter Score standard (% promoters − % detractors); reserved for the NPS KPI.</summary>
    NPSStandard,

    /// <summary>Weighted composite of other KPIs' scores; reserved for the CXI composite KPI.</summary>
    WeightedComposite,
}
