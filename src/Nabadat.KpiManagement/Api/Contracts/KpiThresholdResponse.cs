using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>Threshold band edges in a KPI configuration response (contracts/kpi-api.md).</summary>
public sealed record KpiThresholdResponse
{
    [JsonPropertyName("lower_bound")]
    public required decimal LowerBound { get; init; }

    [JsonPropertyName("x")]
    public required decimal X { get; init; }

    [JsonPropertyName("y")]
    public required decimal Y { get; init; }

    [JsonPropertyName("upper_bound")]
    public required decimal UpperBound { get; init; }
}
