using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Template"/> to the tenant-schema <c>templates</c> table (DB-08, explicit columns —
/// data-model.md §2.8). <see cref="Template.Class"/> persists as its PascalCase name (matching the
/// <c>ck_templates_class</c> CHECK); <see cref="Template.Tags"/>/<see cref="Template.Sectors"/> map to
/// Postgres <c>text[]</c> (Npgsql). <c>row_version</c> is the app-managed ETag counter, not an EF
/// concurrency token. The functional/GIN indexes live in <c>_Baseline.sql</c> and are not modelled
/// here (EF owns no migrations for this schema).
/// </summary>
public sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("templates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.Class).HasColumnName("class").HasConversion<string>();
        builder.Property(t => t.NameEn).HasColumnName("name_en");
        builder.Property(t => t.NameAr).HasColumnName("name_ar");
        builder.Property(t => t.Description).HasColumnName("description");
        builder.Property(t => t.Tags).HasColumnName("tags");
        builder.Property(t => t.Sectors).HasColumnName("sectors");
        builder.Property(t => t.PreviewThumbnailFileHandle).HasColumnName("preview_thumbnail_file_handle");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.RowVersion).HasColumnName("row_version");
    }
}
