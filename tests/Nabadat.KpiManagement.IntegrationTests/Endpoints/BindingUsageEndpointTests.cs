using System.Net;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for <c>GET /api/v1/kpis/{id}/binding-usage</c> (US-2, contracts/kpi-api.md). The
/// endpoint delegates to M-16's published <c>IJourneyBindingQuery</c> via <c>KpiBindingUsageProbe</c>
/// and returns <c>{touchpoint_count, journey_count}</c>. A bound KPI reports its usage; an unbound
/// KPI reports zeros; a missing id is 404.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class BindingUsageEndpointTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public BindingUsageEndpointTests(KpiManagementApplicationFactory factory) => _factory = factory;

    private static string UniqueShortName(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    [Fact]
    public async Task GET_binding_usage_returns_one_touchpoint_and_one_journey_when_kpi_is_bound()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var kpiId = await _factory.SeedCustomKpiAsync(UniqueShortName("USE"), "Bound usage KPI");
        await _factory.SeedBoundTouchpointAsync(kpiId);

        var response = await client.GetAsync($"/api/v1/kpis/{kpiId}/binding-usage");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("touchpoint_count").GetInt32().Should().Be(1);
        body.GetProperty("journey_count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GET_binding_usage_returns_zeros_when_kpi_is_unbound()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var kpiId = await _factory.SeedCustomKpiAsync(UniqueShortName("UNB"), "Unbound usage KPI");

        var response = await client.GetAsync($"/api/v1/kpis/{kpiId}/binding-usage");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("touchpoint_count").GetInt32().Should().Be(0);
        body.GetProperty("journey_count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GET_binding_usage_returns_404_when_kpi_does_not_exist()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.GetAsync($"/api/v1/kpis/{Guid.NewGuid()}/binding-usage");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).Should().Be("KPI_NOT_FOUND");
    }
}
