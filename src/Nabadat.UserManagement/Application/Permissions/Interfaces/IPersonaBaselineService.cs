using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Application.Permissions.Interfaces;

/// <summary>
/// Per-tenant persona authorization-matrix baselines over the control-plane
/// <c>persona_baselines</c> table (EF / <c>ControlPlaneDbContext</c>). Implemented by
/// <c>PersonaBaselineService</c>. Exposes the reads consumed by user provisioning and the
/// baselines controller, the provisioning seed, and the edit use case (which runs the FR-007
/// authority guard, marks the baseline customised, and audits). Control-plane writes are a
/// separate unit of work and are never atomic with a tenant write (DB-08).
/// </summary>
public interface IPersonaBaselineService
{
    /// <summary>Reads one persona's baseline for a tenant, or null if not seeded.</summary>
    Task<PersonaBaseline?> GetAsync(Guid tenantId, string personaId, CancellationToken ct = default);

    /// <summary>The authorization-matrix default module grants for a persona (empty if unseeded).</summary>
    Task<IReadOnlyList<PersonaModuleAssignment>> GetDefaultPermissionsForPersonaAsync(
        Guid tenantId,
        string personaId,
        CancellationToken ct = default);

    /// <summary>All persona baselines for a tenant, ordered by persona id.</summary>
    Task<IReadOnlyList<PersonaBaseline>> GetAllBaselinesAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Replaces a persona baseline's module assignments (P-07 may set only non-CX modules), marks
    /// it customised, and emits a <c>persona_baseline.updated</c> event — control-plane write and
    /// tenant audit sequenced across databases (DB-08).
    /// </summary>
    Task UpdateBaselineAsync(
        Guid tenantId,
        Guid actorId,
        string actorPersona,
        string personaId,
        IReadOnlyList<PersonaModuleAssignment> assignments,
        CancellationToken ct = default);

    /// <summary>
    /// Idempotently seeds the 8 platform-default persona baselines (P-01..P-08) for a tenant at
    /// provisioning. Existing baselines are left untouched (never clobbers a customisation).
    /// </summary>
    Task SeedDefaultsAsync(Guid tenantId, CancellationToken ct = default);
}
