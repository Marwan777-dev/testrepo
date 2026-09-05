using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// One parameter-contract row on the wire (SCR-04's contract table, FR-S4-04). <c>required</c> is only
/// honoured while <c>supported</c> is <c>true</c> — the server normalises the pair rather than rejecting a
/// contradiction, so a stale client cannot post an impossible row.
/// </summary>
public sealed record ChannelParameterAssignmentPayload
{
    [JsonPropertyName("parameter_id")]
    public Guid ParameterId { get; init; }

    [JsonPropertyName("supported")]
    public bool Supported { get; init; }

    [JsonPropertyName("required")]
    public bool Required { get; init; }
}
