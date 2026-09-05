using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// Request body of <c>PUT /api/v1/tenant/scoring-config</c> (contracts/settings-api.md). snake_case
/// on the wire. β is NOT accepted — it is derived (<c>1 − α</c>) server-side.
/// </summary>
public sealed record ScoringConfigUpdateRequest
{
    [JsonPropertyName("alpha")]
    public decimal Alpha { get; init; }

    [JsonPropertyName("mot_multiplier")]
    public decimal MotMultiplier { get; init; }

    [JsonPropertyName("n_floor")]
    public int NFloor { get; init; }

    [JsonPropertyName("flag_percentile")]
    public int FlagPercentile { get; init; }

    [JsonPropertyName("rolling_window_days")]
    public int RollingWindowDays { get; init; }
}
