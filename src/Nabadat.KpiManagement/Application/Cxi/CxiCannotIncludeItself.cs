namespace Nabadat.KpiManagement.Application.Cxi;

/// <summary>
/// Thrown when a member-set transition would make the CXI composite a member of itself
/// (<c>candidate == cxi_kpi_id</c>) — the <c>member_kpi_id &lt;&gt; cxi_kpi_id</c> invariant
/// (data-model.md §1.4). Surfaces at the API boundary as 400 <c>CXI_CANNOT_INCLUDE_ITSELF</c>.
/// </summary>
public sealed class CxiCannotIncludeItself : Exception
{
    /// <summary>The CXI composite KPI that was being asked to include itself.</summary>
    public Guid CxiKpiId { get; }

    public CxiCannotIncludeItself(Guid cxiKpiId)
        : base("The CXI composite KPI cannot include itself as a member.") => CxiKpiId = cxiKpiId;
}
