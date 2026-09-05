using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="KpiDefinition"/> to <c>kpi_definitions</c> (DB-08, explicit columns onto the
/// <c>KpiManagement_Baseline.sql</c> schema). Enum-typed properties persist as their PascalCase
/// member name via <c>HasConversion&lt;string&gt;()</c> — matching the seed data and the CHECK
/// constraints (e.g. <c>"Standard"</c>, <c>"WeightedComposite"</c>, <c>"Scale0_10"</c>).
/// </summary>
public sealed class KpiDefinitionConfiguration : IEntityTypeConfiguration<KpiDefinition>
{
    public void Configure(EntityTypeBuilder<KpiDefinition> builder)
    {
        builder.ToTable("kpi_definitions");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id).HasColumnName("id");
        builder.Property(k => k.ShortName).HasColumnName("short_name");
        builder.Property(k => k.FullName).HasColumnName("full_name");
        builder.Property(k => k.KpiType).HasColumnName("kpi_type").HasConversion<string>();
        builder.Property(k => k.IsComposite).HasColumnName("is_composite");
        builder.Property(k => k.CalculationMethod).HasColumnName("calculation_method").HasConversion<string>();
        builder.Property(k => k.TopNValue).HasColumnName("top_n_value");
        builder.Property(k => k.Scale).HasColumnName("scale").HasConversion<string>();
        builder.Property(k => k.MinScaleDescriptionEn).HasColumnName("min_scale_description_en");
        builder.Property(k => k.MinScaleDescriptionAr).HasColumnName("min_scale_description_ar");
        builder.Property(k => k.MaxScaleDescriptionEn).HasColumnName("max_scale_description_en");
        builder.Property(k => k.MaxScaleDescriptionAr).HasColumnName("max_scale_description_ar");
        builder.Property(k => k.RepresentationStyle).HasColumnName("representation_style").HasConversion<string>();
        builder.Property(k => k.EmojiSet).HasColumnName("emoji_set").HasConversion<string>();
        builder.Property(k => k.Target).HasColumnName("target");
        builder.Property(k => k.IsActive).HasColumnName("is_active");
        builder.Property(k => k.ShowOnDashboard).HasColumnName("show_on_dashboard");
        builder.Property(k => k.CreatedAt).HasColumnName("created_at");
        builder.Property(k => k.CreatedBy).HasColumnName("created_by");
        builder.Property(k => k.UpdatedAt).HasColumnName("updated_at");
        builder.Property(k => k.UpdatedBy).HasColumnName("updated_by");

        // Case-insensitive Short Name uniqueness is enforced by the functional unique index
        // kpi_definitions_short_name_lower_uniq in the SQL baseline (EF cannot model LOWER(...)).
    }
}
