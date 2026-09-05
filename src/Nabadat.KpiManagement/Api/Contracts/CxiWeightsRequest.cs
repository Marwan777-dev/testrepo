using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>Request body for <c>PUT /api/v1/kpis/{cxi_id}/weights</c> — the full-replace set of CXI
/// member weights (contracts/kpi-api.md).</summary>
public sealed record CxiWeightsRequest
{
    [JsonPropertyName("weights")]
    public IReadOnlyList<CxiWeightItemRequest> Weights { get; init; } = [];
}
