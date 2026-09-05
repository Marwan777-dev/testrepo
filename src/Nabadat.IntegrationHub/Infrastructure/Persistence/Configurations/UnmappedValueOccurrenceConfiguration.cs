using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.IntegrationHub.Domain.Entities;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="UnmappedValueOccurrence"/> to <c>unmapped_value_occurrences</c> (DB-08) — the backing
/// rows for SCR-07's 7-day queue (FR-S7-02), with the intra-module FK to <see cref="Parameter"/>. The
/// case-insensitive <c>(parameter_id, LOWER(raw_value))</c> uniqueness used by the repeat-sighting upsert
/// lives in the baseline as a functional index.
/// </summary>
public sealed class UnmappedValueOccurrenceConfiguration : IEntityTypeConfiguration<UnmappedValueOccurrence>
{
    public void Configure(EntityTypeBuilder<UnmappedValueOccurrence> builder)
    {
        builder.ToTable("unmapped_value_occurrences");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.ParameterId).HasColumnName("parameter_id");
        builder.Property(o => o.RawValue).HasColumnName("raw_value");
        builder.Property(o => o.FirstSeenAt).HasColumnName("first_seen_at");
        builder.Property(o => o.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(o => o.OccurrenceCount).HasColumnName("occurrence_count");

        builder.HasOne<Parameter>()
            .WithMany()
            .HasForeignKey(o => o.ParameterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
