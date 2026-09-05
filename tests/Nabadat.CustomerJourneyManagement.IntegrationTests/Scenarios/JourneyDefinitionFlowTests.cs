using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Scenarios;

/// <summary>
/// US-1 business-cycle scenario (T034, <c>quickstart.md §3</c>): a P-01 author defines a
/// journey end-to-end and activates it. One test walks the whole journey-definition flow
/// and asserts the final state-of-the-world (full tree + status + emitted audit events),
/// matching the spec's <c>Independent Test</c>.
///
/// Two steps from the spec narrative are intentionally out of scope here:
/// <list type="bullet">
///   <item><b>KPI binding + measured-touchpoint</b> — the <c>PUT /touchpoints/{id}/kpis</c>
///   endpoint and the <c>isMeasured</c> flip land in US-2; this scenario asserts the
///   US-1 reality that every touchpoint is unmeasured.</item>
///   <item><b>P-03 → 403</b> — persona authorization is not yet enforced in M-16
///   (deferred to the M-10 authorization integration); the case is present below but
///   <c>Skip</c>ped. The published-interface smoke (quickstart §6) is likewise deferred:
///   IJourneyConfigReader/IReportContractReader are still NotImplemented stubs until
///   US-2/US-4.</item>
/// </list>
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class JourneyDefinitionFlowTests
{
    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public JourneyDefinitionFlowTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task P01_defines_journey_with_stages_and_touchpoints_then_activates_it()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");

        // 1. Create the journey → starts in Draft.
        var name = $"Customer Onboarding {Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name, description = "End-to-end onboarding", journeyType = "Onboarding" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await create.ReadJsonAsync();
        createBody.GetProperty("status").GetString().Should().Be("Draft");
        var journeyId = createBody.GetProperty("journeyId").GetGuid();

        // 2. Add 3 stages, each with one or two touchpoints.
        var stageNames = new[] { "Awareness", "Consideration", "Purchase" };
        foreach (var stageName in stageNames)
        {
            var stageId = (await AddStageAsync(client, journeyId, stageName));
            (await client.PostAsJsonAsync($"/api/v1/stages/{stageId}/touchpoints", new { name = $"{stageName} touchpoint" }))
                .StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // 3. GET returns the full structure: 3 stages, each carrying its touchpoint(s).
        var tree = await (await client.GetAsync($"/api/v1/journeys/{journeyId}")).ReadJsonAsync();
        var stages = tree.GetProperty("stages").EnumerateArray().ToList();
        stages.Should().HaveCount(3);
        stages.Select(s => s.GetProperty("name").GetString()).Should().Equal(stageNames);
        stages.Should().OnlyContain(s => s.GetProperty("touchpoints").GetArrayLength() >= 1);

        // 4. Every touchpoint is unmeasured in US-1 (no KPI bindings yet).
        var firstTouchpoint = stages[0].GetProperty("touchpoints").EnumerateArray().First();
        firstTouchpoint.GetProperty("isMeasured").GetBoolean().Should().BeFalse();

        // 5. P-01 activates the journey → Draft → Active.
        var activate = await client.PatchAsJsonAsync($"/api/v1/journeys/{journeyId}/status", new { status = "Active" });
        activate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await activate.ReadJsonAsync()).GetProperty("status").GetString().Should().Be("Active");

        // 6. The flow emitted its audit trail in-band (FR-015): one create + one status change.
        (await _factory.CountEventsAsync(actor.UserId, "journey.created")).Should().Be(1);
        (await _factory.CountEventsAsync(actor.UserId, "journey.status.changed")).Should().Be(1);
    }

    [Fact(Skip = "Persona authorization (P-03 cannot create journeys) is not yet enforced in M-16: " +
                 "no AddAuthorization/UseAuthorization pipeline or persona check exists. Deferred to the " +
                 "M-10 authorization integration. Un-skip once journey.write authorization lands.")]
    public async Task P03_is_forbidden_from_creating_a_journey()
    {
        var client = await _factory.SignedInClientAsync("P-03");

        var response = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name = $"Journey {Guid.NewGuid():N}", journeyType = "Onboarding" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<Guid> AddStageAsync(HttpClient client, Guid journeyId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/journeys/{journeyId}/stages", new { name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync()).GetProperty("stageId").GetGuid();
    }
}
