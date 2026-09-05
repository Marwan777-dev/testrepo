namespace Nabadat.KpiManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Canonical metadata for the eight platform-seeded standard KPIs (data-model.md §4), shared by the
/// integration tests so catalogue-order and seed-content assertions have a single source of truth
/// (e.g. T044 asserts <c>GET /api/v1/kpis</c> returns these in <see cref="CanonicalOrder"/>). The
/// rows themselves are written by <c>KpiManagement_Baseline.sql</c> during provisioning, not here.
/// </summary>
public static class KpiSeedHelper
{
    /// <summary>The eight standard KPI short names in the canonical catalogue order.</summary>
    public static readonly IReadOnlyList<string> CanonicalOrder =
        ["NPS", "CSAT", "CES", "CXI", "FCR", "VFM", "AgentScore", "CHS"];

    /// <summary>The short name of the seeded composite KPI (CXI) — the only <c>is_composite=true</c> seed.</summary>
    public const string CompositeShortName = "CXI";

    /// <summary>NPS-specific default threshold edges (data-model.md §4 / Clarifications round 2 Q1).</summary>
    public static readonly (decimal Lower, decimal X, decimal Y, decimal Upper) NpsThreshold = (-100, 0, 30, 100);

    /// <summary>Default threshold edges for every non-NPS standard KPI.</summary>
    public static readonly (decimal Lower, decimal X, decimal Y, decimal Upper) DefaultThreshold = (0, 20, 70, 100);
}
