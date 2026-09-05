using Microsoft.EntityFrameworkCore;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Application.Perspectives.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Perspectives;

/// <summary>
/// EF <see cref="IKpiPerspectiveService"/> over <see cref="ITenantDbContext"/> for the
/// tenant-schema <c>kpi_perspectives</c> table (DB-08 — CRUD over the context, no repository).
/// This IS the per-entity service — there is no companion <c>*DataService</c>.
/// <see cref="ReplaceAllAsync"/> implements the FR-028 full-replace: delete-all-then-insert in a
/// single save, which commits atomically when wrapped in the KPI-save transaction.
/// </summary>
public sealed class KpiPerspectiveService : IKpiPerspectiveService
{
    private readonly ITenantDbContext _context;

    public KpiPerspectiveService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyList<KpiPerspective>> ListByKpiIdAsync(Guid kpiId, CancellationToken ct = default) =>
        await _context.KpiPerspectives.AsNoTracking()
            .Where(p => p.KpiId == kpiId)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task ReplaceAllAsync(Guid kpiId, IEnumerable<KpiPerspective> perspectives, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(perspectives);

        var existing = await _context.KpiPerspectives.Where(p => p.KpiId == kpiId).ToListAsync(ct);
        _context.KpiPerspectives.RemoveRange(existing);

        foreach (var perspective in perspectives)
        {
            perspective.KpiId = kpiId;
            _context.KpiPerspectives.Add(perspective);
        }

        await _context.SaveChangesAsync(ct);
    }
}
