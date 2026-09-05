using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Scenarios;

/// <summary>
/// End-to-end business journeys for User Story 1 (mandatory-MFA tenant login).
/// Each test walks the user-facing flow across multiple endpoints and asserts the
/// final state of the world (sessions, lockout, audit events).
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class TenantLoginWithMandatoryMfaTests
{
    private readonly UserManagementApplicationFactory _factory;

    public TenantLoginWithMandatoryMfaTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task NewUser_enrolls_mfa_then_signs_in_and_out()
    {
        var user = await _factory.SeedPendingEnrollmentUserAsync();
        var client = _factory.CreateClient();

        // 1. Credentials → enrollment-required challenge (no session yet).
        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = user.Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await login.ReadJsonAsync();
        loginBody.GetProperty("requiresMfaEnrollment").GetBoolean().Should().BeTrue();
        var challengeId = loginBody.GetProperty("challengeId").GetString();

        // 2. Enroll → provisioning material.
        var enroll = await client.PostAsJsonAsync("/api/v1/auth/mfa/enroll", new { challengeId });
        var enrollBody = await enroll.ReadJsonAsync();
        var enrollmentToken = enrollBody.GetProperty("enrollmentToken").GetString();
        var secret = enrollBody.GetProperty("base32Secret").GetString()!;

        // 3. Confirm first code → session created.
        var confirm = await client.PostAsJsonAsync(
            "/api/v1/auth/mfa/enroll/confirm",
            new { enrollmentToken, totpCode = UserManagementApplicationFactory.ComputeTotp(secret) });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await confirm.ReadJsonAsync()).GetProperty("sessionToken").GetString();

        // 4. The session is valid.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var session = await client.GetAsync("/api/v1/auth/session");
        session.StatusCode.Should().Be(HttpStatusCode.OK);
        (await session.ReadJsonAsync()).GetProperty("userId").GetGuid().Should().Be(user.UserId);

        // 5. Logout invalidates it.
        var logout = await client.PostAsync("/api/v1/auth/logout", content: null);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterLogout = await client.GetAsync("/api/v1/auth/session");
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // End state: exactly one enrollment and one session created for this user.
        (await _factory.CountEventsAsync(user.UserId, "mfa.enrolled")).Should().Be(1);
        (await _factory.CountEventsAsync(user.UserId, "session.created")).Should().Be(1);
    }

    [Fact]
    public async Task Account_locks_after_five_failed_codes_then_login_allowed_after_cooldown()
    {
        var user = await _factory.SeedEnrolledUserAsync();
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = user.Password });
        var challengeId = (await login.ReadJsonAsync()).GetProperty("challengeId").GetString();

        // Five consecutive invalid codes lock the account (each rejected as 422).
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var bad = await client.PostAsJsonAsync(
                "/api/v1/auth/mfa/verify", new { challengeId, totpCode = "000000" });
            bad.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }

        // A locked account rejects the next login with 423.
        var locked = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = user.Password });
        locked.StatusCode.Should().Be(HttpStatusCode.Locked);
        (await _factory.CountEventsAsync(user.UserId, "authentication.account.locked")).Should().Be(1);

        // Once the cooldown elapses, login is accepted again.
        await _factory.ExpireLockoutAsync(user.UserId);
        var afterCooldown = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = user.Password });
        afterCooldown.StatusCode.Should().Be(HttpStatusCode.OK);
        (await afterCooldown.ReadJsonAsync()).GetProperty("challengeId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SelfService_password_reset_completes_round_trip()
    {
        var user = await _factory.SeedEnrolledUserAsync();
        const string newPassword = "NewValidP@ss2";

        // Swap in a successful, capturing M-09 so the request reaches 202 and we can
        // read the delivered raw token (the production stub fails closed → 503).
        var m09 = new CapturingM09NotificationService();
        var client = _factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                s.AddScoped<IM09NotificationService>(_ => m09)))
            .CreateClient();

        var request = await client.PostAsJsonAsync(
            "/api/v1/auth/password-reset/request", new { email = user.Username });
        request.StatusCode.Should().Be(HttpStatusCode.Accepted);
        m09.LastRawToken.Should().NotBeNullOrWhiteSpace();

        var redeem = await client.PostAsJsonAsync(
            "/api/v1/auth/password-reset/redeem", new { token = m09.LastRawToken, newPassword });
        redeem.StatusCode.Should().Be(HttpStatusCode.OK);

        // The new password authenticates; the old one no longer does.
        var withNew = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = newPassword });
        withNew.StatusCode.Should().Be(HttpStatusCode.OK);

        var withOld = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = user.Username, password = user.Password });
        withOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
