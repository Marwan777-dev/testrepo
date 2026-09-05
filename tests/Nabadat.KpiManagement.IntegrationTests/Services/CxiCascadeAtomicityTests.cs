using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Services;

/// <summary>
/// T126 [US5] — atomicity + cascade coverage for the FR-026 deactivation (research.md R5). A custom
/// member of the seeded <c>CXI</c> composite is deactivated with <c>confirm=true</c>; we assert the
/// single-transaction guarantees: exactly ONE <c>settings.changed</c> event carrying the nested
/// <c>cxi_side_effect</c> for the affected CXI, the member's <c>cxi_weights</c> row deleted, and the
/// CXI's remaining effective percentages re-normalised to sum 100 (±0.1, SC-004) on the next read.
/// <para>Weights are seeded directly (not via the PUT-weights endpoint) so the only audit event the
/// actor accrues is the deactivation itself.</para>
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class CxiCascadeAtomicityTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public CxiCascadeAtomicityTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PATCH_activation_deactivating_a_cxi_member_cascades_in_one_event_and_renormalises_the_rest()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);
        var cxiId = (await _factory.GetKpiIdByShortNameAsync("CXI"))!.Value;

        var keepA = await _factory.SeedCustomKpiAsync("CASA" + Guid.NewGuid().ToString("N")[..6], "Cascade member A");
        var keepB = await _factory.SeedCustomKpiAsync("CASB" + Guid.NewGuid().ToString("N")[..6], "Cascade member B");
        var dropped = await _factory.SeedCustomKpiAsync("CASD" + Guid.NewGuid().ToString("N")[..6], "Cascade dropped");

        try
        {
            await _factory.ClearCxiWeightsAsync(cxiId);
            await _factory.SeedCxiWeightAsync(cxiId, keepA, 2);
            await _factory.SeedCxiWeightAsync(cxiId, keepB, 3);
            await _factory.SeedCxiWeightAsync(cxiId, dropped, 5);

            var response = await client.PatchAsJsonAsync(
                $"/api/v1/kpis/{dropped}/activation", new { active = false, confirm = true });
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Exactly one event, and its payload carries the cascade for this CXI.
            (await _factory.CountEventsAsync(actor.UserId, "settings.changed")).Should().Be(1);
            var newValue = await _factory.LatestEventNewValueAsync(actor.UserId, "settings.changed");
            newValue.Should().NotBeNull();
            using var doc = JsonDocument.Parse(newValue!);
            var sideEffects = doc.RootElement.GetProperty("cxi_side_effect").EnumerateArray().ToList();
            sideEffects.Should().ContainSingle();
            sideEffects[0].GetProperty("cxi_kpi_id").GetGuid().Should().Be(cxiId);
            sideEffects[0].GetProperty("removed_member_kpi_id").GetGuid().Should().Be(dropped);

            // The dropped member's weight row is gone; only the survivors remain.
            var members = await _factory.ListCxiWeightMembersAsync(cxiId);
            members.Should().BeEquivalentTo(new[] { keepA, keepB });

            // The survivors' effective percentages re-normalise to 100 (±0.1) on the next read.
            var read = await (await client.GetAsync($"/api/v1/kpis/{cxiId}")).ReadJsonAsync();
            var weights = read.GetProperty("cxi_weights").EnumerateArray().ToList();
            weights.Select(w => w.GetProperty("member_kpi_id").GetGuid()).Should().NotContain(dropped);
            weights.Sum(w => w.GetProperty("effective_percentage").GetDecimal()).Should().BeApproximately(100m, 0.1m);
        }
        finally
        {
            await _factory.ClearCxiWeightsAsync(cxiId);
        }
    }
}
