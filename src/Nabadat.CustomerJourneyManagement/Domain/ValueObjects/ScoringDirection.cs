namespace Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

/// <summary>
/// Whether a KPI scores higher-is-better or lower-is-better (column
/// <c>kpi_type_definitions.scoring_direction</c>, <c>varchar(16)</c>, default <c>Ascending</c>).
/// <para>
/// Wire/storage form is the exact PascalCase member name; the entity models the column as
/// <see langword="string"/> (T008) and converts at the service boundary, where the
/// published-interface twin <see cref="Nabadat.Platform.Contracts.M16.ScoringDirection"/>
/// carries the same members for M-06 consumption.
/// </para>
/// </summary>
public enum ScoringDirection
{
    /// <summary>Higher value = better performance (e.g. NPS, CSAT) (<c>Ascending</c>).</summary>
    Ascending,

    /// <summary>Lower value = better performance (e.g. CES) (<c>Descending</c>).</summary>
    Descending,
}
