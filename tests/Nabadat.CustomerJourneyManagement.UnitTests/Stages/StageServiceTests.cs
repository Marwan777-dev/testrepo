using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Limits;
using Nabadat.CustomerJourneyManagement.Application.Stages;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Stages;

/// <summary>
/// Unit tests for <see cref="StageService"/> (T018 / US-1): appends stages at the next
/// sequence position, enforces the per-journey stage limit, blocks deletion of a stage that
/// still owns touchpoints, and persists a wholesale reorder.
/// </summary>
public sealed class StageServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly ActorContext Actor = new(
        UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Persona: "P-01",
        CorrelationId: Guid.Parse("44444444-4444-4444-4444-444444444444"));

    private readonly IJourneyDataService _journeys = Substitute.For<IJourneyDataService>();
    private readonly IStageDataService _stages = Substitute.For<IStageDataService>();
    private readonly ITouchpointDataService _touchpoints = Substitute.For<ITouchpointDataService>();
    private readonly IJourneyLimitProvider _limits = Substitute.For<IJourneyLimitProvider>();
    private readonly IM17EventPublisher _events = Substitute.For<IM17EventPublisher>();
    private readonly FakeTimeProvider _time = new(Now);

    public StageServiceTests()
    {
        _limits.GetLimitsAsync(Arg.Any<CancellationToken>())
            .Returns(new JourneyLimits(MaxStagesPerJourney: 20, MaxTouchpointsPerStage: 30));
    }

    private StageService CreateSut() => new(
        _journeys,
        _stages,
        _touchpoints,
        _limits,
        TestSupport.FakeTenantDb.Immediate(),
        _events,
        _time);

    [Fact]
    public async Task AddStage_persists_stage_with_correct_sequence()
    {
        var journeyId = Guid.NewGuid();
        _journeys.GetByIdAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new Journey { JourneyId = journeyId, Name = "J", Status = "Draft" });
        _stages.CountByJourneyAsync(journeyId, Arg.Any<CancellationToken>()).Returns(2);
        _stages.GetMaxSequenceNumberAsync(journeyId, Arg.Any<CancellationToken>()).Returns(2);

        var result = await CreateSut().AddStageAsync(journeyId, new AddStageRequest("Consideration"), Actor);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SequenceNumber.Should().Be(3);
        await _stages.Received(1).CreateAsync(
            Arg.Is<Stage>(s => s.JourneyId == journeyId && s.Name == "Consideration" && s.SequenceNumber == 3),
            Arg.Any<CancellationToken>());
        await _events.Received(1).PublishAsync(
            Arg.Is<CustomerJourneyManagementEvent>(e => e.EventType == CustomerJourneyManagementEventTypes.JourneyStageAdded),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddStage_fails_when_stage_limit_reached()
    {
        var journeyId = Guid.NewGuid();
        _journeys.GetByIdAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new Journey { JourneyId = journeyId, Name = "J", Status = "Draft" });
        _stages.CountByJourneyAsync(journeyId, Arg.Any<CancellationToken>()).Returns(20);

        var result = await CreateSut().AddStageAsync(journeyId, new AddStageRequest("One Too Many"), Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("journey.stage_limit_reached");
        await _stages.DidNotReceive().CreateAsync(
            Arg.Any<Stage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteStage_fails_when_stage_has_touchpoints()
    {
        var journeyId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        _stages.GetByIdAsync(stageId, Arg.Any<CancellationToken>())
            .Returns(new Stage { StageId = stageId, JourneyId = journeyId, SequenceNumber = 1, Name = "Awareness" });
        _journeys.GetByIdAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new Journey { JourneyId = journeyId, Name = "J", Status = "Draft" });
        _touchpoints.CountByStageAsync(stageId, Arg.Any<CancellationToken>()).Returns(3);

        var result = await CreateSut().DeleteStageAsync(stageId, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("journey.stage_has_touchpoints");
        await _stages.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReorderStages_persists_new_sequence()
    {
        var journeyId = Guid.NewGuid();
        var stageA = Guid.NewGuid();
        var stageB = Guid.NewGuid();
        var stageC = Guid.NewGuid();
        _journeys.GetByIdAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new Journey { JourneyId = journeyId, Name = "J", Status = "Draft" });
        _stages.ListByJourneyAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new List<Stage>
            {
                new() { StageId = stageA, JourneyId = journeyId, SequenceNumber = 1, Name = "A" },
                new() { StageId = stageB, JourneyId = journeyId, SequenceNumber = 2, Name = "B" },
                new() { StageId = stageC, JourneyId = journeyId, SequenceNumber = 3, Name = "C" },
            });
        var newOrder = new[] { stageC, stageA, stageB };

        var result = await CreateSut().ReorderStagesAsync(journeyId, newOrder, Actor);

        result.IsSuccess.Should().BeTrue();
        await _stages.Received(1).ReorderAsync(
            journeyId,
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(newOrder)),
            Arg.Any<CancellationToken>());
    }
}
