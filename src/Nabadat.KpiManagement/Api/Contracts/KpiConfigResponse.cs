using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>
/// Full KPI configuration body returned by <c>GET /api/v1/kpis/{id}</c> and the 201/200 bodies of
/// create/update (contracts/kpi-api.md). Enum-derived fields are emitted as their canonical
/// PascalCase string names (e.g. <c>"WeightedAverage"</c>). <see cref="CxiWeights"/> is populated
/// only for the composite (CXI) KPI; null otherwise.
/// </summary>
public sealed record KpiConfigResponse
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

    [JsonPropertyName("calculation_method")]
    public required string CalculationMethod { get; init; }

    [JsonPropertyName("top_n_value")]
    public int? TopNValue { get; init; }

    [JsonPropertyName("scale")]
    public string? Scale { get; init; }

    [JsonPropertyName("min_scale_description")]
    public BilingualTextDto? MinScaleDescription { get; init; }

    [JsonPropertyName("max_scale_description")]
    public BilingualTextDto? MaxScaleDescription { get; init; }

    [JsonPropertyName("representation_style")]
    public string? RepresentationStyle { get; init; }

    [JsonPropertyName("emoji_set")]
    public string? EmojiSet { get; init; }

    [JsonPropertyName("target")]
    public decimal? Target { get; init; }

    [JsonPropertyName("is_active")]
    public required bool IsActive { get; init; }

    [JsonPropertyName("show_on_dashboard")]
    public required bool ShowOnDashboard { get; init; }

    [JsonPropertyName("thresholds")]
    public required KpiThresholdResponse Thresholds { get; init; }

    [JsonPropertyName("perspectives")]
    public required IReadOnlyList<KpiPerspectiveResponse> Perspectives { get; init; }

    [JsonPropertyName("cxi_weights")]
    public IReadOnlyList<CxiWeightResponse>? CxiWeights { get; init; }

    [JsonPropertyName("audit")]
    public KpiAuditResponse? Audit { get; init; }
}
