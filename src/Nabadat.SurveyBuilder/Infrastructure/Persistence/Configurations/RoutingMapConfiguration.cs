using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="RoutingMap"/> to the tenant-schema <c>routing_maps</c> table (DB-08, explicit
/// columns — data-model.md §2.5). No <c>row_version</c> column exists on this table. The
/// same-survey and standalone-only invariants are enforced at the App layer
/// (<c>RoutingEligibilityService</c>), not here. Indexes mirror <c>_Baseline.sql</c>: a unique
/// <c>(source_question_id, answer_key)</c> plus survey / target lookups (the latter powers the
/// FR-2.7 reset-to-default cascade).
/// </summary>
public sealed class RoutingMapConfiguration : IEntityTypeConfiguration<RoutingMap>
{
    public void Configure(EntityTypeBuilder<RoutingMap> builder)
    {
        builder.ToTable("routing_maps");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.SurveyId).HasColumnName("survey_id");
        builder.Property(r => r.SourceQuestionId).HasColumnName("source_question_id");
        builder.Property(r => r.AnswerKey).HasColumnName("answer_key");
        builder.Property(r => r.TargetQuestionId).HasColumnName("target_question_id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => new { r.SourceQuestionId, r.AnswerKey })
            .IsUnique()
            .HasDatabaseName("idx_routing_maps_source_answer");
        builder.HasIndex(r => r.SurveyId).HasDatabaseName("idx_routing_maps_survey_id");
        builder.HasIndex(r => r.TargetQuestionId).HasDatabaseName("idx_routing_maps_target_question_id");
    }
}
