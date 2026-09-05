using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Touchpoint"/> to the tenant-schema <c>touchpoints</c> table (DB-08, explicit
/// columns). <see cref="Touchpoint.Channels"/> is the PostgreSQL <c>text[]</c> array column.
/// </summary>
public sealed class TouchpointConfiguration : IEntityTypeConfiguration<Touchpoint>
{
    public void Configure(EntityTypeBuilder<Touchpoint> builder)
    {
        builder.ToTable("touchpoints");

        builder.HasKey(t => t.TouchpointId);

        builder.Property(t => t.TouchpointId).HasColumnName("touchpoint_id").ValueGeneratedNever();
        builder.Property(t => t.StageId).HasColumnName("stage_id");
        builder.Property(t => t.Name).HasColumnName("name");
        builder.Property(t => t.Description).HasColumnName("description");
        builder.Property(t => t.Channels).HasColumnName("channels").HasColumnType("text[]");
        builder.Property(t => t.Importance).HasColumnName("importance");
        builder.Property(t => t.IsMot).HasColumnName("is_mot");
        builder.Property(t => t.IsMandatory).HasColumnName("is_mandatory");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
    }
}
