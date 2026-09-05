using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

namespace Nabadat.UserManagement.Infrastructure.ControlPlane.Configurations;

/// <summary>Maps <see cref="PersonaBaseline"/> to the control-plane table
/// <c>persona_baselines</c> (DB-08). Both <c>permission_module_assignments</c> and
/// <c>default_data_scope_rules</c> are jsonb.</summary>
public sealed class PersonaBaselineConfiguration : IEntityTypeConfiguration<PersonaBaseline>
{
    public void Configure(EntityTypeBuilder<PersonaBaseline> builder)
    {
        builder.ToTable("persona_baselines");

        builder.HasKey(b => b.BaselineId);

        builder.Property(b => b.BaselineId).HasColumnName("baseline_id").ValueGeneratedNever();
        builder.Property(b => b.TenantId).HasColumnName("tenant_id");
        builder.Property(b => b.PersonaId).HasColumnName("persona_id");
        builder.Property(b => b.PermissionModuleAssignments)
            .HasColumnName("permission_module_assignments")
            .HasColumnType("jsonb")
            .HasConversion(
                UserManagementConverters.Jsonb<IReadOnlyList<PersonaModuleAssignment>>(),
                UserManagementConverters.JsonbComparer<IReadOnlyList<PersonaModuleAssignment>>());
        builder.Property(b => b.DefaultDataScopeRules)
            .HasColumnName("default_data_scope_rules")
            .HasColumnType("jsonb")
            .HasConversion(
                UserManagementConverters.Jsonb<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                UserManagementConverters.JsonbComparer<IReadOnlyDictionary<string, IReadOnlyList<string>>>());
        builder.Property(b => b.IsCustomised).HasColumnName("is_customised");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(b => new { b.TenantId, b.PersonaId }).IsUnique();
    }
}
