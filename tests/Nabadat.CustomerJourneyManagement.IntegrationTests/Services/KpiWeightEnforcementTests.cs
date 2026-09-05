using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.KpiBindings;
using Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Services;

/// <summary>
/// Service-method integration test for the touchpoint KPI-binding full-replace save (T053 / US-2,
/// <c>contracts/configuration-api.md §PUT /api/v1/touchpoints/{id}/kpis</c>). It lives in
/// <c>Services/</c> rather than <c>Endpoints/</c> because the concern under test is an inner atomic-write
/// guarantee — "weights are validated before any DB write; a rejected save leaves no partial state in
/// <c>kpi_bindings</c>" — which is verified most directly by driving
/// <see cref="KpiBindingService.SaveKpiBindingsAsync"/> and reading the table back, not through HTTP
/// status codes. The journey → stage → touchpoint fixture is built over the real authenticated API so
/// the rows exist exactly as production would create them; the save-under-test then runs against the
/// same Testcontainers PostgreSQL.
///
/// Two angles prove "no partial state":
/// <list type="bullet">
///   <item><description>a sum-≠-100 save on a fresh touchpoint writes zero rows (validation precedes the
///   transaction, so neither the DELETE nor the INSERT half of the full replace fires); and</description></item>
///   <item><description>a sum-≠-100 save on a touchpoint that already has a valid 100% set leaves that
///   set byte-for-byte intact (the atomic replace never half-applies — the DELETE does not run on a
///   rejected save).</description></item>
/// </list>
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class KpiWeightEnforcementTests
{
    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public KpiWeightEnforcementTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SaveKpiBindings_with_85_percent_sum_fails_and_writes_nothing()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");
        var touchpointId = await SeedTouchpointAsync(client);

        // NPS 45 + CSAT 40 = 85% — below the required 100%. The validator runs before the
        // transaction opens, so this must fail without touching the database.
        var result = await SaveBindingsAsync(
            touchpointId, actor, ("NPS", 45m), ("CSAT", 40m));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("kpi.weight_sum_invalid");

        // No partial state: not a single binding row was written for this touchpoint.
        (await CountBindingsAsync(touchpointId)).Should().Be(0);
    }

    [Fact]
    public async Task SaveKpiBindings_invalid_attempt_leaves_existing_bindings_intact()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");
        var touchpointId = await SeedTouchpointAsync(client);

        // First persist a valid 100% set.
        var valid = await SaveBindingsAsync(
            touchpointId, actor, ("NPS", 60m), ("CSAT", 40m));
        valid.IsSuccess.Should().BeTrue();
        (await CountBindingsAsync(touchpointId)).Should().Be(2);

        // Then attempt an invalid 85% save. A full replace is DELETE + INSERT; if validation did not
        // gate the write, the DELETE could strip the existing set before the INSERT was rejected.
        var invalid = await SaveBindingsAsync(
            touchpointId, actor, ("NPS", 45m), ("CSAT", 40m));
        invalid.IsSuccess.Should().BeFalse();
        invalid.Error!.Code.Should().Be("kpi.weight_sum_invalid");

        // The original set survives untouched — the rejected save never opened the transaction.
        var persisted = await ReadBindingsAsync(touchpointId);
        persisted.Should().HaveCount(2);
        persisted.Should().Contain("NPS", 60m);
        persisted.Should().Contain("CSAT", 40m);
    }

    /// <summary>
    /// Drives <see cref="KpiBindingService"/> directly (resolved from a fresh DI scope, as the service
    /// is registered <c>Scoped</c>) with the given weights for <paramref name="actor"/>.
    /// </summary>
    private async Task<ServiceResult<SaveKpiBindingsResult>> SaveBindingsAsync(
        Guid touchpointId,
        SeededUser actor,
        params (string KpiType, decimal Weight)[] bindings)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<KpiBindingService>();

        var inputs = bindings.Select(b => new KpiBindingInput(b.KpiType, b.Weight)).ToList();
        var actorContext = new ActorContext(actor.UserId, "P-01", Guid.NewGuid());

        return await service.SaveKpiBindingsAsync(touchpointId, inputs, actorContext);
    }

    /// <summary>Creates a journey → stage → touchpoint over the real API and returns the touchpoint id.</summary>
    private static async Task<Guid> SeedTouchpointAsync(HttpClient client)
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
        return (await touchpoint.ReadJsonAsync()).GetProperty("touchpointId").GetGuid();
    }

    /// <summary>Counts <c>kpi_bindings</c> rows for a touchpoint via direct SQL (schema-relative, like production).</summary>
    private async Task<int> CountBindingsAsync(Guid touchpointId)
    {
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM kpi_bindings WHERE touchpoint_id = @t", connection);
        command.Parameters.AddWithValue("t", touchpointId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>Reads the persisted <c>(kpi_type → weight)</c> set for a touchpoint via direct SQL.</summary>
    private async Task<Dictionary<string, decimal>> ReadBindingsAsync(Guid touchpointId)
    {
        var bindings = new Dictionary<string, decimal>(StringComparer.Ordinal);
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT kpi_type, weight FROM kpi_bindings WHERE touchpoint_id = @t", connection);
        command.Parameters.AddWithValue("t", touchpointId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            bindings[reader.GetString(0)] = reader.GetDecimal(1);
        }

        return bindings;
    }
}
