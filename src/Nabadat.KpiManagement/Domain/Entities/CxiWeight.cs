namespace Nabadat.KpiManagement.Domain.Entities;

/// <summary>
/// A weighted membership of one KPI within the CXI composite KPI (tenant-schema table
/// <c>cxi_weights</c>, data-model.md §1.4). 0..N rows — populated only when the CXI KPI has
/// members. The composite key is (<see cref="CxiKpiId"/>, <see cref="MemberKpiId"/>);
/// <see cref="Weight"/> is a positive relative integer (CHECK <c>weight &gt; 0</c>), and a member
/// may not be the CXI itself (CHECK <c>member_kpi_id &lt;&gt; cxi_kpi_id</c>). Effective
/// percentages are derived from the weights at read time, never stored.
/// </summary>
public sealed class CxiWeight
{
    /// <summary>The composite (CXI) KPI (FK → <c>kpi_definitions.id</c>, ON DELETE RESTRICT).</summary>
    public Guid CxiKpiId { get; set; }

    /// <summary>The member KPI contributing to the composite (FK → <c>kpi_definitions.id</c>, ON DELETE RESTRICT).</summary>
    public Guid MemberKpiId { get; set; }

    /// <summary>Relative weight (positive integer); effective % is derived from the member weights.</summary>
    public short Weight { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
