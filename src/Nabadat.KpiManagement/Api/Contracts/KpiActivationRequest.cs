using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// Request body for <c>PATCH /api/v1/kpis/{id}/activation</c> (contracts/kpi-api.md). <see cref="Active"/>
/// is the target state; <see cref="Confirm"/> is the explicit acknowledgement that bypasses the
/// deactivation confirmation gate when the KPI is bound to M-16 touchpoints (FR-026).
/// </summary>
public sealed record KpiActivationRequest
{
    [JsonPropertyName("active")]
    public required bool Active { get; init; }

    [JsonPropertyName("confirm")]
    public bool Confirm { get; init; }
}
