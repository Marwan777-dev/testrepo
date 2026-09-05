using System.Text.Json;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Events;

/// <summary>
/// Appends M-06's audit events to M-17's shared <c>event_log</c> by tracking them on the scoped
/// <see cref="ITenantDbContext"/> and saving. M-06 emits only the registered <c>settings.changed</c>
/// event type (plan.md §Event Catalogue); these helpers pin that type and the right
/// <c>entity_type</c> (<c>kpi</c> / <c>organization</c>) so callers can't mismatch them. The
/// per-action detail (<c>created</c> / <c>updated</c> / <c>deactivated</c> / the <c>cxi_side_effect</c>
/// payload, …) travels inside the <paramref name="newValue"/> diff object (data-model.md §8).
///
/// <para>There is no transaction parameter: when the caller performs the KPI/settings write and
/// this publish inside one <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>,
/// the audit row and the change commit (or roll back) together — the context IS the unit of work.
/// Time is supplied by the caller (a <c>TimeProvider</c>-derived <paramref name="occurredAtUtc"/>),
/// never read here, per the time-injection rule.</para>
/// </summary>
public sealed class KpiEventPublisher
{
    private const string SettingsChanged = "settings.changed";
    private const string EntityKpi = "kpi";
    private const string EntityOrganization = "organization";

    private readonly ITenantDbContext _context;

    public KpiEventPublisher(ITenantDbContext context) => _context = context;

    /// <summary>
    /// Emits a <c>settings.changed</c> event for a KPI (entity_type <c>kpi</c>) — catalogue,
    /// configuration, and activation changes. <paramref name="oldValue"/> is null on create.
    /// </summary>
    public Task PublishKpiSettingsChangedAsync(
        Guid kpiId,
        Guid actorId,
        string actorPersona,
        object? oldValue,
        object newValue,
        DateTimeOffset occurredAtUtc,
        Guid correlationId,
        CancellationToken ct = default) =>
        AppendAsync(EntityKpi, kpiId, actorId, actorPersona, oldValue, newValue, occurredAtUtc, correlationId, ct);

    /// <summary>
    /// Emits a <c>settings.changed</c> event for the tenant Organization settings (entity_type
    /// <c>organization</c>) — name/industry/logo edits.
    /// </summary>
    public Task PublishOrganizationSettingsChangedAsync(
        Guid organizationId,
        Guid actorId,
        string actorPersona,
        object? oldValue,
        object newValue,
        DateTimeOffset occurredAtUtc,
        Guid correlationId,
        CancellationToken ct = default) =>
        AppendAsync(EntityOrganization, organizationId, actorId, actorPersona, oldValue, newValue, occurredAtUtc, correlationId, ct);

    private async Task AppendAsync(
        string entityType,
        Guid entityId,
        Guid actorId,
        string actorPersona,
        object? oldValue,
        object newValue,
        DateTimeOffset occurredAtUtc,
        Guid correlationId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(newValue);

        _context.EventLogs.Add(new EventLog
        {
            EventId = Guid.NewGuid(),
            EventType = SettingsChanged,
            ActorId = actorId,
            ActorPersona = actorPersona,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = SerializeOrNull(oldValue),
            NewValue = JsonSerializer.Serialize(newValue),
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId,
        });

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>Serializes a payload to jsonb text; <c>null</c> stays SQL NULL.</summary>
    private static string? SerializeOrNull(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value);
}
