using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Personas;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Personas;

/// <summary>
/// Unit tests for <see cref="PersonaStatusTransitionService"/> (T063 / US-3) — the persona
/// lifecycle state machine (<c>Draft → Active ↔ Inactive → Archived</c>, <c>Archived</c>
/// terminal) per <c>contracts/personas-api.md</c> (<c>PATCH /api/v1/personas/{id}/status</c>).
/// Every accepted transition must persist the new status and publish a
/// <c>persona.status.changed</c> event in the same unit of work; archiving is additionally
/// guarded — a persona bound to one or more journeys cannot be archived
/// (<c>persona.archive_blocked_active_bindings</c>, 409).
/// </summary>
public sealed class PersonaStatusTransitionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly ActorContext Actor = new(
        UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Persona: "P-01",
        CorrelationId: Guid.Parse("33333333-3333-3333-3333-333333333333"));

    private readonly IPersonaDataService _personas = Substitute.For<IPersonaDataService>();
    private readonly IM17EventPublisher _events = Substitute.For<IM17EventPublisher>();
    private readonly FakeTimeProvider _time = new(Now);

    private PersonaStatusTransitionService CreateSut() => new(
        _personas,
        TestSupport.FakeTenantDb.Immediate(),
        _events,
        _time);

    private static Persona PersonaWith(Guid personaId, string status) => new()
    {
        PersonaId = personaId,
        NameAr = "العميل الرقمي",
        NameEn = "Digital Customer",
        Status = status,
    };

    [Theory]
    [InlineData("Draft", PersonaStatus.Active)]
    [InlineData("Active", PersonaStatus.Inactive)]
    [InlineData("Inactive", PersonaStatus.Active)]
    [InlineData("Draft", PersonaStatus.Archived)]
    [InlineData("Active", PersonaStatus.Archived)]
    [InlineData("Inactive", PersonaStatus.Archived)]
    public async Task ChangeStatus_persists_and_publishes_status_changed_when_transition_is_valid(
        string currentStatus, PersonaStatus target)
    {
        var personaId = Guid.NewGuid();
        _personas.GetByIdAsync(personaId, Arg.Any<CancellationToken>())
            .Returns(PersonaWith(personaId, currentStatus));
        // No active journey bindings — the archive guard passes for the Archived targets too.
        _personas.CountBindingsAsync(personaId, Arg.Any<CancellationToken>()).Returns(0);

        var result = await CreateSut().ChangeStatusAsync(personaId, target, Actor);

        result.IsSuccess.Should().BeTrue();
        await _personas.Received(1).UpdateAsync(
            Arg.Is<Persona>(p => p.Status == target.ToString()),
            Arg.Any<CancellationToken>());
        await _events.Received(1).PublishAsync(
            Arg.Is<CustomerJourneyManagementEvent>(e => e.EventType == CustomerJourneyManagementEventTypes.PersonaStatusChanged && e.EntityId == personaId),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PersonaStatus.Active)]
    [InlineData(PersonaStatus.Inactive)]
    [InlineData(PersonaStatus.Draft)]
    public async Task ChangeStatus_rejects_with_archived_terminal_when_persona_is_Archived(PersonaStatus target)
    {
        var personaId = Guid.NewGuid();
        _personas.GetByIdAsync(personaId, Arg.Any<CancellationToken>())
            .Returns(PersonaWith(personaId, "Archived"));

        var result = await CreateSut().ChangeStatusAsync(personaId, target, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("persona.archived_terminal");
        await _personas.DidNotReceive().UpdateAsync(
            Arg.Any<Persona>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().PublishAsync(
            Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatus_rejects_with_invalid_transition_when_step_is_not_allowed()
    {
        var personaId = Guid.NewGuid();
        _personas.GetByIdAsync(personaId, Arg.Any<CancellationToken>())
            .Returns(PersonaWith(personaId, "Draft"));

        // Draft → Inactive is not a defined transition (Draft may only go to Active or Archived).
        var result = await CreateSut().ChangeStatusAsync(personaId, PersonaStatus.Inactive, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("persona.invalid_transition");
        await _personas.DidNotReceive().UpdateAsync(
            Arg.Any<Persona>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().PublishAsync(
            Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatus_rejects_with_archive_blocked_when_persona_has_active_bindings()
    {
        var personaId = Guid.NewGuid();
        _personas.GetByIdAsync(personaId, Arg.Any<CancellationToken>())
            .Returns(PersonaWith(personaId, "Active"));
        // Bound to two journeys — archiving must be blocked until the caller unbinds.
        _personas.CountBindingsAsync(personaId, Arg.Any<CancellationToken>()).Returns(2);

        var result = await CreateSut().ChangeStatusAsync(personaId, PersonaStatus.Archived, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("persona.archive_blocked_active_bindings");
        await _personas.DidNotReceive().UpdateAsync(
            Arg.Any<Persona>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().PublishAsync(
            Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }
}
