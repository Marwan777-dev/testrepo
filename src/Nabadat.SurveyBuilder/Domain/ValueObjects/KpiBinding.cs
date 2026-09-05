namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// The KPI binding of a KPI question (data-model.md §2.4 — <c>kpi_code</c>, <c>perspective</c>,
/// <c>bound_journey_on</c>, <c>stage_id</c>, <c>touchpoint_id</c>). The stage/touchpoint validity
/// constraints (FR-8.4 / BR-8.2) are computed locally by <see cref="IsValid"/>; cross-module
/// existence of the KPI code / stage / touchpoint is validated separately via
/// <c>IKpiCatalogReader</c> / <c>IJourneyReader</c> at write time.
/// </summary>
/// <param name="KpiCode">The bound KPI's code (M-06 catalogue identifier).</param>
/// <param name="Perspective">Optional KPI perspective (sub-dimension); null when unset.</param>
/// <param name="BoundJourneyOn">Whether journey binding is active for this question.</param>
/// <param name="StageId">Optional journey stage; required before a touchpoint may be set (FR-8.4).</param>
/// <param name="TouchpointId">Optional journey touchpoint (FR-8.4).</param>
public sealed record KpiBinding(
    string KpiCode,
    string? Perspective,
    bool BoundJourneyOn,
    Guid? StageId,
    Guid? TouchpointId)
{
    /// <summary>
    /// Validates the stage/touchpoint shape locally:
    /// <list type="bullet">
    ///   <item>Binding <b>on</b> — a touchpoint requires a stage (FR-8.4); a stage alone (or
    ///   neither) is valid, and a touchpoint without a stage is invalid.</item>
    ///   <item>Binding <b>off</b> — neither stage nor touchpoint may be set (BR-8.2); anything
    ///   else must be stripped before persisting.</item>
    /// </list>
    /// Does not check KPI-code presence or cross-module existence — those are separate concerns.
    /// </summary>
    public static bool IsValid(KpiBinding binding) =>
        binding.BoundJourneyOn
            ? binding.TouchpointId is null || binding.StageId is not null
            : binding.StageId is null && binding.TouchpointId is null;
}
