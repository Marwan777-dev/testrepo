namespace Nabadat.UserManagement.Application.Events.Dtos;

/// <summary>
/// An auditable M-10 event, published to M-17's <c>event_log</c> in the same
/// transaction as the action that produced it (FR-015). <see cref="OldValue"/> /
/// <see cref="NewValue"/> are serialized to jsonb; <c>null</c> means "not applicable"
/// (e.g. no prior value on a create).
/// </summary>
public sealed record UserManagementEvent
{
    public required string EventType { get; init; }

    public required Guid ActorId { get; init; }

    /// <summary>Actor persona <c>P-01</c>..<c>P-08</c>.</summary>
    public required string ActorPersona { get; init; }

    public required string EntityType { get; init; }

    public required Guid EntityId { get; init; }

    public object? OldValue { get; init; }

    public object? NewValue { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required Guid CorrelationId { get; init; }
}
