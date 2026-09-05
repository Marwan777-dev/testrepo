namespace Nabadat.KpiManagement.Application.Cxi;

/// <summary>
/// T084 [US-3] — the FR-043 gate on whether a CXI composite may be activated: a composite needs at
/// least two members carrying a positive weight (a composite of zero or one member is not a
/// composite). Pure logic, no state — hence <see langword="static"/>.
/// </summary>
public static class CxiActivationRule
{
    /// <summary>True iff at least two of <paramref name="weights"/> are positive (weight &gt; 0).</summary>
    public static bool CanActivate(IReadOnlyList<int> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        return weights.Count(w => w > 0) >= 2;
    }
}
