using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// Request body for <c>POST /api/v1/integration-hub/parameters</c> — SCR-06's "New parameter" submission.
///
/// <para>Every field is <b>optional at the binding layer on purpose</b>, matching
/// <see cref="CreateServiceChannelRequest"/>: a missing name must come back as the inline
/// <c>validation.name_en_required</c> error inside the API-05 envelope, not as a raw System.Text.Json
/// deserialisation failure with no code and no field.</para>
///
/// <para><c>data_type</c> travels as its <b>snake_case wire literal</b> (<c>"date_time"</c>, not <c>9</c>): the
/// host registers no <c>JsonStringEnumConverter</c>, so a bare enum would serialise as an integer and couple the
/// console to C# member ordering. Parsed through <c>ParameterWireValues</c>.</para>
///
/// <para>The four boolean usage flags carry FR-S6-04's ratified defaults, so a client that omits them gets the
/// documented behaviour rather than <c>false</c> across the board. <c>mapping_support</c> is nullable because it
/// is a <i>request</i> BR-27 may override — see <c>MappingSupportPolicy</c>.</para>
/// </summary>
public sealed record CreateParameterRequest
{
    [JsonPropertyName("name_en")]
    public string? NameEn { get; init; }

    [JsonPropertyName("name_ar")]
    public string? NameAr { get; init; }

    /// <summary>The <c>snake_case</c> wire key (VR-F06); client-suggested, editable until BR-11's lock.</summary>
    [JsonPropertyName("api_field")]
    public string? ApiField { get; init; }

    /// <summary>One of the 13 ratified literals (FR-F0-04); <c>duration</c>/<c>identifier</c> are not among them.</summary>
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
    public bool Enabled { get; init; } = true;

    /// <summary>FR-S6-04 default: Off.</summary>
    [JsonPropertyName("required_by_default")]
    public bool RequiredByDefault { get; init; }

    /// <summary>FR-S6-04 default: On.</summary>
    [JsonPropertyName("filterable")]
    public bool Filterable { get; init; } = true;

    /// <summary>FR-S6-04 default: On.</summary>
    [JsonPropertyName("reporting_visibility")]
    public bool ReportingVisibility { get; init; } = true;

    /// <summary>FR-S6-04 default: Off.</summary>
    [JsonPropertyName("dashboard_visibility")]
    public bool DashboardVisibility { get; init; }

    /// <summary>BR-27 resolves the stored value from the data type; this is only what the client asked for.</summary>
    [JsonPropertyName("mapping_support")]
    public bool? MappingSupport { get; init; }

    /// <summary>SCR-06's channel-assignment pills (FR-S6-05).</summary>
    [JsonPropertyName("channel_ids")]
    public IReadOnlyList<Guid>? ChannelIds { get; init; }
}
