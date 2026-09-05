using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Personas;

/// <summary>
/// EF <see cref="IPersonaDataService"/> over <see cref="ITenantDbContext"/> for the tenant-schema
/// <c>personas</c> table and its <c>journey_persona_bindings</c> join. Replaces the raw-Npgsql
/// <c>PersonaRepository</c>. <see cref="AddBindingAsync"/> is idempotent (re-binding the same pair
/// is a no-op, matching the old <c>ON CONFLICT DO NOTHING</c>). The binding queries back the archive
/// guard, the Active-only selector, and the persona-detail / list responses.
/// </summary>
public sealed class PersonaDataService : IPersonaDataService
{
    private readonly ITenantDbContext _context;

    public PersonaDataService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<Persona?> GetByIdAsync(Guid personaId, CancellationToken ct = default) =>
        _context.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.PersonaId == personaId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Persona>> ListAsync(string? status, CancellationToken ct = default)
    {
        var statusFilter = string.IsNullOrWhiteSpace(status) ? null : status;
        return await _context.Personas.AsNoTracking()
            .Where(p => statusFilter == null || p.Status == statusFilter)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.PersonaId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Persona>> ListBoundPersonasAsync(Guid journeyId, CancellationToken ct = default) =>
        await _context.Personas.AsNoTracking()
            .Where(p => _context.JourneyPersonaBindings.Any(b => b.JourneyId == journeyId && b.PersonaId == p.PersonaId))
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.PersonaId)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<int> CountBindingsAsync(Guid personaId, CancellationToken ct = default) =>
        _context.JourneyPersonaBindings.AsNoTracking().CountAsync(b => b.PersonaId == personaId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PersonaJourneyBinding>> ListBindingsForPersonaAsync(
        Guid personaId,
        CancellationToken ct = default) =>
        await _context.Journeys.AsNoTracking()
            .Where(j => _context.JourneyPersonaBindings.Any(b => b.PersonaId == personaId && b.JourneyId == j.JourneyId))
            .OrderBy(j => j.Name)
            .ThenBy(j => j.JourneyId)
            .Select(j => new PersonaJourneyBinding(j.JourneyId, j.Name))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> CountBindingsByPersonaAsync(CancellationToken ct = default) =>
        await _context.JourneyPersonaBindings.AsNoTracking()
            .GroupBy(b => b.PersonaId)
            .Select(g => new { PersonaId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PersonaId, x => x.Count, ct);

    /// <inheritdoc />
    public async Task CreateAsync(Persona persona, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(persona);
        _context.Personas.Add(persona);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Persona persona, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(persona);
        _context.Personas.Update(persona);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddBindingAsync(JourneyPersonaBinding binding, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(binding);

        // Idempotent: re-binding the same (journey, persona) is a no-op rather than a PK violation.
        var exists = await _context.JourneyPersonaBindings.AsNoTracking()
            .AnyAsync(b => b.JourneyId == binding.JourneyId && b.PersonaId == binding.PersonaId, ct);
        if (exists)
        {
            return;
        }

        _context.JourneyPersonaBindings.Add(binding);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task RemoveBindingAsync(Guid journeyId, Guid personaId, CancellationToken ct = default) =>
        _context.JourneyPersonaBindings
            .Where(b => b.JourneyId == journeyId && b.PersonaId == personaId)
            .ExecuteDeleteAsync(ct);
}
