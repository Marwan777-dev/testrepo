using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>One perspective in a KPI configuration response (contracts/kpi-api.md).</summary>
public sealed record KpiPerspectiveResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("display_order")]
    public required short DisplayOrder { get; init; }
}
