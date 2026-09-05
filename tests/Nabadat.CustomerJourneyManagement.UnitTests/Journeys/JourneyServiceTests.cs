using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Journeys;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Journeys;

/// <summary>
/// Unit tests for <see cref="JourneyService"/> (T015 / US-1). Validation and rejection paths
/// are exercised with fully mocked repositories; the happy create path runs through an
/// <see cref="TestSupport.FakeTenantDb"/> so persistence and the
/// <c>journey.created</c> event publication are both verified without a database.
/// </summary>
public sealed class JourneyServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly ActorContext Actor = new(
        UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Persona: "P-01",
        CorrelationId: Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private readonly IJourneyDataService _journeys = Substitute.For<IJourneyDataService>();
    private readonly IStageDataService _stages = Substitute.For<IStageDataService>();
    private readonly ITouchpointDataService _touchpoints = Substitute.For<ITouchpointDataService>();
    private readonly IPersonaDataService _personas = Substitute.For<IPersonaDataService>();
    private readonly IJourneyNameUniquenessValidator _uniqueness = Substitute.For<IJourneyNameUniquenessValidator>();
    private readonly IM17EventPublisher _events = Substitute.For<IM17EventPublisher>();
    private readonly FakeTimeProvider _time = new(Now);

    private JourneyService CreateSut() => new(
        _journeys,
        _stages,
        _touchpoints,
        _personas,
        _uniqueness,
        TestSupport.FakeTenantDb.Immediate(),
        _events,
        _time);

    [Fact]
    public async Task CreateJourney_persists_journey_and_returns_journeyId_when_input_is_valid()
    {
        _uniqueness
            .ValidateAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult.Success());
        var request = new CreateJourneyRequest("Customer Onboarding Journey", "End-to-end onboarding", "Onboarding");

        var result = await CreateSut().CreateJourneyAsync(request, Actor);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        await _journeys.Received(1).CreateAsync(
            Arg.Is<Journey>(j =>
                j.Name == "Customer Onboarding Journey"
                && j.JourneyType == "Onboarding"
                && j.Status == "Draft"
                && j.CreatedBy == Actor.UserId
                && j.CreatedAt == Now
                && j.UpdatedAt == Now),
            Arg.Any<CancellationToken>());
        await _events.Received(1).PublishAsync(
            Arg.Is<CustomerJourneyManagementEvent>(e => e.EventType == CustomerJourneyManagementEventTypes.JourneyCreated && e.EntityId == result.Value),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateJourney_returns_name_conflict_when_name_already_taken_case_insensitive()
    {
        _uniqueness
            .ValidateAsync("customer onboarding journey", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult.Failure("journey.name_conflict", "A journey with this name already exists."));
        var request = new CreateJourneyRequest("customer onboarding journey", null, "Onboarding");

        var result = await CreateSut().CreateJourneyAsync(request, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("journey.name_conflict");
        await _journeys.DidNotReceive().CreateAsync(
            Arg.Any<Journey>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().PublishAsync(
            Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetJourney_returns_full_journey_tree_when_id_is_valid()
    {
        var journeyId = Guid.NewGuid();
        var stageOneId = Guid.NewGuid();
        var stageTwoId = Guid.NewGuid();
        _journeys.GetByIdAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new Journey { JourneyId = journeyId, Name = "Support", Status = "Active" });
        _stages.ListByJourneyAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new List<Stage>
            {
                new() { StageId = stageOneId, JourneyId = journeyId, SequenceNumber = 1, Name = "Awareness" },
                new() { StageId = stageTwoId, JourneyId = journeyId, SequenceNumber = 2, Name = "Purchase" },
            });
        _touchpoints.ListByStageAsync(stageOneId, Arg.Any<CancellationToken>())
            .Returns(new List<Touchpoint>
            {
                new() { TouchpointId = Guid.NewGuid(), StageId = stageOneId, Name = "Landing page" },
                new() { TouchpointId = Guid.NewGuid(), StageId = stageOneId, Name = "Ad click" },
            });
        _touchpoints.ListByStageAsync(stageTwoId, Arg.Any<CancellationToken>())
            .Returns(new List<Touchpoint>
            {
                new() { TouchpointId = Guid.NewGuid(), StageId = stageTwoId, Name = "Checkout" },
            });
        _touchpoints.ListKpiBindingsByJourneyAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new List<KpiBinding>());
        _personas.ListBoundPersonasAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new List<Persona>());

        var result = await CreateSut().GetJourneyAsync(journeyId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Journey.JourneyId.Should().Be(journeyId);
        result.Value.Stages.Should().HaveCount(2);
        result.Value.Stages[0].Touchpoints.Should().HaveCount(2);
        result.Value.Stages[1].Touchpoints.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateJourney_rejects_when_status_is_Archived()
    {
        var journeyId = Guid.NewGuid();
        _journeys.GetByIdAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new Journey { JourneyId = journeyId, Name = "Old", Status = "Archived" });
        var request = new UpdateJourneyRequest("Renamed", null, "Support");

        var result = await CreateSut().UpdateJourneyAsync(journeyId, request, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("journey.archived_immutable");
        await _journeys.DidNotReceive().UpdateAsync(
            Arg.Any<Journey>(), Arg.Any<CancellationToken>());
    }
}
