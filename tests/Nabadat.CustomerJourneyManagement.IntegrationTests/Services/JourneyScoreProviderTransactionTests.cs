using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.Platform.Contracts.M16;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Scores;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Services;

/// <summary>
/// Service-method integration test for the journey score refresh (T075 / US-3,
/// <c>contracts/published-interfaces.md §IJourneyScoreProvider</c>). It lives in <c>Services/</c>
/// because the concern under test is an inner atomic-write guarantee — "the <c>journey_scores</c>
/// upsert and the <c>journey.score.updated</c> M-17 event commit in the <b>same</b> transaction, and a
/// failure in the M-06 scoring call rolls back <b>both</b>" — verified most directly by driving
/// <see cref="JourneyScoreProviderService.GetScoresAsync"/> and reading the tables back, not through
/// HTTP.
///
/// The service is constructed by hand with the <b>real</b> DB-backed collaborators (config reader,
/// score data service, tenant DbContext, event publisher, clock) resolved from a DI scope, and a
/// <b>stub</b> <see cref="IM06ScoringService"/> standing in for the not-yet-present M-06 engine — so
/// the test controls M-06's success/failure while everything else runs against the real
/// Testcontainers PostgreSQL. (M-06 is otherwise a throwing placeholder in this tree, so the live DI
/// graph cannot exercise the happy path.)
///
/// Two angles, each on its own journey:
/// <list type="bullet">
///   <item><description>M-06 succeeds → the composite score row is written AND exactly one
///   <c>journey.score.updated</c> event is appended (both present after the call = committed
///   together);</description></item>
///   <item><description>M-06 throws → the exception propagates and neither a score row nor an event
///   is written (the transaction never opened — no partial state).</description></item>
/// </list>
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class JourneyScoreProviderTransactionTests
{
    private const string ScoreUpdatedEvent = "journey.score.updated";

    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public JourneyScoreProviderTransactionTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetScores_upserts_score_and_emits_event_in_same_transaction_when_m06_succeeds()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var journeyId = await SeedMeasuredJourneyAsync(client);

        using var scope = _factory.Services.CreateScope();
        var m06 = new StubM06ScoringService(config => new JourneyScoreResultDto(
            config.JourneyId,
            JourneyScore: 78.5m,
            ComputedAt: DateTime.UtcNow,
            StageScores: new List<StageScoreDto>(),
            TouchpointScores: new List<TouchpointScoreDto>()));
        var provider = BuildProvider(scope.ServiceProvider, m06);

        var result = await provider.GetScoresAsync(journeyId);

        // The M-06 result is returned verbatim.
        result.Should().NotBeNull();
        result!.JourneyScore.Should().Be(78.5m);

        // Both writes landed — proving they committed in the same transaction.
        (await ReadCompositeScoreAsync(journeyId)).Should().Be(78.5m);
        (await CountScoreEventsAsync(journeyId)).Should().Be(1);
    }

    [Fact]
    public async Task GetScores_rolls_back_score_and_event_when_m06_computation_fails()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var journeyId = await SeedMeasuredJourneyAsync(client);

        using var scope = _factory.Services.CreateScope();
        var m06 = new StubM06ScoringService(_ =>
            throw new InvalidOperationException("simulated M-06 scoring-engine failure"));
        var provider = BuildProvider(scope.ServiceProvider, m06);

        // M-06 throws before the transaction opens, so the failure propagates to the caller.
        var act = () => provider.GetScoresAsync(journeyId);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // No partial state: neither the score row nor the audit event was written.
        (await CountScoreRowsAsync(journeyId)).Should().Be(0);
        (await CountScoreEventsAsync(journeyId)).Should().Be(0);
    }

    /// <summary>
    /// Builds the SUT with the real DB-backed collaborators from <paramref name="sp"/> and the supplied
    /// stub M-06 engine. Mirrors the production constructor wiring in <c>CustomerJourneyManagementServiceCollectionExtensions</c>.
    /// </summary>
    private static JourneyScoreProviderService BuildProvider(IServiceProvider sp, IM06ScoringService m06) =>
        new(
            sp.GetRequiredService<IJourneyConfigReader>(),
            m06,
            sp.GetRequiredService<IJourneyScoreDataService>(),
            sp.GetRequiredService<ITenantDbContext>(),
            sp.GetRequiredService<IM17EventPublisher>(),
            sp.GetRequiredService<TimeProvider>());

    /// <summary>
    /// Creates a journey → stage → touchpoint with a valid 100% KPI binding set (NPS 60 + CSAT 40) so
    /// <see cref="IJourneyConfigReader"/> returns a non-null config (a measured journey to score).
    /// </summary>
    private static async Task<Guid> SeedMeasuredJourneyAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name = $"Journey {Guid.NewGuid():N}", journeyType = "Onboarding" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var journeyId = (await create.ReadJsonAsync()).GetProperty("journeyId").GetGuid();

        var stage = await client.PostAsJsonAsync($"/api/v1/journeys/{journeyId}/stages", new { name = "Awareness" });
        stage.StatusCode.Should().Be(HttpStatusCode.Created);
        var stageId = (await stage.ReadJsonAsync()).GetProperty("stageId").GetGuid();

        var touchpoint = await client.PostAsJsonAsync($"/api/v1/stages/{stageId}/touchpoints", new { name = "Landing page" });
        touchpoint.StatusCode.Should().Be(HttpStatusCode.Created);
        var touchpointId = (await touchpoint.ReadJsonAsync()).GetProperty("touchpointId").GetGuid();

        var kpis = await client.PutAsJsonAsync(
            $"/api/v1/touchpoints/{touchpointId}/kpis",
            new { kpiBindings = new[] { new { kpiType = "NPS", weight = 60 }, new { kpiType = "CSAT", weight = 40 } } });
        kpis.StatusCode.Should().Be(HttpStatusCode.OK);

        return journeyId;
    }

    /// <summary>Reads the composite <c>journey_score</c> for a journey (null when no row exists).</summary>
    private async Task<decimal?> ReadCompositeScoreAsync(Guid journeyId)
    {
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT journey_score FROM journey_scores WHERE journey_id = @j", connection);
        command.Parameters.AddWithValue("j", journeyId);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToDecimal(value);
    }

    /// <summary>Counts <c>journey_scores</c> rows for a journey (the upsert target — one row per journey).</summary>
    private async Task<int> CountScoreRowsAsync(Guid journeyId)
    {
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM journey_scores WHERE journey_id = @j", connection);
        command.Parameters.AddWithValue("j", journeyId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// Counts <c>journey.score.updated</c> events for a journey. Scored by <c>entity_id</c> (= the
    /// journey id) rather than actor: a score refresh is system-triggered, so every score event is
    /// stamped with the empty system actor and could not be distinguished by actor across journeys.
    /// </summary>
    private async Task<int> CountScoreEventsAsync(Guid journeyId)
    {
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM event_log WHERE entity_id = @j AND event_type = @t", connection);
        command.Parameters.AddWithValue("j", journeyId);
        command.Parameters.AddWithValue("t", ScoreUpdatedEvent);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// A hand-rolled <see cref="IM06ScoringService"/> stub that returns (or throws) according to the
    /// supplied function — the test's lever over M-06 success vs failure.
    /// </summary>
    private sealed class StubM06ScoringService : IM06ScoringService
    {
        private readonly Func<JourneyConfigDto, JourneyScoreResultDto> _compute;

        public StubM06ScoringService(Func<JourneyConfigDto, JourneyScoreResultDto> compute) => _compute = compute;

        public Task<JourneyScoreResultDto> ComputeJourneyScoreAsync(JourneyConfigDto config, CancellationToken ct = default)
            => Task.FromResult(_compute(config));
    }
}
