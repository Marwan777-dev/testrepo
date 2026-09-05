namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// T258 [US9] — picks the <b>default</b> trend granularity for a named report period (FR-14.1): short
/// windows default to <c>daily</c>, mid-length windows to <c>weekly</c>, long windows to
/// <c>monthly</c>. The user may still override the granularity in the UI. Pure — no I/O, no clock.
/// Unit-tested by <c>TrendGranularityResolverTests</c> (T253).
/// <para>Accepts the same wire period values as the report's <see cref="Report.PeriodResolver"/>;
/// <c>custom</c> needs an explicit range and is resolved at the controller, not here, so any
/// unrecognised period throws <see cref="ArgumentException"/>.</para>
/// </summary>
public sealed class TrendGranularityResolver
{
    /// <exception cref="ArgumentException">The period is not a known named window.</exception>
    public string Resolve(string period) => period switch
    {
        "last_1_day" or "last_7_days" or "last_month" => "daily",
        "last_3_months" or "last_6_months" => "weekly",
        "last_9_months" or "last_year" => "monthly",
        _ => throw new ArgumentException($"Unknown analytics period '{period}'.", nameof(period)),
    };
}
