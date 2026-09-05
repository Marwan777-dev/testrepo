using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Scenarios;

/// <summary>
/// T127 [US5] — the FR-026 deactivation-cascade business journey (spec.md US-5 Independent Test):
/// a CXI composite carries NPS + CSAT + CES; P-01 deactivates CSAT with <c>confirm=true</c>; reading
/// the CXI back shows CSAT gone and NPS/CES re-normalised to sum 100; and exactly ONE
/// <c>settings.changed</c> event was emitted whose payload's <c>action</c> is <c>deactivated</c> and
/// whose <c>cxi_side_effect</c> carries the recomputed map. Weights are seeded directly so the only
/// audit event in the journey is the deactivation itself. CSAT is a shared seeded standard, so it is
/// restored (and the composite cleared) in a <c>finally</c> for sibling tests in the collection.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class KpiDeactivationCascadeScenarioTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public KpiDeactivationCascadeScenarioTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task P01_deactivates_a_cxi_member_and_the_composite_drops_it_and_renormalises()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);
        var cxiId = (await _factory.GetKpiIdByShortNameAsync("CXI"))!.Value;
        var npsId = (await _factory.GetKpiIdByShortNameAsync("NPS"))!.Value;
        var csatId = (await _factory.GetKpiIdByShortNameAsync("CSAT"))!.Value;
        var cesId = (await _factory.GetKpiIdByShortNameAsync("CES"))!.Value;

        try
        {
            // Compose CXI = NPS(4) + CSAT(3) + CES(3).
            await _factory.ClearCxiWeightsAsync(cxiId);
            await _factory.SeedCxiWeightAsync(cxiId, npsId, 4);
            await _factory.SeedCxiWeightAsync(cxiId, csatId, 3);
            await _factory.SeedCxiWeightAsync(cxiId, cesId, 3);

            // P-01 deactivates CSAT, confirming the cascade.
            var response = await client.PatchAsJsonAsync(
                $"/api/v1/kpis/{csatId}/activation", new { active = false, confirm = true });
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Reading the CXI back: CSAT is gone; NPS + CES re-normalise to 100 (±0.1).
            var read = await (await client.GetAsync($"/api/v1/kpis/{cxiId}")).ReadJsonAsync();
            var weights = read.GetProperty("cxi_weights").EnumerateArray().ToList();
            weights.Select(w => w.GetProperty("member_kpi_id").GetGuid())
                .Should().BeEquivalentTo(new[] { npsId, cesId });
            weights.Sum(w => w.GetProperty("effective_percentage").GetDecimal())
                .Should().BeApproximately(100m, 0.1m);

            // Exactly one settings.changed event, action = deactivated, with the recomputed cascade map.
            (await _factory.CountEventsAsync(actor.UserId, "settings.changed")).Should().Be(1);
            var newValue = await _factory.LatestEventNewValueAsync(actor.UserId, "settings.changed");
            newValue.Should().NotBeNull();
            using var doc = JsonDocument.Parse(newValue!);
            doc.RootElement.GetProperty("action").GetString().Should().Be("deactivated");
            var sideEffect = doc.RootElement.GetProperty("cxi_side_effect").EnumerateArray().Single();
            sideEffect.GetProperty("cxi_kpi_id").GetGuid().Should().Be(cxiId);
            sideEffect.GetProperty("removed_member_kpi_id").GetGuid().Should().Be(csatId);
            sideEffect.GetProperty("effective_percentages").EnumerateArray()
                .Select(p => p.GetProperty("member_kpi_id").GetGuid())
                .Should().BeEquivalentTo(new[] { npsId, cesId });
        }
        finally
        {
            await _factory.ClearCxiWeightsAsync(cxiId);
            await _factory.SetKpiActiveByShortNameAsync("CSAT", true);
        }
    }
}
