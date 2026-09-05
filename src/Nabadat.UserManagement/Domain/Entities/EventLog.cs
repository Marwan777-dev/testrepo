namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// An audit row in M-10's tenant-schema <c>event_log</c> table. M-10 appends one of
/// these to the <b>same</b> EF context as the business change it audits, so the
/// business write and its audit row commit (or roll back) atomically via a single
/// <c>SaveChangesAsync</c> (FR-015 / database-constitution Article 7 / router DB-08).
/// <see cref="OldValue"/> / <see cref="NewValue"/> hold the serialized jsonb payloads
/// (<c>null</c> = not applicable, e.g. no prior value on a create).
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
