using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Detection;

/// <summary>
/// Resolves the effective pain/happy detection thresholds for a journey / stage / touchpoint scope
/// using the most-specific-wins rule (research.md §5): <c>touchpoint &gt; stage &gt; journey default</c>.
/// Resolution is a deterministic walk that starts from the journey-level <c>detection_configs</c> pair,
/// applies the stage override (for a touchpoint, its <em>parent</em> stage's override), then the
/// touchpoint override — each field taken as <c>override.Field ?? accumulated-parent</c>, so a more
/// specific scope wins and a <c>null</c> override field inherits the value resolved so far. Overrides
/// are matched by <see cref="DetectionThresholdOverride.ScopeType"/>/<see cref="DetectionThresholdOverride.ScopeId"/>,
/// never by their order in the repository result, so the outcome is independent of override list ordering.
/// Returns <c>null</c> when the journey has no detection config at all.
/// </summary>
public sealed class DetectionOverrideResolver
{
    private const string StageScope = "stage";
    private const string TouchpointScope = "touchpoint";

    private readonly IDetectionDataService _detection;
    private readonly ITouchpointDataService _touchpoints;

    public DetectionOverrideResolver(IDetectionDataService detection, ITouchpointDataService touchpoints)
    {
        _detection = detection;
        _touchpoints = touchpoints;
    }

    /// <summary>
    /// Resolves the effective thresholds for the given scope. <paramref name="scopeType"/> is
    /// <c>"touchpoint"</c>, <c>"stage"</c>, or <c>"journey"</c>; <paramref name="scopeId"/> is the
    /// touchpoint/stage id (ignored for journey scope). Returns <c>null</c> when the journey has no
    /// detection config.
    /// </summary>
    public async Task<EffectiveThresholds?> GetEffectiveThresholdsAsync(
        string scopeType,
        Guid scopeId,
        Guid journeyId,
        CancellationToken ct = default)
    {
        var config = await _detection.GetByJourneyAsync(journeyId, ct);
        if (config is null)
        {
            // No journey-level config → nothing to resolve against (FR: detection is opt-in per journey).
            return null;
        }

        // The journey-level pair is the starting point for the most-specific-wins walk.
        var pain = config.PainThreshold;
        var happy = config.HappyThreshold;

        var isStage = string.Equals(scopeType, StageScope, StringComparison.OrdinalIgnoreCase);
        var isTouchpoint = string.Equals(scopeType, TouchpointScope, StringComparison.OrdinalIgnoreCase);

        // "journey" scope (or any unknown type) stops at the journey-level defaults — no override applies.
        if (isStage || isTouchpoint)
        {
            var overrides = await _detection.ListOverridesAsync(config.DetectionConfigId, ct);

            // For a touchpoint the stage override is keyed by its PARENT stage, so locate that first;
            // for a stage scope the stage is the scope itself.
            var stageId = isTouchpoint
                ? (await _touchpoints.GetByIdAsync(scopeId, ct))?.StageId
                : scopeId;

            // Apply the stage override (a touchpoint inherits its parent stage's resolved values).
            if (stageId is { } resolvedStageId)
            {
                Apply(FindOverride(overrides, StageScope, resolvedStageId), ref pain, ref happy);
            }

            // Then the touchpoint override — the most specific scope, applied last so it wins.
            if (isTouchpoint)
            {
                Apply(FindOverride(overrides, TouchpointScope, scopeId), ref pain, ref happy);
            }
        }

        return new EffectiveThresholds(pain, happy);
    }

    private static DetectionThresholdOverride? FindOverride(
        IReadOnlyList<DetectionThresholdOverride> overrides,
        string scopeType,
        Guid scopeId)
        => overrides.FirstOrDefault(o =>
            string.Equals(o.ScopeType, scopeType, StringComparison.OrdinalIgnoreCase) && o.ScopeId == scopeId);

    /// <summary>
    /// Folds a single override into the accumulated pair: a non-null field replaces the parent value,
    /// a null field leaves the inherited value untouched.
    /// </summary>
    private static void Apply(DetectionThresholdOverride? ovr, ref decimal pain, ref decimal happy)
    {
        if (ovr is null)
        {
            return;
        }

        pain = ovr.PainThreshold ?? pain;
        happy = ovr.HappyThreshold ?? happy;
    }
}
