using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="EventLog"/> to the shared M-17 tenant-schema <c>event_log</c> table. Explicit
/// <c>HasColumnName</c> per property (DB-08); <c>old_value</c> / <c>new_value</c> are <c>jsonb</c>.
/// The table is append-only — M-06 only ever inserts rows here. Mirrors the M-10 / M-16 mapping.
/// </summary>
public sealed class EventLogConfiguration : IEntityTypeConfiguration<EventLog>
{
    public void Configure(EntityTypeBuilder<EventLog> builder)
    {
        builder.ToTable("event_log");

        builder.HasKey(e => e.EventId);

        builder.Property(e => e.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(e => e.EventType).HasColumnName("event_type");
        builder.Property(e => e.ActorId).HasColumnName("actor_id");
        builder.Property(e => e.ActorPersona).HasColumnName("actor_persona");
        builder.Property(e => e.EntityType).HasColumnName("entity_type");
        builder.Property(e => e.EntityId).HasColumnName("entity_id");
        builder.Property(e => e.OldValue).HasColumnName("old_value").HasColumnType("jsonb");
        builder.Property(e => e.NewValue).HasColumnName("new_value").HasColumnType("jsonb");
        builder.Property(e => e.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id");
    }
}
