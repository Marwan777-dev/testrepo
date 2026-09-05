using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for the touchpoint endpoints (T033 / US-1, <c>contracts/journeys-api.md</c>):
/// adding a touchpoint to a stage, and that a touchpoint with no KPI bindings surfaces as
/// <c>isMeasured: false</c> in the journey tree. (KPI binding — which flips a touchpoint to
/// measured — lands in US-2; in US-1 every touchpoint is unmeasured.)
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class TouchpointsEndpointTests
{
    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public TouchpointsEndpointTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_touchpoints_adds_a_touchpoint_to_a_stage()
    {
        var client = await _factory.SignedInClientAsync();
        var journeyId = await CreateJourneyAsync(client);
        var stageId = await AddStageAsync(client, journeyId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/stages/{stageId}/touchpoints",
            new { name = "IVR menu", channels = new[] { "IVR", "Web" }, importance = "High", isMoT = true });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadJsonAsync()).GetProperty("touchpointId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task GET_journey_returns_unmeasured_touchpoint_with_isMeasured_false()
    {
        var client = await _factory.SignedInClientAsync();
        var journeyId = await CreateJourneyAsync(client);
        var stageId = await AddStageAsync(client, journeyId);
        (await client.PostAsJsonAsync($"/api/v1/stages/{stageId}/touchpoints", new { name = "Landing page" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var tree = await (await client.GetAsync($"/api/v1/journeys/{journeyId}")).ReadJsonAsync();

        var touchpoint = tree.GetProperty("stages").EnumerateArray().Single()
            .GetProperty("touchpoints").EnumerateArray().Single();
        touchpoint.GetProperty("name").GetString().Should().Be("Landing page");
        touchpoint.GetProperty("isMeasured").GetBoolean().Should().BeFalse();
    }

    private async Task<Guid> CreateJourneyAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name = $"Journey {Guid.NewGuid():N}", journeyType = "Onboarding" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync()).GetProperty("journeyId").GetGuid();
    }

    private static async Task<Guid> AddStageAsync(HttpClient client, Guid journeyId)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/journeys/{journeyId}/stages", new { name = "Awareness" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync()).GetProperty("stageId").GetGuid();
    }
}
