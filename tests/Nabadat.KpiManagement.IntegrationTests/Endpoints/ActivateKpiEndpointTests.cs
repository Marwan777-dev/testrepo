using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Endpoints;

/// <summary>
/// T125 [US5] — HTTP-level tests for <c>PATCH /api/v1/kpis/{id}/activation</c> (FR-026,
/// contracts/kpi-api.md). Covers the four documented outcomes: deactivating an unbound KPI succeeds
/// with one event; deactivating a bound KPI without <c>confirm</c> is gated with 409 + the
/// binding-usage counts; a confirmed deactivation succeeds and forces Show-on-Dashboard off; a
/// persona without the Manage grant is forbidden.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class ActivateKpiEndpointTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public ActivateKpiEndpointTests(KpiManagementApplicationFactory factory) => _factory = factory;

    private static object Body(bool active, bool confirm) => new { active, confirm };

    [Fact]
    public async Task PATCH_activation_returns_200_and_emits_one_event_when_deactivating_an_unbound_kpi()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);
        var id = await _factory.SeedCustomKpiAsync("ACTU" + Guid.NewGuid().ToString("N")[..6], "Unbound KPI");

        var response = await client.PatchAsJsonAsync($"/api/v1/kpis/{id}/activation", Body(active: false, confirm: false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("is_active").GetBoolean().Should().BeFalse();
        (await _factory.CountEventsAsync(actor.UserId, "settings.changed")).Should().Be(1);
    }

    [Fact]
    public async Task PATCH_activation_returns_409_with_binding_counts_when_deactivating_a_bound_kpi_without_confirm()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);
        var id = await _factory.SeedCustomKpiAsync("ACTB" + Guid.NewGuid().ToString("N")[..6], "Bound KPI");
        await _factory.SeedBoundTouchpointAsync(id);

        var response = await client.PatchAsJsonAsync($"/api/v1/kpis/{id}/activation", Body(active: false, confirm: false));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.ReadJsonAsync();
        body.GetProperty("error").GetProperty("code").GetString().Should().Be("KPI_DEACTIVATION_REQUIRES_CONFIRMATION");
        body.GetProperty("touchpoint_count").GetInt32().Should().Be(1);
        body.GetProperty("journey_count").GetInt32().Should().Be(1);
        // The gated path must not write anything.
        (await _factory.CountEventsAsync(actor.UserId, "settings.changed")).Should().Be(0);
    }

    [Fact]
    public async Task PATCH_activation_with_confirm_succeeds_and_forces_show_on_dashboard_off()
    {
        var (client, _) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);
        var id = await _factory.SeedCustomKpiAsync("ACTC" + Guid.NewGuid().ToString("N")[..6], "Confirmed KPI");
        await _factory.SeedBoundTouchpointAsync(id);
        await _factory.SetShowOnDashboardAsync(id, true);

        var response = await client.PatchAsJsonAsync($"/api/v1/kpis/{id}/activation", Body(active: false, confirm: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var read = await (await client.GetAsync($"/api/v1/kpis/{id}")).ReadJsonAsync();
        read.GetProperty("is_active").GetBoolean().Should().BeFalse();
        read.GetProperty("show_on_dashboard").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task PATCH_activation_returns_403_when_persona_lacks_manage_permission()
    {
        var managerId = await _factory.SeedCustomKpiAsync("ACTF" + Guid.NewGuid().ToString("N")[..6], "Forbidden KPI");
        // P-07 (non-CX admin) gets no KpiConfiguration grant → the default-deny gate forbids the write.
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.TenantItAdministrator);

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/kpis/{managerId}/activation", Body(active: false, confirm: true));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
