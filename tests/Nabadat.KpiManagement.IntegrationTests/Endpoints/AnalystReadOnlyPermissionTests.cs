using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Endpoints;

/// <summary>
/// US-7 persona-permission tests for the M-06 endpoints (T147, contracts/kpi-api.md). Proves the
/// <c>[RequirePermission(KpiConfiguration, …)]</c> gate: a P-02 Analyst — whose session snapshot
/// carries <c>KpiConfiguration: ["View"]</c> only — may READ any KPI (200) but every write
/// (<c>PUT</c> / <c>POST</c>) is refused with 403 <c>PERMISSION_DENIED</c> and the API-05 envelope,
/// because the snapshot lacks the <c>Manage</c> mode the write actions require (spec.md US-7
/// Integration Test Coverage).
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class AnalystReadOnlyPermissionTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public AnalystReadOnlyPermissionTests(KpiManagementApplicationFactory factory) => _factory = factory;

    private static string UniqueShortName(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    [Fact]
    public async Task GET_kpi_returns_200_when_actor_is_analyst()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxAnalyst);
        var npsId = await _factory.GetKpiIdByShortNameAsync("NPS");
        npsId.Should().NotBeNull();

        var response = await client.GetAsync($"/api/v1/kpis/{npsId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PUT_kpi_returns_403_permission_denied_when_actor_is_analyst()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxAnalyst);
        var npsId = await _factory.GetKpiIdByShortNameAsync("NPS");

        // The authorization filter short-circuits before the action runs, so the body never reaches
        // the immutability / scale-change logic — the analyst is refused for lacking Manage.
        var response = await client.PutAsJsonAsync($"/api/v1/kpis/{npsId}", KpiRequestBodies.Nps("NPS"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).Should().Be("PERMISSION_DENIED");
    }

    [Fact]
    public async Task POST_kpi_returns_403_permission_denied_when_actor_is_analyst()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxAnalyst);

        var response = await client.PostAsJsonAsync(
            "/api/v1/kpis", KpiRequestBodies.Custom(UniqueShortName("RO")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).Should().Be("PERMISSION_DENIED");
    }

    [Fact(Skip = "PATCH /api/v1/kpis/{id}/activation is a US-5 deliverable (T122) and is not yet " +
        "routed, so this call would 404/405 rather than 403. Re-enable once the activation endpoint " +
        "ships — it carries [RequirePermission(KpiConfiguration, Manage)], so the P-02 → 403 assertion " +
        "will hold the moment the route exists.")]
    public async Task PATCH_activation_returns_403_permission_denied_when_actor_is_analyst()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxAnalyst);
        var npsId = await _factory.GetKpiIdByShortNameAsync("NPS");

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/kpis/{npsId}/activation", new { active = false });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).Should().Be("PERMISSION_DENIED");
    }
}
