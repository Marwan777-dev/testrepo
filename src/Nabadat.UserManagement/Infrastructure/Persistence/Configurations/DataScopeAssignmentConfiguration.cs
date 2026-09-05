using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="DataScopeAssignment"/> to <c>data_scope_assignments</c>
/// (DB-08); <c>allowed_values</c> is a <c>varchar[]</c>.</summary>
public sealed class DataScopeAssignmentConfiguration : IEntityTypeConfiguration<DataScopeAssignment>
{
    public void Configure(EntityTypeBuilder<DataScopeAssignment> builder)
    {
        builder.ToTable("data_scope_assignments");

        builder.HasKey(a => a.AssignmentId);

        builder.Property(a => a.AssignmentId).HasColumnName("assignment_id").ValueGeneratedNever();
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.ParameterName).HasColumnName("parameter_name");
        builder.Property(a => a.AllowedValues)
            .HasColumnName("allowed_values")
            .HasConversion(UserManagementConverters.StringArray, UserManagementConverters.StringArrayComparer);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(a => new { a.UserId, a.ParameterName }).IsUnique();

        builder.HasOne<TenantUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
