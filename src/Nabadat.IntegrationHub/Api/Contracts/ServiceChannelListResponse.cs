using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// 200 body for <c>GET /api/v1/integration-hub/service-channels</c>: one cursor page (API-04 — cursor, never
/// offset). <see cref="NextCursor"/> is opaque to the client and <c>null</c> once the list is exhausted.
/// </summary>
public sealed record ServiceChannelListResponse
{
    [JsonPropertyName("items")]
    public required IReadOnlyList<ServiceChannelListItemResponse> Items { get; init; }

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; init; }
}
