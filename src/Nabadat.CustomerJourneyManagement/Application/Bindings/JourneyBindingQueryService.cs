using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Bindings.Dtos;
using Nabadat.CustomerJourneyManagement.Application.Bindings.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Bindings;

/// <summary>
/// EF implementation of <see cref="IJourneyBindingQuery"/> (M-16 → M-06, Feature 003 / T020). Runs
/// the binding-usage aggregation over M-16's own tenant-schema tables via
/// <see cref="ITenantDbContext"/> (DB-08 — no raw SQL, no repository): for the given KPI id it
/// joins <c>kpi_bindings → touchpoints → stages → journeys</c> and counts the distinct touchpoints
/// and distinct non-archived journeys. This service is the only point that reads M-16's tables on
/// M-06's behalf (AD-01). Returns (0, 0) for an unbound KPI.
/// </summary>
public sealed class JourneyBindingQueryService : IJourneyBindingQuery
{
    private readonly ITenantDbContext _context;

    public JourneyBindingQueryService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<KpiBindingUsage> GetKpiBindingUsageAsync(Guid kpiId, CancellationToken ct = default)
    {
        // kpi_bindings → touchpoints → stages → journeys, excluding archived journeys.
        var bound =
            from kb in _context.KpiBindings.AsNoTracking()
            where kb.KpiId == kpiId
            join t in _context.Touchpoints.AsNoTracking() on kb.TouchpointId equals t.TouchpointId
            join s in _context.Stages.AsNoTracking() on t.StageId equals s.StageId
            join j in _context.Journeys.AsNoTracking() on s.JourneyId equals j.JourneyId
            where j.Status != "Archived"
            select new { t.TouchpointId, s.JourneyId };

        var touchpointCount = await bound.Select(x => x.TouchpointId).Distinct().CountAsync(ct);
        var journeyCount = await bound.Select(x => x.JourneyId).Distinct().CountAsync(ct);

        return new KpiBindingUsage(touchpointCount, journeyCount);
    }
}
