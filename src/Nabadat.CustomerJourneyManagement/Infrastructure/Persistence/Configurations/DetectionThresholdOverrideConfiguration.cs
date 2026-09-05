using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="DetectionThresholdOverride"/> to the tenant-schema
/// <c>detection_threshold_overrides</c> table (DB-08, explicit columns). Threshold columns are
/// nullable <c>numeric(5,2)</c> — null means "inherit from the parent".
/// </summary>
public sealed class DetectionThresholdOverrideConfiguration : IEntityTypeConfiguration<DetectionThresholdOverride>
{
    public void Configure(EntityTypeBuilder<DetectionThresholdOverride> builder)
    {
        builder.ToTable("detection_threshold_overrides");

        builder.HasKey(o => o.OverrideId);

        builder.Property(o => o.OverrideId).HasColumnName("override_id").ValueGeneratedNever();
        builder.Property(o => o.DetectionConfigId).HasColumnName("detection_config_id");
        builder.Property(o => o.ScopeType).HasColumnName("scope_type");
        builder.Property(o => o.ScopeId).HasColumnName("scope_id");
        builder.Property(o => o.PainThreshold).HasColumnName("pain_threshold").HasColumnType("numeric(5,2)");
        builder.Property(o => o.HappyThreshold).HasColumnName("happy_threshold").HasColumnType("numeric(5,2)");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
    }
}
