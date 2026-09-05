using FluentAssertions;
using Nabadat.CustomerJourneyManagement.Application.Detection;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Detection;

/// <summary>
/// Unit tests for <see cref="DetectionOverrideResolver"/> (T084 / US-4) — the most-specific-wins
/// threshold resolution (research.md §5: touchpoint &gt; stage &gt; journey default). Authored FIRST
/// (red→green per the Unit Test Policy); they define the contract the T084 implementation must satisfy:
/// <list type="bullet">
///   <item><c>DetectionOverrideResolver(IDetectionDataService, ITouchpointDataService)</c> — the
///   touchpoint repo resolves a touchpoint's parent stage so the stage-level override can be located.</item>
///   <item><c>Task&lt;EffectiveThresholds?&gt; GetEffectiveThresholdsAsync(string scopeType, Guid
///   scopeId, Guid journeyId, CancellationToken ct = default)</c> — <c>scopeType</c> is
///   <c>"touchpoint"</c> | <c>"stage"</c> | <c>"journey"</c>; returns <c>null</c> when the journey has
///   no detection config.</item>
///   <item><c>record EffectiveThresholds(decimal PainThreshold, decimal HappyThreshold)</c> — the
///   resolved (never-null) pain/happy pair.</item>
/// </list>
/// Resolution is a deterministic walk: start from the journey-level <c>detection_configs</c> pair, apply
/// the stage override (for a touchpoint, its parent stage), then the touchpoint override — each field
/// taken as <c>override.Field ?? accumulated-parent</c>, so a more-specific scope wins and a null field
/// inherits the value resolved so far. Because resolution keys off <c>scope_type</c>/<c>scope_id</c> (not
/// the order rows arrive from the repository), the result is independent of override list ordering.
/// </summary>
public sealed class DetectionOverrideResolverTests
{
    private const decimal JourneyPain = 40m;
    private const decimal JourneyHappy = 75m;

    private readonly IDetectionDataService _detection = Substitute.For<IDetectionDataService>();
    private readonly ITouchpointDataService _touchpoints = Substitute.For<ITouchpointDataService>();

    private DetectionOverrideResolver CreateSut() => new(_detection, _touchpoints);

    /// <summary>
    /// Wires a journey-level config (40/75), the supplied overrides, and a touchpoint whose parent is
    /// <paramref name="stageId"/> — so a <c>"touchpoint"</c> resolution can reach the stage override.
    /// </summary>
    private void GivenJourney(
        Guid journeyId, Guid configId, Guid stageId, Guid touchpointId,
        params DetectionThresholdOverride[] overrides)
    {
        _detection.GetByJourneyAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new DetectionConfig
            {
                DetectionConfigId = configId,
                JourneyId = journeyId,
                PainThreshold = JourneyPain,
                HappyThreshold = JourneyHappy,
            });
        _detection.ListOverridesAsync(configId, Arg.Any<CancellationToken>())
            .Returns(overrides.ToList());
        _touchpoints.GetByIdAsync(touchpointId, Arg.Any<CancellationToken>())
            .Returns(new Touchpoint { TouchpointId = touchpointId, StageId = stageId, Name = "Checkout" });
    }

    private static DetectionThresholdOverride StageOverride(Guid stageId, decimal? pain, decimal? happy) =>
        new() { OverrideId = Guid.NewGuid(), ScopeType = "stage", ScopeId = stageId, PainThreshold = pain, HappyThreshold = happy };

    private static DetectionThresholdOverride TouchpointOverride(Guid touchpointId, decimal? pain, decimal? happy) =>
        new() { OverrideId = Guid.NewGuid(), ScopeType = "touchpoint", ScopeId = touchpointId, PainThreshold = pain, HappyThreshold = happy };

    [Fact]
    public async Task GetEffectiveThresholdsAsync_touchpoint_override_wins_over_stage_override()
    {
        var journeyId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var touchpointId = Guid.NewGuid();
        GivenJourney(journeyId, configId, stageId, touchpointId,
            StageOverride(stageId, pain: 35m, happy: 70m),
            TouchpointOverride(touchpointId, pain: 20m, happy: 90m));

        var result = await CreateSut().GetEffectiveThresholdsAsync("touchpoint", touchpointId, journeyId);

        // The touchpoint override is the most specific scope — both its values win over the stage's.
        result!.PainThreshold.Should().Be(20m);
        result.HappyThreshold.Should().Be(90m);
    }

    [Fact]
    public async Task GetEffectiveThresholdsAsync_stage_override_wins_over_journey_default()
    {
        var journeyId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        GivenJourney(journeyId, configId, stageId, touchpointId: Guid.NewGuid(),
            StageOverride(stageId, pain: 35m, happy: 70m));

        var result = await CreateSut().GetEffectiveThresholdsAsync("stage", stageId, journeyId);

        // The stage override replaces the journey-level 40/75 default.
        result!.PainThreshold.Should().Be(35m);
        result.HappyThreshold.Should().Be(70m);
    }

    [Fact]
    public async Task GetEffectiveThresholdsAsync_null_override_fields_inherit_the_resolved_parent_value()
    {
        var journeyId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var touchpointId = Guid.NewGuid();
        // Stage sets pain (35) but leaves happy null → happy inherits the journey 75.
        // Touchpoint sets happy (80) but leaves pain null → pain inherits the stage-resolved 35.
        GivenJourney(journeyId, configId, stageId, touchpointId,
            StageOverride(stageId, pain: 35m, happy: null),
            TouchpointOverride(touchpointId, pain: null, happy: 80m));

        var result = await CreateSut().GetEffectiveThresholdsAsync("touchpoint", touchpointId, journeyId);

        // pain walked journey(40) → stage(35) → touchpoint(inherit 35); happy walked journey(75) →
        // stage(inherit 75) → touchpoint(80).
        result!.PainThreshold.Should().Be(35m);
        result.HappyThreshold.Should().Be(80m);
    }

    [Fact]
    public async Task GetEffectiveThresholdsAsync_resolution_is_deterministic_regardless_of_override_order()
    {
        var journeyId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var touchpointId = Guid.NewGuid();
        // Overrides returned in REVERSED specificity order (touchpoint before stage). A correct resolver
        // keys off scope_type/scope_id, not list position, so the touchpoint must still win.
        GivenJourney(journeyId, configId, stageId, touchpointId,
            TouchpointOverride(touchpointId, pain: 20m, happy: 90m),
            StageOverride(stageId, pain: 35m, happy: 70m));

        var result = await CreateSut().GetEffectiveThresholdsAsync("touchpoint", touchpointId, journeyId);

        result!.PainThreshold.Should().Be(20m);
        result.HappyThreshold.Should().Be(90m);
    }
}
