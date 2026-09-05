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
/// US-2 business-cycle scenario (T054, <c>quickstart.md §3</c>): a journey author configures KPI
/// weights and the strategic scoring model on a touchpoint, then M-06 reads that configuration back
/// through the published <see cref="IJourneyConfigReader"/>. One test walks the whole flow and asserts
/// the final state-of-the-world — persisted bindings, the scoring config, the in-band audit trail, and
/// the cross-module read — matching the spec's <c>Independent Test</c>:
/// <list type="number">
///   <item><description>save weights summing to 85% → rejected with <c>kpi.weight_sum_invalid</c> (422);</description></item>
///   <item><description>correct to 100% → bindings persist, response carries <c>isMeasured: true</c> and
///   the non-blocking <c>npsWarning: true</c> (NPS is in the set);</description></item>
///   <item><description>configure the <b>tenant-level</b> scoring parameters via the published
///   <see cref="IScoringConfigStore"/> (SRS §4.2.9 / §11.7, Q11: per-tenant, not per-journey),
///   round-tripped through <c>GetAsync</c> with β derived as 1 − α; and</description></item>
///   <item><description><see cref="IJourneyConfigReader.GetJourneyConfigAsync"/> returns the journey
///   config with the correct KPI types, weights, and resolved scoring directions (scoring is no longer
///   embedded in the journey config — it is tenant-level).</description></item>
/// </list>
/// The final aggregate audit check (exactly one <c>journey.kpi_bindings.updated</c> and one
/// <c>journey.scoring_config.updated</c>) doubles as proof that the rejected 85% save emitted nothing.
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class KpiAndScoringConfigurationTests
{
    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public KpiAndScoringConfigurationTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Author_configures_kpi_weights_and_scoring_then_journey_config_reader_exposes_it()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");

        // 1. Build a journey → stage → touchpoint to configure.
        var journeyId = await CreateJourneyAsync(client);
        var stageId = await AddStageAsync(client, journeyId);
        var touchpointId = await AddTouchpointAsync(client, stageId);

        // 2. Save KPI weights summing to 85% → rejected, no bindings written.
        var invalid = await client.PutAsJsonAsync(
            $"/api/v1/touchpoints/{touchpointId}/kpis",
            new { kpiBindings = new[] { new { kpiType = "NPS", weight = 45 }, new { kpiType = "CSAT", weight = 40 } } });
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await invalid.ReadErrorCodeAsync()).Should().Be("kpi.weight_sum_invalid");

        // 3. Correct to 100% → bindings persist; isMeasured flips true; NPS raises the non-blocking warning.
        var valid = await client.PutAsJsonAsync(
            $"/api/v1/touchpoints/{touchpointId}/kpis",
            new { kpiBindings = new[] { new { kpiType = "NPS", weight = 60 }, new { kpiType = "CSAT", weight = 40 } } });
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
        var savedBody = await valid.ReadJsonAsync();
        savedBody.GetProperty("isMeasured").GetBoolean().Should().BeTrue();
        savedBody.GetProperty("npsWarning").GetBoolean().Should().BeTrue();
        var savedBindings = savedBody.GetProperty("kpiBindings").EnumerateArray()
            .ToDictionary(b => b.GetProperty("kpiType").GetString()!, b => b.GetProperty("weight").GetDecimal());
        savedBindings.Should().HaveCount(2);
        savedBindings.Should().Contain("NPS", 60m).And.Contain("CSAT", 40m);

        // 4. Configure the TENANT-level strategic scoring parameters via the published IScoringConfigStore
        //    (SRS §4.2.9 / §11.7, Q11: scoring is per-tenant, not per-journey — there is no journey-level
        //    scoring endpoint; feature 003's Settings → Customer Journey page is the editing surface).
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IScoringConfigStore>();

        var update = new ScoringConfigUpdate(Alpha: 0.700m, MotMultiplier: 1.5m, NFloor: 100, FlagPercentile: 25, RollingWindowDays: 30);
        var storeActor = new ScoringConfigActor(actor.UserId, "P-01", Guid.NewGuid());
        var updateResult = await store.UpdateAsync(update, storeActor);
        updateResult.IsSuccess.Should().BeTrue();

        // GET round-trips the singleton; β is derived (1 − α) and shared by every journey in the tenant.
        var scoring = await store.GetAsync();
        scoring.Alpha.Should().Be(0.700m);
        scoring.Beta.Should().Be(0.300m);
        scoring.MotMultiplier.Should().Be(1.5m);
        scoring.NFloor.Should().Be(100);
        scoring.FlagPercentile.Should().Be(25);
        scoring.RollingWindowDays.Should().Be(30);

        // 5. Published-interface smoke (quickstart §6): M-06 reads the journey config through the
        //    in-process IJourneyConfigReader. The journey config no longer carries scoring (it is
        //    tenant-level, read separately via IScoringConfigStore) — only KPI bindings + structure.
        var configReader = scope.ServiceProvider.GetRequiredService<IJourneyConfigReader>();
        var config = await configReader.GetJourneyConfigAsync(journeyId);

        config.Should().NotBeNull();

        var touchpoint = config!.Stages.Should().ContainSingle().Which
            .Touchpoints.Should().ContainSingle().Which;
        touchpoint.TouchpointId.Should().Be(touchpointId);
        touchpoint.IsMeasured.Should().BeTrue();
        touchpoint.KpiBindings.Should().HaveCount(2);

        var nps = touchpoint.KpiBindings.Single(b => b.KpiType == "NPS");
        nps.Weight.Should().Be(60m);
        nps.IsPlatformStandard.Should().BeTrue();
        nps.ScoringDirection.Should().Be(ScoringDirection.Ascending);

        var csat = touchpoint.KpiBindings.Single(b => b.KpiType == "CSAT");
        csat.Weight.Should().Be(40m);
        csat.IsPlatformStandard.Should().BeTrue();

        // 6. The flow emitted its audit trail in-band, and only for the writes that succeeded: the
        //    rejected 85% save contributed nothing.
        (await _factory.CountEventsAsync(actor.UserId, CustomerJourneyManagementEventTypes.JourneyKpiBindingsUpdated)).Should().Be(1);
        (await _factory.CountEventsAsync(actor.UserId, CustomerJourneyManagementEventTypes.JourneyScoringConfigUpdated)).Should().Be(1);
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

    private static async Task<Guid> AddTouchpointAsync(HttpClient client, Guid stageId)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/stages/{stageId}/touchpoints", new { name = "Landing page" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync()).GetProperty("touchpointId").GetGuid();
    }
}
