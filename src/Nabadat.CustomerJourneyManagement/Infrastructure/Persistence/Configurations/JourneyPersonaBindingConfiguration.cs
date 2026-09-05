using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="JourneyPersonaBinding"/> to the tenant-schema <c>journey_persona_bindings</c>
/// join table (DB-08). Composite primary key <c>(journey_id, persona_id)</c>.
/// </summary>
public sealed class JourneyPersonaBindingConfiguration : IEntityTypeConfiguration<JourneyPersonaBinding>
{
    public void Configure(EntityTypeBuilder<JourneyPersonaBinding> builder)
    {
        builder.ToTable("journey_persona_bindings");

        builder.HasKey(b => new { b.JourneyId, b.PersonaId });

        builder.Property(b => b.JourneyId).HasColumnName("journey_id");
        builder.Property(b => b.PersonaId).HasColumnName("persona_id");
        builder.Property(b => b.BoundAt).HasColumnName("bound_at");
    }
}
