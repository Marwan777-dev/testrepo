using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Scenarios;

/// <summary>
/// US4 scenario (T122) — the immutable tenant audit trail. Walks one business journey
/// end-to-end: a P-01 admin changes a user's permissions, that change is recorded as an
/// audit event with correct old/new values, only an admin may read the audit log, and no
/// API can mutate or delete an audit entry.
///
/// M-17 note: reading events back <i>through the API</i> (<c>GET /api/v1/audit-log</c>)
/// goes through M-17's reader, which has no implementation until M-17 ships (T127) — so
/// the event's presence + old/new content is asserted against the canonical store
/// (<c>event_log</c>) here, and the API read is exercised only for its access-control
/// behaviour (admin authorised vs. P-03 forbidden), which is M-17-independent. When M-17
/// lands, the P-01 read assertion tightens from "authorised" to "returns the event".
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class ImmutableAuditTrailTests
{
    private const string NonCxModule = "UserManagement";

    private readonly UserManagementApplicationFactory _factory;

    public ImmutableAuditTrailTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Permission_change_is_audited_immutably_and_only_admins_may_read_the_log()
    {
        // 1. A P-01 admin grants a permission module to a target user.
        var (admin, _) = await SignedInAsync("P-01");
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");

        var grant = await admin.PutAsJsonAsync(
            $"/api/v1/users/{target.UserId}/permissions",
            new { assignments = new[] { new { moduleId = NonCxModule, allowedModes = new[] { "Full" } } } });
        grant.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. The change is recorded as a permission.modified event with old + new values.
        (await _factory.CountEventsByEntityAsync(target.UserId, "permission.modified")).Should().Be(1);
        var (oldValue, newValue) = await _factory.GetLatestEventValuesAsync(target.UserId, "permission.modified");
        oldValue.Should().NotBeNull();                  // prior (empty) module set captured
        newValue.Should().NotBeNull();
        newValue.Should().Contain(NonCxModule);         // the granted module is in the new value

        // 3. Access control on the read endpoint — a non-admin (P-03) is forbidden.
        var (p03, _) = await SignedInAsync("P-03");
        var p03Read = await p03.GetAsync("/api/v1/audit-log");
        p03Read.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. ...and the admin is authorised to read (passes the persona gate). Until M-17
        // wires the reader this returns 503; afterwards 200 — either way NOT 401/403.
        var adminRead = await admin.GetAsync("/api/v1/audit-log");
        adminRead.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        adminRead.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);

        // 5. No API can modify or delete an audit entry — the endpoint is read-only, so
        // every mutating verb is rejected (no route → 404/405, never a success).
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            var mutate = await admin.SendAsync(new HttpRequestMessage(method, "/api/v1/audit-log"));
            mutate.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
