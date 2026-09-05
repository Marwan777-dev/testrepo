using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>One CXI member weighting in a KPI configuration response (contracts/kpi-api.md).</summary>
public sealed record CxiWeightResponse
{
    [JsonPropertyName("member_kpi_id")]
    public required Guid MemberKpiId { get; init; }

    [JsonPropertyName("member_short_name")]
    public required string MemberShortName { get; init; }

    [JsonPropertyName("weight")]
    public required int Weight { get; init; }

    [JsonPropertyName("effective_percentage")]
    public required decimal EffectivePercentage { get; init; }
}
