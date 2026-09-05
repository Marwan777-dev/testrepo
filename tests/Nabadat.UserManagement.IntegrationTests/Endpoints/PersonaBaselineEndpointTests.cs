using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for the persona-baseline endpoints (permissions-api.md). Exercises the real
/// ASP.NET Core pipeline over <see cref="HttpClient"/> for <c>PUT /api/v1/persona-baselines/{personaId}</c>:
/// the FR-007 split (a P-07 actor may not put a CX-domain module into a baseline → 403) and the
/// success path (a non-CX module is accepted, the baseline is marked customised, and a
/// <c>persona_baseline.updated</c> event is emitted). This replaces the former store-seam unit
/// tests, which can no longer mock the data-access port after the service/store merge.
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class PersonaBaselineEndpointTests
{
    private const string CxModule = "SurveyBuilder";        // CX-domain — P-01 only
    private const string UserManagement = "UserManagement"; // non-CX — P-07 allowed

    private readonly UserManagementApplicationFactory _factory;

    public PersonaBaselineEndpointTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PUT_persona_baselines_returns_403_when_p07_assigns_a_cx_domain_module()
    {
        await _factory.SeedPersonaBaselinesAsync();
        var p07 = await SignedInClientAsync("P-07");

        var response = await p07.PutAsJsonAsync(
            "/api/v1/persona-baselines/P-03",
            new { permissionModuleAssignments = new[] { new { moduleId = CxModule, allowedModes = new[] { "View" } } } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).Should().Be("permissions.forbidden_module");
    }

    [Fact]
    public async Task PUT_persona_baselines_customises_baseline_and_emits_event_when_p07_assigns_a_non_cx_module()
    {
        await _factory.SeedPersonaBaselinesAsync();
        var p07 = await SignedInClientAsync("P-07");

        var update = await p07.PutAsJsonAsync(
            "/api/v1/persona-baselines/P-03",
            new { permissionModuleAssignments = new[] { new { moduleId = UserManagement, allowedModes = new[] { "Full" } } } });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await update.ReadJsonAsync()).GetProperty("isCustomised").GetBoolean().Should().BeTrue();

        // The change is persisted: the listing shows P-03 customised with the non-CX module.
        var list = await (await p07.GetAsync("/api/v1/persona-baselines")).ReadJsonAsync();
        var p03 = list.GetProperty("items").EnumerateArray()
            .Single(b => b.GetProperty("personaId").GetString() == "P-03");
        p03.GetProperty("isCustomised").GetBoolean().Should().BeTrue();
        p03.GetProperty("permissionModuleAssignments").EnumerateArray()
            .Select(a => a.GetProperty("moduleId").GetString())
            .Should().Contain(UserManagement);

        // And the edit emitted its audit event to the tenant event_log.
        (await CountEventsAsync("persona_baseline.updated")).Should().BeGreaterThan(0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<int> CountEventsAsync(string eventType)
    {
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM event_log WHERE event_type = @t", connection);
        command.Parameters.AddWithValue("t", eventType);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<HttpClient> SignedInClientAsync(string persona)
    {
        var actor = await _factory.SeedEnrolledUserAsync(persona: persona);
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = actor.Username, password = actor.Password });
        var challengeId = (await login.ReadJsonAsync()).GetProperty("challengeId").GetString();

        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/mfa/verify",
            new { challengeId, totpCode = UserManagementApplicationFactory.ComputeTotp(actor.Base32Secret!) });
        var token = (await verify.ReadJsonAsync()).GetProperty("sessionToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
