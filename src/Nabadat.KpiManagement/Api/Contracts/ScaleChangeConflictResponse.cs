using System.Text.Json.Serialization;
using Nabadat.UserManagement.Api.Contracts;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// 409 body for <c>PUT /api/v1/kpis/{id}</c> when a Scale change would affect existing M-16
/// touchpoint bindings and <c>confirm_structural_change=true</c> was not supplied (FR-017). Wraps
/// the API-05 error envelope and adds the affected-binding counts so the UI can build its
/// confirmation prompt.
/// </summary>
public sealed record ScaleChangeConflictResponse
{
    [JsonPropertyName("error")]
    public required ApiErrorDetail Error { get; init; }

    [JsonPropertyName("affected_touchpoints")]
    public required int AffectedTouchpoints { get; init; }

    [JsonPropertyName("affected_journeys")]
    public required int AffectedJourneys { get; init; }
}
