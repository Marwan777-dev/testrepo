using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// 200 body for <c>GET /api/v1/kpis/{id}/binding-usage</c> — how many M-16 touchpoints bind the KPI
/// and across how many distinct non-archived journeys (contracts/kpi-api.md).
/// </summary>
public sealed record BindingUsageResponse
{
    [JsonPropertyName("touchpoint_count")]
    public required int TouchpointCount { get; init; }

    [JsonPropertyName("journey_count")]
    public required int JourneyCount { get; init; }
}
