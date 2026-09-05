using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists <see cref="ParameterOrigin"/> as <c>built_in</c> / <c>custom</c> (data-model.md §4, matching
/// the <c>ck_parameters_origin</c> CHECK and the seeded built-in rows). This column is also the source of
/// the derived <c>Parameter.DataTypeLocked</c> projection ([PO-G27]).
/// <para>The mapping lives in <see cref="ParameterWireValues"/>, shared with the Api layer — see its remarks.</para>
/// </summary>
public sealed class ParameterOriginConverter : ValueConverter<ParameterOrigin, string>
{
    public ParameterOriginConverter() : base(v => ParameterWireValues.ToWire(v), v => FromWire(v))
    {
    }

    private static ParameterOrigin FromWire(string value) =>
        ParameterWireValues.TryParseOrigin(value, out var parsed)
            ? parsed
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown parameter origin wire value.");
}
