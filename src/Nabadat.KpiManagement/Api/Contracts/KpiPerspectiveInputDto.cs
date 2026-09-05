using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>One perspective in a KPI create/update request body (contracts/kpi-api.md). Full-replace.</summary>
public sealed record KpiPerspectiveInputDto
{
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("display_order")]
    public short DisplayOrder { get; init; }
}
