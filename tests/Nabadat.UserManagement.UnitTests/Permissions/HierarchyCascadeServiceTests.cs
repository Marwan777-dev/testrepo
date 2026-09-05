using FluentAssertions;
using Nabadat.UserManagement.Application.Hierarchy;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;
using Nabadat.UserManagement.Application.Hierarchy.Interfaces;

namespace Nabadat.UserManagement.UnitTests.Permissions;

/// <summary>
/// T098 — write-first unit tests for <c>HierarchyCascadeService</c> (T103, US3).
/// Hierarchy scope cascades <b>downward only</b>: a user assigned a node may act on
/// that node's descendants, never its siblings or ancestors. The service resolves
/// descendants from the materialized <c>path</c> (a node is a descendant iff its
/// path is prefixed by the target node's path), and the result is what the
/// <c>PermissionSnapshot</c> caches as <c>HierarchyDescendantIds</c> so requests
/// avoid a tree walk.
///
/// The reader port (<c>IOrganizationHierarchyService</c>) here returns the whole flat
/// tree regardless of the requested prefix, so the prefix filtering is genuinely
/// exercised in the service under test rather than delegated to the fake. The real
/// repository narrows with a <c>path LIKE '{path}%'</c> query.
///
/// The fine-grained custom-action rule case from the task brief
/// (<c>EvaluateActionPermission</c>) belongs to <c>PermissionEvaluationService</c>
/// (T105) — out of this slice's scope — and is intentionally not asserted here.
///
/// These production types do not exist yet, so this project fails to COMPILE — the
/// valid red state for a write-first story (CLAUDE.md Unit Test Policy, rule 7).
/// </summary>
public sealed class HierarchyCascadeServiceTests
{
    // A small tree:
    //   /root/                       (Root)
    //   ├── /root/region-a/          (RegionA)
    //   │   ├── /root/region-a/branch-x/   (BranchX)
    //   │   └── /root/region-a/branch-y/   (BranchY)
    //   └── /root/region-b/          (RegionB — sibling of RegionA)
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid RegionA = Guid.NewGuid();
    private static readonly Guid RegionB = Guid.NewGuid();
    private static readonly Guid BranchX = Guid.NewGuid();
    private static readonly Guid BranchY = Guid.NewGuid();

    private static readonly OrganizationHierarchyNode[] Tree =
    [
        new() { NodeId = Root, ParentNodeId = null, Name = "Root", Path = "/root/" },
        new() { NodeId = RegionA, ParentNodeId = Root, Name = "Region A", Path = "/root/region-a/" },
        new() { NodeId = RegionB, ParentNodeId = Root, Name = "Region B", Path = "/root/region-b/" },
        new() { NodeId = BranchX, ParentNodeId = RegionA, Name = "Branch X", Path = "/root/region-a/branch-x/" },
        new() { NodeId = BranchY, ParentNodeId = RegionA, Name = "Branch Y", Path = "/root/region-a/branch-y/" },
    ];

    private readonly IOrganizationHierarchyService _reader = Substitute.For<IOrganizationHierarchyService>();

    public HierarchyCascadeServiceTests()
    {
        foreach (var node in Tree)
        {
            _reader.GetNodeAsync(node.NodeId, Arg.Any<CancellationToken>()).Returns(node);
        }

        // Deliberately return the WHOLE tree for any prefix so the service does the filtering.
        _reader.GetNodesByPathPrefixAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Tree);
    }

    private HierarchyCascadeService CreateSut() => new(_reader);

    // ── Case 1: descendants are included via the materialized path ──
    [Fact]
    public async Task GetDescendantNodeIds_includes_children_via_materialized_path()
    {
        var descendants = await CreateSut().GetDescendantNodeIdsAsync(RegionA);

        descendants.Should().Contain([BranchX, BranchY]);
    }

    // ── Case 2: siblings and ancestors are excluded (downward-only cascade) ──
    [Fact]
    public async Task GetDescendantNodeIds_excludes_siblings_ancestors_and_self()
    {
        var descendants = await CreateSut().GetDescendantNodeIdsAsync(RegionA);

        descendants.Should().NotContain(RegionB);  // sibling
        descendants.Should().NotContain(Root);      // ancestor
        descendants.Should().NotContain(RegionA);   // self is not its own descendant
    }

    // ── Case 3: the root node cascades to every other node in the tree ──
    [Fact]
    public async Task GetDescendantNodeIds_for_root_returns_all_descendant_nodes()
    {
        var descendants = await CreateSut().GetDescendantNodeIdsAsync(Root);

        descendants.Should().BeEquivalentTo([RegionA, RegionB, BranchX, BranchY]);
    }

    // ── Case 4: an unknown node yields no scope (defensive default) ──
    [Fact]
    public async Task GetDescendantNodeIds_returns_empty_when_node_unknown()
    {
        var unknown = Guid.NewGuid();
        _reader.GetNodeAsync(unknown, Arg.Any<CancellationToken>()).Returns((OrganizationHierarchyNode?)null);

        var descendants = await CreateSut().GetDescendantNodeIdsAsync(unknown);

        descendants.Should().BeEmpty();
    }
}
