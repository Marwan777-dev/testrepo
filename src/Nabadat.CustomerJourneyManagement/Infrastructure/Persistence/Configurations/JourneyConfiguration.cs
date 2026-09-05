using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Journey"/> to the tenant-schema <c>journeys</c> table (DB-08, explicit columns).</summary>
public sealed class JourneyConfiguration : IEntityTypeConfiguration<Journey>
{
    public void Configure(EntityTypeBuilder<Journey> builder)
    {
        builder.ToTable("journeys");

        builder.HasKey(j => j.JourneyId);

        builder.Property(j => j.JourneyId).HasColumnName("journey_id").ValueGeneratedNever();
        builder.Property(j => j.Name).HasColumnName("name");
        builder.Property(j => j.Description).HasColumnName("description");
        builder.Property(j => j.JourneyType).HasColumnName("journey_type");
        builder.Property(j => j.Status).HasColumnName("status");
        builder.Property(j => j.CreatedBy).HasColumnName("created_by");
        builder.Property(j => j.UpdatedBy).HasColumnName("updated_by");
        builder.Property(j => j.CreatedAt).HasColumnName("created_at");
        builder.Property(j => j.UpdatedAt).HasColumnName("updated_at");
    }
}
