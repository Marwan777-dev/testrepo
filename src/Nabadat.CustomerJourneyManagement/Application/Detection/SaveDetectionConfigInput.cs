namespace Nabadat.CustomerJourneyManagement.Application.Detection;

/// <summary>
/// The full, authoritative detection configuration for a journey, fed to
/// <see cref="DetectionConfigService.SaveDetectionConfigAsync"/>
/// (<c>contracts/configuration-api.md §PUT /api/v1/journeys/{id}/detection</c>). The save
/// full-replaces the journey's override set (mirroring the KPI-binding save), so the override
/// lists below are the complete desired state, not a delta.
/// </summary>
/// <param name="PainThreshold">Journey-level pain threshold; score ≤ this = pain point. Required, [0, 100].</param>
/// <param name="HappyThreshold">Journey-level happy threshold; score ≥ this = happy moment. Required, [0, 100], &gt; <paramref name="PainThreshold"/>.</param>
/// <param name="StageOverrides">Per-stage threshold overrides (<see cref="DetectionOverrideInput.ScopeId"/> is a <c>stage_id</c>).</param>
/// <param name="TouchpointOverrides">Per-touchpoint threshold overrides (<see cref="DetectionOverrideInput.ScopeId"/> is a <c>touchpoint_id</c>).</param>
public sealed record SaveDetectionConfigInput(
    decimal PainThreshold,
    decimal HappyThreshold,
    IReadOnlyList<DetectionOverrideInput> StageOverrides,
    IReadOnlyList<DetectionOverrideInput> TouchpointOverrides);

/// <summary>
/// A single per-stage or per-touchpoint threshold override. A <c>null</c> threshold means
/// "inherit from the parent level" — resolved at read time by <see cref="DetectionOverrideResolver"/>
/// (T084), never materialised here.
/// </summary>
/// <param name="ScopeId">The <c>stage_id</c> or <c>touchpoint_id</c> this override targets.</param>
/// <param name="PainThreshold">Override pain threshold, or <c>null</c> to inherit. When set, [0, 100].</param>
/// <param name="HappyThreshold">Override happy threshold, or <c>null</c> to inherit. When set, [0, 100].</param>
public sealed record DetectionOverrideInput(
    Guid ScopeId,
    decimal? PainThreshold,
    decimal? HappyThreshold);
