namespace Nabadat.KpiManagement.Application.Cxi;

/// <summary>One requested CXI member weighting: the member KPI and its relative integer weight.
/// Input to <see cref="CxiWeightUpdateService.ReplaceAsync"/> (full-replace semantics).</summary>
public sealed record CxiWeightInput(Guid MemberKpiId, int Weight);
