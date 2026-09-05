namespace Nabadat.KpiManagement.Application.Cxi;

/// <summary>
/// T083 [US-3] — converts a CXI's relative integer member weights into effective percentages
/// (each member's share of the composite). Pure logic, no state — hence <see langword="static"/>.
/// <para>
/// <see cref="Normalise"/> maps each weight to <c>weight / total × 100</c>, rounded to 1 decimal
/// place (away-from-zero, matching the decimal-money/score convention), preserving input order so the
/// caller can zip the result back onto its members. The returned percentages sum to 100 within ±0.1
/// (SC-004). Empty input — or an all-zero input, which has no positive weight — yields an empty list:
/// a CXI with no weighted members has no effective breakdown.
/// </para>
/// </summary>
public static class CxiWeightNormaliser
{
    /// <summary>Relative integer weights → effective percentages (1 dp), in input order.</summary>
    public static IReadOnlyList<decimal> Normalise(IReadOnlyList<int> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var total = weights.Sum();
        if (total <= 0)
        {
            return [];
        }

        return weights
            .Select(w => Math.Round((decimal)w / total * 100m, 1, MidpointRounding.AwayFromZero))
            .ToList();
    }
}
