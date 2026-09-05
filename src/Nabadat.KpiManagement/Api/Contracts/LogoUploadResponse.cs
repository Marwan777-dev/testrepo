using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// Wire shape of <c>POST /api/v1/tenant/organization/logo</c> (contracts/settings-api.md).
/// <c>was_sanitised</c> is true when the upload was SVG AND the sanitiser stripped at least one
/// node/attribute — the frontend uses it to show the non-blocking "sanitised" notice.
/// </summary>
public sealed record LogoUploadResponse
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("content_type")]
    public required string ContentType { get; init; }

    [JsonPropertyName("size_bytes")]
    public required long SizeBytes { get; init; }

    [JsonPropertyName("was_sanitised")]
    public required bool WasSanitised { get; init; }
}
