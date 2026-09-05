using System.Text.Json.Serialization;
using Nabadat.UserManagement.Api.Contracts;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// 409 body for <c>PATCH /api/v1/kpis/{id}/activation</c> when deactivating a KPI that is still bound
/// to M-16 touchpoints and <c>confirm=true</c> was not supplied (FR-026). Wraps the API-05 error
/// envelope (<c>KPI_DEACTIVATION_REQUIRES_CONFIRMATION</c>) and adds the binding-usage counts the UI
/// renders in its confirmation prompt.
/// </summary>
public sealed record DeactivationConfirmationResponse
{
    [JsonPropertyName("error")]
    public required ApiErrorDetail Error { get; init; }

    [JsonPropertyName("touchpoint_count")]
    public required int TouchpointCount { get; init; }

    [JsonPropertyName("journey_count")]
    public required int JourneyCount { get; init; }
}
