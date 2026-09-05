using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="OrganizationSettings"/> to <c>organization_settings</c> (DB-08, explicit columns,
/// data-model.md §2.1). The singleton partial unique index and the <c>industry_valid</c> CHECK live
/// in the SQL baseline (<c>KpiManagement_OrganizationSettings.sql</c>), not here.
/// </summary>
public sealed class OrganizationSettingsConfiguration : IEntityTypeConfiguration<OrganizationSettings>
{
    public void Configure(EntityTypeBuilder<OrganizationSettings> builder)
    {
        builder.ToTable("organization_settings");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.Name).HasColumnName("name");
        builder.Property(o => o.LogoBlobRef).HasColumnName("logo_blob_ref");
        builder.Property(o => o.Industry).HasColumnName("industry");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.CreatedBy).HasColumnName("created_by");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by");
    }
}
