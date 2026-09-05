namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// The raw, in-window aggregate for a survey report, produced by the ES aggregator (T239) from the
/// period-filtered <c>tenant_{id}_responses</c> index. <see cref="ReportService"/> turns it into the
/// wire <c>ReportView</c>: it averages <see cref="CsatValues"/> via <see cref="HeadlineCsatCalculator"/>
/// (FR-13.2), computes deltas against a previous-period aggregate, and shapes each
/// <see cref="PerQuestion"/> entry per its view kind.
/// </summary>
/// <param name="ResponsesCount">Total in-window responses (metric card).</param>
/// <param name="CompletionRate">Finished ÷ total, in <c>[0,1]</c> (metric card).</param>
/// <param name="MedianTimeSeconds">Median completion time (FR-13.4), or <c>null</c> when unknown.</param>
/// <param name="Touchpoints">Distinct touchpoints represented in the window (metric card).</param>
/// <param name="CsatValues">Per-question CSAT averages contributing to the headline CSAT (FR-13.2).</param>
/// <param name="NpsValue">Headline NPS value, or <c>null</c> when the survey has no NPS question.</param>
/// <param name="CesValue">Headline CES value, or <c>null</c> when the survey has no CES question.</param>
/// <param name="PerQuestion">Per-question aggregates keyed by question id.</param>
public sealed record ReportAggregate(
    int ResponsesCount,
    decimal CompletionRate,
    int? MedianTimeSeconds,
    int Touchpoints,
    IReadOnlyList<decimal> CsatValues,
    decimal? NpsValue,
    decimal? CesValue,
    IReadOnlyDictionary<Guid, PerQuestionAggregate> PerQuestion)
{
    /// <summary>An empty aggregate — returned when no responses match or ES is unavailable.</summary>
    public static readonly ReportAggregate Empty = new(
        ResponsesCount: 0,
        CompletionRate: 0m,
        MedianTimeSeconds: null,
        Touchpoints: 0,
        CsatValues: Array.Empty<decimal>(),
        NpsValue: null,
        CesValue: null,
        PerQuestion: new Dictionary<Guid, PerQuestionAggregate>());
}
