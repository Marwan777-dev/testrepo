namespace Nabadat.UserManagement.Domain.ValueObjects;

/// <summary>A single audit event returned by <see cref="Interfaces.IAuditLogReader"/> (read model).</summary>
public sealed record AuditLogEntry
{
    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public Guid? ActorId { get; init; }

    public string? ActorPersona { get; init; }

    public string? EntityType { get; init; }

    public Guid? EntityId { get; init; }

    public string? OldValueJson { get; init; }

    public string? NewValueJson { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public Guid? CorrelationId { get; init; }
}
