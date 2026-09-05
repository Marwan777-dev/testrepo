using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for the journey version (immutable snapshot) endpoints (T074 / US-3,
/// <c>contracts/journeys-api.md</c>). Each test enters the real ASP.NET Core pipeline as an
/// authenticated actor and drives the publish + read flow end-to-end against PostgreSQL.
///
/// Coverage (per <c>quickstart.md §3</c> "Endpoint Tests"):
/// <list type="bullet">
///   <item><description><c>POST /publish</c> freezes the current journey tree as version 1 and the
///   stored snapshot is <b>self-contained</b> — it carries the stages, touchpoints, and KPI bindings
///   inline (no live references);</description></item>
///   <item><description><c>GET /versions/{n}</c> returns the exact snapshot captured at publish time,
///   marked <c>isSnapshot: true</c>, and that snapshot does <b>not</b> change when the live journey is
///   later edited (immutability — <c>research.md §1</c>).</description></item>
/// </list>
///
/// P-02 publish authorization (→ 403) is NOT yet enforced in M-16: no AddAuthorization/UseAuthorization
/// pipeline or persona check exists (deferred to the M-10 authorization integration — see the
/// JourneyVersionsController authorization note and <c>contracts/journeys-api.md</c> "Default personas").
/// That case is present below but <c>Skip</c>ped, mirroring the by-design Skipped P-03/P-02 cases
/// elsewhere in the module. Authentication itself IS enforced — the unauthenticated case asserts 401.
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class JourneyVersionsEndpointTests
{
    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public JourneyVersionsEndpointTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_publish_creates_self_contained_version_1_snapshot()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (journeyId, journeyName) = await SeedMeasuredJourneyAsync(client);

        // Publish → version 1 is created (snapshot row + journey.version.published in one tx).
        var publish = await client.PostAsync($"/api/v1/journeys/{journeyId}/publish", content: null);
        publish.StatusCode.Should().Be(HttpStatusCode.Created);
        var publishBody = await publish.ReadJsonAsync();
        publishBody.GetProperty("versionNumber").GetInt32().Should().Be(1);
        publishBody.GetProperty("versionId").GetGuid().Should().NotBeEmpty();

        // The stored snapshot is self-contained: name + the full stage → touchpoint → KPI-binding
        // tree are captured inline, and it is marked as a historical snapshot.
        var snapshot = await ReadVersionAsync(client, journeyId, 1);
        snapshot.GetProperty("isSnapshot").GetBoolean().Should().BeTrue();
        snapshot.GetProperty("snapshotVersion").GetInt32().Should().Be(1);
        snapshot.GetProperty("name").GetString().Should().Be(journeyName);

        var stages = snapshot.GetProperty("stages").EnumerateArray().ToList();
        stages.Should().ContainSingle();
        var touchpoints = stages[0].GetProperty("touchpoints").EnumerateArray().ToList();
        touchpoints.Should().ContainSingle();
        var kpiTypes = touchpoints[0].GetProperty("kpiBindings").EnumerateArray()
            .Select(k => k.GetProperty("type").GetString())
            .ToList();
        kpiTypes.Should().BeEquivalentTo(new[] { "NPS", "CSAT" });
    }

    [Fact]
    public async Task GET_version_returns_exact_snapshot_unchanged_after_journey_is_edited()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (journeyId, originalName) = await SeedMeasuredJourneyAsync(client);

        (await client.PostAsync($"/api/v1/journeys/{journeyId}/publish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // Edit the live journey — rename it after the version was published.
        var renamed = $"Renamed {Guid.NewGuid():N}";
        var update = await client.PutAsJsonAsync(
            $"/api/v1/journeys/{journeyId}", new { name = renamed, journeyType = "Onboarding" });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        // The live journey reflects the new name …
        var live = await (await client.GetAsync($"/api/v1/journeys/{journeyId}")).ReadJsonAsync();
        live.GetProperty("name").GetString().Should().Be(renamed);

        // … but the published version is frozen: its snapshot still carries the ORIGINAL name.
        var snapshot = await ReadVersionAsync(client, journeyId, 1);
        snapshot.GetProperty("name").GetString().Should().Be(originalName);
    }

    [Fact]
    public async Task GET_version_returns_404_journey_version_not_found_when_version_absent()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var journeyId = await CreateJourneyAsync(client);

        var response = await client.GetAsync($"/api/v1/journeys/{journeyId}/versions/99");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).Should().Be("journey.version_not_found");
    }

    [Fact]
    public async Task POST_publish_returns_401_when_unauthenticated()
    {
        var client = _factory.CreateClient();   // no Authorization header

        var response = await client.PostAsync($"/api/v1/journeys/{Guid.NewGuid()}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(Skip = "Persona authorization (journey.publish → P-02 forbidden on publish) is not yet " +
                 "enforced in M-16: no AddAuthorization/UseAuthorization pipeline or persona check " +
                 "exists. Deferred to the M-10 authorization integration (see " +
                 "contracts/journeys-api.md 'Default personas'). Un-skip once journey.publish " +
                 "authorization lands.")]
    public async Task POST_publish_returns_403_when_caller_is_P02()
    {
        var p01 = await _factory.SignedInClientAsync("P-01");
        var (journeyId, _) = await SeedMeasuredJourneyAsync(p01);

        var p02 = await _factory.SignedInClientAsync("P-02");
        var response = await p02.PostAsync($"/api/v1/journeys/{journeyId}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Creates a journey → stage → touchpoint with a valid 100% KPI binding set (NPS 60 + CSAT 40), so
    /// the published snapshot has a non-trivial, self-contained tree. Returns the journey id and name.
    /// </summary>
    private static async Task<(Guid JourneyId, string Name)> SeedMeasuredJourneyAsync(HttpClient client)
    {
        var name = $"Journey {Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name, journeyType = "Onboarding" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var journeyId = (await create.ReadJsonAsync()).GetProperty("journeyId").GetGuid();

        var stage = await client.PostAsJsonAsync($"/api/v1/journeys/{journeyId}/stages", new { name = "Awareness" });
        stage.StatusCode.Should().Be(HttpStatusCode.Created);
        var stageId = (await stage.ReadJsonAsync()).GetProperty("stageId").GetGuid();

        var touchpoint = await client.PostAsJsonAsync($"/api/v1/stages/{stageId}/touchpoints", new { name = "Landing page" });
        touchpoint.StatusCode.Should().Be(HttpStatusCode.Created);
        var touchpointId = (await touchpoint.ReadJsonAsync()).GetProperty("touchpointId").GetGuid();

        var kpis = await client.PutAsJsonAsync(
            $"/api/v1/touchpoints/{touchpointId}/kpis",
            new { kpiBindings = new[] { new { kpiType = "NPS", weight = 60 }, new { kpiType = "CSAT", weight = 40 } } });
        kpis.StatusCode.Should().Be(HttpStatusCode.OK);

        return (journeyId, name);
    }

    /// <summary>Creates a bare Draft journey (no stages) over the real API and returns its id.</summary>
    private static async Task<Guid> CreateJourneyAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name = $"Journey {Guid.NewGuid():N}", journeyType = "Onboarding" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync()).GetProperty("journeyId").GetGuid();
    }

    /// <summary>Reads a stored version snapshot, asserting a 200, and returns its JSON body.</summary>
    private static async Task<System.Text.Json.JsonElement> ReadVersionAsync(HttpClient client, Guid journeyId, int versionNumber)
    {
        var response = await client.GetAsync($"/api/v1/journeys/{journeyId}/versions/{versionNumber}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }
}
