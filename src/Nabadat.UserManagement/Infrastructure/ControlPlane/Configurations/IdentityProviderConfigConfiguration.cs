using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

namespace Nabadat.UserManagement.Infrastructure.ControlPlane.Configurations;

/// <summary>Maps <see cref="IdentityProviderConfig"/> to the control-plane table
/// <c>identity_provider_configs</c> (DB-08). <see cref="IdentityProviderType"/> persists as
/// its kebab-case wire string; <c>settings</c> is open jsonb.</summary>
public sealed class IdentityProviderConfigConfiguration : IEntityTypeConfiguration<IdentityProviderConfig>
{
    public void Configure(EntityTypeBuilder<IdentityProviderConfig> builder)
    {
        builder.ToTable("identity_provider_configs");

        builder.HasKey(c => c.ProviderId);

        builder.Property(c => c.ProviderId).HasColumnName("provider_id").ValueGeneratedNever();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.ProviderType)
            .HasColumnName("provider_type")
            .HasConversion(v => v.ToWire(), v => IdentityProviderTypeExtensions.ParseProviderType(v));
        builder.Property(c => c.Settings)
            .HasColumnName("settings")
            .HasColumnType("jsonb")
            .HasConversion(
                UserManagementConverters.Jsonb<IReadOnlyDictionary<string, object?>>(),
                UserManagementConverters.JsonbComparer<IReadOnlyDictionary<string, object?>>());
        builder.Property(c => c.IsActive).HasColumnName("is_active");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => new { c.TenantId, c.ProviderType }).IsUnique();
    }
}
