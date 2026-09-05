using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Organization.Interfaces;

/// <summary>
/// Reads and writes the tenant's singleton <see cref="OrganizationSettings"/> row (M-06-internal,
/// re-homed from the never-built M-11, 2026-06-24). Writes are atomic with their M-17
/// <c>settings.changed</c> audit row (entity <c>organization</c>) — the row update and the event
/// commit in one transaction (data-model.md §8). A write whose payload matches current state is a
/// no-op: nothing is written and no event is emitted (settings-api.md).
/// </summary>
public interface IOrganizationSettingsStore
{
    /// <summary>Reads the singleton Organization row, or null if the tenant has none yet.</summary>
    Task<OrganizationSettings?> GetAsync(CancellationToken ct = default);

    /// <summary>Atomically updates Name + Industry and emits one <c>settings.changed</c> event (skipped
    /// on a no-op). Returns the resulting row.</summary>
    Task<OrganizationSettings> UpdateAsync(
        string name,
        string industry,
        Guid actorId,
        string actorPersona,
        Guid correlationId,
        DateTimeOffset occurredAt,
        CancellationToken ct = default);

    /// <summary>Atomically updates the <c>logo_blob_ref</c> and emits one <c>settings.changed</c> event
    /// (action <c>logo_replaced</c>, skipped on a no-op). Returns the resulting row.</summary>
    Task<OrganizationSettings> UpdateLogoRefAsync(
        string blobRef,
        Guid actorId,
        string actorPersona,
        Guid correlationId,
        DateTimeOffset occurredAt,
        CancellationToken ct = default);
}
