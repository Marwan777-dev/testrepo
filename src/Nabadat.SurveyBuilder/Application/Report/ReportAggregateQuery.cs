namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// Input to <see cref="Interfaces.IReportAggregator.AggregateAsync"/>: the survey, the resolved
/// reporting window, the survey's active period (so post-expiry responses are excluded per FR-13.6),
/// and the caller's data scope.
/// </summary>
/// <param name="SurveyId">The survey being reported on.</param>
/// <param name="Period">The resolved <c>[from, to]</c> window (see <see cref="PeriodResolver"/>).</param>
/// <param name="ActivePeriod">
/// The survey's active-period length, or <c>null</c> when the survey never auto-expires (a
/// <c>null</c> active period means no window filtering — every in-period response counts).
/// </param>
/// <param name="Scope">The caller's data scope (Article 4.5).</param>
public sealed record ReportAggregateQuery(
    Guid SurveyId,
    ResolvedPeriod Period,
    TimeSpan? ActivePeriod,
    ReportScope Scope);
