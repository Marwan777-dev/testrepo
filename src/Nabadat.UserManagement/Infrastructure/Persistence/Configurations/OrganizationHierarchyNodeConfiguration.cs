using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="OrganizationHierarchyNode"/> to
/// <c>organization_hierarchy_nodes</c> (DB-08). M-10 reads only; the self-referencing
/// parent FK is declared so descendant/path queries can navigate the tree.</summary>
public sealed class OrganizationHierarchyNodeConfiguration : IEntityTypeConfiguration<OrganizationHierarchyNode>
{
    public void Configure(EntityTypeBuilder<OrganizationHierarchyNode> builder)
    {
        builder.ToTable("organization_hierarchy_nodes");

        builder.HasKey(n => n.NodeId);

        builder.Property(n => n.NodeId).HasColumnName("node_id").ValueGeneratedNever();
        builder.Property(n => n.ParentNodeId).HasColumnName("parent_node_id");
        builder.Property(n => n.Name).HasColumnName("name");
        builder.Property(n => n.Path).HasColumnName("path");
        builder.Property(n => n.Source).HasColumnName("source");
        builder.Property(n => n.ExternalRef).HasColumnName("external_ref");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<OrganizationHierarchyNode>()
            .WithMany()
            .HasForeignKey(n => n.ParentNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.ParentNodeId);
    }
}
