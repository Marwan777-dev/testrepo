namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// A tenant organisational scope node (tenant-schema table
/// <c>organization_hierarchy_nodes</c>). Owned by M-11 (manual) or M-13
/// (integration); <b>M-10 reads only and never writes this table</b>.
/// </summary>
public sealed class OrganizationHierarchyNode
{
    public Guid NodeId { get; set; }

    /// <summary>Parent node; null for root nodes.</summary>
    public Guid? ParentNodeId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Materialized path, e.g. <c>/root/region-a/branch-x/</c> (LIKE-prefix queries).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary><c>manual</c> | <c>integration</c>.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>External id for M-13-supplied nodes; null otherwise.</summary>
    public string? ExternalRef { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
