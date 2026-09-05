using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Scenarios;

/// <summary>
/// End-to-end business journey for User Story 3 — data scope + hierarchy cascade
/// (T111). Walks the full cycle: M-13 parameter definitions are ingested; a P-01 admin
/// assigns a branch parameter scope (<c>Riyadh, Dammam</c>) and an organization
/// hierarchy node to a user; reading the scope back returns only the permitted branch
/// values; and the resolved permission snapshot includes the node's descendants while
/// excluding its siblings (downward-only cascade, AC2).
///
/// Hierarchy nodes are M-11/M-13-owned (M-10 reads only), so the test seeds them with a
/// direct insert — there is no M-10 write path for <c>organization_hierarchy_nodes</c>.
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class DataScopeAndHierarchyCascadeTests
{
    private const string Branch = "branch";

    private readonly UserManagementApplicationFactory _factory;

    public DataScopeAndHierarchyCascadeTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_assigns_branch_scope_and_hierarchy_node_then_cascade_includes_descendants_only()
    {
        var admin = await SignedInClientAsync("P-01");

        // 1. M-13 supplies the branch parameter definition.
        var ingest = await admin.PostAsJsonAsync("/api/v1/authorization/scope/parameters", new
        {
            sourceModule = "M-13",
            parameters = new[]
            {
                new { name = Branch, label = "Branch", allowedValues = new[] { "Riyadh", "Jeddah", "Dammam" } },
            },
        });
        ingest.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Seed a hierarchy: Region A (root) → Branch X (descendant); Region B (sibling).
        var regionA = Guid.NewGuid();
        var branchX = Guid.NewGuid();
        var regionB = Guid.NewGuid();
        await InsertNodeAsync(regionA, null, "Region A", "/region-a/");
        await InsertNodeAsync(branchX, regionA, "Branch X", "/region-a/branch-x/");
        await InsertNodeAsync(regionB, null, "Region B", "/region-b/");

        var target = await _factory.SeedEnrolledUserAsync(persona: "P-03");

        // 3. Assign branch scope (Riyadh, Dammam — NOT Jeddah) and the Region A node.
        var put = await admin.PutAsJsonAsync($"/api/v1/users/{target.UserId}/scope", new
        {
            organizationNodeId = regionA,
            dataScopeAssignments = new[] { new { parameterName = Branch, allowedValues = new[] { "Riyadh", "Dammam" } } },
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Reading the scope back returns the node and only the permitted branch values.
        var scope = await (await admin.GetAsync($"/api/v1/users/{target.UserId}/scope")).ReadJsonAsync();
        scope.GetProperty("organizationNodeId").GetGuid().Should().Be(regionA);
        var branch = scope.GetProperty("dataScopeAssignments").EnumerateArray()
            .Single(a => a.GetProperty("parameterName").GetString() == Branch);
        branch.GetProperty("allowedValues").EnumerateArray().Select(v => v.GetString())
            .Should().BeEquivalentTo(["Riyadh", "Dammam"]);

        // 5. The resolved permission snapshot cascades downward only: Branch X (descendant)
        //    is included; Region B (sibling) is excluded.
        using var services = _factory.Services.CreateScope();
        var permissions = services.ServiceProvider.GetRequiredService<IUserManagementPermissionService>();
        var snapshot = await permissions.GetPermissionSnapshotAsync(target.UserId);

        snapshot.HierarchyNodeId.Should().Be(regionA);
        snapshot.HierarchyDescendantIds.Should().Contain(branchX);
        snapshot.HierarchyDescendantIds.Should().NotContain(regionB);
        snapshot.ScopeAssignments[Branch].Should().BeEquivalentTo(["Riyadh", "Dammam"]);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task InsertNodeAsync(Guid nodeId, Guid? parentNodeId, string name, string path)
    {
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO organization_hierarchy_nodes
                (node_id, parent_node_id, name, path, source, external_ref, created_at, updated_at)
            VALUES (@id, @parent, @name, @path, 'manual', NULL, now(), now());
            """, connection);
        command.Parameters.AddWithValue("id", nodeId);
        command.Parameters.AddWithValue("parent", (object?)parentNodeId ?? DBNull.Value);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("path", path);
        await command.ExecuteNonQueryAsync();
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
