using System.Net;
using System.Text.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level coverage for the standard-catalogue business rules over <c>GET /api/v1/kpis</c>:
/// <list type="bullet">
///   <item>BR-1.1 — the eight standard KPIs are always present regardless of status and cannot be
///   removed (deactivation only hides them from the active view; the no-DELETE-route invariant is
///   asserted by <see cref="GetKpisEndpointTests.No_DELETE_route_is_registered_for_kpis_per_FR_002"/>).</item>
///   <item>BR-1.3 — the "Active KPIs" header count counts every KPI with status = Active, including
///   the composite CXI; this test proves CXI is part of the active set the count is computed from.</item>
/// </list>
/// Runs in the shared fixture as the CX Program Manager (P-01).
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class StandardKpiCatalogueRulesTests
{
    private static readonly string[] CanonicalOrder =
        ["NPS", "CSAT", "CES", "CXI", "FCR", "VFM", "AgentScore", "CHS"];

    private readonly KpiManagementApplicationFactory _factory;

    public StandardKpiCatalogueRulesTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact] // BR-1.1
    public async Task GET_kpis_keeps_all_eight_standards_in_catalogue_when_a_standard_is_deactivated()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        try
        {
            await _factory.SetKpiActiveByShortNameAsync("CSAT", active: false);

            // Regardless of status, the full catalogue still lists all eight standards in canonical
            // order — the deactivated CSAT is hidden from the active view, never removed.
            var full = await client.GetAsync("/api/v1/kpis?active_only=false");
            full.StatusCode.Should().Be(HttpStatusCode.OK);
            var standardsInFull = (await ReadShortNamesAsync(full)).Where(CanonicalOrder.Contains).ToList();
            standardsInFull.Should().Equal(CanonicalOrder);

            // The active-only view excludes the deactivated standard but still carries the others,
            // proving CSAT was deactivated (not deleted).
            var activeView = await ReadShortNamesAsync(await client.GetAsync("/api/v1/kpis?active_only=true"));
            activeView.Should().NotContain("CSAT");
            activeView.Should().Contain("NPS");
        }
        finally
        {
            // Restore the shared fixture so sibling tests still see all eight standards active.
            await _factory.SetKpiActiveByShortNameAsync("CSAT", active: true);
        }
    }

    [Fact] // BR-1.3
    public async Task GET_kpis_active_view_includes_the_composite_cxi_when_tenant_is_fresh()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.GetAsync("/api/v1/kpis?active_only=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await response.ReadJsonAsync()).GetProperty("items").EnumerateArray().ToList();

        // The header's "Active KPIs" count is the size of this active set. The composite CXI must be
        // counted like any other active KPI — so it appears here with is_composite = true.
        var cxi = items.SingleOrDefault(i => i.GetProperty("short_name").GetString() == "CXI");
        cxi.ValueKind.Should().Be(JsonValueKind.Object, "the active set must include the composite CXI");
        cxi.GetProperty("is_composite").GetBoolean().Should().BeTrue();
        cxi.GetProperty("is_active").GetBoolean().Should().BeTrue();
    }

    private static async Task<List<string>> ReadShortNamesAsync(HttpResponseMessage response)
    {
        var body = await response.ReadJsonAsync();
        return body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("short_name").GetString()!)
            .ToList();
    }
}
