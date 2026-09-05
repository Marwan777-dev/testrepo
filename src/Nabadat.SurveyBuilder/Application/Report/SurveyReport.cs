namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// The assembled F13 Survey Report as an Application-layer result (mapped to the wire
/// <c>ReportView</c> by the Api layer): the resolved <see cref="Period"/>, the metric cards, the
/// headline KPI gauges, and the per-question cards in survey order.
/// </summary>
public sealed record SurveyReport(
    ResolvedPeriod Period,
    ReportMetrics Metrics,
    ReportHeadline Headline,
    IReadOnlyList<ReportQuestionCard> Cards);
