using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// The resolved analytics window on the wire (contracts/report-and-analytics.md § GET /analytics,
/// <c>period</c>): the absolute <c>[resolved_from, resolved_to]</c> instants plus the trend
/// <c>granularity</c> the series is bucketed at. (The report's period object omits granularity, which
/// is why analytics carries its own period view.)
/// </summary>
public sealed record AnalyticsPeriodView(
    [property: JsonPropertyName("resolved_from")] DateTimeOffset ResolvedFrom,
    [property: JsonPropertyName("resolved_to")] DateTimeOffset ResolvedTo,
    [property: JsonPropertyName("granularity")] string Granularity);
