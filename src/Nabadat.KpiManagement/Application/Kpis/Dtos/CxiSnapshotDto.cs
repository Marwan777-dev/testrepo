namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// CXI score snapshot: the composite score plus its per-member breakdown. Returned by
/// <c>IKpiConfigReader.GetCxiSnapshotAsync</c> for M-07 dashboard rendering; null when CXI is
/// inactive or has fewer than two members.
/// </summary>
public record CxiSnapshotDto(
    decimal CompositeScore,
    IReadOnlyList<CxiMemberBreakdownDto> MemberBreakdown);
