using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="PermissionModuleAssignment"/> to
/// <c>permission_module_assignments</c> (DB-08); <c>allowed_modes</c> is a
/// <c>varchar[]</c>.</summary>
public sealed class PermissionModuleAssignmentConfiguration : IEntityTypeConfiguration<PermissionModuleAssignment>
{
    public void Configure(EntityTypeBuilder<PermissionModuleAssignment> builder)
    {
        builder.ToTable("permission_module_assignments");

        builder.HasKey(a => a.AssignmentId);

        builder.Property(a => a.AssignmentId).HasColumnName("assignment_id").ValueGeneratedNever();
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.ModuleId).HasColumnName("module_id");
        builder.Property(a => a.AllowedModes)
            .HasColumnName("allowed_modes")
            .HasConversion(UserManagementConverters.StringArray, UserManagementConverters.StringArrayComparer);
        builder.Property(a => a.AssignedBy).HasColumnName("assigned_by");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(a => new { a.UserId, a.ModuleId }).IsUnique();

        // FK to tenant_users so EF orders the user insert before its assignments
        // (matches permission_module_assignments_user_id_fkey).
        builder.HasOne<TenantUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
