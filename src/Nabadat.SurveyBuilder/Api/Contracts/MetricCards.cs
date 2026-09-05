using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// The four report metric cards (FR-13.4, contracts/report-and-analytics.md): total responses, the
/// completion rate in <c>[0,1]</c>, the median completion time in seconds (always available —
/// FR-13.4), and the number of distinct touchpoints represented in the window.
/// </summary>
public sealed record MetricCards(
    [property: JsonPropertyName("responses")] int Responses,
    [property: JsonPropertyName("completion_rate")] decimal CompletionRate,
    [property: JsonPropertyName("median_time_seconds")] int? MedianTimeSeconds,
    [property: JsonPropertyName("touchpoints")] int Touchpoints);
