namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// Read projection of one member's contribution within a CXI score snapshot: the member's own
/// <see cref="Score"/> and its <see cref="EffectivePercentage"/> weighting in the composite.
/// Returned inside <see cref="CxiSnapshotDto"/> for M-07 dashboard rendering.
/// </summary>
public record CxiMemberBreakdownDto(
    Guid KpiId,
    string KpiShortName,
    decimal Score,
    decimal EffectivePercentage);
