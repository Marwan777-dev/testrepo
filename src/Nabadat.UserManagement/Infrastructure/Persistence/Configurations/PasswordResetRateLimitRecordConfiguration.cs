using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="PasswordResetRateLimitRecord"/> to the tenant-schema table
/// <c>password_reset_rate_limit_records</c>. Explicit <c>HasColumnName</c> per property
/// (DB-08 — no naming-convention package). The primary key is the <c>bytea</c> email
/// hash, never generated.
/// </summary>
public sealed class PasswordResetRateLimitRecordConfiguration : IEntityTypeConfiguration<PasswordResetRateLimitRecord>
{
    public void Configure(EntityTypeBuilder<PasswordResetRateLimitRecord> builder)
    {
        builder.ToTable("password_reset_rate_limit_records");

        builder.HasKey(r => r.EmailHash);

        builder.Property(r => r.EmailHash).HasColumnName("email_hash").ValueGeneratedNever();
        builder.Property(r => r.WindowStartUtc).HasColumnName("window_start_utc");
        builder.Property(r => r.RequestCount).HasColumnName("request_count");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
    }
}
