namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// A KPI assignment on a touchpoint (tenant-schema table <c>kpi_bindings</c>). All bindings
/// on a single touchpoint must sum to 100% weight — enforced at the service layer by the
/// KPI weight validator via a full-replace (DELETE + INSERT) save.
/// </summary>
public sealed class KpiBinding
{
    public Guid KpiBindingId { get; set; }

    /// <summary>Parent touchpoint (FK → <c>touchpoints.touchpoint_id</c> ON DELETE CASCADE).</summary>
    public Guid TouchpointId { get; set; }

    /// <summary>
    /// Platform-standard (<c>NPS</c>, <c>CSAT</c>, <c>CES</c>, <c>FCR</c>,
    /// <c>AgentSatisfaction</c>, <c>VFM</c>) or a tenant-defined type key from
    /// <c>kpi_type_definitions</c>.
    /// </summary>
    public string KpiType { get; set; } = string.Empty;

    /// <summary><c>true</c> for the six platform-standard types; <c>false</c> for tenant-defined types.</summary>
    public bool IsPlatformStandard { get; set; }

    /// <summary>
    /// Logical reference to M-06's <c>kpi_definitions.id</c> (Feature 003 / T020) — NOT an enforced
    /// DB FK (cross-module, separately provisioned). Nullable: existing/legacy bindings may not be
    /// linked yet. Lets M-06's <c>IJourneyBindingQuery</c> count touchpoints/journeys per KPI id.
    /// </summary>
    public Guid? KpiId { get; set; }

    /// <summary>Percentage weight; <c>numeric(5,2)</c>, in range (0, 100]. All bindings per touchpoint sum to 100.</summary>
    public decimal Weight { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
