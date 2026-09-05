using System.Text.Json;
using FluentAssertions;
using Nabadat.CustomerJourneyManagement.Application.Versioning;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Versioning;

/// <summary>
/// Unit tests for <see cref="JourneySnapshotSerializer"/> (T066 / US-3) — the pure-logic component
/// that captures a journey's full configuration tree as a single immutable JSON blob at publish
/// time (<c>research.md §1</c>). Authored FIRST (red→green); they pin the contract the T066
/// implementation must satisfy:
/// <list type="bullet">
///   <item>The serializer takes a domain-entity aggregate — <c>record JourneySnapshotInput(Journey
///   Journey, ScoringConfig? ScoringConfig, DetectionConfig? DetectionConfig,
///   IReadOnlyList&lt;StageSnapshotInput&gt; Stages)</c>, <c>record StageSnapshotInput(Stage Stage,
///   IReadOnlyList&lt;TouchpointSnapshotInput&gt; Touchpoints)</c>, <c>record
///   TouchpointSnapshotInput(Touchpoint Touchpoint, IReadOnlyList&lt;KpiBinding&gt; KpiBindings)</c>
///   — because the snapshot needs touchpoint <c>channels</c>/<c>importance</c> that the leaner
///   <c>JourneyConfigDto</c> does not carry.</item>
///   <item><c>string Serialize(JourneySnapshotInput input)</c> — returns the self-contained JSON
///   payload stored verbatim in <c>journey_versions.snapshot_payload</c>.</item>
/// </list>
/// The snapshot must be a point-in-time <i>deep copy</i>: once serialized, later mutations to the
/// live entities can never change the captured payload — the property that makes a published
/// version an immutable historical record.
/// </summary>
public sealed class JourneySnapshotSerializerTests
{
    private static JourneySnapshotInput BuildInput(string stageName = "Awareness", decimal alpha = 0.700m)
    {
        var journey = new Journey
        {
            JourneyId = Guid.NewGuid(),
            Name = "Customer Onboarding",
            Description = "End-to-end onboarding",
            JourneyType = "Onboarding",
            Status = "Active",
        };
        // Tenant-level scoring parameters (SRS §4.2.9 / §11.7) captured into the version snapshot.
        var scoring = new ScoringConfig
        {
            ScoringConfigId = Guid.NewGuid(),
            Alpha = alpha,
            MotMultiplier = 1.5m,
            NFloor = 100,
            FlagPercentile = 25,
            RollingWindowDays = 30,
        };
        var detection = new DetectionConfig
        {
            DetectionConfigId = Guid.NewGuid(),
            JourneyId = journey.JourneyId,
            PainThreshold = 40m,
            HappyThreshold = 75m,
        };
        var touchpoint = new Touchpoint
        {
            TouchpointId = Guid.NewGuid(),
            Name = "Landing page",
            Channels = ["Web", "App"],
            Importance = "High",
            IsMot = true,
            IsMandatory = false,
        };
        var bindings = new List<KpiBinding>
        {
            new() { KpiBindingId = Guid.NewGuid(), TouchpointId = touchpoint.TouchpointId, KpiType = "NPS", Weight = 60m, IsPlatformStandard = true },
            new() { KpiBindingId = Guid.NewGuid(), TouchpointId = touchpoint.TouchpointId, KpiType = "CSAT", Weight = 40m, IsPlatformStandard = true },
        };
        var stage = new Stage { StageId = Guid.NewGuid(), JourneyId = journey.JourneyId, SequenceNumber = 1, Name = stageName };

        return new JourneySnapshotInput(
            journey,
            scoring,
            detection,
            [new StageSnapshotInput(stage, [new TouchpointSnapshotInput(touchpoint, bindings)])]);
    }

    [Fact]
    public void Serialize_includes_all_stages_touchpoints_kpi_bindings_scoring_and_detection()
    {
        var input = BuildInput();

        var payload = new JourneySnapshotSerializer().Serialize(input);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        // Scoring + detection config are captured at the root.
        TryProp(root, "scoringConfig", out var scoring).Should().BeTrue();
        scoring.ValueKind.Should().Be(JsonValueKind.Object);
        TryProp(root, "detectionConfig", out var detection).Should().BeTrue();
        TryProp(detection, "painThreshold", out _).Should().BeTrue();
        TryProp(detection, "happyThreshold", out _).Should().BeTrue();

        // Stage → touchpoint → kpi-binding tree is captured in full.
        TryProp(root, "stages", out var stages).Should().BeTrue();
        stages.GetArrayLength().Should().Be(1);
        var stage0 = stages[0];
        TryProp(stage0, "touchpoints", out var touchpoints).Should().BeTrue();
        touchpoints.GetArrayLength().Should().Be(1);
        TryProp(touchpoints[0], "kpiBindings", out var kpiBindings).Should().BeTrue();
        kpiBindings.GetArrayLength().Should().Be(2);

        // The KPI types and weights survive into the blob.
        payload.Should().Contain("NPS").And.Contain("CSAT");
    }

    [Fact]
    public void Serialize_captures_a_point_in_time_deep_copy_unaffected_by_later_entity_edits()
    {
        var input = BuildInput(stageName: "Awareness", alpha: 0.700m);

        // Snapshot taken NOW...
        var payload = new JourneySnapshotSerializer().Serialize(input);

        // ...then the live entities are edited afterwards (a journey keeps being authored after a
        // version is published; the tenant scoring parameters can be re-tuned too).
        input.Stages[0].Stage.Name = "MUTATED AFTER SNAPSHOT";
        input.ScoringConfig!.Alpha = 0.123m;

        // The captured payload must still reflect the values at serialize time — never the edits.
        using var doc = JsonDocument.Parse(payload);
        TryProp(doc.RootElement, "stages", out var stages).Should().BeTrue();
        TryProp(stages[0], "name", out var stageName).Should().BeTrue();
        stageName.GetString().Should().Be("Awareness");
        payload.Should().NotContain("MUTATED AFTER SNAPSHOT").And.NotContain("0.123");
    }

    /// <summary>
    /// Case-insensitive object-property lookup so the assertions verify the snapshot's <i>structure</i>
    /// and <i>values</i> without coupling to the serializer's exact naming policy.
    /// </summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
