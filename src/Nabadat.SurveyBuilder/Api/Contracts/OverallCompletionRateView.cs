using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// The headline overall completion rate on the wire (FR-14.2/14.3,
/// contracts/report-and-analytics.md § GET /analytics, <c>overall_completion_rate</c>): the value as
/// a percentage plus its deviation vs the previous period in percentage points (<c>null</c> when no
/// prior period exists — FR-14.5).
/// </summary>
public sealed record OverallCompletionRateView(
    [property: JsonPropertyName("value_pct")] decimal ValuePct,
    [property: JsonPropertyName("delta_pp")] decimal? DeltaPp);
