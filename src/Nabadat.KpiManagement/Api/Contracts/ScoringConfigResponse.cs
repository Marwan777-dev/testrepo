using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// Wire shape of <c>GET</c>/<c>PUT /api/v1/tenant/scoring-config</c> (contracts/settings-api.md).
/// snake_case per the API-05 convention. <c>beta</c> is derived (<c>1.000 − alpha</c>) and never
/// persisted; the client cannot send it.
/// </summary>
public sealed record ScoringConfigResponse
{
    [JsonPropertyName("alpha")]
    public required decimal Alpha { get; init; }

    [JsonPropertyName("beta")]
    public required decimal Beta { get; init; }

    [JsonPropertyName("mot_multiplier")]
    public required decimal MotMultiplier { get; init; }

    [JsonPropertyName("n_floor")]
    public required int NFloor { get; init; }

    [JsonPropertyName("flag_percentile")]
    public required int FlagPercentile { get; init; }

    [JsonPropertyName("rolling_window_days")]
    public required int RollingWindowDays { get; init; }

    [JsonPropertyName("audit")]
    public ScoringConfigAuditDto? Audit { get; init; }
}
