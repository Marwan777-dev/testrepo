using System.Text.Json;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Application.Events;

/// <summary>
/// EF <see cref="IM17EventPublisher"/>: maps the <see cref="CustomerJourneyManagementEvent"/> to an <see cref="EventLog"/>
/// (serializing <see cref="CustomerJourneyManagementEvent.OldValue"/>/<see cref="CustomerJourneyManagementEvent.NewValue"/> to jsonb text),
/// tracks it on the scoped <see cref="ITenantDbContext"/>, and saves. Called inside an
/// <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>, the wrapping
/// transaction makes this audit row commit or roll back together with the business change (FR-015).
/// Replaces the raw-Npgsql publisher that wrote directly on the caller's <c>NpgsqlTransaction</c>.
/// </summary>
public sealed class M17EventPublisher : IM17EventPublisher
{
    private readonly ITenantDbContext _context;

    public M17EventPublisher(ITenantDbContext context) => _context = context;

    public async Task PublishAsync(CustomerJourneyManagementEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        _context.EventLogs.Add(new EventLog
        {
            EventId = Guid.NewGuid(),
            EventType = evt.EventType,
            ActorId = evt.ActorId,
            ActorPersona = evt.ActorPersona,
            EntityType = evt.EntityType,
            EntityId = evt.EntityId,
            OldValue = SerializeOrNull(evt.OldValue),
            NewValue = SerializeOrNull(evt.NewValue),
            OccurredAtUtc = evt.OccurredAtUtc,
            CorrelationId = evt.CorrelationId,
        });

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>Serializes an event payload to jsonb text; <c>null</c> stays SQL NULL.</summary>
    private static string? SerializeOrNull(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value);
}
