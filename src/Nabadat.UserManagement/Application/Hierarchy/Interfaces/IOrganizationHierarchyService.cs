using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Application.Hierarchy;

namespace Nabadat.UserManagement.Application.Hierarchy.Interfaces;

/// <summary>
/// Read-only data-access service over <c>organization_hierarchy_nodes</c> (EF Core /
/// <c>TenantDbContext</c>). M-10 never writes this table — it is populated by M-11
/// (manual) or M-13 (integration); this service abstracts that source so
/// <c>HierarchyCascadeService</c> resolves descendants the same way regardless.
/// Replaces the former raw-Npgsql <c>IOrganizationHierarchyReader</c>.
/// </summary>
public interface IOrganizationHierarchyService
{
    /// <summary>Reads a single node (for its materialized <c>path</c>); null if unknown.</summary>
    Task<OrganizationHierarchyNode?> GetNodeAsync(Guid nodeId, CancellationToken ct = default);

    /// <summary>
    /// Returns nodes whose materialized <c>path</c> begins with <paramref name="pathPrefix"/>
    /// (the prefix node and everything beneath it), ordered by <c>path</c>.
    /// </summary>
    Task<IReadOnlyList<OrganizationHierarchyNode>> GetNodesByPathPrefixAsync(
        string pathPrefix,
        CancellationToken ct = default);
}
