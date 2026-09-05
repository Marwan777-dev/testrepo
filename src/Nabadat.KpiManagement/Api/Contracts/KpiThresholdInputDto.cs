using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// Threshold band edges in a KPI create/update request (contracts/kpi-api.md). The inner boundaries
/// <c>x</c> / <c>y</c> are always supplied; <c>lower_bound</c> / <c>upper_bound</c> are optional and
/// default to the normalised 0..100 range when omitted.
/// </summary>
public sealed record KpiThresholdInputDto
{
    [JsonPropertyName("lower_bound")]
    public decimal? LowerBound { get; init; }

    [JsonPropertyName("x")]
    public decimal X { get; init; }

    [JsonPropertyName("y")]
    public decimal Y { get; init; }

    [JsonPropertyName("upper_bound")]
    public decimal? UpperBound { get; init; }
}
