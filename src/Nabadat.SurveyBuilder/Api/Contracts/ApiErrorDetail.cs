using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// The inner object of the API-05 error envelope. Error codes are dot-namespaced by surface
/// (research.md §9), e.g. <c>survey.name_en.required</c>, <c>survey.conflict</c>,
/// <c>survey.publish.requires_content</c>.
/// </summary>
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

    /// <summary>
    /// Structured context for the error when useful (research.md §9) — e.g. the Publish gate's
    /// <c>{ "missing_sections": true }</c>. Null when there is no structured context.
    /// </summary>
    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, object>? Details { get; init; }
}
