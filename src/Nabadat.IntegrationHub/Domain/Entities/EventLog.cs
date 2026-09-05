namespace Nabadat.IntegrationHub.Domain.Entities;

/// <summary>
/// An audit row in M-17's tenant-schema <c>event_log</c> table. M-13 appends one of these to the
/// <b>same</b> EF context as the configuration change it audits, so the business write and its audit row
/// commit (or roll back) together in one transaction (DB-08). Every M-13 configuration change is
/// audited (Scope item 10) — <c>channel.created</c>, <c>parameter.disabled</c>,
/// <c>credential.revoked</c>, <c>mapping.replace_all</c>, and so on.
///
/// <para>The table is shared M-17 infrastructure, not M-13-owned: it is created by whichever module
/// baseline runs first, and M-13's baseline also issues <c>CREATE TABLE IF NOT EXISTS event_log</c> so a
/// standalone M-13 test schema has it. Mirrors the M-06 / M-10 / M-16 mapping exactly.</para>
///
/// <para><see cref="OldValue"/> / <see cref="NewValue"/> hold serialized <c>jsonb</c> payloads;
/// <c>null</c> means not applicable (e.g. no prior value on a create).</para>
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
