namespace Nabadat.KpiManagement.Application.Kpis;

/// <summary>
/// One CXI member's recomputed effective percentage after a sibling member was removed by the
/// deactivation cascade (FR-026). <see cref="EffectivePercentage"/> is the member's share of the
/// composite (1 dp), derived from the surviving relative weights via <c>CxiWeightNormaliser</c>.
/// </summary>
public sealed record CxiMemberEffectivePercentage(Guid MemberKpiId, decimal EffectivePercentage);
