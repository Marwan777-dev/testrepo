using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>One requested CXI member weighting in the <c>PUT /kpis/{cxi_id}/weights</c> body
/// (contracts/kpi-api.md). Zero-weight entries are silently dropped server-side (BR-2.3).</summary>
public sealed record CxiWeightItemRequest
{
    [JsonPropertyName("member_kpi_id")]
    public required Guid MemberKpiId { get; init; }

    [JsonPropertyName("weight")]
    public required int Weight { get; init; }
}
