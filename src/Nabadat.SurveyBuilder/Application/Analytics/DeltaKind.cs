namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// How a headline analytics number deviates against the previous period (FR-14.3), which fixes the
/// unit and formula used by <see cref="PeriodDeltaCalculator"/>.
/// </summary>
public enum DeltaKind
{
    /// <summary>A percentage/rate — deviates in <b>percentage points</b> (<c>current − prior</c>).</summary>
    Rate,

    /// <summary>An absolute count — deviates in <b>percent change</b> (<c>(current − prior) / prior × 100</c>).</summary>
    Count,
}
