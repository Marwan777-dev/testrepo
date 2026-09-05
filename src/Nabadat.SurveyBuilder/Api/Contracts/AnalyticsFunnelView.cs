using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// The analytics funnel object on the wire (FR-14.2): the four ordered stages Sent → Opened →
/// Started → Finished (contracts/report-and-analytics.md § GET /analytics, <c>funnel</c>).
/// </summary>
public sealed record AnalyticsFunnelView(
    [property: JsonPropertyName("sent")] FunnelStage Sent,
    [property: JsonPropertyName("opened")] FunnelStage Opened,
    [property: JsonPropertyName("started")] FunnelStage Started,
    [property: JsonPropertyName("finished")] FunnelStage Finished);
