using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Endpoints;

/// <summary>
/// T144 [US6] — HTTP tests for <c>GET</c>/<c>PUT /api/v1/tenant/organization</c> (contracts/settings-api.md).
/// Enters the real ASP.NET Core pipeline as an authenticated persona. Covers the seeded-defaults GET,
/// a valid PUT (200 + exactly one <c>settings.changed</c> event), and the two 400 validation paths.
/// Each test resets the singleton row first so the shared container gives a deterministic baseline.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class OrganizationEndpointTests
{
    private static readonly string[] CanonicalIndustries =
        ["Banking", "Telecommunications", "Government", "Automotive", "Entertainment", "Services"];

    private readonly KpiManagementApplicationFactory _factory;

    public OrganizationEndpointTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GET_organization_returns_seeded_defaults_when_tenant_is_fresh()
    {
        await _factory.ResetOrganizationAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.GetAsync("/api/v1/tenant/organization");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("name").GetString().Should().Be("My Organization");
        body.GetProperty("industry").GetString().Should().Be("Services");
        body.GetProperty("logo").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        body.GetProperty("industry_options").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Equal(CanonicalIndustries);
    }

    [Fact]
    public async Task PUT_organization_returns_200_and_emits_one_event_when_payload_is_valid()
    {
        await _factory.ResetOrganizationAsync();
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.PutAsJsonAsync(
            "/api/v1/tenant/organization", new { name = "Acme Bank", industry = "Banking" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("name").GetString().Should().Be("Acme Bank");
        body.GetProperty("industry").GetString().Should().Be("Banking");
        (await _factory.CountEventsAsync(actor.UserId, "settings.changed")).Should().Be(1);
    }

    [Fact]
    public async Task PUT_organization_returns_400_organization_name_required_when_name_is_empty()
    {
        await _factory.ResetOrganizationAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.PutAsJsonAsync(
            "/api/v1/tenant/organization", new { name = "", industry = "Banking" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("ORGANIZATION_NAME_REQUIRED");
    }

    [Fact]
    public async Task PUT_organization_returns_400_organization_industry_unknown_when_industry_is_not_canonical()
    {
        await _factory.ResetOrganizationAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.PutAsJsonAsync(
            "/api/v1/tenant/organization", new { name = "Acme Bank", industry = "Aerospace" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("ORGANIZATION_INDUSTRY_UNKNOWN");
    }
}
