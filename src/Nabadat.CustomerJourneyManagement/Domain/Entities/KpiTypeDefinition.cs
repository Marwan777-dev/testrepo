namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// A tenant-defined custom KPI type (tenant-schema table <c>kpi_type_definitions</c>).
/// The six platform-standard types (<c>NPS</c>, <c>CSAT</c>, <c>CES</c>, <c>FCR</c>,
/// <c>AgentSatisfaction</c>, <c>VFM</c>) are built into the platform and NOT stored here.
/// </summary>
public sealed class KpiTypeDefinition
{
    public Guid KpiTypeDefinitionId { get; set; }

    /// <summary>Unique key within the tenant; referenced by <c>kpi_bindings.kpi_type</c>.</summary>
    public string TypeKey { get; set; } = string.Empty;

    /// <summary>Arabic label.</summary>
    public string LabelAr { get; set; } = string.Empty;

    /// <summary>English label.</summary>
    public string LabelEn { get; set; } = string.Empty;

    /// <summary><c>Ascending</c> | <c>Descending</c> — higher-is-better vs lower-is-better scoring.</summary>
    public string ScoringDirection { get; set; } = "Ascending";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
