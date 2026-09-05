using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// One row of SCR-03's service-channels table (FR-S3-01): name + description, the channel-ID chip, status, and
/// the three counts. Deliberately <b>without</b> the contract rows — listing 100 channels must not carry 100
/// contracts, and the table shows only the counts.
/// </summary>
public sealed record ServiceChannelListItemResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name_en")]
    public required string NameEn { get; init; }

    [JsonPropertyName("name_ar")]
    public required string NameAr { get; init; }

    [JsonPropertyName("channel_id")]
    public required string ChannelId { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("active")]
    public required bool Active { get; init; }

    [JsonPropertyName("channel_id_locked")]
    public required bool ChannelIdLocked { get; init; }

    [JsonPropertyName("supported_count")]
    public required int SupportedCount { get; init; }

    [JsonPropertyName("required_count")]
    public required int RequiredCount { get; init; }

    [JsonPropertyName("integrations_count")]
    public required int IntegrationsCount { get; init; }
}
