using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Theme"/> to the tenant-schema <c>themes</c> table (DB-08, explicit columns —
/// data-model.md §2.6). <c>background_type</c> persists as its enum name; <c>background_config</c>
/// uses <see cref="BackgroundConfigConverter"/>; the four <c>advanced_*</c> columns are opaque jsonb
/// strings. <c>survey_id</c> is unique (1:1 with the survey).
/// </summary>
public sealed class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.ToTable("themes");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.SurveyId).HasColumnName("survey_id");
        builder.Property(t => t.PrimaryColor).HasColumnName("primary_color");
        builder.Property(t => t.TextColor).HasColumnName("text_color");
        builder.Property(t => t.ButtonRadiusPx).HasColumnName("button_radius_px");
        builder.Property(t => t.ButtonBorderColor).HasColumnName("button_border_color");
        builder.Property(t => t.ButtonTextColor).HasColumnName("button_text_color");
        builder.Property(t => t.HeaderShowLogo).HasColumnName("header_show_logo");
        builder.Property(t => t.HeaderShowTitle).HasColumnName("header_show_title");
        builder.Property(t => t.HeaderAlignment).HasColumnName("header_alignment");
        builder.Property(t => t.FooterText).HasColumnName("footer_text");
        builder.Property(t => t.BackgroundType).HasColumnName("background_type").HasConversion<string>();
        builder.Property(t => t.BackgroundConfig)
            .HasColumnName("background_config")
            .HasColumnType("jsonb")
            .HasConversion(new BackgroundConfigConverter());
        builder.Property(t => t.BackgroundOpacity).HasColumnName("background_opacity");
        builder.Property(t => t.AdvancedStatusColors).HasColumnName("advanced_status_colors").HasColumnType("jsonb");
        builder.Property(t => t.AdvancedSurfaces).HasColumnName("advanced_surfaces").HasColumnType("jsonb");
        builder.Property(t => t.AdvancedTypography).HasColumnName("advanced_typography").HasColumnType("jsonb");
        builder.Property(t => t.AdvancedLayout).HasColumnName("advanced_layout").HasColumnType("jsonb");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.RowVersion).HasColumnName("row_version");

        builder.HasIndex(t => t.SurveyId).IsUnique().HasDatabaseName("idx_themes_survey_id");
    }
}
