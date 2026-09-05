using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="KpiBinding"/> to the tenant-schema <c>kpi_bindings</c> table (DB-08, explicit columns).</summary>
public sealed class KpiBindingConfiguration : IEntityTypeConfiguration<KpiBinding>
{
    public void Configure(EntityTypeBuilder<KpiBinding> builder)
    {
        builder.ToTable("kpi_bindings");

        builder.HasKey(b => b.KpiBindingId);

        builder.Property(b => b.KpiBindingId).HasColumnName("kpi_binding_id").ValueGeneratedNever();
        builder.Property(b => b.TouchpointId).HasColumnName("touchpoint_id");
        builder.Property(b => b.KpiType).HasColumnName("kpi_type");
        builder.Property(b => b.IsPlatformStandard).HasColumnName("is_platform_standard");
        builder.Property(b => b.KpiId).HasColumnName("kpi_id");
        builder.Property(b => b.Weight).HasColumnName("weight").HasColumnType("numeric(5,2)");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
    }
}
