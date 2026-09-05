using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>The API-05 error envelope returned on every non-2xx M-01 response.</summary>
public sealed record ApiErrorEnvelope
{
    [JsonPropertyName("error")]
    public required ApiErrorDetail Error { get; init; }
}
