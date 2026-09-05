namespace Nabadat.KpiManagement.Domain.ValueObjects;

/// <summary>
/// Response scale of a non-composite <see cref="Entities.KpiDefinition"/>
/// (column <c>kpi_definitions.scale</c>, <c>varchar(16)</c>, NULL for composite KPIs).
/// <para>
/// Wire/storage form is the exact PascalCase member name (e.g. <c>"Scale1_5"</c>). Entities
/// model the column as nullable <see langword="string"/> per the M-16 reference; this enum is
/// the type-safe twin used by the normalisation calculator, validators, and the published
/// <c>KpiDefinitionDto</c>. <c>Nps</c> is the −100..+100 NPS scale; <c>Scale0_10</c> is the
/// 0..10 raw NPS likelihood scale.
/// </para>
/// </summary>
public enum Scale
{
    /// <summary>0..10 (e.g. raw NPS likelihood-to-recommend).</summary>
    Scale0_10,

    /// <summary>1..3.</summary>
    Scale1_3,

    /// <summary>1..5.</summary>
    Scale1_5,

    /// <summary>1..7.</summary>
    Scale1_7,

    /// <summary>1..10.</summary>
    Scale1_10,

    /// <summary>1..100.</summary>
    Scale1_100,

    /// <summary>
    /// The −100..+100 NPS score scale. Unlike the linear response scales above this is already a
    /// final score (% promoters − % detractors), so the normalisation calculator passes it through
    /// unchanged. Distinct from <see cref="Scale0_10"/>, which is the 0..10 raw likelihood-to-recommend
    /// response that feeds the NPS computation.
    /// </summary>
    Nps
}
