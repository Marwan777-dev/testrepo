using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AuthSession"/> to <c>auth_sessions</c> (DB-08). The
/// <see cref="PermissionSnapshot"/> value object persists to the <c>permission_snapshot</c>
/// jsonb column.</summary>
public sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("auth_sessions");

        builder.HasKey(s => s.SessionId);

        builder.Property(s => s.SessionId).HasColumnName("session_id").ValueGeneratedNever();
        builder.Property(s => s.UserId).HasColumnName("user_id");
        builder.Property(s => s.TokenHash).HasColumnName("token_hash");
        builder.Property(s => s.IssuedAtUtc).HasColumnName("issued_at_utc");
        builder.Property(s => s.AbsoluteExpiresAtUtc).HasColumnName("absolute_expires_at_utc");
        builder.Property(s => s.LastActivityAtUtc).HasColumnName("last_activity_at_utc");
        builder.Property(s => s.SlidingTtlMinutes).HasColumnName("sliding_ttl_minutes");
        builder.Property(s => s.PermissionSnapshotVersion).HasColumnName("permission_snapshot_version");
        builder.Property(s => s.PermissionSnapshot)
            .HasColumnName("permission_snapshot")
            .HasColumnType("jsonb")
            .HasConversion(UserManagementConverters.Jsonb<PermissionSnapshot>(), UserManagementConverters.JsonbComparer<PermissionSnapshot>());
        builder.Property(s => s.IsActive).HasColumnName("is_active");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(s => s.TokenHash).IsUnique();

        builder.HasOne<TenantUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
