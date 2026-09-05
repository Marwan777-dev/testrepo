using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for the stage endpoints (T032 / US-1, <c>contracts/journeys-api.md</c>):
/// append-at-end sequencing, the delete guard when a stage still owns touchpoints, and
/// full-permutation reorder — each driven end-to-end against PostgreSQL as an
/// authenticated actor.
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class StagesEndpointTests
{
    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public StagesEndpointTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_stages_appends_at_end_with_incrementing_sequenceNumber()
    {
        var client = await _factory.SignedInClientAsync();
        var journeyId = await CreateJourneyAsync(client);

        var first = await AddStageAsync(client, journeyId, "Awareness");
        var second = await AddStageAsync(client, journeyId, "Consideration");

        first.GetProperty("sequenceNumber").GetInt32().Should().Be(1);
        second.GetProperty("sequenceNumber").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task DELETE_stage_returns_409_journey_stage_has_touchpoints_when_stage_is_not_empty()
    {
        var client = await _factory.SignedInClientAsync();
        var journeyId = await CreateJourneyAsync(client);
        var stageId = (await AddStageAsync(client, journeyId, "Purchase")).GetProperty("stageId").GetGuid();
        (await client.PostAsJsonAsync($"/api/v1/stages/{stageId}/touchpoints", new { name = "Checkout" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.DeleteAsync($"/api/v1/journeys/{journeyId}/stages/{stageId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("journey.stage_has_touchpoints");
    }

    [Fact]
    public async Task PUT_stages_reorder_persists_the_new_sequence()
    {
        var client = await _factory.SignedInClientAsync();
        var journeyId = await CreateJourneyAsync(client);
        var s1 = (await AddStageAsync(client, journeyId, "One")).GetProperty("stageId").GetGuid();
        var s2 = (await AddStageAsync(client, journeyId, "Two")).GetProperty("stageId").GetGuid();
        var s3 = (await AddStageAsync(client, journeyId, "Three")).GetProperty("stageId").GetGuid();

        var reorder = await client.PutAsJsonAsync(
            $"/api/v1/journeys/{journeyId}/stages/reorder", new { stageIds = new[] { s3, s1, s2 } });
        reorder.StatusCode.Should().Be(HttpStatusCode.OK);

        var stages = (await (await client.GetAsync($"/api/v1/journeys/{journeyId}/stages")).ReadJsonAsync())
            .GetProperty("stages")
            .EnumerateArray()
            .Select(s => s.GetProperty("stageId").GetGuid())
            .ToList();
        stages.Should().Equal(s3, s1, s2);
    }

    private async Task<Guid> CreateJourneyAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name = $"Journey {Guid.NewGuid():N}", journeyType = "Onboarding" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync()).GetProperty("journeyId").GetGuid();
    }

    private static async Task<System.Text.Json.JsonElement> AddStageAsync(HttpClient client, Guid journeyId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/journeys/{journeyId}/stages", new { name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.ReadJsonAsync();
    }
}
