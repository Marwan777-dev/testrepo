using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>Audit block (created/updated by whom and when) in a KPI configuration response.</summary>
public sealed record KpiAuditResponse
{
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("created_by")]
    public required Guid CreatedBy { get; init; }

    [JsonPropertyName("updated_at")]
    public required DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("updated_by")]
    public required Guid UpdatedBy { get; init; }
}
