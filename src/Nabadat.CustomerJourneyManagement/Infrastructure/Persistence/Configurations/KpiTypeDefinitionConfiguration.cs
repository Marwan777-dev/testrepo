using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="KpiTypeDefinition"/> to the tenant-schema <c>kpi_type_definitions</c> table (DB-08, explicit columns).</summary>
public sealed class KpiTypeDefinitionConfiguration : IEntityTypeConfiguration<KpiTypeDefinition>
{
    public void Configure(EntityTypeBuilder<KpiTypeDefinition> builder)
    {
        builder.ToTable("kpi_type_definitions");

        builder.HasKey(k => k.KpiTypeDefinitionId);

        builder.Property(k => k.KpiTypeDefinitionId).HasColumnName("kpi_type_definition_id").ValueGeneratedNever();
        builder.Property(k => k.TypeKey).HasColumnName("type_key");
        builder.Property(k => k.LabelAr).HasColumnName("label_ar");
        builder.Property(k => k.LabelEn).HasColumnName("label_en");
        builder.Property(k => k.ScoringDirection).HasColumnName("scoring_direction");
        builder.Property(k => k.CreatedAt).HasColumnName("created_at");
        builder.Property(k => k.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(k => k.TypeKey).IsUnique();
    }
}
