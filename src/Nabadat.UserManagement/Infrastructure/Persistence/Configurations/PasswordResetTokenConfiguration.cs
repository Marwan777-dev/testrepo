using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="PasswordResetToken"/> to <c>password_reset_tokens</c> (DB-08).</summary>
public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(t => t.TokenId);

        builder.Property(t => t.TokenId).HasColumnName("token_id").ValueGeneratedNever();
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.TokenHash).HasColumnName("token_hash");
        builder.Property(t => t.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(t => t.UsedAtUtc).HasColumnName("used_at_utc");
        builder.Property(t => t.Revoked).HasColumnName("revoked");
        builder.Property(t => t.IssuedBy).HasColumnName("issued_by");
        builder.Property(t => t.IssuedVia).HasColumnName("issued_via");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.HasOne<TenantUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
