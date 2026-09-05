using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// The resolved report window on the wire (contracts/report-and-analytics.md — the report's
/// <c>period</c> object): the absolute <c>[resolved_from, resolved_to]</c> instants the metrics
/// cover.
/// </summary>
public sealed record ResolvedPeriodView(
    [property: JsonPropertyName("resolved_from")] DateTimeOffset ResolvedFrom,
    [property: JsonPropertyName("resolved_to")] DateTimeOffset ResolvedTo);
