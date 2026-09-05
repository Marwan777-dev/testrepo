using System.Diagnostics.CodeAnalysis;

namespace Nabadat.IntegrationHub.Domain.ValueObjects;

/// <summary>
/// The single source of truth for the snake_case wire values of <see cref="DataType"/> and
/// <see cref="ParameterOrigin"/> — the literals in the baseline's <c>ck_parameters_data_type</c> /
/// <c>ck_parameters_origin</c> CHECKs (data-model.md §4).
///
/// <para>It lives in Domain because <b>two independent layers need the same mapping</b>: Infrastructure's EF
/// <c>ValueConverter</c>s to read and write the column, and the Api layer's contracts to put the same literals on
/// the wire (the host registers no <c>JsonStringEnumConverter</c>, so a bare enum would serialise as an integer
/// and the console would receive <c>9</c> instead of <c>date_time</c>). Api and Infrastructure may not reference
/// each other, so without this shared helper the table would be spelled out twice and could drift — the exact
/// failure where a stored <c>date_time</c> starts being returned as something else.</para>
/// </summary>
public static class ParameterWireValues
{
    /// <summary>The 13 ratified types, as stored and as returned to the console.</summary>
    public static string ToWire(DataType value) => value switch
    {
        DataType.Text => "text",
        DataType.Number => "number",
        DataType.Boolean => "boolean",
        DataType.Email => "email",
        DataType.Phone => "phone",
        DataType.List => "list",
        DataType.Range => "range",
        DataType.Date => "date",
        DataType.DateTime => "date_time",
        DataType.Currency => "currency",
        DataType.Percentage => "percentage",
        DataType.Url => "url",
        DataType.Geolocation => "geolocation",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown parameter data type."),
    };

    /// <summary>
    /// Parses a wire value. Returns <c>false</c> rather than throwing for an unknown literal — an inbound request
    /// carrying <c>"duration"</c> (<c>[PO-G17]</c>: evaluated and rejected) is a client error to be reported as a
    /// 400, not an unhandled exception.
    /// </summary>
    public static bool TryParseDataType([NotNullWhen(true)] string? wire, out DataType value)
    {
        switch (wire)
        {
            case "text": value = DataType.Text; return true;
            case "number": value = DataType.Number; return true;
            case "boolean": value = DataType.Boolean; return true;
            case "email": value = DataType.Email; return true;
            case "phone": value = DataType.Phone; return true;
            case "list": value = DataType.List; return true;
            case "range": value = DataType.Range; return true;
            case "date": value = DataType.Date; return true;
            case "date_time": value = DataType.DateTime; return true;
            case "currency": value = DataType.Currency; return true;
            case "percentage": value = DataType.Percentage; return true;
            case "url": value = DataType.Url; return true;
            case "geolocation": value = DataType.Geolocation; return true;
            default: value = default; return false;
        }
    }

    public static string ToWire(ParameterOrigin value) => value switch
    {
        ParameterOrigin.BuiltIn => "built_in",
        ParameterOrigin.Custom => "custom",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown parameter origin."),
    };

    public static bool TryParseOrigin([NotNullWhen(true)] string? wire, out ParameterOrigin value)
    {
        switch (wire)
        {
            case "built_in": value = ParameterOrigin.BuiltIn; return true;
            case "custom": value = ParameterOrigin.Custom; return true;
            default: value = default; return false;
        }
    }
}
