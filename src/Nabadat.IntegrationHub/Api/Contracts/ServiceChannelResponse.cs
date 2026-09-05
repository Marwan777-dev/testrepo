using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// A single service channel — the 201 body of <c>POST</c>, the 200 body of <c>PUT</c> and of
/// <c>GET .../{id}</c>. Includes the full parameter contract, which SCR-04's edit form renders.
///
/// <para><see cref="ChannelIdLocked"/> is the flag SCR-04 renders the ID field read-only from, with the BR-05
/// lock explanation (AC-S4-02) — the client must not infer the lock from traffic counts.</para>
/// </summary>
public sealed record ServiceChannelResponse
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

    /// <summary>BR-05 — one-way, set by the channel's first 2xx request.</summary>
    [JsonPropertyName("channel_id_locked")]
    public required bool ChannelIdLocked { get; init; }

    [JsonPropertyName("supported_count")]
    public required int SupportedCount { get; init; }

    [JsonPropertyName("required_count")]
    public required int RequiredCount { get; init; }

    [JsonPropertyName("integrations_count")]
    public required int IntegrationsCount { get; init; }

    [JsonPropertyName("contract")]
    public required IReadOnlyList<ChannelContractRowResponse> Contract { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public required DateTimeOffset UpdatedAt { get; init; }
}
