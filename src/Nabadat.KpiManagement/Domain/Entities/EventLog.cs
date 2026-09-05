namespace Nabadat.KpiManagement.Domain.Entities;

/// <summary>
/// An audit row in M-17's tenant-schema <c>event_log</c> table. M-06 appends one of these to the
/// <b>same</b> EF context as the business change it audits, so the KPI/settings write and its audit
/// row commit (or roll back) atomically via one transaction (data-model.md §8 / DB-08). The
/// <c>event_log</c> table is shared M-17 infrastructure — created by whichever module baseline runs
/// first (M-06's baseline also issues <c>CREATE TABLE IF NOT EXISTS event_log</c> so a standalone
/// M-06 test schema has it). <see cref="OldValue"/> / <see cref="NewValue"/> hold the serialized
/// jsonb payloads (<c>null</c> = not applicable, e.g. no prior value on a create). Mirrors the M-10
/// / M-16 <c>EventLog</c> mapping.
/// </summary>
public sealed class EventLog
{
    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public Guid? ActorId { get; set; }

    /// <summary>Actor persona <c>P-01</c>..<c>P-08</c>; null for unattributed events.</summary>
    public string? ActorPersona { get; set; }

    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    /// <summary>Serialized jsonb payload of the prior state, or null.</summary>
    public string? OldValue { get; set; }

    /// <summary>Serialized jsonb payload of the new state, or null.</summary>
    public string? NewValue { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public Guid? CorrelationId { get; set; }
}
