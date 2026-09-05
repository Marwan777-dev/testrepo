using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// Wire shape of <c>GET</c>/<c>PUT /api/v1/tenant/organization</c> (contracts/settings-api.md).
/// snake_case per the API-05 convention. <c>industry_options</c> is the canonical list (the single
/// source of truth, <c>IIndustryEnumProvider.GetAll()</c>); <c>logo</c> is null when unset.
/// </summary>
public sealed record OrganizationResponse
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("logo")]
    public OrganizationLogoDto? Logo { get; init; }

    [JsonPropertyName("industry")]
    public required string Industry { get; init; }

    [JsonPropertyName("industry_options")]
    public required IReadOnlyList<string> IndustryOptions { get; init; }

    [JsonPropertyName("audit")]
    public OrganizationAuditDto? Audit { get; init; }
}
