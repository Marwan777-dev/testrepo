using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>Request body for <c>PUT /api/v1/tenant/organization</c> (contracts/settings-api.md).
/// Logo is uploaded separately. Both nullable so validation (not binding) surfaces "required".</summary>
public sealed record OrganizationUpdateRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("industry")]
    public string? Industry { get; init; }
}
