using Nabadat.CustomerJourneyManagement.Application.Bindings.Interfaces;

namespace Nabadat.KpiManagement.Application.Kpis.Services;

/// <summary>
/// Thin adapter over M-16's published <see cref="IJourneyBindingQuery"/> — the only access M-06 has
/// to journey-binding state (AD-01: published-interface-only, never M-16's tables). It returns the
/// <c>(touchpointCount, journeyCount)</c> usage for a KPI, which M-06 uses to build the FR-026
/// deactivation confirmation and to detect FR-017 scale-change impact. Pure delegation: no caching,
/// no transformation beyond unwrapping the M-16 DTO into a tuple so M-06's own surface doesn't leak
/// the M-16 type.
/// </summary>
public sealed class KpiBindingUsageProbe
{
    private readonly IJourneyBindingQuery _bindings;

    public KpiBindingUsageProbe(IJourneyBindingQuery bindings) => _bindings = bindings;

    /// <summary>
    /// Returns how many touchpoints bind <paramref name="kpiId"/> and across how many distinct
    /// non-archived journeys. <c>(0, 0)</c> for an unbound KPI.
    /// </summary>
    public async Task<(int TouchpointCount, int JourneyCount)> GetUsageAsync(Guid kpiId, CancellationToken ct = default)
    {
        var usage = await _bindings.GetKpiBindingUsageAsync(kpiId, ct);
        return (usage.TouchpointCount, usage.JourneyCount);
    }
}
