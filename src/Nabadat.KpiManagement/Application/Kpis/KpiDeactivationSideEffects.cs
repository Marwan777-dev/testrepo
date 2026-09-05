using Nabadat.KpiManagement.Application.Cxi;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Kpis;

/// <summary>
/// T120 [US-5] — the pure function that derives the deactivation mutation set for a KPI (FR-026 / R5).
/// Given the KPI being deactivated and the full current membership of every CXI composite that
/// includes it, it returns a <see cref="KpiDeactivationPlan"/>: Show-on-Dashboard forced off, plus one
/// <see cref="CxiDeactivationSideEffect"/> per affected CXI carrying the post-removal effective
/// percentages (recomputed over the surviving members via <see cref="CxiWeightNormaliser"/>). No state,
/// no I/O — reused identically by the unit tests and by <c>KpiActivationCommandHandler</c>, so the
/// in-transaction cascade and its audit payload are computed by the same code the tests pin.
/// </summary>
public static class KpiDeactivationSideEffects
{
    /// <summary>
    /// Computes the deactivation plan. <paramref name="affectedCxiMemberships"/> is the complete row
    /// set of every CXI that lists <paramref name="kpi"/> as a member (each affected CXI's full
    /// membership, including the deactivated member's own row); empty when the KPI is in no CXI.
    /// </summary>
    public static KpiDeactivationPlan Compute(KpiDefinition kpi, IReadOnlyList<CxiWeight> affectedCxiMemberships)
    {
        ArgumentNullException.ThrowIfNull(kpi);
        ArgumentNullException.ThrowIfNull(affectedCxiMemberships);

        var sideEffects = affectedCxiMemberships
            .GroupBy(w => w.CxiKpiId)
            .Select(group =>
            {
                // Drop the deactivated member; recompute the survivors' shares from their weights.
                var survivors = group.Where(w => w.MemberKpiId != kpi.Id).ToList();
                var percentages = CxiWeightNormaliser.Normalise(survivors.Select(w => (int)w.Weight).ToList());
                var recomputed = survivors
                    .Select((w, i) => new CxiMemberEffectivePercentage(w.MemberKpiId, percentages[i]))
                    .ToList();

                return new CxiDeactivationSideEffect(group.Key, kpi.Id, recomputed);
            })
            .ToList();

        // An inactive KPI is never shown on the dashboard (FR-026).
        return new KpiDeactivationPlan(ShowOnDashboard: false, sideEffects);
    }
}
