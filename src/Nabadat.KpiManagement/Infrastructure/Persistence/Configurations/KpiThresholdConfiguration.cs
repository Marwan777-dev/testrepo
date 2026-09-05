using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="KpiThreshold"/> to <c>kpi_thresholds</c> (DB-08, explicit columns). One row
/// per KPI — <see cref="KpiThreshold.KpiId"/> is the primary key and the FK to
/// <c>kpi_definitions.id</c>. The ascending invariant (<c>lower_bound &lt; x &lt; y &lt; upper_bound</c>)
/// is enforced by the SQL baseline CHECK constraint.
/// </summary>
public sealed class KpiThresholdConfiguration : IEntityTypeConfiguration<KpiThreshold>
{
    public void Configure(EntityTypeBuilder<KpiThreshold> builder)
    {
        builder.ToTable("kpi_thresholds");

        builder.HasKey(t => t.KpiId);

        builder.Property(t => t.KpiId).HasColumnName("kpi_id").ValueGeneratedNever();
        builder.Property(t => t.LowerBound).HasColumnName("lower_bound");
        builder.Property(t => t.X).HasColumnName("x");
        builder.Property(t => t.Y).HasColumnName("y");
        builder.Property(t => t.UpperBound).HasColumnName("upper_bound");

        builder.HasOne<KpiDefinition>()
            .WithOne()
            .HasForeignKey<KpiThreshold>(t => t.KpiId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
