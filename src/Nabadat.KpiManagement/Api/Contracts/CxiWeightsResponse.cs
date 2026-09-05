using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>200 response for <c>PUT /api/v1/kpis/{cxi_id}/weights</c> — the persisted CXI member
/// weights with their derived effective percentages (sum 100.0 ±0.1, per SC-004).</summary>
public sealed record CxiWeightsResponse
{
    [JsonPropertyName("weights")]
    public required IReadOnlyList<CxiWeightResponse> Weights { get; init; }
}
