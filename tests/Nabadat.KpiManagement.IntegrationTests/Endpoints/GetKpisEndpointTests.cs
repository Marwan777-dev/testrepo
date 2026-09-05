using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for <c>GET /api/v1/kpis</c> (US-1, contracts/kpi-api.md). Each test enters the
/// real ASP.NET Core pipeline as an authenticated persona (seeded user → login → MFA verify → bearer
/// session). Covers the catalogue list (canonical order, type filter, search), the persona authority
/// model (P-02 reads; P-07 is forbidden), and the FR-002 invariant that NO delete route exists.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class GetKpisEndpointTests
{
    private static readonly string[] CanonicalOrder =
        ["NPS", "CSAT", "CES", "CXI", "FCR", "VFM", "AgentScore", "CHS"];

    private readonly KpiManagementApplicationFactory _factory;

    public GetKpisEndpointTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GET_kpis_returns_eight_standard_kpis_in_canonical_order_when_tenant_is_fresh()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.GetAsync("/api/v1/kpis");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Standards sort first in canonical order; any custom KPIs seeded by other tests in the
        // shared fixture sort after them, so the first eight rows are deterministically the standards.
        (await ReadShortNamesAsync(response)).Take(8).Should().Equal(CanonicalOrder);
    }

    [Fact]
    public async Task GET_kpis_returns_standard_active_subset_when_type_standard_and_active_only()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.GetAsync("/api/v1/kpis?type=Standard&active_only=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadShortNamesAsync(response)).Should().Equal(CanonicalOrder);
    }

    [Fact]
    public async Task GET_kpis_filters_to_nps_only_when_search_is_nps()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.GetAsync("/api/v1/kpis?search=NPS");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadShortNamesAsync(response)).Should().ContainSingle().Which.Should().Be("NPS");
    }

    [Fact]
    public async Task GET_kpis_returns_200_when_actor_is_analyst_P02()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxAnalyst);

        var response = await client.GetAsync("/api/v1/kpis");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_kpis_returns_403_with_envelope_when_persona_lacks_read_permission()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.TenantItAdministrator);

        var response = await client.GetAsync("/api/v1/kpis");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).Should().Be("PERMISSION_DENIED");
    }

    [Fact]
    public async Task GET_kpis_returns_401_with_envelope_when_request_has_no_session()
    {
        // No bearer token → the [Authorize] gate (PortalSession scheme) challenges with 401 and the
        // shared API-05 envelope, rather than each action null-checking the session by hand.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/kpis");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ReadErrorCodeAsync()).Should().Be("auth.required");
    }

    [Fact]
    public void No_DELETE_route_is_registered_for_kpis_per_FR_002()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        var deleteKpiRoutes = endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("api/v1/kpis", StringComparison.OrdinalIgnoreCase) == true)
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("DELETE") == true)
            .ToList();

        deleteKpiRoutes.Should().BeEmpty();
    }

    private static async Task<List<string>> ReadShortNamesAsync(HttpResponseMessage response)
    {
        var body = await response.ReadJsonAsync();
        return body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("short_name").GetString()!)
            .ToList();
    }
}
