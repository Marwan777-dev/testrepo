using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="QuestionsSet"/> to the tenant-schema <c>questions_sets</c> table (DB-08, explicit
/// columns — data-model.md §2.3). <c>order</c> is a reserved word, mapped explicitly;
/// <c>selection_mode</c> persists as its lowercase DDL token (<c>random</c> / <c>low_response</c>).
/// </summary>
public sealed class QuestionsSetConfiguration : IEntityTypeConfiguration<QuestionsSet>
{
    public void Configure(EntityTypeBuilder<QuestionsSet> builder)
    {
        builder.ToTable("questions_sets");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.SectionId).HasColumnName("section_id");
        builder.Property(s => s.Title).HasColumnName("title");
        builder.Property(s => s.Description).HasColumnName("description");
        builder.Property(s => s.SelectionMode).HasColumnName("selection_mode").HasConversion(
            mode => mode == QuestionsSetSelectionMode.LowResponse ? "low_response" : "random",
            text => text == "low_response" ? QuestionsSetSelectionMode.LowResponse : QuestionsSetSelectionMode.Random);
        builder.Property(s => s.Count).HasColumnName("count");
        builder.Property(s => s.Order).HasColumnName("order");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.RowVersion).HasColumnName("row_version");

        builder.HasIndex(s => s.SectionId).HasDatabaseName("idx_questions_sets_section_id");
    }
}
