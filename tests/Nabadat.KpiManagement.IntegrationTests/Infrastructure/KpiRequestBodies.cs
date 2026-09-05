namespace Nabadat.KpiManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Builds <c>POST</c>/<c>PUT /api/v1/kpis</c> request bodies as anonymous objects. Field names are
/// snake_case (matching the controller's <c>[JsonPropertyName]</c> contract) and enum fields are sent
/// as their INTEGER ordinal — the .NET host has no <c>JsonStringEnumConverter</c>, so System.Text.Json
/// binds enums only from numbers (CLAUDE.md "Backend Integration"). Ordinals match the C# enum
/// declaration order: CalculationMethod {WeightedAverage=0, TopNBox=1, NPSStandard=2,
/// WeightedComposite=3}; Scale {Scale0_10=0, Scale1_3=1, Scale1_5=2, Scale1_7=3, Scale1_10=4,
/// Scale1_100=5, Nps=6}; RepresentationStyle {Number=0, …}.
/// </summary>
internal static class KpiRequestBodies
{
    /// <summary>A valid custom-KPI body (WeightedAverage, Scale1_5, Number, ascending 0/20/70/100).</summary>
    public static object Custom(
        string shortName,
        string? fullName = null,
        int calculationMethod = 0,
        int scale = 2,
        decimal? target = 80m,
        bool isActive = true,
        IEnumerable<object>? perspectives = null) => new
    {
        short_name = shortName,
        full_name = fullName ?? $"{shortName} full name",
        perspectives = perspectives ?? Array.Empty<object>(),
        calculation_method = calculationMethod,
        top_n_value = (int?)null,
        scale = (int?)scale,
        min_scale_description = (object?)null,
        max_scale_description = (object?)null,
        representation_style = (int?)0,
        emoji_set = (int?)null,
        thresholds = new { x = 20, y = 70 },
        target,
        is_active = isActive,
        show_on_dashboard = false,
    };

    /// <summary>An NPS-shaped body (NPSStandard, Scale0_10, −100/0/30/100, target 50) for standard-KPI edit tests.</summary>
    public static object Nps(string shortName, int calculationMethod = 2, int scale = 0) => new
    {
        short_name = shortName,
        full_name = "Net Promoter Score",
        perspectives = Array.Empty<object>(),
        calculation_method = calculationMethod,
        top_n_value = (int?)null,
        scale = (int?)scale,
        min_scale_description = (object?)null,
        max_scale_description = (object?)null,
        representation_style = (int?)null,
        emoji_set = (int?)null,
        thresholds = new { lower_bound = -100, x = 0, y = 30, upper_bound = 100 },
        target = (decimal?)50m,
        is_active = true,
        show_on_dashboard = false,
    };
}
