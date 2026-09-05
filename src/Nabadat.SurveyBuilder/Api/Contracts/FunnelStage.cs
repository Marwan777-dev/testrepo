using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// One funnel stage on the wire (FR-14.2, contracts/report-and-analytics.md § GET /analytics). The
/// <c>sent</c> stage carries a count and a percent-change delta (<see cref="DeltaPct"/>); the
/// <c>opened</c>/<c>started</c>/<c>finished</c> stages carry <see cref="PctOfSent"/>, a
/// percentage-point delta (<see cref="DeltaPp"/>) and the stage-to-stage conversion
/// (<see cref="ConversionFromPrevStagePct"/>). Fields that don't apply to a stage — and any delta
/// with no comparable prior period (FR-14.5) — are <c>null</c>.
/// </summary>
public sealed record FunnelStage(
    [property: JsonPropertyName("count")] long Count,
    [property: JsonPropertyName("pct_of_sent")] decimal? PctOfSent,
    [property: JsonPropertyName("delta_pct")] decimal? DeltaPct,
    [property: JsonPropertyName("delta_pp")] decimal? DeltaPp,
    [property: JsonPropertyName("conversion_from_prev_stage_pct")] decimal? ConversionFromPrevStagePct);
