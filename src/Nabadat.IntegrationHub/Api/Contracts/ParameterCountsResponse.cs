using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>SCR-05's origin-tab counts (FR-S5-01): "All · 23" / "Built-in" / "Custom".</summary>
public sealed record ParameterCountsResponse
{
    [JsonPropertyName("all")]
    public int All { get; init; }

    [JsonPropertyName("built_in")]
    public int BuiltIn { get; init; }

    /// <summary>The population VR-F13's 200 ceiling applies to — built-ins do not count.</summary>
    [JsonPropertyName("custom")]
    public int Custom { get; init; }
}
