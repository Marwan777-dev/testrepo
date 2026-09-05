using System.Text.Json.Serialization;

namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>
/// One field-level validation failure inside the API-05 error envelope's
/// <c>details</c> array (e.g. <c>{ "field": "parameters[0].allowedValues", "code": "empty" }</c>).
/// </summary>
public sealed record ApiErrorFieldDetail
{
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    [JsonPropertyName("code")]
    public required string Code { get; init; }
}
