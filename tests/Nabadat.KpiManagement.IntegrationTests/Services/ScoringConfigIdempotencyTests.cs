using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Services;

/// <summary>
/// T114 [US4] — idempotency of <c>PUT /api/v1/tenant/scoring-config</c>. The update service reads the
/// current row first and skips the write (and therefore the event) when every field already matches
/// (spec Edge Cases "ScoringConfig idempotent save"). PUTting the same payload twice must therefore
/// write exactly one row update and emit exactly one <c>journey.scoring_config.updated</c> event in
/// total — the second call is a no-op that still returns 200. Driven over HTTP through the real
/// pipeline, but asserts the service-level invariant (one event total), so it lives under Services/.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class ScoringConfigIdempotencyTests
{
    private const string ScoringConfigUpdatedEvent = "journey.scoring_config.updated";

    private readonly KpiManagementApplicationFactory _factory;

    public ScoringConfigIdempotencyTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PUT_scoring_config_twice_with_identical_payload_emits_exactly_one_event()
    {
        await _factory.ResetScoringConfigAsync();
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);

        var payload = new
        {
            alpha = 0.650m,
            mot_multiplier = 1.7m,
            n_floor = 200,
            flag_percentile = 20,
            rolling_window_days = 45,
        };

        // First PUT — a real change off the defaults: persists + emits one event.
        var first = await client.PutAsJsonAsync("/api/v1/tenant/scoring-config", payload);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second PUT — identical payload: a no-op. Still 200, but writes nothing and emits no event.
        var second = await client.PutAsJsonAsync("/api/v1/tenant/scoring-config", payload);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.ReadJsonAsync()).GetProperty("alpha").GetDecimal().Should().Be(0.650m);

        (await _factory.CountEventsAsync(actor.UserId, ScoringConfigUpdatedEvent)).Should().Be(1);
    }
}
