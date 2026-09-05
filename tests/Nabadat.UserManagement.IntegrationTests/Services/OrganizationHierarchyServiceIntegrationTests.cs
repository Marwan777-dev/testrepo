using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Infrastructure.Persistence;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Xunit;
using Nabadat.UserManagement.Application.Hierarchy.Interfaces;
using Nabadat.UserManagement.Application.Hierarchy;

namespace Nabadat.UserManagement.IntegrationTests.Services;

/// <summary>
/// Integration coverage for the EF <c>OrganizationHierarchyService</c> (the new
/// context-holding data-access service that replaced the raw-Npgsql
/// <c>OrganizationHierarchyReader</c>). Seeds a small tree via <see cref="TenantDbContext"/>
/// and verifies the <c>path LIKE 'prefix%'</c> translation and single-node read against
/// the Testcontainers Postgres. The descendant-cascade logic that composes this service
/// is unit-tested in <c>HierarchyCascadeServiceTests</c>.
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class OrganizationHierarchyServiceIntegrationTests
{
    private readonly UserManagementApplicationFactory _factory;

    public OrganizationHierarchyServiceIntegrationTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetNodesByPathPrefix_returns_the_prefix_subtree_and_excludes_unrelated_nodes()
    {
        // Unique root path so the test is isolated from other rows in the shared DB.
        var root = $"/it-{Guid.NewGuid():N}/";
        var rootId = await SeedNodeAsync(null, "Root", root);
        var regionId = await SeedNodeAsync(rootId, "Region", root + "region/");
        var branchId = await SeedNodeAsync(regionId, "Branch", root + "region/branch/");
        var unrelatedId = await SeedNodeAsync(null, "Other", $"/other-{Guid.NewGuid():N}/");

        var nodes = await ResolveAsync(s => s.GetNodesByPathPrefixAsync(root));

        var ids = nodes.Select(n => n.NodeId).ToList();
        ids.Should().Contain(new[] { rootId, regionId, branchId });
        ids.Should().NotContain(unrelatedId);
        nodes.Select(n => n.Path).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetNode_returns_null_when_the_node_is_unknown()
    {
        var node = await ResolveAsync(s => s.GetNodeAsync(Guid.NewGuid()));
        node.Should().BeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> SeedNodeAsync(Guid? parentId, string name, string path)
    {
        var id = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        context.OrganizationHierarchyNodes.Add(new OrganizationHierarchyNode
        {
            NodeId = id,
            ParentNodeId = parentId,
            Name = name,
            Path = path,
            Source = "manual",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();
        return id;
    }

    private async Task<T> ResolveAsync<T>(Func<IOrganizationHierarchyService, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrganizationHierarchyService>();
        return await query(service);
    }
}
