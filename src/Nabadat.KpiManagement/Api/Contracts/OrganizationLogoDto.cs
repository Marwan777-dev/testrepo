using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// The <c>logo</c> block of the Organization response (contracts/settings-api.md). Null when no logo
/// has been uploaded. <c>url</c> is the app-relative endpoint that serves the persisted bytes.
/// </summary>
public sealed record OrganizationLogoDto
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("content_type")]
    public required string ContentType { get; init; }

    [JsonPropertyName("size_bytes")]
    public required long SizeBytes { get; init; }
}
