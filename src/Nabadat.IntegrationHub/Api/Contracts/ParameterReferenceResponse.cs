using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// One entry in BR-10's impact-warning list (Dialog D-6), returned by <c>PATCH .../parameters/{id}</c> when a
/// disable would affect existing consumers.
/// </summary>
public sealed record ParameterReferenceResponse
{
    /// <summary><c>channel_contract</c> | <c>data_scope_filter</c> | <c>rule_builder</c>.</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>The consumer's display name, as shown in the dialog.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
