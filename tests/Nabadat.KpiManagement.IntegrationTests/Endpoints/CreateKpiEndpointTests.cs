using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for <c>POST /api/v1/kpis</c> (US-2, contracts/kpi-api.md). A valid create returns
/// 201 with the full configuration, persists the row (visible via GET), and emits exactly one
/// <c>settings.changed</c> event. Duplicate Short Name and the reserved calculation methods
/// (NPSStandard / WeightedComposite, which custom KPIs may not select) map to their documented 400
/// codes.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class CreateKpiEndpointTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public CreateKpiEndpointTests(KpiManagementApplicationFactory factory) => _factory = factory;

    private static string UniqueShortName(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    [Fact]
    public async Task POST_kpis_returns_201_persists_row_and_emits_one_event_when_input_is_valid()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);
        var shortName = UniqueShortName("QUAL");

        var response = await client.PostAsJsonAsync("/api/v1/kpis", KpiRequestBodies.Custom(shortName));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("short_name").GetString().Should().Be(shortName);
        var id = body.GetProperty("id").GetString();

        // Row is visible through the read endpoint.
        var get = await client.GetAsync($"/api/v1/kpis/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        // Exactly one settings.changed event was appended for this actor (data-model §8).
        (await _factory.CountEventsAsync(actor.UserId, "settings.changed")).Should().Be(1);
    }

    [Fact]
    public async Task POST_kpis_returns_400_short_name_duplicate_when_short_name_already_exists()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var shortName = UniqueShortName("DUP");
        await _factory.SeedCustomKpiAsync(shortName, "Original custom KPI");

        var response = await client.PostAsJsonAsync("/api/v1/kpis", KpiRequestBodies.Custom(shortName));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("KPI_SHORT_NAME_DUPLICATE");
    }

    [Fact]
    public async Task POST_kpis_returns_400_calculation_method_reserved_when_custom_picks_nps_standard()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.PostAsJsonAsync(
            "/api/v1/kpis",
            KpiRequestBodies.Custom(UniqueShortName("RSV"), calculationMethod: 2)); // NPSStandard

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("KPI_CALCULATION_METHOD_RESERVED");
    }

    [Fact]
    public async Task POST_kpis_returns_400_calculation_method_reserved_when_custom_picks_weighted_composite()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.PostAsJsonAsync(
            "/api/v1/kpis",
            KpiRequestBodies.Custom(UniqueShortName("RSV"), calculationMethod: 3)); // WeightedComposite

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("KPI_CALCULATION_METHOD_RESERVED");
    }
}
