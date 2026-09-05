using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for the MFA enrollment endpoints: initiation returns the
/// provisioning material, and confirming the first code creates a session.
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class MfaEnrollEndpointTests
{
    private readonly UserManagementApplicationFactory _factory;

    public MfaEnrollEndpointTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_auth_mfa_enroll_returns_otpauth_uri_when_challenge_valid()
    {
        var user = await _factory.SeedPendingEnrollmentUserAsync();
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = user.Password });
        var loginBody = await login.ReadJsonAsync();
        loginBody.GetProperty("requiresMfaEnrollment").GetBoolean().Should().BeTrue();
        var challengeId = loginBody.GetProperty("challengeId").GetString();

        var enroll = await client.PostAsJsonAsync("/api/v1/auth/mfa/enroll", new { challengeId });

        enroll.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await enroll.ReadJsonAsync();
        body.GetProperty("otpauthUri").GetString().Should().StartWith("otpauth://totp/");
        body.GetProperty("base32Secret").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("enrollmentToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task POST_auth_mfa_enroll_confirm_creates_session_when_totp_valid()
    {
        var user = await _factory.SeedPendingEnrollmentUserAsync();
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = user.Password });
        var challengeId = (await login.ReadJsonAsync()).GetProperty("challengeId").GetString();

        var enroll = await client.PostAsJsonAsync("/api/v1/auth/mfa/enroll", new { challengeId });
        var enrollBody = await enroll.ReadJsonAsync();
        var enrollmentToken = enrollBody.GetProperty("enrollmentToken").GetString();
        var base32Secret = enrollBody.GetProperty("base32Secret").GetString()!;

        var confirm = await client.PostAsJsonAsync(
            "/api/v1/auth/mfa/enroll/confirm",
            new { enrollmentToken, totpCode = UserManagementApplicationFactory.ComputeTotp(base32Secret) });

        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await confirm.ReadJsonAsync();
        body.GetProperty("sessionToken").GetString().Should().StartWith("nbd_");
        body.GetProperty("userId").GetGuid().Should().Be(user.UserId);
    }
}
