using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Scenarios;

/// <summary>
/// End-to-end business journey for User Story 2 — persona-baseline provisioning and
/// the FR-007 authority split. Walks the full cycle across multiple endpoints:
/// P-01 and P-07 both create users (each provisioned from its persona's baseline);
/// P-07 is blocked from assigning a CX-domain module (403) while P-01 succeeds; the
/// target's permission-snapshot version is bumped on the change and the target's own
/// live session reflects the new module on its next refresh (FR-013).
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class PersonaBaselineAndEnforcementTests
{
    // Canonical DOC-02 module ids: SurveyBuilder is CX-domain (P-01 only); the other two are P-07's.
    private const string CxModule = "SurveyBuilder";
    private const string UserManagement = "UserManagement";
    private const string TenantConfiguration = "TenantConfiguration";

    private readonly UserManagementApplicationFactory _factory;

    public PersonaBaselineAndEnforcementTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task P01_and_P07_provision_users_and_only_P01_assigns_cx_modules()
    {
        await _factory.SeedPersonaBaselinesAsync();
        var p01 = await SignedInClientAsync("P-01");
        var p07 = await SignedInClientAsync("P-07");

        // 1. P-01 creates a user → provisioned from that persona's baseline (P-07 baseline
        //    grants exactly the two non-CX modules).
        var newUsername = UniqueEmail();
        var create = await p01.PostAsJsonAsync("/api/v1/users", new { username = newUsername, persona = "P-07", password = "ValidP@ss1" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdId = (await create.ReadJsonAsync()).GetProperty("userId").GetGuid();

        var createdDetail = await (await p01.GetAsync($"/api/v1/users/{createdId}")).ReadJsonAsync();
        ModuleIds(createdDetail.GetProperty("permissionModuleAssignments"))
            .Should().BeEquivalentTo([UserManagement, TenantConfiguration]);

        // 2. P-07 can also create users.
        var p07Create = await p07.PostAsJsonAsync("/api/v1/users", new { username = UniqueEmail(), persona = "P-03", password = "ValidP@ss1" });
        p07Create.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. A live, enrolled target with no permissions yet — its own session sees no modules.
        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");
        var targetClient = await AuthenticateAsync(target);
        var beforeSnapshot = (await (await targetClient.GetAsync("/api/v1/auth/session")).ReadJsonAsync())
            .GetProperty("permissionSnapshot");
        HasModule(beforeSnapshot, CxModule).Should().BeFalse();
        var versionBefore = beforeSnapshot.GetProperty("version").GetInt64();

        // 4. P-07 may NOT assign a CX-domain module — blocked at the data layer (403).
        var forbidden = await p07.PutAsJsonAsync(
            $"/api/v1/users/{target.UserId}/permissions",
            new { assignments = new[] { new { moduleId = CxModule, allowedModes = new[] { "View" } } } });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await forbidden.ReadErrorCodeAsync()).Should().Be("permissions.forbidden_module");

        // 5. P-01 assigns the same CX-domain module → success.
        var assign = await p01.PutAsJsonAsync(
            $"/api/v1/users/{target.UserId}/permissions",
            new { assignments = new[] { new { moduleId = CxModule, allowedModes = new[] { "View", "Manage" } } } });
        assign.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. The target's snapshot version was incremented by the change.
        var targetDetail = await (await p01.GetAsync($"/api/v1/users/{target.UserId}")).ReadJsonAsync();
        targetDetail.GetProperty("lastPermissionSnapshotVersion").GetInt64().Should().BeGreaterThan(versionBefore);
        ModuleIds(targetDetail.GetProperty("permissionModuleAssignments")).Should().Contain(CxModule);

        // 7. The target's existing session reflects the updated permissions on its next
        //    refresh (version mismatch rebuilds the snapshot — FR-013).
        var afterSnapshot = (await (await targetClient.GetAsync("/api/v1/auth/session")).ReadJsonAsync())
            .GetProperty("permissionSnapshot");
        HasModule(afterSnapshot, CxModule).Should().BeTrue();
        afterSnapshot.GetProperty("version").GetInt64().Should().BeGreaterThan(versionBefore);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<HttpClient> SignedInClientAsync(string persona) =>
        await AuthenticateAsync(await _factory.SeedEnrolledUserAsync(persona: persona));

    /// <summary>Drives login → MFA verify for a seeded enrolled user; returns a client carrying its bearer token.</summary>
    private async Task<HttpClient> AuthenticateAsync(SeededUser actor)
    {
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

    private static IEnumerable<string> ModuleIds(JsonElement assignments) =>
        assignments.EnumerateArray().Select(a => a.GetProperty("moduleId").GetString()!);

    private static bool HasModule(JsonElement snapshot, string moduleId) =>
        snapshot.GetProperty("modules").TryGetProperty(moduleId, out _);

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";
}
