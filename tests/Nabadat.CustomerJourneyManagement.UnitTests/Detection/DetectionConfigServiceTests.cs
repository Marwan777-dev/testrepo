using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.Platform.Contracts.M16;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Detection;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Reports;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Detection;

/// <summary>
/// Unit tests for <see cref="DetectionConfigService"/> (T085 / US-4) — the journey-level pain/happy
/// detection save (<c>contracts/configuration-api.md §PUT /api/v1/journeys/{id}/detection</c>).
/// Authored FIRST (red→green per the Unit Test Policy); they define the contract the T085
/// implementation must satisfy:
/// <list type="bullet">
///   <item><c>record SaveDetectionConfigInput(decimal PainThreshold, decimal HappyThreshold,
///   IReadOnlyList&lt;DetectionOverrideInput&gt; StageOverrides,
///   IReadOnlyList&lt;DetectionOverrideInput&gt; TouchpointOverrides)</c> — the journey-level
///   thresholds plus the full, authoritative override set (the save full-replaces overrides, mirroring
///   the KPI-binding save).</item>
///   <item><c>record DetectionOverrideInput(Guid ScopeId, decimal? PainThreshold,
///   decimal? HappyThreshold)</c> — <c>ScopeId</c> is a <c>stage_id</c> (StageOverrides) or
///   <c>touchpoint_id</c> (TouchpointOverrides); a null threshold means "inherit from the parent
///   level" (resolved later by <c>DetectionOverrideResolver</c>, T084).</item>
///   <item><c>DetectionConfigService(IDetectionDataService, IStageDataService, ITouchpointDataService,
///   ITransactionRunner, IM17EventPublisher, ReportContractService, TimeProvider)</c>.</item>
///   <item><c>Task&lt;ServiceResult&lt;SaveDetectionConfigResult&gt;&gt; SaveDetectionConfigAsync(Guid
///   journeyId, SaveDetectionConfigInput input, ActorContext actor, CancellationToken ct = default)</c>
///   — validates the thresholds and override scopes BEFORE any write, then upserts the journey-level
///   config, full-replaces its overrides, publishes <c>journey.detection_config.updated</c>, and
///   rebuilds the report contract — all in the SAME transaction (FR-015).</item>
///   <item><c>record SaveDetectionConfigResult(DetectionConfig Config, int StageOverrideCount,
///   int TouchpointOverrideCount)</c> — backs the PUT 200 body's <c>stageOverrideCount</c> /
///   <c>touchpointOverrideCount</c>.</item>
/// </list>
/// The overrides FK to the journey's single detection config, so the service loads-or-creates the
/// config id and reuses it for the override rows (asserted in the happy path). The negative-case
/// inputs are deliberately chosen to trip exactly one rule regardless of validation ordering. The
/// report-contract rebuild runs through the real (T087) <see cref="ReportContractService"/> — it is
/// <c>sealed</c>, not substitutable — wired here with NSubstitute dependencies whose journey-config
/// read returns null, so it no-ops (no contract built, no upsert) and is not asserted on here; its
/// own behaviour is covered by <c>ReportContractServiceTests</c>. The fake transaction runner invokes
/// the unit-of-work with a <c>null</c> transaction; the repository and event publisher are NSubstitute
/// mocks that only record the <c>ITenantDbContext.ExecuteAsync</c> argument, so persistence and event
/// publication run end-to-end without a database. The genuine atomic commit/rollback is proven by the
/// integration suite (T091/T092).
/// </summary>
public sealed class DetectionConfigServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly ActorContext Actor = new(
        UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Persona: "P-01",
        CorrelationId: Guid.Parse("66666666-6666-6666-6666-666666666666"));

    private readonly IDetectionDataService _detection = Substitute.For<IDetectionDataService>();
    private readonly IStageDataService _stages = Substitute.For<IStageDataService>();
    private readonly ITouchpointDataService _touchpoints = Substitute.For<ITouchpointDataService>();
    private readonly IM17EventPublisher _events = Substitute.For<IM17EventPublisher>();
    private readonly FakeTimeProvider _time = new(Now);

    private DetectionConfigService CreateSut() => new(
        _detection,
        _stages,
        _touchpoints,
        TestSupport.FakeTenantDb.Immediate(),
        _events,
        // Real (sealed) T087 service wired with substitute deps whose journey-config read returns
        // null ⇒ RebuildContractAsync no-ops, so these cases isolate DetectionConfigService.
        new ReportContractService(
            Substitute.For<IJourneyConfigReader>(),
            Substitute.For<IReportContractDataService>(),
            Substitute.For<IDetectionDataService>(),
            _time),
        _time);

    /// <summary>Registers a single stage belonging to <paramref name="journeyId"/> and returns its id.</summary>
    private Guid GivenStageInJourney(Guid journeyId)
    {
        var stageId = Guid.NewGuid();
        _stages.ListByJourneyAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new List<Stage>
            {
                new() { StageId = stageId, JourneyId = journeyId, SequenceNumber = 1, Name = "Awareness" },
            });
        return stageId;
    }

    [Fact]
    public async Task SaveDetectionConfigAsync_persists_config_and_stage_overrides_and_publishes_event_when_input_is_valid()
    {
        var journeyId = Guid.NewGuid();
        var stageId = GivenStageInJourney(journeyId);
        // No prior config: the service mints a fresh detection_config_id and links the overrides to it.
        _detection.GetByJourneyAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns((DetectionConfig?)null);

        // Capture the id assigned to the upserted config and the parent id passed for the overrides,
        // so we can prove the override rows FK to the same config (the load-or-create-id contract).
        var configId = Guid.Empty;
        var overrideParentId = Guid.Empty;
        _detection
            .When(d => d.UpsertConfigAsync(Arg.Any<DetectionConfig>(), Arg.Any<CancellationToken>()))
            .Do(ci => configId = ci.Arg<DetectionConfig>().DetectionConfigId);
        _detection
            .When(d => d.ReplaceOverridesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<DetectionThresholdOverride>>(), Arg.Any<CancellationToken>()))
            .Do(ci => overrideParentId = ci.Arg<Guid>());

        var input = new SaveDetectionConfigInput(
            PainThreshold: 40m,
            HappyThreshold: 75m,
            StageOverrides: new[] { new DetectionOverrideInput(stageId, PainThreshold: 35m, HappyThreshold: 70m) },
            TouchpointOverrides: Array.Empty<DetectionOverrideInput>());

        var result = await CreateSut().SaveDetectionConfigAsync(journeyId, input, Actor);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Config.JourneyId.Should().Be(journeyId);
        result.Value.Config.PainThreshold.Should().Be(40m);
        result.Value.Config.HappyThreshold.Should().Be(75m);
        // UpdatedAt is stamped from the injected TimeProvider (no DateTime.UtcNow in production code).
        result.Value.Config.UpdatedAt.Should().Be(Now);
        result.Value.StageOverrideCount.Should().Be(1);
        result.Value.TouchpointOverrideCount.Should().Be(0);

        // The journey-level config is upserted (one row per journey) with the requested thresholds...
        await _detection.Received(1).UpsertConfigAsync(
            Arg.Is<DetectionConfig>(c =>
                c.JourneyId == journeyId
                && c.PainThreshold == 40m
                && c.HappyThreshold == 75m),
            Arg.Any<CancellationToken>());
        // ...the override set is full-replaced, the stage override carrying scope_type "stage" + its values...
        await _detection.Received(1).ReplaceOverridesAsync(
            Arg.Any<Guid>(),
            Arg.Is<IReadOnlyList<DetectionThresholdOverride>>(o =>
                o.Count == 1
                && o[0].ScopeType == "stage"
                && o[0].ScopeId == stageId
                && o[0].PainThreshold == 35m
                && o[0].HappyThreshold == 70m),
            Arg.Any<CancellationToken>());
        // ...and the audit event is published in the same transaction (FR-015).
        await _events.Received(1).PublishAsync(
            Arg.Is<CustomerJourneyManagementEvent>(e =>
                e.EventType == CustomerJourneyManagementEventTypes.JourneyDetectionConfigUpdated
                && e.EntityId == journeyId
                && e.ActorId == Actor.UserId
                && e.CorrelationId == Actor.CorrelationId),
            Arg.Any<CancellationToken>());

        // The overrides FK to the very config that was upserted — same detection_config_id.
        overrideParentId.Should().Be(configId);
        configId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SaveDetectionConfigAsync_returns_threshold_invalid_when_pain_is_not_less_than_happy()
    {
        // Both thresholds are in range, but pain >= happy violates the neutral-band invariant
        // (pain_threshold < happy_threshold). Chosen so out_of_range can never fire instead.
        var journeyId = Guid.NewGuid();
        var input = new SaveDetectionConfigInput(
            PainThreshold: 80m,
            HappyThreshold: 75m,
            StageOverrides: Array.Empty<DetectionOverrideInput>(),
            TouchpointOverrides: Array.Empty<DetectionOverrideInput>());

        var result = await CreateSut().SaveDetectionConfigAsync(journeyId, input, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("detection.threshold_invalid");
        await AssertNothingWritten();
    }

    [Theory]
    // pain below 0, happy above 100, and both above 100 — each keeps pain < happy so only
    // the [0,100] range rule can fire (order-independent of the invariant check).
    [InlineData(-5, 75)]
    [InlineData(40, 120)]
    [InlineData(101, 105)]
    public async Task SaveDetectionConfigAsync_returns_out_of_range_when_a_threshold_is_outside_0_to_100(
        int pain, int happy)
    {
        var journeyId = Guid.NewGuid();
        var input = new SaveDetectionConfigInput(
            PainThreshold: pain,
            HappyThreshold: happy,
            StageOverrides: Array.Empty<DetectionOverrideInput>(),
            TouchpointOverrides: Array.Empty<DetectionOverrideInput>());

        var result = await CreateSut().SaveDetectionConfigAsync(journeyId, input, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("detection.out_of_range");
        await AssertNothingWritten();
    }

    [Fact]
    public async Task SaveDetectionConfigAsync_returns_unknown_stage_when_override_references_a_stage_not_in_the_journey()
    {
        // The journey owns one stage, but the override targets a different stage id — the service
        // must reject it (the scope_id existence guard for the polymorphic override reference).
        var journeyId = Guid.NewGuid();
        GivenStageInJourney(journeyId);
        var foreignStageId = Guid.NewGuid();

        var input = new SaveDetectionConfigInput(
            PainThreshold: 40m,
            HappyThreshold: 75m,
            StageOverrides: new[] { new DetectionOverrideInput(foreignStageId, PainThreshold: 35m, HappyThreshold: 70m) },
            TouchpointOverrides: Array.Empty<DetectionOverrideInput>());

        var result = await CreateSut().SaveDetectionConfigAsync(journeyId, input, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("detection.unknown_stage");
        await AssertNothingWritten();
    }

    /// <summary>Asserts a rejected save touched neither the detection tables nor the audit log.</summary>
    private async Task AssertNothingWritten()
    {
        await _detection.DidNotReceive().UpsertConfigAsync(
            Arg.Any<DetectionConfig>(), Arg.Any<CancellationToken>());
        await _detection.DidNotReceive().ReplaceOverridesAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<DetectionThresholdOverride>>(),
            Arg.Any<CancellationToken>());
        await _events.DidNotReceive().PublishAsync(
            Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }
}
