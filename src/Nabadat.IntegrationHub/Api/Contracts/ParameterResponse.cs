using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// One parameter as SCR-05's row and SCR-06's drawer read it (FR-S5-02, FR-S6-02…05).
///
/// <para><c>data_type</c> and <c>origin</c> are the snake_case wire literals, not enum ordinals — see
/// <see cref="CreateParameterRequest"/>. The three derived booleans (<c>api_field_locked</c>,
/// <c>data_type_locked</c>, <c>mapping_support_changeable</c>) are computed server-side so the console can render
/// the locks without re-implementing BR-09/BR-11/BR-27 in TypeScript.</para>
/// </summary>
public sealed record ParameterResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name_en")]
    public string NameEn { get; init; } = string.Empty;

    [JsonPropertyName("name_ar")]
    public string NameAr { get; init; } = string.Empty;

    [JsonPropertyName("api_field")]
    public string ApiField { get; init; } = string.Empty;

    /// <summary>BR-11 — render the API-field input read-only (always true for a built-in, BR-09).</summary>
    [JsonPropertyName("api_field_locked")]
    public bool ApiFieldLocked { get; init; }

    [JsonPropertyName("data_type")]
    public string DataType { get; init; } = string.Empty;

    /// <summary><c>[PO-G27]</c> — render the type select read-only.</summary>
    [JsonPropertyName("data_type_locked")]
    public bool DataTypeLocked { get; init; }

    [JsonPropertyName("range_min")]
    public decimal? RangeMin { get; init; }

    [JsonPropertyName("range_max")]
    public decimal? RangeMax { get; init; }

    [JsonPropertyName("range_unit")]
    public string? RangeUnit { get; init; }

    [JsonPropertyName("validation_rule")]
    public string? ValidationRule { get; init; }

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("required_by_default")]
    public bool RequiredByDefault { get; init; }

    [JsonPropertyName("filterable")]
    public bool Filterable { get; init; }

    [JsonPropertyName("reporting_visibility")]
    public bool ReportingVisibility { get; init; }

    [JsonPropertyName("dashboard_visibility")]
    public bool DashboardVisibility { get; init; }

    [JsonPropertyName("mapping_support")]
    public bool MappingSupport { get; init; }

    /// <summary>BR-27 — whether SCR-06 may offer the Mapping-support switch at all.</summary>
    [JsonPropertyName("mapping_support_changeable")]
    public bool MappingSupportChangeable { get; init; }

    /// <summary>Drives SCR-05's "Mapped" link vs "—".</summary>
    [JsonPropertyName("mappings_count")]
    public int MappingsCount { get; init; }

    /// <summary>The channels whose contract includes this parameter (SCR-05's Channels count).</summary>
    [JsonPropertyName("channel_ids")]
    public IReadOnlyList<Guid> ChannelIds { get; init; } = Array.Empty<Guid>();

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }
}
