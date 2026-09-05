using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// Request body for <c>POST /api/v1/integration-hub/service-channels</c> (SCR-04 create, FR-S4-01…04).
///
/// <para>Every field is <b>optional at the binding layer on purpose</b> — none is marked <c>required</c>.
/// A missing name must come back as the inline <c>validation.name_en_required</c> error inside the API-05
/// envelope (so SCR-04 can attach it to the field), not as a raw System.Text.Json deserialisation failure
/// with no code and no field. Requiredness is the validators' job (T033), not the binder's.</para>
/// </summary>
public sealed record CreateServiceChannelRequest
{
    [JsonPropertyName("name_en")]
    public string? NameEn { get; init; }

    [JsonPropertyName("name_ar")]
    public string? NameAr { get; init; }

    /// <summary>Sanitised server-side to <c>[A-Za-z0-9-]</c>, ≤ 19 chars (VR-F04).</summary>
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Defaults to Active when the client omits it, matching SCR-04's default toggle state.</summary>
    [JsonPropertyName("active")]
    public bool Active { get; init; } = true;

    /// <summary>The parameter contract; omit or send an empty list for a channel that supports nothing yet.</summary>
    [JsonPropertyName("contract")]
    public IReadOnlyList<ChannelParameterAssignmentPayload>? Contract { get; init; }
}
