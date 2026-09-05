using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Endpoints;

/// <summary>
/// T113 [US4] — HTTP tests for <c>GET</c>/<c>PUT /api/v1/tenant/scoring-config</c> (contracts/settings-api.md).
/// Enters the real ASP.NET Core pipeline as an authenticated persona. Covers the seeded-defaults GET,
/// a valid PUT (200 + exactly one <c>journey.scoring_config.updated</c> event), the two 400 validation
/// paths, and the persona authority model (P-07 holds Manage but is read-only → 403; P-04 has no
/// TenantConfiguration grant → 403; P-01 → 200). Each test resets the singleton row first so the shared
/// container gives a deterministic baseline.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class ScoringConfigEndpointTests
{
    private const string ScoringConfigUpdatedEvent = "journey.scoring_config.updated";

    private readonly KpiManagementApplicationFactory _factory;

    public ScoringConfigEndpointTests(KpiManagementApplicationFactory factory) => _factory = factory;

    private static object ValidPayload() => new
    {
        alpha = 0.700m,
        mot_multiplier = 1.8m,
        n_floor = 250,
        flag_percentile = 30,
        rolling_window_days = 60,
    };

    [Fact]
    public async Task GET_scoring_config_returns_seeded_defaults_when_tenant_is_fresh()
    {
        await _factory.ResetScoringConfigAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.GetAsync("/api/v1/tenant/scoring-config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("alpha").GetDecimal().Should().Be(0.500m);
        body.GetProperty("beta").GetDecimal().Should().Be(0.500m); // derived 1 − α
        body.GetProperty("mot_multiplier").GetDecimal().Should().Be(1.5m);
        body.GetProperty("n_floor").GetInt32().Should().Be(100);
        body.GetProperty("flag_percentile").GetInt32().Should().Be(25);
        body.GetProperty("rolling_window_days").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task PUT_scoring_config_returns_200_and_emits_one_event_when_payload_is_valid()
    {
        await _factory.ResetScoringConfigAsync();
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.PutAsJsonAsync("/api/v1/tenant/scoring-config", ValidPayload());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("alpha").GetDecimal().Should().Be(0.700m);
        body.GetProperty("beta").GetDecimal().Should().Be(0.300m); // 1 − 0.700, no IEEE-754 drift
        body.GetProperty("mot_multiplier").GetDecimal().Should().Be(1.8m);
        body.GetProperty("n_floor").GetInt32().Should().Be(250);
        body.GetProperty("flag_percentile").GetInt32().Should().Be(30);
        body.GetProperty("rolling_window_days").GetInt32().Should().Be(60);
        (await _factory.CountEventsAsync(actor.UserId, ScoringConfigUpdatedEvent)).Should().Be(1);
    }

    [Fact]
    public async Task PUT_scoring_config_returns_400_invalid_alpha_beta_sum_when_alpha_out_of_range()
    {
        await _factory.ResetScoringConfigAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.PutAsJsonAsync("/api/v1/tenant/scoring-config", new
        {
            alpha = 1.5m,
            mot_multiplier = 1.5m,
            n_floor = 100,
            flag_percentile = 25,
            rolling_window_days = 30,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("INVALID_ALPHA_BETA_SUM");
    }

    [Fact]
    public async Task PUT_scoring_config_returns_400_mot_multiplier_out_of_range_when_mot_above_2()
    {
        await _factory.ResetScoringConfigAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);

        var response = await client.PutAsJsonAsync("/api/v1/tenant/scoring-config", new
        {
            alpha = 0.500m,
            mot_multiplier = 2.5m,
            n_floor = 100,
            flag_percentile = 25,
            rolling_window_days = 30,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("MOT_MULTIPLIER_OUT_OF_RANGE");
    }

    [Fact]
    public async Task PUT_scoring_config_returns_403_when_actor_is_tenant_it_administrator()
    {
        // P-07 holds TenantConfiguration Manage (it can edit Organization) but is read-only for
        // ScoringConfig (FR-062) — the controller's explicit P-01-only guard rejects it.
        await _factory.ResetScoringConfigAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.TenantItAdministrator);

        var response = await client.PutAsJsonAsync("/api/v1/tenant/scoring-config", ValidPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).Should().Be("PERMISSION_DENIED");
    }

    [Fact]
    public async Task PUT_scoring_config_returns_403_when_actor_lacks_tenant_configuration_grant()
    {
        // P-04 holds no TenantConfiguration mode at all → the [RequirePermission(Manage)] filter denies.
        await _factory.ResetScoringConfigAsync();
        var client = await _factory.SignedInClientAsync("P-04");

        var response = await client.PutAsJsonAsync("/api/v1/tenant/scoring-config", ValidPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).Should().Be("PERMISSION_DENIED");
    }
}
