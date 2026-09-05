using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="JourneyScore"/> to the tenant-schema <c>journey_scores</c> table (DB-08,
/// explicit columns). <see cref="JourneyScore.CompositeScore"/> maps to the <c>journey_score</c>
/// column; <see cref="JourneyScore.StageScores"/> / <see cref="JourneyScore.TouchpointScores"/>
/// are opaque <c>jsonb</c> payloads.
/// </summary>
public sealed class JourneyScoreConfiguration : IEntityTypeConfiguration<JourneyScore>
{
    public void Configure(EntityTypeBuilder<JourneyScore> builder)
    {
        builder.ToTable("journey_scores");

        builder.HasKey(s => s.JourneyScoreId);

        builder.Property(s => s.JourneyScoreId).HasColumnName("journey_score_id").ValueGeneratedNever();
        builder.Property(s => s.JourneyId).HasColumnName("journey_id");
        builder.Property(s => s.ComputedAt).HasColumnName("computed_at");
        builder.Property(s => s.CompositeScore).HasColumnName("journey_score").HasColumnType("numeric(5,2)");
        builder.Property(s => s.StageScores).HasColumnName("stage_scores").HasColumnType("jsonb");
        builder.Property(s => s.TouchpointScores).HasColumnName("touchpoint_scores").HasColumnType("jsonb");

        builder.HasIndex(s => s.JourneyId).IsUnique();
    }
}
