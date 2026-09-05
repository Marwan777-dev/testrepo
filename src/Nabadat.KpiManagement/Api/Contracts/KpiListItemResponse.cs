using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// Wire shape of one catalogue row in the <c>GET /api/v1/kpis</c> response (contracts/kpi-api.md).
/// snake_case per the platform's API-05 convention (the M-10 reference annotates every contract
/// field). Mapped from the application projection <c>Application.Catalogue.Dtos.KpiListItemDto</c> by
/// the controller.
/// </summary>
public sealed record KpiListItemResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("short_name")]
    public required string ShortName { get; init; }

    [JsonPropertyName("full_name")]
    public required string FullName { get; init; }

    [JsonPropertyName("kpi_type")]
    public required string KpiType { get; init; }

    [JsonPropertyName("is_composite")]
    public required bool IsComposite { get; init; }

    [JsonPropertyName("scale")]
    public string? Scale { get; init; }

    [JsonPropertyName("calculation_method")]
    public required string CalculationMethod { get; init; }

    [JsonPropertyName("calculation_method_label")]
    public required string CalculationMethodLabel { get; init; }

    [JsonPropertyName("scale_label")]
    public required string ScaleLabel { get; init; }

    [JsonPropertyName("target")]
    public decimal? Target { get; init; }

    [JsonPropertyName("is_active")]
    public required bool IsActive { get; init; }

    [JsonPropertyName("show_on_dashboard")]
    public required bool ShowOnDashboard { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }
}
