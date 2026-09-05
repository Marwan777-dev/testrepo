namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// The wire field names a <see cref="ParameterValidationError"/> can point at — the same snake_case keys the
/// SCR-06 request body uses, so the API-05 envelope's <c>details[].field</c> matches what the client sent and the
/// drawer can attach each error to the right input.
/// </summary>
public static class ParameterFields
{
    /// <summary>Parameter name · EN (VR-F05); typing it drives the API-field auto-suggest.</summary>
    public const string NameEn = "name_en";

    /// <summary>Parameter name · AR (VR-F05), rendered RTL.</summary>
    public const string NameAr = "name_ar";

    /// <summary>The <c>snake_case</c> wire key the caller sends (VR-F06, BR-11).</summary>
    public const string ApiField = "api_field";

    /// <summary>One of the 13 ratified types (FR-F0-04).</summary>
    public const string DataType = "data_type";

    /// <summary>Range card — Minimum (VR-F07).</summary>
    public const string RangeMin = "range_min";

    /// <summary>Range card — Maximum (VR-F07).</summary>
    public const string RangeMax = "range_max";

    /// <summary>Range card — optional unit label.</summary>
    public const string RangeUnit = "range_unit";

    /// <summary>The optional per-type validation rule; violations reject a request with <c>E-1003</c>.</summary>
    public const string ValidationRule = "validation_rule";

    /// <summary>The Enabled ⇄ Disabled toggle (FR-S5-03, guarded by BR-10).</summary>
    public const string Enabled = "enabled";

    /// <summary>The SCR-06 channel-assignment pills (FR-S6-05).</summary>
    public const string ChannelIds = "channel_ids";
}
