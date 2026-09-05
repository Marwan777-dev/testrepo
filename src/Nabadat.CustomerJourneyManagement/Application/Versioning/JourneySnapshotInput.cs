using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Application.Versioning;

/// <summary>
/// The full domain-entity aggregate a journey version captures at publish time, fed to
/// <see cref="JourneySnapshotSerializer.Serialize"/> (T066 / US-3). It is built from <i>domain
/// entities</i> rather than the leaner <c>JourneyConfigDto</c> because a snapshot must record
/// touchpoint <see cref="Touchpoint.Channels"/>/<see cref="Touchpoint.Importance"/> and the
/// <see cref="DetectionConfig"/> — fields the M-06 config DTO does not carry. The
/// <see cref="IJourneySnapshotBuilder"/> assembles this from the live tenant-schema tree.
/// </summary>
/// <param name="Journey">The journey root row.</param>
/// <param name="ScoringConfig">The tenant's scoring parameters active at publish (SRS §4.2.9, per-tenant), or <c>null</c> when none is saved.</param>
/// <param name="DetectionConfig">The journey's pain/happy thresholds, or <c>null</c> when none is saved.</param>
/// <param name="Stages">The journey's stages in sequence order, each with its touchpoints.</param>
public sealed record JourneySnapshotInput(
    Journey Journey,
    ScoringConfig? ScoringConfig,
    DetectionConfig? DetectionConfig,
    IReadOnlyList<StageSnapshotInput> Stages);

/// <summary>A stage and its touchpoints, as captured in a journey version snapshot.</summary>
/// <param name="Stage">The stage row.</param>
/// <param name="Touchpoints">The stage's touchpoints, each with its KPI bindings.</param>
public sealed record StageSnapshotInput(
    Stage Stage,
    IReadOnlyList<TouchpointSnapshotInput> Touchpoints);

/// <summary>A touchpoint and its KPI bindings, as captured in a journey version snapshot.</summary>
/// <param name="Touchpoint">The touchpoint row (including channels/importance/flags).</param>
/// <param name="KpiBindings">The touchpoint's KPI bindings; empty for an unmeasured touchpoint.</param>
public sealed record TouchpointSnapshotInput(
    Touchpoint Touchpoint,
    IReadOnlyList<KpiBinding> KpiBindings);
