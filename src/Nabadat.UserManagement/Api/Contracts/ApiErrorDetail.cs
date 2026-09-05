using System.Text.Json.Serialization;

namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>The inner object of the API-05 error envelope.</summary>
public sealed record ApiErrorDetail
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("tenant_id")]
    public string? TenantId { get; init; }

    /// <summary>Field-level failures for validation errors (400/422); null otherwise.</summary>
    [JsonPropertyName("details")]
    public IReadOnlyList<ApiErrorFieldDetail>? Details { get; init; }
}
