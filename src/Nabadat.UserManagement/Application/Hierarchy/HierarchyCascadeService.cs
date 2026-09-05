using Nabadat.UserManagement.Application.Hierarchy.Interfaces;

namespace Nabadat.UserManagement.Application.Hierarchy;

/// <summary>
/// Resolves the <b>downward-only</b> organization-hierarchy cascade for scope
/// enforcement (T103, US3). A user assigned a node may act on that node's descendants
/// — never its siblings or ancestors. Descendants are derived from the materialized
/// <c>path</c>: node B descends from node A iff <c>B.path</c> is prefixed by
/// <c>A.path</c> (and B is not A itself). The result is what the
/// <c>PermissionSnapshot</c> caches as <c>HierarchyDescendantIds</c> during session
/// creation/refresh, so request-time checks avoid a tree walk.
///
/// Reads go through <see cref="IOrganizationHierarchyService"/>, which abstracts the
/// hierarchy source (M-11 manual vs. M-13 integration) — M-10 only ever reads.
/// </summary>
public sealed class HierarchyCascadeService
{
    private readonly IOrganizationHierarchyService _hierarchy;

    public HierarchyCascadeService(IOrganizationHierarchyService hierarchy) => _hierarchy = hierarchy;

    /// <summary>
    /// The ids of every node beneath <paramref name="nodeId"/> in the tree (descendants
    /// only, excluding the node itself). Empty when the node is unknown.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetDescendantNodeIdsAsync(Guid nodeId, CancellationToken ct = default)
    {
        var node = await _hierarchy.GetNodeAsync(nodeId, ct);
        if (node is null)
        {
            return [];
        }

        var candidates = await _hierarchy.GetNodesByPathPrefixAsync(node.Path, ct);
        return candidates
            .Where(n => n.NodeId != nodeId && n.Path.StartsWith(node.Path, StringComparison.Ordinal))
            .Select(n => n.NodeId)
            .ToList();
    }
}
