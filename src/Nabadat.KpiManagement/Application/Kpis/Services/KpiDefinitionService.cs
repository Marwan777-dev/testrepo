using Microsoft.EntityFrameworkCore;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Dtos;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;
// KpiCataloguePage lives in Nabadat.KpiManagement.Application.Kpis.Dtos (imported above).

namespace Nabadat.KpiManagement.Application.Kpis.Services;

/// <summary>
/// The single per-entity service for the KPI aggregate (DB-08 — CRUD over
/// <see cref="ITenantDbContext"/>, no repository). It is the internal entity service
/// (<see cref="IKpiDefinitionService"/>) whose reads return entities; the published read contract
/// (<see cref="IKpiConfigReader"/>, returning assembled DTOs to M-01 / M-07 / M-09) is a separate
/// class, <see cref="KpiConfigReader"/>. Reads use <c>AsNoTracking</c>;
/// <see cref="AddAsync"/>/<see cref="UpdateAsync"/> track the change and save — when invoked inside
/// <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/> the row and its M-17
/// event commit atomically. Short Name comparisons are case-insensitive, matching the
/// <c>LOWER(short_name)</c> functional unique index.
/// </summary>
public sealed class KpiDefinitionService : IKpiDefinitionService
{
    private readonly ITenantDbContext _context;

    public KpiDefinitionService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<KpiDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.KpiDefinitions.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id, ct);

    /// <inheritdoc />
    public Task<KpiDefinition?> GetByShortNameAsync(string shortName, CancellationToken ct = default)
    {
        var lowered = (shortName ?? string.Empty).ToLower();
        return _context.KpiDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(k => k.ShortName.ToLower() == lowered, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KpiDefinition>> ListAllAsync(CancellationToken ct = default) =>
        await _context.KpiDefinitions.AsNoTracking().ToListAsync(ct);

    /// <inheritdoc />
    public async Task<KpiCataloguePage> ListCatalogueAsync(
        KpiTypeFilter type,
        bool activeOnly,
        string? search,
        string? cursor,
        int limit,
        CancellationToken ct = default)
    {
        // The catalogue is bounded (≤ ~60 rows/tenant per R8), so materialise the canonically-ordered
        // set once and slice in memory. This keeps the cursor correct even though the primary sort is
        // the canonical CASE (not created_at): we locate the cursor row by id and take the next page.
        var ordered = await KpiCatalogueQuery
            .Build(_context.KpiDefinitions.AsNoTracking(), type, activeOnly, search)
            .ToListAsync(ct);

        var startIndex = 0;
        if (KpiCatalogueCursor.TryDecode(cursor, out _, out var afterId))
        {
            var cursorIndex = ordered.FindIndex(k => k.Id == afterId);
            startIndex = cursorIndex >= 0 ? cursorIndex + 1 : 0;
        }

        var pageItems = ordered.Skip(startIndex).Take(limit).ToList();

        string? nextCursor = null;
        if (pageItems.Count > 0 && startIndex + pageItems.Count < ordered.Count)
        {
            var last = pageItems[^1];
            nextCursor = KpiCatalogueCursor.Encode(last.CreatedAt, last.Id);
        }

        return new KpiCataloguePage(pageItems, nextCursor);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByShortNameAsync(string shortName, Guid? excludeId = null, CancellationToken ct = default)
    {
        var lowered = (shortName ?? string.Empty).ToLower();
        return _context.KpiDefinitions.AsNoTracking().AnyAsync(
            k => k.ShortName.ToLower() == lowered && (excludeId == null || k.Id != excludeId),
            ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(KpiDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _context.KpiDefinitions.Add(definition);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(KpiDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _context.KpiDefinitions.Update(definition);
        await _context.SaveChangesAsync(ct);
    }
}
