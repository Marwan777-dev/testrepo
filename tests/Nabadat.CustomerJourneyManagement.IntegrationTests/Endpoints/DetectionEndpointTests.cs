using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for the detection-config and report-contract endpoints (T091 / US-4,
/// <c>contracts/configuration-api.md</c> + <c>contracts/journeys-api.md</c>):
/// <list type="bullet">
///   <item><description><c>PUT /api/v1/journeys/{id}/detection</c> persists the journey-level config and
///     its overrides, round-tripped through <c>GET /detection</c>;</description></item>
///   <item><description><c>painThreshold &gt;= happyThreshold</c> is rejected <c>422
///     detection.threshold_invalid</c>;</description></item>
///   <item><description><c>GET /api/v1/journeys/{id}/reports</c> returns the rebuilt contract — an
///     unmeasured touchpoint surfaces <c>isMeasured:false</c> with an empty <c>kpiTypes</c> list
///     (FR-008).</description></item>
/// </list>
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class DetectionEndpointTests
{
    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public DetectionEndpointTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PUT_detection_persists_config_and_GET_returns_it_when_input_is_valid()
    {
        var client = await _factory.SignedInClientAsync();
        var journeyId = await CreateJourneyAsync(client);
        var stageId = await AddStageAsync(client, journeyId);

        var save = await client.PutAsJsonAsync(
            $"/api/v1/journeys/{journeyId}/detection",
            new
            {
                painThreshold = 40,
                happyThreshold = 75,
                stageOverrides = new[] { new { stageId, painThreshold = 35, happyThreshold = 70 } }
            });

        save.StatusCode.Should().Be(HttpStatusCode.OK);
        var savedBody = await save.ReadJsonAsync();
        savedBody.GetProperty("painThreshold").GetDecimal().Should().Be(40m);
        savedBody.GetProperty("happyThreshold").GetDecimal().Should().Be(75m);
        savedBody.GetProperty("stageOverrideCount").GetInt32().Should().Be(1);
        savedBody.GetProperty("touchpointOverrideCount").GetInt32().Should().Be(0);

        // GET round-trips the persisted config, including the stage override.
        var read = await (await client.GetAsync($"/api/v1/journeys/{journeyId}/detection")).ReadJsonAsync();
        read.GetProperty("painThreshold").GetDecimal().Should().Be(40m);
        read.GetProperty("happyThreshold").GetDecimal().Should().Be(75m);
        var stageOverride = read.GetProperty("stageOverrides").EnumerateArray().Single();
        stageOverride.GetProperty("stageId").GetGuid().Should().Be(stageId);
        stageOverride.GetProperty("painThreshold").GetDecimal().Should().Be(35m);
        stageOverride.GetProperty("happyThreshold").GetDecimal().Should().Be(70m);
    }

    [Fact]
    public async Task PUT_detection_returns_422_when_pain_threshold_not_below_happy()
    {
        var client = await _factory.SignedInClientAsync();
        var journeyId = await CreateJourneyAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/journeys/{journeyId}/detection",
            new { painThreshold = 80, happyThreshold = 75 });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).Should().Be("detection.threshold_invalid");
    }

    [Fact]
    public async Task GET_reports_returns_contract_with_unmeasured_touchpoint_isMeasured_false()
    {
        var client = await _factory.SignedInClientAsync();
        var journeyId = await CreateJourneyAsync(client);
        var stageId = await AddStageAsync(client, journeyId);
        var touchpointId = await AddTouchpointAsync(client, stageId, "Landing page");

        // Saving the detection config rebuilds the report contract in the same transaction
        // (FR-015), so the contract row exists for GET /reports to return.
        (await client.PutAsJsonAsync(
            $"/api/v1/journeys/{journeyId}/detection",
            new { painThreshold = 40, happyThreshold = 75 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var reports = await client.GetAsync($"/api/v1/journeys/{journeyId}/reports");
        reports.StatusCode.Should().Be(HttpStatusCode.OK);
        var contract = await reports.ReadJsonAsync();

        contract.GetProperty("journeyId").GetGuid().Should().Be(journeyId);
        contract.GetProperty("scoreDimensions").EnumerateArray().Select(d => d.GetString())
            .Should().Equal("journey_score", "stage_score", "touchpoint_score", "kpi_score");
        contract.GetProperty("detectionConfig").GetProperty("painThreshold").GetDecimal().Should().Be(40m);

        var touchpoint = contract.GetProperty("stages").EnumerateArray().Single()
            .GetProperty("touchpoints").EnumerateArray().Single();
        touchpoint.GetProperty("touchpointId").GetGuid().Should().Be(touchpointId);
        touchpoint.GetProperty("isMeasured").GetBoolean().Should().BeFalse();
        touchpoint.GetProperty("kpiTypes").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task GET_reports_returns_404_when_no_contract_generated()
    {
        var client = await _factory.SignedInClientAsync();
        var journeyId = await CreateJourneyAsync(client);

        var response = await client.GetAsync($"/api/v1/journeys/{journeyId}/reports");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).Should().Be("journey.no_report_contract");
    }

    private static async Task<Guid> CreateJourneyAsync(HttpClient client)
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

    private static async Task<Guid> AddTouchpointAsync(HttpClient client, Guid stageId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/stages/{stageId}/touchpoints", new { name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync()).GetProperty("touchpointId").GetGuid();
    }
}
