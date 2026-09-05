using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Personas;
using Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for the persona CRUD + lifecycle endpoints (T073 / US-3,
/// <c>contracts/personas-api.md</c>). Each test enters the real ASP.NET Core pipeline as an
/// authenticated actor (seeded M-10 user → login → MFA verify → bearer session), so persona
/// creation, the lifecycle state machine, the unsupported-delete contract, the archive-while-bound
/// guard, and the API-05 error envelope are all exercised end-to-end against PostgreSQL.
///
/// Coverage (per <c>quickstart.md §3</c> "Endpoint Tests"):
/// <list type="bullet">
///   <item><description><c>POST</c> creates a persona in <c>Draft</c>;</description></item>
///   <item><description>P-01 <c>PATCH .../status</c> succeeds (Draft → Active);</description></item>
///   <item><description><c>DELETE</c> is unsupported → 405 <c>persona.use_archive_instead</c>;</description></item>
///   <item><description>archiving a persona that still has an active journey binding → 409
///   <c>persona.archive_blocked_active_bindings</c>.</description></item>
/// </list>
///
/// Persona authorization (P-02 → 403 on a status transition) is NOT yet enforced in M-16: no
/// AddAuthorization/UseAuthorization pipeline or persona-permission check exists (deferred to the
/// M-10 authorization integration, see <c>contracts/personas-api.md</c> "Default personas" and the
/// PersonasController authorization note). That case is present below but <c>Skip</c>ped, mirroring
/// the by-design Skipped P-03/P-02 cases on the Journeys controller. Authentication itself IS
/// enforced — the unauthenticated case asserts 401.
///
/// Persona→journey binding has no HTTP endpoint (it is a service-layer operation on
/// <see cref="PersonaService"/>), so the archive-blocked test seeds the active binding by driving
/// the service directly from a DI scope — the same pattern <c>KpiWeightEnforcementTests</c> uses.
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class PersonasEndpointTests
{
    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public PersonasEndpointTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_personas_creates_persona_with_Draft_status_when_input_is_valid()
    {
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.PostAsJsonAsync(
            "/api/v1/personas",
            new { nameAr = "العميل الرقمي", nameEn = $"Digital Customer {Guid.NewGuid():N}" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("personaId").GetGuid().Should().NotBeEmpty();
        body.GetProperty("status").GetString().Should().Be("Draft");
    }

    [Fact]
    public async Task PATCH_status_persists_Draft_to_Active_when_caller_is_P01()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var personaId = await CreatePersonaAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/personas/{personaId}/status", new { status = "Active" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("status").GetString().Should().Be("Active");

        // The transition persisted: a fresh GET reflects the new status.
        var get = await client.GetAsync($"/api/v1/personas/{personaId}");
        (await get.ReadJsonAsync()).GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task DELETE_persona_returns_405_persona_use_archive_instead()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var personaId = await CreatePersonaAsync(client);

        var response = await client.DeleteAsync($"/api/v1/personas/{personaId}");

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        (await response.ReadErrorCodeAsync()).Should().Be("persona.use_archive_instead");
    }

    [Fact]
    public async Task PATCH_status_returns_409_persona_archive_blocked_active_bindings_when_persona_is_bound()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");

        // A persona may only be archived once it has no active journey bindings. Stand up an Active
        // persona bound to a journey, then attempt to archive it.
        var journeyId = await CreateJourneyAsync(client);
        var personaId = await CreatePersonaAsync(client);
        (await client.PatchAsJsonAsync($"/api/v1/personas/{personaId}/status", new { status = "Active" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await BindPersonaToJourneyAsync(journeyId, personaId, actor);

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/personas/{personaId}/status", new { status = "Archived" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("persona.archive_blocked_active_bindings");

        // The blocked archive wrote nothing: the persona is still Active.
        var get = await client.GetAsync($"/api/v1/personas/{personaId}");
        (await get.ReadJsonAsync()).GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task POST_personas_returns_401_when_unauthenticated()
    {
        var client = _factory.CreateClient();   // no Authorization header

        var response = await client.PostAsJsonAsync(
            "/api/v1/personas", new { nameAr = "العميل", nameEn = $"Customer {Guid.NewGuid():N}" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(Skip = "Persona authorization (journey.personas.publish → P-02 forbidden on a status " +
                 "transition) is not yet enforced in M-16: no AddAuthorization/UseAuthorization " +
                 "pipeline or persona check exists. Deferred to the M-10 authorization integration " +
                 "(see contracts/personas-api.md 'Default personas'). Un-skip once " +
                 "journey.personas.publish authorization lands.")]
    public async Task PATCH_status_returns_403_when_caller_is_P02()
    {
        var p01 = await _factory.SignedInClientAsync("P-01");
        var personaId = await CreatePersonaAsync(p01);

        var p02 = await _factory.SignedInClientAsync("P-02");
        var response = await p02.PatchAsJsonAsync(
            $"/api/v1/personas/{personaId}/status", new { status = "Active" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Creates a Draft persona over the real API and returns its id.</summary>
    private static async Task<Guid> CreatePersonaAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/personas",
            new { nameAr = "العميل الرقمي", nameEn = $"Digital Customer {Guid.NewGuid():N}" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync()).GetProperty("personaId").GetGuid();
    }

    /// <summary>Creates a Draft journey over the real API and returns its id.</summary>
    private static async Task<Guid> CreateJourneyAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name = $"Journey {Guid.NewGuid():N}", journeyType = "Onboarding" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync()).GetProperty("journeyId").GetGuid();
    }

    /// <summary>
    /// Binds an Active persona to a journey by driving <see cref="PersonaService"/> directly (resolved
    /// from a fresh DI scope, as the service is registered <c>Scoped</c>) — binding is a service-layer
    /// operation with no HTTP endpoint.
    /// </summary>
    private async Task BindPersonaToJourneyAsync(Guid journeyId, Guid personaId, SeededUser actor)
    {
        using var scope = _factory.Services.CreateScope();
        var personas = scope.ServiceProvider.GetRequiredService<PersonaService>();
        var actorContext = new ActorContext(actor.UserId, "P-01", Guid.NewGuid());

        var result = await personas.BindPersonaToJourneyAsync(journeyId, personaId, actorContext);
        result.IsSuccess.Should().BeTrue("an Active persona should bind to a journey");
    }
}
