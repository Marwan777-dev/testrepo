using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>The <c>audit</c> block of the Organization response (contracts/settings-api.md).</summary>
public sealed record OrganizationAuditDto
{
    [JsonPropertyName("updated_at")]
    public required DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("updated_by")]
    public required Guid UpdatedBy { get; init; }
}
