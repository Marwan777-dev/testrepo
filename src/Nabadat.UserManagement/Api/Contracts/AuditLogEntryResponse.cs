using System.Text.Json.Nodes;

namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>
/// One audit event in the <c>GET /api/v1/audit-log</c> response — a read-only
/// projection of an M-17 <c>event_log</c> row (permissions-api.md). <see cref="OldValue"/>
/// / <see cref="NewValue"/> are the stored jsonb payloads re-emitted as JSON objects
/// (not strings); <c>null</c> means the row had none.
/// </summary>
public sealed record AuditLogEntryResponse
{
    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public Guid? ActorId { get; init; }

    /// <summary>Resolved at read time; <c>"[erased]"</c> when the actor has been erased (GP-03).</summary>
    public string? ActorUsername { get; init; }

    public string? EntityType { get; init; }

    public Guid? EntityId { get; init; }

    public JsonNode? OldValue { get; init; }

    public JsonNode? NewValue { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public Guid? CorrelationId { get; init; }
}
