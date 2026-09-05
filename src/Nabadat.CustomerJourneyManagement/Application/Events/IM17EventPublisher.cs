namespace Nabadat.CustomerJourneyManagement.Application.Events;

/// <summary>
/// Appends one M-16 audit event to M-17's <c>event_log</c> by tracking it on the shared
/// <c>ITenantDbContext</c> and saving (FR-015). There is no transaction parameter anymore: when the
/// caller performs the business write and this publish inside one
/// <c>ITenantDbContext.ExecuteAsync</c>, the audit row and the change commit (or roll back)
/// together — the context IS the unit of work. Outside such a block the save is its own atomic
/// single-row insert.
/// </summary>
public interface IM17EventPublisher
{
    /// <summary>Tracks one event row and saves. Throws on failure (rolling back the ambient unit of work).</summary>
    Task PublishAsync(CustomerJourneyManagementEvent evt, CancellationToken ct = default);
}
