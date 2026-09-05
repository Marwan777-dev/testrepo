using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.KpiManagement.Application.Kpis.Dtos;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Scenarios;

/// <summary>
/// Multi-step business-cycle test for the US-3 CXI configure-then-rebalance journey (Independent
/// Test). A P-01 CX Program Manager configures the seeded CXI with three members (NPS=3, CSAT=2,
/// CES=1), reads the snapshot composer's <c>member_breakdown</c> and confirms the 50 / 33.3 / 16.7
/// effective split, then rebalances via a full-replace that drops CSAT, and confirms the next
/// snapshot read recomputes to NPS + CES at 75 / 25 (CSAT removed, remaining proportions still summing
/// to 100 ±0.1).
///
/// <para>NB — the rebalance here is driven through the weights endpoint's full-replace semantics, the
/// member-removal path US-3 actually ships. Removing a member by <em>deactivating its KPI elsewhere</em>
/// (the <c>PATCH …/activation</c> cascade) is a US-5 deliverable (T122/T127) — its scenario lives in
/// <c>KpiDeactivationCascadeScenarioTests</c>.</para>
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class CxiConfiguresAndRebalancesScenarioTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public CxiConfiguresAndRebalancesScenarioTests(KpiManagementApplicationFactory factory) => _factory = factory;

    /// <summary>Reads the single composite's M-07 snapshot via the in-process reader (no HTTP endpoint exists).</summary>
    private async Task<CxiSnapshotDto> ReadSnapshotAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IKpiConfigReader>();
        var snapshot = await reader.GetCxiSnapshotAsync();
        snapshot.Should().NotBeNull();
        return snapshot!;
    }

    [Fact]
    public async Task Cxi_configures_three_members_then_rebalances_to_two_and_breakdown_recomputes_to_100()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var cxiId = await _factory.GetKpiIdByShortNameAsync("CXI");
        var npsId = await _factory.GetKpiIdByShortNameAsync("NPS");
        var csatId = await _factory.GetKpiIdByShortNameAsync("CSAT");
        var cesId = await _factory.GetKpiIdByShortNameAsync("CES");
        cxiId.Should().NotBeNull();
        npsId.Should().NotBeNull();
        csatId.Should().NotBeNull();
        cesId.Should().NotBeNull();

        // 1. Configure NPS=3, CSAT=2, CES=1 (6 relative units → 50 / 33.3 / 16.7).
        var configure = await client.PutAsJsonAsync($"/api/v1/kpis/{cxiId}/weights", new
        {
            weights = new[]
            {
                new { member_kpi_id = npsId!.Value, weight = 3 },
                new { member_kpi_id = csatId!.Value, weight = 2 },
                new { member_kpi_id = cesId!.Value, weight = 1 },
            },
        });
        configure.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. The snapshot composer's member_breakdown carries all three members summing to 100.
        var first = await ReadSnapshotAsync();
        first.MemberBreakdown.Should().HaveCount(3);
        first.MemberBreakdown.Sum(m => m.EffectivePercentage).Should().BeApproximately(100m, 0.1m);
        first.MemberBreakdown.Single(m => m.KpiShortName == "NPS").EffectivePercentage
            .Should().BeApproximately(50.0m, 0.05m);
        first.MemberBreakdown.Single(m => m.KpiShortName == "CSAT").EffectivePercentage
            .Should().BeApproximately(33.3m, 0.05m);
        first.MemberBreakdown.Single(m => m.KpiShortName == "CES").EffectivePercentage
            .Should().BeApproximately(16.7m, 0.05m);

        // 3. Rebalance via full-replace: drop CSAT, leaving NPS=3 + CES=1 (4 units → 75 / 25).
        var rebalance = await client.PutAsJsonAsync($"/api/v1/kpis/{cxiId}/weights", new
        {
            weights = new[]
            {
                new { member_kpi_id = npsId.Value, weight = 3 },
                new { member_kpi_id = cesId.Value, weight = 1 },
            },
        });
        rebalance.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. The next snapshot read recomputes: CSAT is gone, NPS/CES proportions still sum to 100.
        var second = await ReadSnapshotAsync();
        second.MemberBreakdown.Should().HaveCount(2);
        second.MemberBreakdown.Should().NotContain(m => m.KpiShortName == "CSAT");
        second.MemberBreakdown.Sum(m => m.EffectivePercentage).Should().BeApproximately(100m, 0.1m);
        second.MemberBreakdown.Single(m => m.KpiShortName == "NPS").EffectivePercentage
            .Should().BeApproximately(75.0m, 0.05m);
        second.MemberBreakdown.Single(m => m.KpiShortName == "CES").EffectivePercentage
            .Should().BeApproximately(25.0m, 0.05m);
    }
}
