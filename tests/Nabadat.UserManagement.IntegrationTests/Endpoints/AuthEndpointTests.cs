using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for the login and MFA-verify endpoints (auth-api.md). Each test
/// enters via the real ASP.NET Core pipeline over <see cref="HttpClient"/>.
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class AuthEndpointTests
{
    private readonly UserManagementApplicationFactory _factory;

    public AuthEndpointTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_auth_login_returns_challengeId_when_credentials_valid()
    {
        var user = await _factory.SeedEnrolledUserAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = user.Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("challengeId").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("requiresMfaEnrollment").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task POST_auth_login_returns_401_when_credentials_invalid()
    {
        var user = await _factory.SeedEnrolledUserAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = "WrongP@ss9" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ReadErrorCodeAsync()).Should().Be("auth.invalid_credentials");
    }

    [Fact]
    public async Task POST_auth_mfa_verify_returns_sessionToken_when_code_valid()
    {
        var user = await _factory.SeedEnrolledUserAsync();
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = user.Password });
        var challengeId = (await login.ReadJsonAsync()).GetProperty("challengeId").GetString();

        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/mfa/verify",
            new { challengeId, totpCode = UserManagementApplicationFactory.ComputeTotp(user.Base32Secret!) });

        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await verify.ReadJsonAsync();
        body.GetProperty("sessionToken").GetString().Should().StartWith("nbd_");
        body.GetProperty("userId").GetGuid().Should().Be(user.UserId);
    }

    [Fact]
    public async Task POST_auth_mfa_verify_returns_422_when_code_invalid()
    {
        var user = await _factory.SeedEnrolledUserAsync();
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = user.Password });
        var challengeId = (await login.ReadJsonAsync()).GetProperty("challengeId").GetString();

        // A code that is definitely not the current valid one.
        var current = UserManagementApplicationFactory.ComputeTotp(user.Base32Secret!);
        var wrong = current == "000000" ? "111111" : "000000";

        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/mfa/verify", new { challengeId, totpCode = wrong });

        verify.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await verify.ReadErrorCodeAsync()).Should().Be("auth.mfa.invalid_code");
    }

    [Fact]
    public async Task POST_auth_login_returns_423_when_account_locked()
    {
        var user = await _factory.SeedEnrolledUserAsync(
            status: UserStatus.Locked,
            lockedUntilUtc: DateTimeOffset.UtcNow.AddMinutes(15),
            failedAttemptCount: 5);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = user.Password });

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
        (await response.ReadErrorCodeAsync()).Should().Be("auth.account_locked");
    }

    [Fact]
    public async Task POST_auth_password_reset_request_returns_202_regardless_of_email_existence()
    {
        var client = _factory.CreateClient();

        // Unknown email → still 202 (no user enumeration), and no M-09 delivery occurs.
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/password-reset/request", new { email = $"missing-{Guid.NewGuid():N}@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
