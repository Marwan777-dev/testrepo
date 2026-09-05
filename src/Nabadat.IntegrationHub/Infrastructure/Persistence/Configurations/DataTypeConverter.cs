using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists <see cref="DataType"/> as its snake_case wire value (data-model.md §4 / FR-F0-04, matching
/// the <c>ck_parameters_data_type</c> CHECK — which lists exactly these 13 literals and nothing else).
/// <para>The type list is closed: <c>duration</c> and <c>identifier</c> were evaluated and rejected
/// (<c>[PO-G17]</c>) and must never be added.</para>
/// <para>The mapping itself lives in <see cref="ParameterWireValues"/> because the Api layer needs the same
/// literals on the wire and may not reference Infrastructure — see that type's remarks.</para>
/// </summary>
public sealed class DataTypeConverter : ValueConverter<DataType, string>
{
    public DataTypeConverter() : base(v => ParameterWireValues.ToWire(v), v => FromWire(v))
    {
    }

    /// <summary>
    /// Reading is strict — unlike the Api layer's <c>TryParse</c>, an unrecognised literal in the <b>column</b>
    /// means the CHECK constraint and this enum have diverged, which must fail loudly rather than resolve to a
    /// default type.
    /// </summary>
    private static DataType FromWire(string value) =>
        ParameterWireValues.TryParseDataType(value, out var parsed)
            ? parsed
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown parameter data type wire value.");
}
