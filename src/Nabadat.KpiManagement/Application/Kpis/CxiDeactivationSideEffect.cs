namespace Nabadat.KpiManagement.Application.Kpis;

/// <summary>
/// The per-CXI side-effect of deactivating a member KPI (FR-026 / R5): the composite
/// (<see cref="CxiKpiId"/>) lost <see cref="RemovedMemberKpiId"/>, and
/// <see cref="RecomputedEffectivePercentages"/> is the post-removal effective-percentage map over the
/// surviving members (it EXCLUDES the removed member). One of these is emitted per affected CXI inside
/// the deactivation event's nested <c>cxi_side_effect</c> payload.
/// </summary>
public sealed record CxiDeactivationSideEffect(
    Guid CxiKpiId,
    Guid RemovedMemberKpiId,
    IReadOnlyList<CxiMemberEffectivePercentage> RecomputedEffectivePercentages);
