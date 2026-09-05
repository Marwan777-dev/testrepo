using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Limits;
using Nabadat.CustomerJourneyManagement.Application.Touchpoints;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Touchpoints;

/// <summary>
/// Unit tests for <see cref="TouchpointService"/> (T019 / US-1): adds touchpoints with their
/// channel set, enforces the per-stage touchpoint limit, and reports a touchpoint as
/// unmeasured (<c>isMeasured: false</c>) until it carries at least one KPI binding.
/// </summary>
public sealed class TouchpointServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly ActorContext Actor = new(
        UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Persona: "P-01",
        CorrelationId: Guid.Parse("55555555-5555-5555-5555-555555555555"));

    private readonly ITouchpointDataService _touchpoints = Substitute.For<ITouchpointDataService>();
    private readonly IStageDataService _stages = Substitute.For<IStageDataService>();
    private readonly IJourneyDataService _journeys = Substitute.For<IJourneyDataService>();
    private readonly IJourneyLimitProvider _limits = Substitute.For<IJourneyLimitProvider>();
    private readonly IM17EventPublisher _events = Substitute.For<IM17EventPublisher>();
    private readonly FakeTimeProvider _time = new(Now);

    public TouchpointServiceTests()
    {
        _limits.GetLimitsAsync(Arg.Any<CancellationToken>())
            .Returns(new JourneyLimits(MaxStagesPerJourney: 20, MaxTouchpointsPerStage: 30));
    }

    private TouchpointService CreateSut() => new(
        _touchpoints,
        _stages,
        _journeys,
        _limits,
        TestSupport.FakeTenantDb.Immediate(),
        _events,
        _time);

    private void GivenWritableStage(Guid stageId, Guid journeyId)
    {
        _stages.GetByIdAsync(stageId, Arg.Any<CancellationToken>())
            .Returns(new Stage { StageId = stageId, JourneyId = journeyId, SequenceNumber = 1, Name = "Awareness" });
        _journeys.GetByIdAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new Journey { JourneyId = journeyId, Name = "J", Status = "Draft" });
    }

    [Fact]
    public async Task AddTouchpoint_persists_touchpoint_with_channels()
    {
        var stageId = Guid.NewGuid();
        var journeyId = Guid.NewGuid();
        GivenWritableStage(stageId, journeyId);
        _touchpoints.CountByStageAsync(stageId, Arg.Any<CancellationToken>()).Returns(5);
        var request = new AddTouchpointRequest(
            Name: "IVR Greeting",
            Description: null,
            Channels: new[] { "IVR", "Web" },
            Importance: "High",
            IsMot: true,
            IsMandatory: false);

        var result = await CreateSut().AddTouchpointAsync(stageId, request, Actor);

        result.IsSuccess.Should().BeTrue();
        await _touchpoints.Received(1).CreateAsync(
            Arg.Is<Touchpoint>(t =>
                t.StageId == stageId
                && t.Name == "IVR Greeting"
                && t.Channels.SequenceEqual(new[] { "IVR", "Web" })
                && t.Importance == "High"
                && t.IsMot),
            Arg.Any<CancellationToken>());
        await _events.Received(1).PublishAsync(
            Arg.Is<CustomerJourneyManagementEvent>(e => e.EventType == CustomerJourneyManagementEventTypes.JourneyTouchpointAdded),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddTouchpoint_fails_when_touchpoint_limit_reached()
    {
        var stageId = Guid.NewGuid();
        var journeyId = Guid.NewGuid();
        GivenWritableStage(stageId, journeyId);
        _touchpoints.CountByStageAsync(stageId, Arg.Any<CancellationToken>()).Returns(30);
        var request = new AddTouchpointRequest(
            Name: "Overflow",
            Description: null,
            Channels: new[] { "Web" },
            Importance: "Medium",
            IsMot: false,
            IsMandatory: false);

        var result = await CreateSut().AddTouchpointAsync(stageId, request, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("journey.touchpoint_limit_reached");
        await _touchpoints.DidNotReceive().CreateAsync(
            Arg.Any<Touchpoint>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTouchpoint_returns_isMeasured_false_when_no_kpi_bindings()
    {
        var touchpointId = Guid.NewGuid();
        _touchpoints.GetByIdAsync(touchpointId, Arg.Any<CancellationToken>())
            .Returns(new Touchpoint { TouchpointId = touchpointId, StageId = Guid.NewGuid(), Name = "Unmeasured" });
        _touchpoints.HasKpiBindingsAsync(touchpointId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateSut().GetTouchpointAsync(touchpointId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsMeasured.Should().BeFalse();
        result.Value.Touchpoint.TouchpointId.Should().Be(touchpointId);
    }
}
