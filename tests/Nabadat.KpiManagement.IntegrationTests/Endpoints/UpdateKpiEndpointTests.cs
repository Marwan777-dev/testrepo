using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for <c>PUT /api/v1/kpis/{id}</c> (US-2, contracts/kpi-api.md). Covers the
/// standard-KPI immutability locks on the seeded NPS row (Short Name FR-004; Scale + Calculation
/// Method FR-005) and the FR-017 scale-change confirmation gate: changing a bound custom KPI's scale
/// without <c>confirm_structural_change=true</c> returns 409 with the affected-binding counts; with
/// the flag it succeeds.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class UpdateKpiEndpointTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public UpdateKpiEndpointTests(KpiManagementApplicationFactory factory) => _factory = factory;

    private static string UniqueShortName(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    [Fact]
    public async Task PUT_kpis_returns_400_short_name_immutable_when_changing_nps_short_name()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var npsId = await _factory.GetKpiIdByShortNameAsync("NPS");
        npsId.Should().NotBeNull();

        // Same scale + calc as NPS (so the standard-field lock isn't what fires) — only Short Name changes.
        var response = await client.PutAsJsonAsync($"/api/v1/kpis/{npsId}", KpiRequestBodies.Nps("NPSCHANGED"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("KPI_SHORT_NAME_IMMUTABLE");
    }

    [Fact]
    public async Task PUT_kpis_returns_400_field_immutable_when_changing_nps_calculation_method()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var npsId = await _factory.GetKpiIdByShortNameAsync("NPS");

        // Short Name unchanged ("NPS"); calculation method changed NPSStandard → WeightedAverage.
        var response = await client.PutAsJsonAsync(
            $"/api/v1/kpis/{npsId}", KpiRequestBodies.Nps("NPS", calculationMethod: 0));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("KPI_FIELD_IMMUTABLE_FOR_STANDARD");
    }

    [Fact]
    public async Task PUT_kpis_returns_400_field_immutable_when_changing_nps_scale()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var npsId = await _factory.GetKpiIdByShortNameAsync("NPS");

        // NPS is unbound in the fixture, so the scale-change gate passes (0 bindings) and the
        // standard-field lock is what fires. Short Name + calc unchanged; scale Scale0_10 → Scale1_5.
        var response = await client.PutAsJsonAsync(
            $"/api/v1/kpis/{npsId}", KpiRequestBodies.Nps("NPS", scale: 2));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("KPI_FIELD_IMMUTABLE_FOR_STANDARD");
    }

    [Fact]
    public async Task PUT_kpis_returns_409_then_200_for_a_bound_custom_kpi_scale_change()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var shortName = UniqueShortName("BND");
        var kpiId = await _factory.SeedCustomKpiAsync(shortName, "Bound custom KPI", scale: "Scale1_5");
        await _factory.SeedBoundTouchpointAsync(kpiId);

        // Scale1_5 → Scale1_7 without confirmation → 409 with the affected-binding counts.
        var conflict = await client.PutAsJsonAsync(
            $"/api/v1/kpis/{kpiId}", KpiRequestBodies.Custom(shortName, scale: 3));

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await conflict.ReadJsonAsync();
        body.GetProperty("error").GetProperty("code").GetString().Should().Be("KPI_SCALE_CHANGE_AFFECTS_BINDINGS");
        body.GetProperty("affected_touchpoints").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("affected_journeys").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        // With the confirmation flag the same change succeeds.
        var confirmed = await client.PutAsJsonAsync(
            $"/api/v1/kpis/{kpiId}?confirm_structural_change=true", KpiRequestBodies.Custom(shortName, scale: 3));

        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
