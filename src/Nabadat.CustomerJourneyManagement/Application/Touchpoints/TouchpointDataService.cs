using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Touchpoints;

/// <summary>
/// EF <see cref="ITouchpointDataService"/> over <see cref="ITenantDbContext"/> for the tenant-schema
/// <c>touchpoints</c> table. Replaces the raw-Npgsql <c>TouchpointRepository</c>. Deleting a
/// touchpoint cascades to its <c>kpi_bindings</c> via the FK. <see cref="ReplaceKpiBindingsAsync"/>
/// is a full replace (delete all + insert the supplied set) and MUST run inside the caller's
/// <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/> so the touchpoint
/// never transiently holds a partial (≠100%) binding set (FR-015).
/// </summary>
public sealed class TouchpointDataService : ITouchpointDataService
{
    private readonly ITenantDbContext _context;

    public TouchpointDataService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<Touchpoint?> GetByIdAsync(Guid touchpointId, CancellationToken ct = default) =>
        _context.Touchpoints.AsNoTracking().FirstOrDefaultAsync(t => t.TouchpointId == touchpointId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Touchpoint>> ListByStageAsync(Guid stageId, CancellationToken ct = default) =>
        await _context.Touchpoints.AsNoTracking()
            .Where(t => t.StageId == stageId)
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.TouchpointId)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<int> CountByStageAsync(Guid stageId, CancellationToken ct = default) =>
        _context.Touchpoints.AsNoTracking().CountAsync(t => t.StageId == stageId, ct);

    /// <inheritdoc />
    public Task<bool> HasKpiBindingsAsync(Guid touchpointId, CancellationToken ct = default) =>
        _context.KpiBindings.AsNoTracking().AnyAsync(b => b.TouchpointId == touchpointId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<KpiBinding>> ListKpiBindingsByJourneyAsync(
        Guid journeyId,
        CancellationToken ct = default) =>
        // All bindings whose touchpoint belongs to a stage of this journey — one query, grouped by
        // touchpoint in memory by the caller (mirrors the JourneySnapshotBuilder journey-tree read).
        await _context.KpiBindings.AsNoTracking()
            .Where(b => _context.Touchpoints.Any(t => t.TouchpointId == b.TouchpointId
                && _context.Stages.Any(s => s.StageId == t.StageId && s.JourneyId == journeyId)))
            .OrderBy(b => b.TouchpointId).ThenBy(b => b.KpiType)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task CreateAsync(Touchpoint touchpoint, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(touchpoint);
        _context.Touchpoints.Add(touchpoint);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Touchpoint touchpoint, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(touchpoint);
        _context.Touchpoints.Update(touchpoint);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid touchpointId, CancellationToken ct = default) =>
        _context.Touchpoints.Where(t => t.TouchpointId == touchpointId).ExecuteDeleteAsync(ct);

    /// <inheritdoc />
    public async Task ReplaceKpiBindingsAsync(
        Guid touchpointId,
        IReadOnlyList<KpiBinding> bindings,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        // Delete-all + re-insert as one unit (the caller's ExecuteAsync transaction): the touchpoint
        // never transiently holds a partial binding set. ExecuteDeleteAsync runs immediately on the
        // ambient transaction; the inserts flush on SaveChangesAsync.
        await _context.KpiBindings.Where(b => b.TouchpointId == touchpointId).ExecuteDeleteAsync(ct);

        if (bindings.Count > 0)
        {
            _context.KpiBindings.AddRange(bindings);
            await _context.SaveChangesAsync(ct);
        }
    }
}
