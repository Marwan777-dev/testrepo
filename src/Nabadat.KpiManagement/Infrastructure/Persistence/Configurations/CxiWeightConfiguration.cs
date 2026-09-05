using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="CxiWeight"/> to <c>cxi_weights</c> (DB-08, explicit columns). Composite key
/// (<c>cxi_kpi_id</c>, <c>member_kpi_id</c>); both FKs reference <c>kpi_definitions.id</c> ON
/// DELETE RESTRICT. The <c>weight &gt; 0</c> and <c>member_kpi_id &lt;&gt; cxi_kpi_id</c> invariants
/// are enforced by SQL baseline CHECK constraints.
/// </summary>
public sealed class CxiWeightConfiguration : IEntityTypeConfiguration<CxiWeight>
{
    public void Configure(EntityTypeBuilder<CxiWeight> builder)
    {
        builder.ToTable("cxi_weights");

        builder.HasKey(w => new { w.CxiKpiId, w.MemberKpiId });

        builder.Property(w => w.CxiKpiId).HasColumnName("cxi_kpi_id");
        builder.Property(w => w.MemberKpiId).HasColumnName("member_kpi_id");
        builder.Property(w => w.Weight).HasColumnName("weight");
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");

        builder.HasOne<KpiDefinition>()
            .WithMany()
            .HasForeignKey(w => w.CxiKpiId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<KpiDefinition>()
            .WithMany()
            .HasForeignKey(w => w.MemberKpiId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => w.MemberKpiId);  // deactivation-cascade lookups
    }
}
