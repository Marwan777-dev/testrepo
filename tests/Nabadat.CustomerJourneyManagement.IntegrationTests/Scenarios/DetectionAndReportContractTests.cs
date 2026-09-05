using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.Platform.Contracts.M16;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Scenarios;

/// <summary>
/// US-4 business-cycle scenario (T092, <c>quickstart.md §3</c>): a journey author configures pain/happy
/// detection thresholds (journey-level plus a stage-level override), then M-07 reads the rebuilt report
/// contract back through the published <see cref="IReportContractReader"/>. One test walks the whole flow
/// and asserts the final state-of-the-world — the persisted config + override, the cross-module contract
/// read (measured vs. unmeasured touchpoints), the rejected invalid save, and the in-band audit trail —
/// matching the spec's <c>Independent Test</c>:
/// <list type="number">
///   <item><description>save journey-level <c>painThreshold=40, happyThreshold=75</c> with a stage-level
///     override (<c>35/70</c>) → persisted (round-tripped via <c>GET /detection</c>);</description></item>
///   <item><description><see cref="IReportContractReader.GetReportContractAsync"/> returns the contract
///     with all stages/touchpoints, the fixed score-dimension quad, and the journey-level
///     thresholds;</description></item>
///   <item><description>the measured touchpoint surfaces its KPI types while the unmeasured one is
///     <c>isMeasured:false</c> with an empty <c>kpiTypes</c> list (absent from the KPI dimension list,
///     FR-008); and</description></item>
///   <item><description><c>painThreshold &gt;= happyThreshold</c> is rejected <c>422
///     detection.threshold_invalid</c> and writes nothing.</description></item>
/// </list>
/// The final aggregate audit check (exactly one <c>journey.detection_config.updated</c>) doubles as proof
/// that the rejected invalid save emitted nothing.
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class DetectionAndReportContractTests
{
    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public DetectionAndReportContractTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Author_saves_detection_config_then_report_contract_reader_exposes_it()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");

        // Build a journey → stage → two touchpoints: one measured (KPI-bound), one not.
        var journeyId = await CreateJourneyAsync(client);
        var stageId = await AddStageAsync(client, journeyId);
        var measuredId = await AddTouchpointAsync(client, stageId, "Survey CSAT");
        var unmeasuredId = await AddTouchpointAsync(client, stageId, "Landing page");

        // KPI bindings (NPS 60 + CSAT 40 = 100) flip the first touchpoint to measured.
        (await client.PutAsJsonAsync(
            $"/api/v1/touchpoints/{measuredId}/kpis",
            new { kpiBindings = new[] { new { kpiType = "NPS", weight = 60 }, new { kpiType = "CSAT", weight = 40 } } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // 1+2. Save journey-level thresholds (40/75) with a stage-level override (35/70).
        var save = await client.PutAsJsonAsync(
            $"/api/v1/journeys/{journeyId}/detection",
            new
            {
                painThreshold = 40,
                happyThreshold = 75,
                stageOverrides = new[] { new { stageId, painThreshold = 35, happyThreshold = 70 } }
            });
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        // The config + override persisted (round-tripped via GET /detection).
        var detection = await (await client.GetAsync($"/api/v1/journeys/{journeyId}/detection")).ReadJsonAsync();
        detection.GetProperty("painThreshold").GetDecimal().Should().Be(40m);
        detection.GetProperty("happyThreshold").GetDecimal().Should().Be(75m);
        var stageOverride = detection.GetProperty("stageOverrides").EnumerateArray().Single();
        stageOverride.GetProperty("stageId").GetGuid().Should().Be(stageId);
        stageOverride.GetProperty("painThreshold").GetDecimal().Should().Be(35m);
        stageOverride.GetProperty("happyThreshold").GetDecimal().Should().Be(70m);

        // 3+4. M-07 reads the rebuilt contract in-process through the published IReportContractReader
        //      (Scoped — resolved from a fresh scope).
        using var scope = _factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IReportContractReader>();
        var contract = await reader.GetReportContractAsync(journeyId);

        contract.Should().NotBeNull();
        contract!.JourneyId.Should().Be(journeyId);
        contract.ScoreDimensions.Should().Equal("journey_score", "stage_score", "touchpoint_score", "kpi_score");
        // The contract's detectionConfig is the journey-level pair (overrides resolve at read time,
        // they are not materialised into the contract DTO).
        contract.DetectionConfig.PainThreshold.Should().Be(40m);
        contract.DetectionConfig.HappyThreshold.Should().Be(75m);

        var stage = contract.Stages.Should().ContainSingle().Which;
        stage.StageId.Should().Be(stageId);
        stage.Touchpoints.Should().HaveCount(2);

        var measured = stage.Touchpoints.Single(t => t.TouchpointId == measuredId);
        measured.IsMeasured.Should().BeTrue();
        measured.KpiTypes.Should().BeEquivalentTo(["NPS", "CSAT"]);

        var unmeasured = stage.Touchpoints.Single(t => t.TouchpointId == unmeasuredId);
        unmeasured.IsMeasured.Should().BeFalse();
        unmeasured.KpiTypes.Should().BeEmpty();  // absent from the KPI dimension list (FR-008)

        // 5. An invalid save (pain >= happy) is rejected and writes nothing.
        var invalid = await client.PutAsJsonAsync(
            $"/api/v1/journeys/{journeyId}/detection",
            new { painThreshold = 80, happyThreshold = 75 });
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await invalid.ReadErrorCodeAsync()).Should().Be("detection.threshold_invalid");

        // The rejected save left the persisted config untouched (still 40/75).
        var afterInvalid = await (await client.GetAsync($"/api/v1/journeys/{journeyId}/detection")).ReadJsonAsync();
        afterInvalid.GetProperty("painThreshold").GetDecimal().Should().Be(40m);

        // Exactly one detection-config event was emitted in-band (the successful save); the rejected
        // invalid save contributed nothing.
        (await _factory.CountEventsAsync(actor.UserId, CustomerJourneyManagementEventTypes.JourneyDetectionConfigUpdated)).Should().Be(1);
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
