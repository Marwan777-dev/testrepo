using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Domain.Interfaces;

/// <summary>
/// Data-access service for <see cref="Persona"/> and its <see cref="JourneyPersonaBinding"/> join
/// (tenant-schema, EF-backed over <c>ITenantDbContext</c>). Personas follow the lifecycle Draft →
/// Active ↔ Inactive → Archived; only Active personas may be bound to journeys (guarded at the
/// service layer). Multi-step writes commit atomically with their M-17 event when the caller wraps
/// them in <c>ITenantDbContext.ExecuteAsync</c>.
/// </summary>
public interface IPersonaDataService
{
    /// <summary>Loads a single persona by id; null when it does not exist.</summary>
    Task<Persona?> GetByIdAsync(Guid personaId, CancellationToken ct = default);

    /// <summary>
    /// Personas optionally filtered by lifecycle <paramref name="status"/>. The journey
    /// binding selector passes <c>"Active"</c> to list only bindable personas.
    /// </summary>
    Task<IReadOnlyList<Persona>> ListAsync(string? status, CancellationToken ct = default);

    /// <summary>Inserts a new persona (tracks + saves; flushes within an ambient transaction).</summary>
    Task CreateAsync(Persona persona, CancellationToken ct = default);

    /// <summary>Updates a persona, incl. status (tracks + saves; flushes within an ambient transaction).</summary>
    Task UpdateAsync(Persona persona, CancellationToken ct = default);

    /// <summary>All personas currently bound to a journey (join over <c>journey_persona_bindings</c>).</summary>
    Task<IReadOnlyList<Persona>> ListBoundPersonasAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>
    /// Number of journeys a persona is bound to; backs the archive guard
    /// (<c>persona.archive_blocked_active_bindings</c> when greater than zero).
    /// </summary>
    Task<int> CountBindingsAsync(Guid personaId, CancellationToken ct = default);

    /// <summary>Creates a journey↔persona binding (idempotent — re-binding is a no-op).</summary>
    Task AddBindingAsync(JourneyPersonaBinding binding, CancellationToken ct = default);

    /// <summary>Removes a journey↔persona binding; always permitted.</summary>
    Task RemoveBindingAsync(Guid journeyId, Guid personaId, CancellationToken ct = default);

    /// <summary>
    /// The journeys a single persona is currently bound to, each with its display name
    /// (join over <c>journey_persona_bindings ⋈ journeys</c>). Backs the persona-detail
    /// <c>journeyBindings</c> array (<c>GET /api/v1/personas/{id}</c>).
    /// </summary>
    Task<IReadOnlyList<PersonaJourneyBinding>> ListBindingsForPersonaAsync(Guid personaId, CancellationToken ct = default);

    /// <summary>
    /// Journey-binding counts for every bound persona in the tenant, keyed by persona id
    /// (personas with zero bindings are absent from the map). One grouped query backs the
    /// list endpoint's <c>journeyBindingCount</c> without an N+1 per-persona count.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountBindingsByPersonaAsync(CancellationToken ct = default);
}

/// <summary>
/// A journey a persona is bound to, projected for the persona-detail <c>journeyBindings</c>
/// response (read-only — never persisted).
/// </summary>
/// <param name="JourneyId">The bound journey's id.</param>
/// <param name="JourneyName">The bound journey's display name.</param>
public sealed record PersonaJourneyBinding(Guid JourneyId, string JourneyName);
