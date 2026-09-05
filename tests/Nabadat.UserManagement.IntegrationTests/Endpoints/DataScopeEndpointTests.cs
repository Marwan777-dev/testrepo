using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for the data-scope endpoints (permissions-api.md, US3, T110).
/// Exercises the real ASP.NET Core pipeline over <see cref="HttpClient"/>:
/// <list type="bullet">
///   <item>M-13 parameter definitions are ingested (no user session — internal call)
///   and persisted to <c>data_scope_parameter_definitions</c>;</item>
///   <item>a P-01 admin can read a user's scope and replace it;</item>
///   <item>a value outside its parameter's definition is rejected with 422.</item>
/// </list>
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class DataScopeEndpointTests
{
    private const string Branch = "branch";

    private readonly UserManagementApplicationFactory _factory;

    public DataScopeEndpointTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_authorization_scope_parameters_stores_definitions()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/authorization/scope/parameters", new
        {
            sourceModule = "M-13",
            parameters = new[]
            {
                new { name = Branch, label = "Branch", allowedValues = new[] { "Riyadh", "Jeddah", "Dammam" } },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountParameterDefinitionsAsync(Branch)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GET_users_id_scope_returns_active_assignments()
    {
        var client = await SignedInClientAsync("P-01");
        await IngestBranchDefinitionAsync(client);
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");

        var put = await client.PutAsJsonAsync($"/api/v1/users/{target.UserId}/scope", new
        {
            organizationNodeId = (string?)null,
            dataScopeAssignments = new[] { new { parameterName = Branch, allowedValues = new[] { "Riyadh" } } },
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync($"/api/v1/users/{target.UserId}/scope");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var assignments = (await get.ReadJsonAsync()).GetProperty("dataScopeAssignments").EnumerateArray().ToList();
        var branch = assignments.Should().ContainSingle(a => a.GetProperty("parameterName").GetString() == Branch).Subject;
        branch.GetProperty("allowedValues").EnumerateArray().Select(v => v.GetString())
            .Should().BeEquivalentTo(["Riyadh"]);
    }

    [Fact]
    public async Task PUT_users_id_scope_rejects_invalid_parameter_values()
    {
        var client = await SignedInClientAsync("P-01");
        await IngestBranchDefinitionAsync(client);
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");

        // "Mecca" is not among the branch parameter's defined allowed values.
        var put = await client.PutAsJsonAsync($"/api/v1/users/{target.UserId}/scope", new
        {
            organizationNodeId = (string?)null,
            dataScopeAssignments = new[] { new { parameterName = Branch, allowedValues = new[] { "Mecca" } } },
        });

        put.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await put.ReadErrorCodeAsync()).Should().Be("scope.invalid_assignment");
    }

    // T110a — target MISS: PUT scope for an unseeded user → 404. Empty assignments pass
    // definition validation, so the request reaches (and fails) the user lookup.
    [Fact]
    public async Task PUT_users_id_scope_returns_404_when_target_user_missing()
    {
        var client = await SignedInClientAsync("P-01");

        var put = await client.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/scope", new
        {
            organizationNodeId = (string?)null,
            dataScopeAssignments = Array.Empty<object>(),
        });

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await put.ReadErrorCodeAsync()).Should().Be("users.not_found");
    }

    // T110a — definition MISS: target hit + a parameter never ingested → 422 with the
    // field-level detail code `parameter.not_found` (rejected before any write).
    [Fact]
    public async Task PUT_users_id_scope_returns_422_when_parameter_definition_missing()
    {
        var client = await SignedInClientAsync("P-01");
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");

        var put = await client.PutAsJsonAsync($"/api/v1/users/{target.UserId}/scope", new
        {
            organizationNodeId = (string?)null,
            dataScopeAssignments = new[] { new { parameterName = "never_ingested_param", allowedValues = new[] { "x" } } },
        });

        put.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = (await put.ReadJsonAsync()).GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("scope.invalid_assignment");
        error.GetProperty("details").EnumerateArray()
            .Select(d => d.GetProperty("code").GetString())
            .Should().Contain("parameter.not_found");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Task IngestBranchDefinitionAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/authorization/scope/parameters", new
        {
            sourceModule = "M-13",
            parameters = new[]
            {
                new { name = Branch, label = "Branch", allowedValues = new[] { "Riyadh", "Jeddah", "Dammam" } },
            },
        });

    private async Task<int> CountParameterDefinitionsAsync(string parameterName)
    {
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM data_scope_parameter_definitions WHERE parameter_name = @n", connection);
        command.Parameters.AddWithValue("n", parameterName);
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
