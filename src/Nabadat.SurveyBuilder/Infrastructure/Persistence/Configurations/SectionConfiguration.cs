using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Section"/> to the tenant-schema <c>sections</c> table (DB-08, explicit columns —
/// data-model.md §2.2). <c>order</c> is a reserved word, mapped explicitly; <c>(survey_id, order)</c>
/// is unique.
/// </summary>
public sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("sections");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.SurveyId).HasColumnName("survey_id");
        builder.Property(s => s.Name).HasColumnName("name");
        builder.Property(s => s.Description).HasColumnName("description");
        builder.Property(s => s.Order).HasColumnName("order");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.RowVersion).HasColumnName("row_version");

        builder.HasIndex(s => new { s.SurveyId, s.Order })
            .IsUnique()
            .HasDatabaseName("idx_sections_survey_id_order");
    }
}
