using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// Request body for <c>PUT /api/v1/integration-hub/service-channels/{id}</c> (SCR-04 edit).
///
/// <para>Omitting <see cref="ChannelId"/> means "leave the ID as it is" — which is exactly what a locked
/// channel's read-only form sends (AC-S4-02). Sending a <i>different</i> value for a locked channel is a
/// change attempt and returns <b>409 <c>channel.id_locked</c></b> (BR-05), regardless of whether the client
/// believed the field was editable.</para>
///
/// <para><see cref="Contract"/> is a <b>full replacement</b>: rows absent from the list are unassigned. That
/// mirrors how SCR-04's contract table submits its complete state on save.</para>
/// </summary>
public sealed record UpdateServiceChannelRequest
{
    [JsonPropertyName("name_en")]
    public string? NameEn { get; init; }

    [JsonPropertyName("name_ar")]
    public string? NameAr { get; init; }

    /// <summary><c>null</c> = not submitted; the persisted ID stands.</summary>
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("active")]
    public bool Active { get; init; } = true;

    [JsonPropertyName("contract")]
    public IReadOnlyList<ChannelParameterAssignmentPayload>? Contract { get; init; }
}
