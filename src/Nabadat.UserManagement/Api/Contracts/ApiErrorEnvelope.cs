using System.Text.Json.Serialization;

namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>The API-05 error envelope returned on every non-2xx response.</summary>
public sealed record ApiErrorEnvelope
{
    [JsonPropertyName("error")]
    public required ApiErrorDetail Error { get; init; }
}
