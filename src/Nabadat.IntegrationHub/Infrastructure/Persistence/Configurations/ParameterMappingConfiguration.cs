using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.IntegrationHub.Domain.Entities;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="ParameterMapping"/> to <c>parameter_mappings</c> (DB-08), with the intra-module FK to
/// <see cref="Parameter"/>.
/// <para>Not modelled here: VR-F08's <b>case-insensitive</b> uniqueness of <c>source_value</c> within a
/// parameter, which is the functional unique index <c>parameter_mappings_parameter_source_lower_uniq</c>
/// in the baseline (EF cannot express <c>LOWER(...)</c> indexes).</para>
/// </summary>
public sealed class ParameterMappingConfiguration : IEntityTypeConfiguration<ParameterMapping>
{
    public void Configure(EntityTypeBuilder<ParameterMapping> builder)
    {
        builder.ToTable("parameter_mappings");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.ParameterId).HasColumnName("parameter_id");
        builder.Property(m => m.SourceValue).HasColumnName("source_value");
        builder.Property(m => m.DisplayEn).HasColumnName("display_en");
        builder.Property(m => m.DisplayAr).HasColumnName("display_ar");
        builder.Property(m => m.Status).HasColumnName("status");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Parameter>()
            .WithMany()
            .HasForeignKey(m => m.ParameterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
