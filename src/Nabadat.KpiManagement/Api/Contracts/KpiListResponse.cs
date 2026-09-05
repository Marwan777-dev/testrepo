using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// 200 body for <c>GET /api/v1/kpis</c> (contracts/kpi-api.md): the catalogue page
/// <see cref="Items"/> and the opaque <see cref="NextCursor"/> (<c>null</c> when exhausted).
/// </summary>
public sealed record KpiListResponse
{
    [JsonPropertyName("items")]
    public required IReadOnlyList<KpiListItemResponse> Items { get; init; }

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; init; }
}
