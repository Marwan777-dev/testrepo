using System.Text.Json.Serialization;
using Nabadat.SurveyBuilder.Application.Report;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// F13 Survey Report payload on the wire (contracts/report-and-analytics.md § GET /report):
/// the resolved <see cref="Period"/>, the four <see cref="MetricCards"/>, the
/// <see cref="HeadlineKpis"/> gauges, and the per-question result cards. Assembled by
/// <c>ReportService</c> (T242) from the ES aggregate + the survey's question structure. Read-only —
/// no ETag.
/// </summary>
public sealed record ReportView(
    [property: JsonPropertyName("period")] ResolvedPeriodView Period,
    [property: JsonPropertyName("metric_cards")] MetricCards MetricCards,
    [property: JsonPropertyName("headline_kpis")] HeadlineKpisView HeadlineKpis,
    [property: JsonPropertyName("per_question")] IReadOnlyList<PerQuestionResult> PerQuestion)
{
    /// <summary>Maps the Application-layer <see cref="SurveyReport"/> to its wire shape.</summary>
    public static ReportView From(SurveyReport report) => new(
        new ResolvedPeriodView(report.Period.From, report.Period.To),
        new MetricCards(
            report.Metrics.Responses,
            report.Metrics.CompletionRate,
            report.Metrics.MedianTimeSeconds,
            report.Metrics.Touchpoints),
        new HeadlineKpisView(
            HeadlineKpi.From(report.Headline.Csat),
            HeadlineKpi.From(report.Headline.Nps),
            HeadlineKpi.From(report.Headline.Ces)),
        report.Cards.Select(PerQuestionResult.From).ToList());
}
