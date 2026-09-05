using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="DetectionConfig"/> to the tenant-schema <c>detection_configs</c> table (DB-08, explicit columns).</summary>
public sealed class DetectionConfigConfiguration : IEntityTypeConfiguration<DetectionConfig>
{
    public void Configure(EntityTypeBuilder<DetectionConfig> builder)
    {
        builder.ToTable("detection_configs");

        builder.HasKey(c => c.DetectionConfigId);

        builder.Property(c => c.DetectionConfigId).HasColumnName("detection_config_id").ValueGeneratedNever();
        builder.Property(c => c.JourneyId).HasColumnName("journey_id");
        builder.Property(c => c.PainThreshold).HasColumnName("pain_threshold").HasColumnType("numeric(5,2)");
        builder.Property(c => c.HappyThreshold).HasColumnName("happy_threshold").HasColumnType("numeric(5,2)");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.JourneyId).IsUnique();
    }
}
