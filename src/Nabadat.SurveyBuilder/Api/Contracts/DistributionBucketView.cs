using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// One bar/slice of a per-question distribution on the wire (FR-13.3/13.5): the option
/// <c>label</c>, its <c>count</c>, and — for multi-select — the percentage of respondents who chose
/// it (<c>pct_of_respondents</c>, which may total &gt; 100% across buckets; <c>null</c> for
/// single-choice distributions).
/// </summary>
public sealed record DistributionBucketView(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("pct_of_respondents")] decimal? PctOfRespondents);
