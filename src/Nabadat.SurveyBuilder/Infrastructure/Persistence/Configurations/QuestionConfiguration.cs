using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Question"/> to the tenant-schema <c>questions</c> table (DB-08, explicit columns
/// — data-model.md §2.4, research.md §5). The <c>type_payload</c> jsonb uses
/// <see cref="QuestionTypePayloadConverter"/>. <see cref="Question.Type"/> and
/// <see cref="Question.Subtype"/> persist as their enum names, except the two whose DDL text differs
/// from the C# identifier: <see cref="QuestionType.Kpi"/> ↔ <c>"KPI"</c> and
/// <see cref="QuestionSubType.KpiScale"/> ↔ <c>"KPIScale"</c> (the ck_questions_kpi_code_present
/// constraint compares against those literals).
/// </summary>
public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(q => q.SurveyId).HasColumnName("survey_id");
        builder.Property(q => q.SectionId).HasColumnName("section_id");
        builder.Property(q => q.SetId).HasColumnName("set_id");

        builder.Property(q => q.Type)
            .HasColumnName("type")
            .HasConversion(
                type => type == QuestionType.Kpi ? "KPI" : type.ToString(),
                text => text == "KPI" ? QuestionType.Kpi : Enum.Parse<QuestionType>(text));

        builder.Property(q => q.Subtype)
            .HasColumnName("subtype")
            .HasConversion(
                sub => sub == QuestionSubType.KpiScale ? "KPIScale" : sub.ToString(),
                text => text == "KPIScale" ? QuestionSubType.KpiScale : Enum.Parse<QuestionSubType>(text));

        builder.Property(q => q.Text).HasColumnName("text");
        builder.Property(q => q.Description).HasColumnName("description");
        builder.Property(q => q.Required).HasColumnName("required");
        builder.Property(q => q.Comments).HasColumnName("comments");
        builder.Property(q => q.CommentLabel).HasColumnName("comment_label");
        builder.Property(q => q.CommentMaxLength).HasColumnName("comment_max_length");
        builder.Property(q => q.Sentiment).HasColumnName("sentiment");
        builder.Property(q => q.KpiCode).HasColumnName("kpi_code");
        builder.Property(q => q.Perspective).HasColumnName("perspective");
        builder.Property(q => q.BoundJourneyOn).HasColumnName("bound_journey_on");
        builder.Property(q => q.StageId).HasColumnName("stage_id");
        builder.Property(q => q.TouchpointId).HasColumnName("touchpoint_id");
        builder.Property(q => q.TypePayload)
            .HasColumnName("type_payload")
            .HasColumnType("jsonb")
            .HasConversion(new QuestionTypePayloadConverter());
        builder.Property(q => q.Order).HasColumnName("order");
        builder.Property(q => q.CreatedAt).HasColumnName("created_at");
        builder.Property(q => q.UpdatedAt).HasColumnName("updated_at");
        builder.Property(q => q.RowVersion).HasColumnName("row_version");

        builder.HasIndex(q => q.SurveyId).HasDatabaseName("idx_questions_survey_id");
        builder.HasIndex(q => new { q.SectionId, q.Order }).HasDatabaseName("idx_questions_section_id_order");
    }
}
