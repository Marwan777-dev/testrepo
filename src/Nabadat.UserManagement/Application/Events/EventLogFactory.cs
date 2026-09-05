using System.Text.Json;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Application.Events;

/// <summary>
/// Maps an <see cref="UserManagementEvent"/> to the <see cref="EventLog"/> entity for the EF flow
/// (DB-08): a service adds the result to its <c>TenantDbContext</c> and commits it
/// together with the business change in one <c>SaveChangesAsync</c>, so the audit row is
/// atomic with the action it records (FR-015). <c>OldValue</c>/<c>NewValue</c> are
/// serialized to jsonb with default options, matching the payload shape the prior
/// <c>M17EventPublisher</c> wrote, so existing audit consumers are unaffected.
/// </summary>
public static class EventLogFactory
{
    public static EventLog ToEventLog(this UserManagementEvent evt) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = evt.EventType,
        ActorId = evt.ActorId,
        ActorPersona = evt.ActorPersona,
        EntityType = evt.EntityType,
        EntityId = evt.EntityId,
        OldValue = Serialize(evt.OldValue),
        NewValue = Serialize(evt.NewValue),
        OccurredAtUtc = evt.OccurredAtUtc,
        CorrelationId = evt.CorrelationId,
    };

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value);
}
