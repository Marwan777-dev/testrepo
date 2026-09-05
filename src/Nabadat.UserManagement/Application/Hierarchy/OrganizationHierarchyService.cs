using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Application.Hierarchy.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Hierarchy;

namespace Nabadat.UserManagement.Application.Hierarchy;

/// <summary>
/// EF Core <see cref="IOrganizationHierarchyService"/> over <see cref="ITenantDbContext"/>
/// (read-only; M-10 never writes <c>organization_hierarchy_nodes</c>). The path-prefix
/// query uses <c>StartsWith</c>, which EF translates to <c>path LIKE 'prefix%'</c> (with
/// escaping) and is served by the <c>varchar_pattern_ops</c> index on <c>path</c>.
/// </summary>
public sealed class OrganizationHierarchyService : IOrganizationHierarchyService
{
    private readonly ITenantDbContext _context;

    public OrganizationHierarchyService(ITenantDbContext context) => _context = context;

    public async Task<OrganizationHierarchyNode?> GetNodeAsync(Guid nodeId, CancellationToken ct = default) =>
        await _context.OrganizationHierarchyNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.NodeId == nodeId, ct);

    public async Task<IReadOnlyList<OrganizationHierarchyNode>> GetNodesByPathPrefixAsync(
        string pathPrefix,
        CancellationToken ct = default) =>
        await _context.OrganizationHierarchyNodes
            .AsNoTracking()
            .Where(n => n.Path.StartsWith(pathPrefix))
            .OrderBy(n => n.Path)
            .ToListAsync(ct);
}
