using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="ScoringConfig"/> to the tenant-schema <c>scoring_configs</c> table (DB-08,
/// explicit columns). The table is a <b>per-tenant singleton</b> (SRS §4.2.9 / §11.7): one row per
/// tenant, enforced by the SQL-owned unique index <c>scoring_configs_singleton_uniq</c> on
/// <c>((true))</c> (defined in the migration, not here). No <c>journey_id</c>.
/// </summary>
public sealed class ScoringConfigConfiguration : IEntityTypeConfiguration<ScoringConfig>
{
    public void Configure(EntityTypeBuilder<ScoringConfig> builder)
    {
        builder.ToTable("scoring_configs");

        builder.HasKey(c => c.ScoringConfigId);

        builder.Property(c => c.ScoringConfigId).HasColumnName("scoring_config_id").ValueGeneratedNever();
        builder.Property(c => c.Alpha).HasColumnName("alpha");
        builder.Property(c => c.MotMultiplier).HasColumnName("mot_multiplier");
        builder.Property(c => c.NFloor).HasColumnName("n_floor");
        builder.Property(c => c.FlagPercentile).HasColumnName("flag_percentile");
        builder.Property(c => c.RollingWindowDays).HasColumnName("rolling_window_days");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
    }
}
