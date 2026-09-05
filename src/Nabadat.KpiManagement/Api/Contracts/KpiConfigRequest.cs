using System.Text.Json.Serialization;
using Nabadat.KpiManagement.Domain.ValueObjects;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// Request body for <c>POST /api/v1/kpis</c> (create) and <c>PUT /api/v1/kpis/{id}</c> (update)
/// (contracts/kpi-api.md). Enum fields arrive as integers on the wire (System.Text.Json default —
/// no <c>JsonStringEnumConverter</c> is registered; the frontend converts at its request boundary).
/// </summary>
public sealed record KpiConfigRequest
{
    [JsonPropertyName("short_name")]
    public string? ShortName { get; init; }

    [JsonPropertyName("full_name")]
    public string? FullName { get; init; }

    [JsonPropertyName("perspectives")]
    public IReadOnlyList<KpiPerspectiveInputDto>? Perspectives { get; init; }

    [JsonPropertyName("calculation_method")]
    public CalculationMethod CalculationMethod { get; init; }

    [JsonPropertyName("top_n_value")]
    public short? TopNValue { get; init; }

    [JsonPropertyName("scale")]
    public Scale? Scale { get; init; }

    [JsonPropertyName("min_scale_description")]
    public BilingualTextDto? MinScaleDescription { get; init; }

    [JsonPropertyName("max_scale_description")]
    public BilingualTextDto? MaxScaleDescription { get; init; }

    [JsonPropertyName("representation_style")]
    public RepresentationStyle? RepresentationStyle { get; init; }

    [JsonPropertyName("emoji_set")]
    public EmojiSet? EmojiSet { get; init; }

    [JsonPropertyName("thresholds")]
    public KpiThresholdInputDto? Thresholds { get; init; }

    [JsonPropertyName("target")]
    public decimal? Target { get; init; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }

    [JsonPropertyName("show_on_dashboard")]
    public bool ShowOnDashboard { get; init; }
}
