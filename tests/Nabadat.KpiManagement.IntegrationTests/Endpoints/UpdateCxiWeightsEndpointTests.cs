using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for <c>PUT /api/v1/kpis/{cxi_id}/weights</c> (US-3, contracts/kpi-api.md). The
/// seeded composite <c>CXI</c> row is active, so the FR-043 "≥2 weighted members" gate applies.
/// Covers the three weight-update outcomes that the US-3 surface implements today: a valid full
/// replace (200 + one <c>settings.changed</c> event), the insufficient-members rejection, and the
/// cannot-include-itself rejection. The member-active guard (<c>CXI_MEMBER_NOT_ACTIVE</c>) stands in
/// for the spec's "a deactivated member is excluded" assertion at the level US-3 actually enforces it
/// — see the skipped <see cref="PATCH_activation_excludes_inactive_member_from_next_cxi_read"/> for
/// why the <c>PATCH …/activation</c> cascade itself is a US-5 deliverable.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class UpdateCxiWeightsEndpointTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public UpdateCxiWeightsEndpointTests(KpiManagementApplicationFactory factory) => _factory = factory;

    /// <summary>Builds the snake_case weights body: <c>{ "weights": [{ member_kpi_id, weight }, …] }</c>.</summary>
    private static object WeightsBody(params (Guid Id, int Weight)[] items) => new
    {
        weights = items.Select(i => new { member_kpi_id = i.Id, weight = i.Weight }).ToArray(),
    };

    [Fact]
    public async Task PUT_kpis_weights_returns_200_and_emits_one_event_when_weights_are_valid()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);
        var cxiId = await _factory.GetKpiIdByShortNameAsync("CXI");
        var npsId = await _factory.GetKpiIdByShortNameAsync("NPS");
        var csatId = await _factory.GetKpiIdByShortNameAsync("CSAT");
        cxiId.Should().NotBeNull();
        npsId.Should().NotBeNull();
        csatId.Should().NotBeNull();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/kpis/{cxiId}/weights", WeightsBody((npsId!.Value, 3), (csatId!.Value, 2)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var weights = (await response.ReadJsonAsync()).GetProperty("weights").EnumerateArray().ToList();
        weights.Should().HaveCount(2);
        weights.Sum(w => w.GetProperty("effective_percentage").GetDecimal())
            .Should().BeApproximately(100m, 0.1m);
        weights.Single(w => w.GetProperty("member_kpi_id").GetGuid() == npsId.Value)
            .GetProperty("weight").GetInt32().Should().Be(3);

        // The fresh actor's audit log carries exactly one event from this single full-replace save.
        (await _factory.CountEventsAsync(actor.UserId, "settings.changed")).Should().Be(1);
    }

    [Fact]
    public async Task PUT_kpis_weights_returns_400_insufficient_members_when_only_one_nonzero_weight()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var cxiId = await _factory.GetKpiIdByShortNameAsync("CXI");
        var npsId = await _factory.GetKpiIdByShortNameAsync("NPS");

        // Active CXI + a single weighted member violates FR-043 (would leave it un-activatable).
        var response = await client.PutAsJsonAsync(
            $"/api/v1/kpis/{cxiId}/weights", WeightsBody((npsId!.Value, 3)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("CXI_INSUFFICIENT_MEMBERS");
    }

    [Fact]
    public async Task PUT_kpis_weights_returns_400_cannot_include_itself_when_member_is_the_cxi()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var cxiId = await _factory.GetKpiIdByShortNameAsync("CXI");
        var npsId = await _factory.GetKpiIdByShortNameAsync("NPS");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/kpis/{cxiId}/weights", WeightsBody((cxiId!.Value, 2), (npsId!.Value, 3)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("CXI_CANNOT_INCLUDE_ITSELF");
    }

    [Fact]
    public async Task PUT_kpis_weights_returns_400_member_not_active_when_a_referenced_member_is_inactive()
    {
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var cxiId = await _factory.GetKpiIdByShortNameAsync("CXI");
        var npsId = await _factory.GetKpiIdByShortNameAsync("NPS");
        var csatId = await _factory.GetKpiIdByShortNameAsync("CSAT");

        try
        {
            // Deactivate a shared seeded standard; the weights save must reject it as a member.
            await _factory.SetKpiActiveByShortNameAsync("CSAT", false);

            var response = await client.PutAsJsonAsync(
                $"/api/v1/kpis/{cxiId}/weights", WeightsBody((npsId!.Value, 3), (csatId!.Value, 2)));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.ReadErrorCodeAsync()).Should().Be("CXI_MEMBER_NOT_ACTIVE");
        }
        finally
        {
            // Restore the shared seed so sibling tests in the collection see CSAT active.
            await _factory.SetKpiActiveByShortNameAsync("CSAT", true);
        }
    }

    [Fact(Skip =
        "Blocked on US-5: the PATCH /api/v1/kpis/{id}/activation endpoint (T122) and the deactivation " +
        "cascade that deletes cxi_weights rows (T120/T121) are not yet implemented, and KpiConfigReader " +
        "does not filter inactive members on read. The 'deactivate a member → next CXI read excludes it' " +
        "behaviour is verified by US-5's ActivateKpiEndpointTests (T125) / CxiCascadeAtomicityTests (T126).")]
    public async Task PATCH_activation_excludes_inactive_member_from_next_cxi_read()
    {
        // Intentionally not implemented — see the Skip reason (US-5 dependency).
        await Task.CompletedTask;
    }
}
