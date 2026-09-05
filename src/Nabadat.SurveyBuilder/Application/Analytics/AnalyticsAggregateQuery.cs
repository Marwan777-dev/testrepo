using Nabadat.SurveyBuilder.Application.Report;

namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// Input to <see cref="Interfaces.IAnalyticsAggregator.AggregateAsync"/>: the survey, the resolved
/// current window, the resolved previous window of equal length (for FR-14.3 deltas), and the trend
/// bucket granularity. Reuses the report's <see cref="ResolvedPeriod"/> so both surfaces resolve
/// periods identically.
/// </summary>
/// <param name="SurveyId">The survey being analysed.</param>
/// <param name="Current">The resolved <c>[from, to]</c> window for the selected period.</param>
/// <param name="Prior">The equal-length window immediately preceding <see cref="Current"/>.</param>
/// <param name="Granularity">The trend bucket size — <c>daily</c> / <c>weekly</c> / <c>monthly</c>.</param>
public sealed record AnalyticsAggregateQuery(
    Guid SurveyId,
    ResolvedPeriod Current,
    ResolvedPeriod Prior,
    string Granularity);
