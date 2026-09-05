using System.Text.Json.Serialization;
using Nabadat.SurveyBuilder.Application.Report;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// The per-question report visual on the wire (FR-13.3, contracts/report-and-analytics.md — the
/// <c>view</c> object). A single flattened shape discriminated by <see cref="Kind"/> (snake_case,
/// e.g. <c>bar_with_counts_and_pct</c>); only the fields relevant to that kind are populated, the
/// rest are omitted. Chosen by <c>PerQuestionViewSelector</c> and filled by <c>ReportService</c>.
/// </summary>
public sealed record PerQuestionView
{
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>Option/answer buckets (single/multi-select, Yes-No, KPI bars).</summary>
    [JsonPropertyName("distribution")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DistributionBucketView>? Distribution { get; init; }

    /// <summary>Respondent base for multi-select percentages (FR-13.5).</summary>
    [JsonPropertyName("respondents_base")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RespondentsBase { get; init; }

    /// <summary>Aggregate gauge for KPI / Scale questions.</summary>
    [JsonPropertyName("gauge")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GaugeView? Gauge { get; init; }

    /// <summary>Numeric average for Number questions (FR-13.3).</summary>
    [JsonPropertyName("average")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Average { get; init; }

    /// <summary>Verbatim sample for Text/Paragraph questions (FR-13.7).</summary>
    [JsonPropertyName("sample")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<VerbatimSampleResponse>? Sample { get; init; }

    /// <summary>Total verbatims available for the "show more" control (FR-13.7).</summary>
    [JsonPropertyName("total_available")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalAvailable { get; init; }

    /// <summary>Default verbatim sample size shown before "show more".</summary>
    [JsonPropertyName("sample_size_default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SampleSizeDefault { get; init; }

    /// <summary>Maximum verbatim sample size revealed by "show more" (100 per FR-13.7).</summary>
    [JsonPropertyName("sample_size_max")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SampleSizeMax { get; init; }

    private const int DefaultVerbatimSampleSize = 5;
    private const int MaxVerbatimSampleSize = 100;

    /// <summary>
    /// Builds the wire view for a question from its chosen <paramref name="kind"/> and its raw
    /// <paramref name="aggregate"/> (null when no in-window response answered it — the card renders
    /// an empty view of the right shape). Multi-select is the only kind that emits
    /// <c>pct_of_respondents</c> (FR-13.5); verbatim carries the sample-size envelope (FR-13.7).
    /// </summary>
    public static PerQuestionView For(PerQuestionViewKind kind, PerQuestionAggregate? aggregate) => kind switch
    {
        PerQuestionViewKind.BarDistributionPlusGauge => new PerQuestionView
        {
            Kind = "bar_distribution_plus_gauge",
            Distribution = Buckets(aggregate, withPct: false),
            Gauge = ToGauge(aggregate),
        },
        PerQuestionViewKind.DistributionDonut => new PerQuestionView
        {
            Kind = "distribution_donut",
            Distribution = Buckets(aggregate, withPct: false),
        },
        PerQuestionViewKind.BarWithCountsAndPct => new PerQuestionView
        {
            Kind = "bar_with_counts_and_pct",
            RespondentsBase = aggregate?.RespondentsBase ?? 0,
            Distribution = Buckets(aggregate, withPct: true),
        },
        PerQuestionViewKind.GaugePlusStars => new PerQuestionView { Kind = "gauge_plus_stars", Gauge = ToGauge(aggregate) },
        PerQuestionViewKind.GaugePlusFaces => new PerQuestionView { Kind = "gauge_plus_faces", Gauge = ToGauge(aggregate) },
        PerQuestionViewKind.ValueDistributionLine => new PerQuestionView
        {
            Kind = "value_distribution_line",
            Distribution = Buckets(aggregate, withPct: false),
            Average = aggregate?.Average,
        },
        PerQuestionViewKind.VerbatimSample => new PerQuestionView
        {
            Kind = "verbatim_sample",
            Sample = (aggregate?.VerbatimSample ?? [])
                .Select(VerbatimSampleResponse.From)
                .ToList(),
            TotalAvailable = aggregate?.ResponsesCount ?? 0,
            SampleSizeDefault = DefaultVerbatimSampleSize,
            SampleSizeMax = MaxVerbatimSampleSize,
        },
        // GaugeOnly (Scale/Labels/Slider) and any future kind → the aggregate gauge alone.
        _ => new PerQuestionView { Kind = "gauge_only", Gauge = ToGauge(aggregate) },
    };

    private static IReadOnlyList<DistributionBucketView> Buckets(PerQuestionAggregate? aggregate, bool withPct) =>
        (aggregate?.Distribution ?? [])
            .Select(b => new DistributionBucketView(b.Label, b.Count, withPct ? b.PctOfRespondents : null))
            .ToList();

    private static GaugeView? ToGauge(PerQuestionAggregate? aggregate) =>
        aggregate?.GaugeValue is { } value ? new GaugeView(value, aggregate.GaugeTarget) : null;
}
