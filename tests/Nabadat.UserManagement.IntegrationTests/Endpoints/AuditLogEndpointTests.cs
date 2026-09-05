using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Endpoints;

/// <summary>
/// HTTP-level tests for US4 (FR-015), entering the real ASP.NET Core pipeline as an
/// authenticated P-01 actor:
/// <list type="bullet">
///   <item><b>Write side</b> — every state-mutating action co-writes its <c>event_log</c>
///   row in the same transaction; the trail is append-only (asserted against the table).</item>
///   <item><b>Read side</b> — <c>GET /api/v1/audit-log</c> reads those events back through
///   M-10's own <c>IAuditLogReader</c> (M-10 owns the audit cycle; no M-17 dependency),
///   honouring the event-type filter and the P-01/P-07 access gate.</item>
/// </list>
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class AuditLogEndpointTests
{
    private const string NonCxModule = "UserManagement";

    private readonly UserManagementApplicationFactory _factory;

    public AuditLogEndpointTests(UserManagementApplicationFactory factory) => _factory = factory;

    // A profile update writes an immutable, fully-populated audit entry (old + new value).
    [Fact]
    public async Task PUT_users_id_updates_user_and_writes_locked_audit_entry()
    {
        var (client, actor) = await SignedInAsync("P-01");
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");

        // A persona change is a real profile update (P-01 may change persona). We avoid
        // setting organizationNodeId because tenant_users.organization_node_id carries a
        // FK to an M-11-seeded hierarchy node — a synthetic id would 500 on the FK.
        var put = await client.PutAsJsonAsync(
            $"/api/v1/users/{target.UserId}",
            new { persona = "P-04", organizationNodeId = (string?)null });

        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Exactly one user.updated event for the target, attributed to the acting admin.
        (await _factory.CountEventsByEntityAsync(target.UserId, "user.updated")).Should().Be(1);
        (await _factory.CountEventsAsync(actor.UserId, "user.updated")).Should().Be(1);

        // FR-015 payload completeness (T116): both before and after states are captured.
        var (oldValue, newValue) = await _factory.GetLatestEventValuesAsync(target.UserId, "user.updated");
        oldValue.Should().NotBeNull();
        newValue.Should().NotBeNull();
    }

    // Logout invalidates the session and appends a session.revoked event.
    [Fact]
    public async Task POST_auth_logout_writes_session_revoked_event()
    {
        var (client, _) = await SignedInAsync("P-01");

        // Delta around the action — the shared fixture accumulates events across tests.
        var before = await _factory.CountEventsByTypeAsync("session.revoked");

        var logout = await client.PostAsync("/api/v1/auth/logout", content: null);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await _factory.CountEventsByTypeAsync("session.revoked");
        after.Should().Be(before + 1);
    }

    // Granting then revoking permissions leaves BOTH events in the trail (append-only history).
    [Fact]
    public async Task PUT_users_id_permissions_revoke_persists_audit_history()
    {
        var (client, _) = await SignedInAsync("P-01");
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");

        var grant = await client.PutAsJsonAsync(
            $"/api/v1/users/{target.UserId}/permissions",
            new { assignments = new[] { new { moduleId = NonCxModule, allowedModes = new[] { "Full" } } } });
        grant.StatusCode.Should().Be(HttpStatusCode.OK);

        // Revoke everything by replacing with an empty module set.
        var revoke = await client.PutAsJsonAsync(
            $"/api/v1/users/{target.UserId}/permissions",
            new { assignments = Array.Empty<object>() });
        revoke.StatusCode.Should().Be(HttpStatusCode.OK);

        // The grant and the revoke are both retained — the audit log is append-only.
        (await _factory.CountEventsByEntityAsync(target.UserId, "permission.modified")).Should().Be(2);
    }

    // ── Read side (GET /api/v1/audit-log via M-10's own reader) ──────────────

    [Fact]
    public async Task GET_audit_log_returns_recent_events_for_admin()
    {
        var (client, _) = await SignedInAsync("P-01");
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");
        await client.PostAsync($"/api/v1/users/{target.UserId}/deactivate", content: null); // user.deactivated

        var res = await client.GetAsync("/api/v1/audit-log");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await res.ReadJsonAsync()).GetProperty("items").EnumerateArray().ToList();
        items.Should().Contain(e => e.GetProperty("eventType").GetString() == "user.deactivated");
    }

    [Fact]
    public async Task GET_audit_log_filters_by_event_type()
    {
        var (client, _) = await SignedInAsync("P-01");
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");
        await client.PutAsJsonAsync(
            $"/api/v1/users/{target.UserId}/permissions",
            new { assignments = new[] { new { moduleId = NonCxModule, allowedModes = new[] { "Full" } } } }); // permission.modified
        await client.PostAsync($"/api/v1/users/{target.UserId}/deactivate", content: null); // user.deactivated

        var res = await client.GetAsync("/api/v1/audit-log?event_type=permission.modified");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await res.ReadJsonAsync()).GetProperty("items").EnumerateArray().ToList();
        items.Should().NotBeEmpty();
        items.Should().OnlyContain(e => e.GetProperty("eventType").GetString() == "permission.modified");
    }

    [Fact]
    public async Task GET_audit_log_returns_403_for_non_admin()
    {
        var (client, _) = await SignedInAsync("P-03");

        var res = await client.GetAsync("/api/v1/audit-log");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds an MFA-enrolled actor with the given persona, drives login → MFA verify, and
    /// returns a bearer-authenticated client alongside the seeded actor (so tests can
    /// assert the event's <c>actor_id</c>).
    /// </summary>
    private async Task<(HttpClient Client, SeededUser Actor)> SignedInAsync(string persona)
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
        return (client, actor);
    }
}
