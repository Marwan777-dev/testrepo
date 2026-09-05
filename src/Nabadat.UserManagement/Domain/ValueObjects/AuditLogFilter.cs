namespace Nabadat.UserManagement.Domain.ValueObjects;

/// <summary>
/// Filter criteria for reading M-10's own audit events from the tenant-schema
/// <c>event_log</c> (via <see cref="Interfaces.IAuditLogReader"/>). All fields are
/// optional; null means "no constraint on this field".
/// </summary>
public sealed record AuditLogFilter
{
    public string? EventType { get; init; }

    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    public Guid? ActorId { get; init; }

    public Guid? EntityId { get; init; }
}
