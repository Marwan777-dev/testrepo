using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CustomAuthorizationRule"/> to <c>custom_authorization_rules</c>
/// (DB-08): <c>allowed_actions</c> is a <c>varchar[]</c>, <c>parameter_scope_assignments</c>
/// is jsonb.</summary>
public sealed class CustomAuthorizationRuleConfiguration : IEntityTypeConfiguration<CustomAuthorizationRule>
{
    public void Configure(EntityTypeBuilder<CustomAuthorizationRule> builder)
    {
        builder.ToTable("custom_authorization_rules");

        builder.HasKey(r => r.RuleId);

        builder.Property(r => r.RuleId).HasColumnName("rule_id").ValueGeneratedNever();
        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.AllowedActions)
            .HasColumnName("allowed_actions")
            .HasConversion(UserManagementConverters.StringArray, UserManagementConverters.StringArrayComparer);
        builder.Property(r => r.ParameterScopeAssignments)
            .HasColumnName("parameter_scope_assignments")
            .HasColumnType("jsonb")
            .HasConversion(
                UserManagementConverters.Jsonb<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                UserManagementConverters.JsonbComparer<IReadOnlyDictionary<string, IReadOnlyList<string>>>());
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => r.UserId);

        builder.HasOne<TenantUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
