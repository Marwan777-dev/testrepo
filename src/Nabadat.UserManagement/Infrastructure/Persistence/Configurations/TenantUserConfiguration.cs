using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="TenantUser"/> to <c>tenant_users</c> (DB-08, explicit columns).
/// <see cref="UserStatus"/> persists as its lowercase wire string (varchar(32)).</summary>
public sealed class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
{
    public void Configure(EntityTypeBuilder<TenantUser> builder)
    {
        builder.ToTable("tenant_users");

        builder.HasKey(u => u.UserId);

        builder.Property(u => u.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(u => u.Username).HasColumnName("username");
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash");
        builder.Property(u => u.IsMfaEnrolled).HasColumnName("is_mfa_enrolled");
        builder.Property(u => u.MfaSecretEncrypted).HasColumnName("mfa_secret_encrypted");
        builder.Property(u => u.MfaSecretKeyRef).HasColumnName("mfa_secret_key_ref");
        builder.Property(u => u.LastUsedTotpStep).HasColumnName("last_used_totp_step");
        builder.Property(u => u.Persona).HasColumnName("persona");
        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasConversion(v => v.ToWire(), v => UserStatusExtensions.ParseStatus(v));
        builder.Property(u => u.FailedAttemptCount).HasColumnName("failed_attempt_count");
        builder.Property(u => u.LockedUntilUtc).HasColumnName("locked_until_utc");
        builder.Property(u => u.OrganizationNodeId).HasColumnName("organization_node_id");
        builder.Property(u => u.LastPermissionSnapshotVersion).HasColumnName("last_permission_snapshot_version");
        builder.Property(u => u.RequiresPasswordChange).HasColumnName("requires_password_change");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.Username).IsUnique();
    }
}
