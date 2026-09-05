using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for the journey CRUD + lifecycle endpoints (T031 / US-1,
/// <c>contracts/journeys-api.md</c>). Each test enters the real ASP.NET Core pipeline as
/// an authenticated actor (seeded M-10 user → login → MFA verify → bearer session), so
/// creation, case-insensitive name uniqueness, the lifecycle state machine, and the
/// API-05 error envelope are all exercised end-to-end against PostgreSQL.
///
/// Persona authorization (P-03 → 403) is not yet enforced in M-16 (no
/// AddAuthorization/UseAuthorization or persona check exists); that case is present but
/// <c>Skip</c>ped pending the M-10 authorization integration. Authentication itself IS
/// enforced — the unauthenticated case asserts 401.
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class JourneysEndpointTests
{
    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public JourneysEndpointTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_journeys_creates_journey_with_Draft_status_when_input_is_valid()
    {
        var client = await _factory.SignedInClientAsync();
        var name = UniqueName();

        var response = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name, description = "End-to-end onboarding", journeyType = "Onboarding" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("journeyId").GetGuid().Should().NotBeEmpty();
        body.GetProperty("name").GetString().Should().Be(name);
        body.GetProperty("status").GetString().Should().Be("Draft");
    }

    [Fact]
    public async Task POST_journeys_returns_409_journey_name_conflict_when_name_is_taken_case_insensitive()
    {
        var client = await _factory.SignedInClientAsync();
        var name = UniqueName();
        (await client.PostAsJsonAsync("/api/v1/journeys", new { name, journeyType = "Onboarding" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // Same name in a different case → still a conflict (case-insensitive uniqueness).
        var response = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name = name.ToUpperInvariant(), journeyType = "Onboarding" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("journey.name_conflict");
    }

    [Fact]
    public async Task PATCH_status_persists_valid_transitions_Draft_to_Active_to_Inactive()
    {
        var client = await _factory.SignedInClientAsync();
        var journeyId = await CreateJourneyAsync(client);

        var toActive = await client.PatchAsJsonAsync($"/api/v1/journeys/{journeyId}/status", new { status = "Active" });
        toActive.StatusCode.Should().Be(HttpStatusCode.OK);
        (await toActive.ReadJsonAsync()).GetProperty("status").GetString().Should().Be("Active");

        var toInactive = await client.PatchAsJsonAsync($"/api/v1/journeys/{journeyId}/status", new { status = "Inactive" });
        toInactive.StatusCode.Should().Be(HttpStatusCode.OK);
        (await toInactive.ReadJsonAsync()).GetProperty("status").GetString().Should().Be("Inactive");

        // The transition persisted: a fresh GET reflects the latest status.
        var get = await client.GetAsync($"/api/v1/journeys/{journeyId}");
        (await get.ReadJsonAsync()).GetProperty("status").GetString().Should().Be("Inactive");
    }

    [Fact]
    public async Task PATCH_status_returns_422_journey_archived_terminal_when_leaving_Archived()
    {
        var client = await _factory.SignedInClientAsync();
        var journeyId = await CreateJourneyAsync(client);
        (await client.PatchAsJsonAsync($"/api/v1/journeys/{journeyId}/status", new { status = "Archived" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/journeys/{journeyId}/status", new { status = "Active" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).Should().Be("journey.archived_terminal");
    }

    [Fact]
    public async Task POST_journeys_returns_401_when_unauthenticated()
    {
        var client = _factory.CreateClient();   // no Authorization header

        var response = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name = UniqueName(), journeyType = "Onboarding" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(Skip = "Persona authorization (journey.write → P-03 forbidden) is not yet enforced in M-16: " +
                 "no AddAuthorization/UseAuthorization pipeline or persona check exists. Deferred to the " +
                 "M-10 authorization integration (see contracts/journeys-api.md 'Default personas'). " +
                 "Un-skip once journey.write authorization lands.")]
    public async Task POST_journeys_returns_403_when_actor_is_P03()
    {
        var client = await _factory.SignedInClientAsync("P-03");

        var response = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name = UniqueName(), journeyType = "Onboarding" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> CreateJourneyAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name = UniqueName(), journeyType = "Onboarding" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync()).GetProperty("journeyId").GetGuid();
    }

    private static string UniqueName() => $"Journey {Guid.NewGuid():N}";
}
