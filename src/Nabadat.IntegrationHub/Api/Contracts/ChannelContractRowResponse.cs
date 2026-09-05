using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// One persisted contract row in a channel response, carrying the parameter's identity alongside the flags so
/// SCR-04 can render its contract table without a second call to the parameter catalogue.
/// </summary>
public sealed record ChannelContractRowResponse
{
    [JsonPropertyName("parameter_id")]
    public required Guid ParameterId { get; init; }

    [JsonPropertyName("api_field")]
    public required string ApiField { get; init; }

    [JsonPropertyName("name_en")]
    public required string NameEn { get; init; }

    [JsonPropertyName("name_ar")]
    public required string NameAr { get; init; }

    [JsonPropertyName("supported")]
    public required bool Supported { get; init; }

    [JsonPropertyName("required")]
    public required bool Required { get; init; }
}
