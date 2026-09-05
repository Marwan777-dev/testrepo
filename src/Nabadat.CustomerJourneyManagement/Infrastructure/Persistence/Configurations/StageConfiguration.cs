using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Stage"/> to the tenant-schema <c>stages</c> table (DB-08, explicit columns).</summary>
public sealed class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("stages");

        builder.HasKey(s => s.StageId);

        builder.Property(s => s.StageId).HasColumnName("stage_id").ValueGeneratedNever();
        builder.Property(s => s.JourneyId).HasColumnName("journey_id");
        builder.Property(s => s.SequenceNumber).HasColumnName("sequence_number");
        builder.Property(s => s.Name).HasColumnName("name");
        builder.Property(s => s.Description).HasColumnName("description");
        builder.Property(s => s.CustomerGoal).HasColumnName("customer_goal");
        builder.Property(s => s.ExpectedEmotion).HasColumnName("expected_emotion");
        builder.Property(s => s.DurationHint).HasColumnName("duration_hint");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
    }
}
