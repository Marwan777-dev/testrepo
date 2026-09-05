using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="KpiPerspective"/> to <c>kpi_perspectives</c> (DB-08, explicit columns). 0..10
/// rows per KPI, FK to <c>kpi_definitions.id</c> ON DELETE CASCADE (full-replace save semantics,
/// FR-028).
/// </summary>
public sealed class KpiPerspectiveConfiguration : IEntityTypeConfiguration<KpiPerspective>
{
    public void Configure(EntityTypeBuilder<KpiPerspective> builder)
    {
        builder.ToTable("kpi_perspectives");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.KpiId).HasColumnName("kpi_id");
        builder.Property(p => p.Label).HasColumnName("label");
        builder.Property(p => p.DisplayOrder).HasColumnName("display_order");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");

        builder.HasOne<KpiDefinition>()
            .WithMany()
            .HasForeignKey(p => p.KpiId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.KpiId, p.DisplayOrder });
    }
}
