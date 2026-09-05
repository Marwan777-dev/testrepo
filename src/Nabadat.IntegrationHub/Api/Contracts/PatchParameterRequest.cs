using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// Request body for <c>PATCH /api/v1/integration-hub/parameters/{id}</c> — SCR-05's inline enable/disable toggle
/// and SCR-06's edit drawer share this shape.
///
/// <para><b>Every member is nullable and omission means "leave unchanged"</b>, which is load-bearing rather than
/// cosmetic: a locked parameter's read-only form omits <c>api_field</c> (so BR-11's guard must not read the
/// omission as a rename), a built-in's read-only select omits <c>data_type</c> (so BR-09's guard is only consulted
/// on a real retype), and the inline toggle sends nothing but <c>enabled</c>.</para>
/// </summary>
public sealed record PatchParameterRequest
{
    [JsonPropertyName("name_en")]
    public string? NameEn { get; init; }

    [JsonPropertyName("name_ar")]
    public string? NameAr { get; init; }

    [JsonPropertyName("api_field")]
    public string? ApiField { get; init; }

    /// <summary>One of the 13 ratified literals; omitted by a built-in's read-only type select.</summary>
    [JsonPropertyName("data_type")]
    public string? DataType { get; init; }

    [JsonPropertyName("range_min")]
    public decimal? RangeMin { get; init; }

    [JsonPropertyName("range_max")]
    public decimal? RangeMax { get; init; }

    [JsonPropertyName("range_unit")]
    public string? RangeUnit { get; init; }

    [JsonPropertyName("validation_rule")]
    public string? ValidationRule { get; init; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    [JsonPropertyName("required_by_default")]
    public bool? RequiredByDefault { get; init; }

    [JsonPropertyName("filterable")]
    public bool? Filterable { get; init; }

    [JsonPropertyName("reporting_visibility")]
    public bool? ReportingVisibility { get; init; }

    [JsonPropertyName("dashboard_visibility")]
    public bool? DashboardVisibility { get; init; }

    [JsonPropertyName("mapping_support")]
    public bool? MappingSupport { get; init; }

    [JsonPropertyName("channel_ids")]
    public IReadOnlyList<Guid>? ChannelIds { get; init; }

    /// <summary>
    /// BR-10 — set once the user has acknowledged Dialog D-6. Without it, a disable on a referenced parameter
    /// returns <b>200</b> with <c>requires_confirmation: true</c> and the reference list, and the parameter is
    /// left unchanged.
    /// </summary>
    [JsonPropertyName("confirm_disable")]
    public bool ConfirmDisable { get; init; }
}
