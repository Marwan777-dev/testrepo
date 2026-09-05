namespace Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

/// <summary>
/// The six platform-standard KPI types recognised across all tenants. A
/// <see cref="Entities.KpiBinding"/> whose <c>KpiType</c> matches one of these members has
/// <c>IsPlatformStandard = true</c>; any other key is a tenant-defined type resolved against
/// <see cref="Entities.KpiTypeDefinition"/> (<c>kpi_type_definitions</c>).
/// <para>
/// Wire/storage form is the exact member name (the KPI <c>typeKey</c>, e.g. <c>"NPS"</c>,
/// <c>"AgentSatisfaction"</c>). Each standard type carries a default scoring direction
/// (<see cref="ScoringDirection"/>): all are <c>Ascending</c> except <see cref="CES"/>
/// (<c>Descending</c> — lower effort is better).
/// </para>
/// </summary>
public enum PlatformKpiType
{
    /// <summary>Net Promoter Score — <c>Ascending</c> (higher is better).</summary>
    NPS,

    /// <summary>Customer Satisfaction — <c>Ascending</c>.</summary>
    CSAT,

    /// <summary>Customer Effort Score — <c>Descending</c> (lower effort is better).</summary>
    CES,

    /// <summary>First Contact Resolution — <c>Ascending</c>.</summary>
    FCR,

    /// <summary>Agent Satisfaction — <c>Ascending</c>.</summary>
    AgentSatisfaction,

    /// <summary>Value for Money — <c>Ascending</c>.</summary>
    VFM,
}
