using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Journeys;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Journeys;

/// <summary>
/// Unit tests for <see cref="JourneyStatusTransitionService"/> (T016 / US-1) — the journey
/// lifecycle state machine (<c>Draft → Active ↔ Inactive → Archived</c>, <c>Archived</c>
/// terminal). Every accepted transition must persist the new status and publish a
/// <c>journey.status.changed</c> event in the same unit of work.
/// </summary>
public sealed class JourneyStatusTransitionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly ActorContext Actor = new(
        UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Persona: "P-01",
        CorrelationId: Guid.Parse("33333333-3333-3333-3333-333333333333"));

    private readonly IJourneyDataService _journeys = Substitute.For<IJourneyDataService>();
    private readonly IM17EventPublisher _events = Substitute.For<IM17EventPublisher>();
    private readonly FakeTimeProvider _time = new(Now);

    private JourneyStatusTransitionService CreateSut() => new(
        _journeys,
        TestSupport.FakeTenantDb.Immediate(),
        _events,
        _time);

    [Theory]
    [InlineData("Draft", JourneyStatus.Active)]
    [InlineData("Active", JourneyStatus.Inactive)]
    [InlineData("Inactive", JourneyStatus.Active)]
    [InlineData("Draft", JourneyStatus.Archived)]
    [InlineData("Active", JourneyStatus.Archived)]
    [InlineData("Inactive", JourneyStatus.Archived)]
    public async Task ChangeStatus_persists_and_publishes_status_changed_when_transition_is_valid(
        string currentStatus, JourneyStatus target)
    {
        var journeyId = Guid.NewGuid();
        _journeys.GetByIdAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new Journey { JourneyId = journeyId, Name = "J", Status = currentStatus });

        var result = await CreateSut().ChangeStatusAsync(journeyId, target, Actor);

        result.IsSuccess.Should().BeTrue();
        await _journeys.Received(1).UpdateAsync(
            Arg.Is<Journey>(j => j.Status == target.ToString()),
            Arg.Any<CancellationToken>());
        await _events.Received(1).PublishAsync(
            Arg.Is<CustomerJourneyManagementEvent>(e => e.EventType == CustomerJourneyManagementEventTypes.JourneyStatusChanged && e.EntityId == journeyId),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(JourneyStatus.Active)]
    [InlineData(JourneyStatus.Inactive)]
    [InlineData(JourneyStatus.Draft)]
    public async Task ChangeStatus_rejects_with_archived_terminal_when_journey_is_Archived(JourneyStatus target)
    {
        var journeyId = Guid.NewGuid();
        _journeys.GetByIdAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new Journey { JourneyId = journeyId, Name = "J", Status = "Archived" });

        var result = await CreateSut().ChangeStatusAsync(journeyId, target, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("journey.archived_terminal");
        await _journeys.DidNotReceive().UpdateAsync(
            Arg.Any<Journey>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().PublishAsync(
            Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatus_rejects_with_invalid_transition_when_step_is_not_allowed()
    {
        var journeyId = Guid.NewGuid();
        _journeys.GetByIdAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new Journey { JourneyId = journeyId, Name = "J", Status = "Draft" });

        // Draft → Inactive is not a defined transition (Draft may only go to Active or Archived).
        var result = await CreateSut().ChangeStatusAsync(journeyId, JourneyStatus.Inactive, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("journey.invalid_transition");
        await _events.DidNotReceive().PublishAsync(
            Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }
}
