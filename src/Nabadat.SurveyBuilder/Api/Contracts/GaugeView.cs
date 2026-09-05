using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// A per-question gauge on the wire (contracts/report-and-analytics.md — the <c>gauge</c> object on
/// KPI / Scale question views): the aggregate <c>value</c> and its <c>target</c> marker (null when
/// unknown).
/// </summary>
public sealed record GaugeView(
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("target")] decimal? Target);
