using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for the user-management endpoints (users-api.md, US2). Each test
/// enters the real ASP.NET Core pipeline over <see cref="HttpClient"/> as an
/// authenticated actor (seeded user → login → MFA verify → bearer session), so the
/// data-layer authority split (FR-007) is exercised end-to-end:
/// <list type="bullet">
///   <item>only P-01/P-07 may create or view users (others → 403);</item>
///   <item>P-07 may assign only non-CX modules — a CX-domain module is 403, the
///   non-CX <c>UserManagement</c> module is 200.</item>
/// </list>
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class UsersEndpointTests
{
    // Canonical DOC-02 module ids: SurveyBuilder is CX-domain (P-01 only); UserManagement is non-CX.
    private const string CxModule = "SurveyBuilder";
    private const string NonCxModule = "UserManagement";

    private readonly UserManagementApplicationFactory _factory;

    public UsersEndpointTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_users_returns_201_when_actor_is_P01()
    {
        var client = await SignedInClientAsync("P-01");
        var username = UniqueEmail();

        var response = await client.PostAsJsonAsync(
            "/api/v1/users", new { username, persona = "P-03", password = "ValidP@ss1" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("username").GetString().Should().Be(username);
        body.GetProperty("persona").GetString().Should().Be("P-03");
        body.GetProperty("status").GetString().Should().Be("pending-enrollment");
    }

    [Fact]
    public async Task POST_users_returns_201_when_actor_is_P07()
    {
        var client = await SignedInClientAsync("P-07");
        var username = UniqueEmail();

        var response = await client.PostAsJsonAsync(
            "/api/v1/users", new { username, persona = "P-03", password = "ValidP@ss1" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadJsonAsync()).GetProperty("username").GetString().Should().Be(username);
    }

    [Fact]
    public async Task POST_users_returns_403_when_actor_is_P02()
    {
        var client = await SignedInClientAsync("P-02");

        var response = await client.PostAsJsonAsync(
            "/api/v1/users", new { username = UniqueEmail(), persona = "P-03", password = "ValidP@ss1" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task POST_users_returns_422_when_password_is_weak()
    {
        var client = await SignedInClientAsync("P-01");

        var response = await client.PostAsJsonAsync(
            "/api/v1/users", new { username = UniqueEmail(), persona = "P-03", password = "weak" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).Should().Be("users.weak_password");
    }

    [Fact]
    public async Task PUT_users_permissions_by_P07_with_CX_module_returns_403()
    {
        var client = await SignedInClientAsync("P-07");
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/users/{target.UserId}/permissions",
            new { assignments = new[] { new { moduleId = CxModule, allowedModes = new[] { "View" } } } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).Should().Be("permissions.forbidden_module");
    }

    [Fact]
    public async Task PUT_users_permissions_by_P07_with_UserManagement_returns_200()
    {
        var client = await SignedInClientAsync("P-07");
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/users/{target.UserId}/permissions",
            new { assignments = new[] { new { moduleId = NonCxModule, allowedModes = new[] { "Full" } } } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var assignments = (await response.ReadJsonAsync()).GetProperty("assignments").EnumerateArray().ToList();
        assignments.Should().ContainSingle()
            .Which.GetProperty("moduleId").GetString().Should().Be(NonCxModule);
    }

    [Fact]
    public async Task GET_users_id_by_actor_without_permission_returns_403()
    {
        var client = await SignedInClientAsync("P-02");
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");

        var response = await client.GetAsync($"/api/v1/users/{target.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds an MFA-enrolled actor with the given persona, drives login → MFA verify,
    /// and returns a client whose default Authorization header carries the session token.
    /// </summary>
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

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";
}
