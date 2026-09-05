using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// One per-channel breakdown row on the wire (FR-14.4, contracts/report-and-analytics.md § GET
/// /analytics, <c>channels[]</c>): the channel's send count, its completion rate as a ratio in
/// <c>[0,1]</c>, and the deviation of that rate vs the previous period in percentage points
/// (<c>null</c> when no comparable prior period exists — FR-14.5).
/// </summary>
public sealed record ChannelBreakdown(
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("sent")] long Sent,
    [property: JsonPropertyName("completion_rate")] decimal CompletionRate,
    [property: JsonPropertyName("delta_pp")] decimal? DeltaPp);
