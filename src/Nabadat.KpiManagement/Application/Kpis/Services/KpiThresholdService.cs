using Microsoft.EntityFrameworkCore;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Kpis.Services;

/// <summary>
/// EF <see cref="IKpiThresholdService"/> over <see cref="ITenantDbContext"/> for the tenant-schema
/// <c>kpi_thresholds</c> table (DB-08 — CRUD over the context, no repository). <see cref="UpsertAsync"/>
/// is a load-or-add that preserves the <c>kpi_id</c> PK on replace; when invoked inside
/// <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/> the write commits
/// atomically with the owning KPI definition and audit row.
/// </summary>
public sealed class KpiThresholdService : IKpiThresholdService
{
    private readonly ITenantDbContext _context;

    public KpiThresholdService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<KpiThreshold?> GetByKpiIdAsync(Guid kpiId, CancellationToken ct = default) =>
        _context.KpiThresholds.AsNoTracking().FirstOrDefaultAsync(t => t.KpiId == kpiId, ct);

    /// <inheritdoc />
    public async Task UpsertAsync(KpiThreshold threshold, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(threshold);

        var existing = await _context.KpiThresholds.FirstOrDefaultAsync(t => t.KpiId == threshold.KpiId, ct);
        if (existing is null)
        {
            _context.KpiThresholds.Add(threshold);
        }
        else
        {
            existing.LowerBound = threshold.LowerBound;
            existing.X = threshold.X;
            existing.Y = threshold.Y;
            existing.UpperBound = threshold.UpperBound;
        }

        await _context.SaveChangesAsync(ct);
    }
}
