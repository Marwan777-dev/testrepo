using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Persona"/> to the tenant-schema <c>personas</c> table (DB-08, explicit columns).</summary>
public sealed class PersonaConfiguration : IEntityTypeConfiguration<Persona>
{
    public void Configure(EntityTypeBuilder<Persona> builder)
    {
        builder.ToTable("personas");

        builder.HasKey(p => p.PersonaId);

        builder.Property(p => p.PersonaId).HasColumnName("persona_id").ValueGeneratedNever();
        builder.Property(p => p.NameAr).HasColumnName("name_ar");
        builder.Property(p => p.NameEn).HasColumnName("name_en");
        builder.Property(p => p.DescriptionAr).HasColumnName("description_ar");
        builder.Property(p => p.DescriptionEn).HasColumnName("description_en");
        builder.Property(p => p.Status).HasColumnName("status");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
    }
}
