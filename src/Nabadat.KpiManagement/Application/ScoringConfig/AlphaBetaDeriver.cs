namespace Nabadat.KpiManagement.Application.ScoringConfig;

/// <summary>
/// Derives β from α as <c>1.000 − α</c> using <see cref="decimal"/> arithmetic (US-4 / FR-053, R6):
/// β is never persisted and never sent by the client — it is a pure function of α, displayed
/// read-only beside the α slider. Using <see cref="decimal"/> (not <see cref="double"/>) keeps the
/// 3-dp result exact, e.g. <c>1.000 − 0.123 = 0.877</c> with no floating-point drift.
/// </summary>
public static class AlphaBetaDeriver
{
    public static decimal Beta(decimal alpha) => 1.000m - alpha;
}
