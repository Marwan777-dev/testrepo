namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// The raw aggregate for one question, produced by the ES aggregator (T239) and shaped into a
/// per-question report card by <see cref="ReportService"/> according to the question's
/// <see cref="PerQuestionViewKind"/>. Fields not relevant to a given view are empty/null (e.g. a
/// Text question carries only <see cref="VerbatimSample"/>; a Scale question carries
/// <see cref="GaugeValue"/>).
/// </summary>
/// <param name="QuestionId">The question this aggregate belongs to.</param>
/// <param name="ResponsesCount">Number of responses that answered the question (the top-right label).</param>
/// <param name="RespondentsBase">The respondent base for multi-select percentages (FR-13.5).</param>
/// <param name="Distribution">Option/answer buckets (single/multi-select, Yes-No, KPI bars).</param>
/// <param name="GaugeValue">Aggregate gauge value for KPI / Scale questions, else <c>null</c>.</param>
/// <param name="GaugeTarget">Gauge target for KPI questions when known, else <c>null</c>.</param>
/// <param name="Average">Numeric average for Number questions (FR-13.3), else <c>null</c>.</param>
/// <param name="VerbatimSample">Sampled verbatims for Text/Paragraph questions (FR-13.7), else empty.</param>
public sealed record PerQuestionAggregate(
    Guid QuestionId,
    int ResponsesCount,
    int RespondentsBase,
    IReadOnlyList<DistributionBucket> Distribution,
    decimal? GaugeValue,
    decimal? GaugeTarget,
    decimal? Average,
    IReadOnlyList<VerbatimResponse> VerbatimSample);
