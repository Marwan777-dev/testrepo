using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Personas;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Personas;

/// <summary>
/// Unit tests for <see cref="PersonaService"/> (T064 / US-3) — persona CRUD plus the Active-only
/// journey-binding guard (<c>contracts/personas-api.md</c>, <c>contracts/journeys-api.md</c>).
/// Authored FIRST (red→green per the Unit Test Policy); they pin the contract the T064
/// implementation must satisfy:
/// <list type="bullet">
///   <item><c>record CreatePersonaRequest(string NameAr, string NameEn, string? DescriptionAr,
///   string? DescriptionEn)</c>.</item>
///   <item><c>PersonaService(IPersonaDataService, ITransactionRunner, IM17EventPublisher,
///   TimeProvider)</c> — same shape as every other M-16 application service.</item>
///   <item><c>Task&lt;ServiceResult&lt;Guid&gt;&gt; CreatePersonaAsync(CreatePersonaRequest,
///   ActorContext, CancellationToken = default)</c> — persists the persona at status
///   <c>Draft</c> and publishes <c>persona.created</c> in the SAME transaction (FR-015).</item>
///   <item><c>Task&lt;ServiceResult&gt; BindPersonaToJourneyAsync(Guid journeyId, Guid personaId,
///   ActorContext, CancellationToken = default)</c> — only <c>Active</c> personas may be bound
///   (FR-005); a non-Active persona is rejected with <c>journey.invalid_persona</c> (the code the
///   journeys API already defines for "referenced persona is not Active") and writes nothing.</item>
/// </list>
/// Persona <b>authorization</b> (only P-01 may write personas / transition status) is NOT enforced
/// in M-16 yet — it is deferred to the M-10 authorization integration, so the P-02→403 case below is
/// <c>Skip</c>ped, exactly as the journey suite skips its P-03→403 case (<c>JourneyDefinitionFlowTests</c>).
/// </summary>
public sealed class PersonaServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly ActorContext Actor = new(
        UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Persona: "P-01",
        CorrelationId: Guid.Parse("44444444-4444-4444-4444-444444444444"));

    private readonly IPersonaDataService _personas = Substitute.For<IPersonaDataService>();
    private readonly IM17EventPublisher _events = Substitute.For<IM17EventPublisher>();
    private readonly FakeTimeProvider _time = new(Now);

    private PersonaService CreateSut() => new(
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

    [Fact]
    public async Task CreatePersonaAsync_persists_persona_at_Draft_and_publishes_persona_created()
    {
        var request = new CreatePersonaRequest(
            NameAr: "العميل الرقمي",
            NameEn: "Digital Customer",
            DescriptionAr: "عملاء يفضلون القنوات الرقمية",
            DescriptionEn: "Customers who prefer digital channels");

        var result = await CreateSut().CreatePersonaAsync(request, Actor);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        // New personas always start in Draft (contract: POST /personas → status "Draft").
        await _personas.Received(1).CreateAsync(
            Arg.Is<Persona>(p =>
                p.PersonaId == result.Value
                && p.Status == "Draft"
                && p.NameAr == request.NameAr
                && p.NameEn == request.NameEn
                && p.CreatedBy == Actor.UserId),
            Arg.Any<CancellationToken>());
        // ...and the audit event is published in the same transaction (FR-015).
        await _events.Received(1).PublishAsync(
            Arg.Is<CustomerJourneyManagementEvent>(e =>
                e.EventType == CustomerJourneyManagementEventTypes.PersonaCreated
                && e.EntityId == result.Value
                && e.ActorId == Actor.UserId
                && e.CorrelationId == Actor.CorrelationId),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Inactive")]
    [InlineData("Archived")]
    public async Task BindPersonaToJourneyAsync_rejects_non_active_persona_and_writes_nothing(string status)
    {
        var journeyId = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        _personas.GetByIdAsync(personaId, Arg.Any<CancellationToken>())
            .Returns(PersonaWith(personaId, status));

        var result = await CreateSut().BindPersonaToJourneyAsync(journeyId, personaId, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("journey.invalid_persona");
        await _personas.DidNotReceive().AddBindingAsync(
            Arg.Any<JourneyPersonaBinding>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BindPersonaToJourneyAsync_binds_when_persona_is_Active()
    {
        // Companion to the rejection case — proves the guard reads the persona's status through to
        // the repository rather than being hard-coded to always reject.
        var journeyId = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        _personas.GetByIdAsync(personaId, Arg.Any<CancellationToken>())
            .Returns(PersonaWith(personaId, "Active"));

        var result = await CreateSut().BindPersonaToJourneyAsync(journeyId, personaId, Actor);

        result.IsSuccess.Should().BeTrue();
        await _personas.Received(1).AddBindingAsync(
            Arg.Is<JourneyPersonaBinding>(b => b.JourneyId == journeyId && b.PersonaId == personaId),
            Arg.Any<CancellationToken>());
    }

    [Fact(Skip = "Persona authorization (only P-01 may create/transition personas) is not yet enforced " +
                 "in M-16: no AddAuthorization/UseAuthorization pipeline or persona-gate exists. Deferred " +
                 "to the M-10 authorization integration, mirroring the Skipped P-03→403 journey case in " +
                 "JourneyDefinitionFlowTests. Un-skip once journey.personas.* authorization lands.")]
    public async Task CreatePersonaAsync_is_forbidden_for_non_P01_caller()
    {
        var p02 = Actor with { Persona = "P-02" };
        var request = new CreatePersonaRequest("العميل", "Customer", null, null);

        var result = await CreateSut().CreatePersonaAsync(request, p02);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("persona.forbidden");
    }
}
