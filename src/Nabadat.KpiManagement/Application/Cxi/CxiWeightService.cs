using Microsoft.EntityFrameworkCore;
using Nabadat.KpiManagement.Application.Cxi.Interfaces;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Cxi;

/// <summary>
/// EF <see cref="ICxiWeightService"/> over <see cref="ITenantDbContext"/> for the tenant-schema
/// <c>cxi_weights</c> table (DB-08 — CRUD over the context, no repository). Holds the CXI weight
/// CRUD plus the deactivation-cascade query <see cref="GetCxiMembershipsForKpiAsync"/>; the
/// relative-weight → effective-% maths lives in <c>CxiWeightNormaliser</c> (US-3). Writes commit
/// atomically when wrapped in <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>.
/// </summary>
public sealed class CxiWeightService : ICxiWeightService
{
    private readonly ITenantDbContext _context;

    public CxiWeightService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CxiWeight>> ListByCxiKpiIdAsync(Guid cxiKpiId, CancellationToken ct = default) =>
        await _context.CxiWeights.AsNoTracking()
            .Where(w => w.CxiKpiId == cxiKpiId)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CxiWeight>> GetCxiMembershipsForKpiAsync(Guid memberKpiId, CancellationToken ct = default) =>
        await _context.CxiWeights.AsNoTracking()
            .Where(w => w.MemberKpiId == memberKpiId)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task ReplaceAllAsync(Guid cxiKpiId, IEnumerable<CxiWeight> weights, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var existing = await _context.CxiWeights.Where(w => w.CxiKpiId == cxiKpiId).ToListAsync(ct);
        _context.CxiWeights.RemoveRange(existing);

        foreach (var weight in weights)
        {
            weight.CxiKpiId = cxiKpiId;
            _context.CxiWeights.Add(weight);
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RemoveMemberAsync(Guid cxiKpiId, Guid memberKpiId, CancellationToken ct = default)
    {
        var row = await _context.CxiWeights
            .FirstOrDefaultAsync(w => w.CxiKpiId == cxiKpiId && w.MemberKpiId == memberKpiId, ct);
        if (row is not null)
        {
            _context.CxiWeights.Remove(row);
            await _context.SaveChangesAsync(ct);
        }
    }
}
