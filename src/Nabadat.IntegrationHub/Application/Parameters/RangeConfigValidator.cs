using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// T054 — enforces VR-F07 / FR-S6-03: a <see cref="DataType.Range"/> parameter's Minimum and Maximum are both
/// required and Minimum must be strictly less than Maximum; the Unit label is optional.
///
/// <para>The data type is an input because the rule is <b>conditional</b> — the SCR-06 Range card only exists for
/// Range (AC-S6-01). The validator therefore enforces the rule in both directions:</para>
/// <list type="bullet">
///   <item><b>Range</b> — both bounds required, <c>min &lt; max</c>.</item>
///   <item><b>every other type</b> — no range configuration at all, mirroring the baseline's
///   <c>ck_parameters_range_only_for_range</c> CHECK. Without this half, a client that switches Range → List
///   while leaving the card populated would hit a database exception instead of an inline error.</item>
/// </list>
/// </summary>
public sealed class RangeConfigValidator
{
    /// <summary>
    /// Validates the Range sub-configuration for <paramref name="dataType"/>. Failures accumulate, so a
    /// submission missing both bounds reports both.
    /// </summary>
    public ParameterValidationResult Validate(DataType dataType, decimal? min, decimal? max, string? unit = null)
    {
        var errors = new List<ParameterValidationError>();

        if (dataType != DataType.Range)
        {
            if (min is not null || max is not null || !string.IsNullOrWhiteSpace(unit))
            {
                errors.Add(new ParameterValidationError(
                    ParameterErrorCodes.RangeNotApplicable,
                    "Minimum, Maximum and Unit apply only to a Range parameter",
                    ParameterFields.DataType));
            }

            return ParameterValidationResult.From(errors);
        }

        if (min is null)
        {
            errors.Add(new ParameterValidationError(
                ParameterErrorCodes.RangeMinRequired, "Minimum is required", ParameterFields.RangeMin));
        }

        if (max is null)
        {
            errors.Add(new ParameterValidationError(
                ParameterErrorCodes.RangeMaxRequired, "Maximum is required", ParameterFields.RangeMax));
        }

        // Strict, per VR-F07's "Minimum < Maximum": equal bounds describe an empty range and are rejected.
        if (min is not null && max is not null && min >= max)
        {
            errors.Add(new ParameterValidationError(
                ParameterErrorCodes.RangeMinMax, "Minimum must be less than Maximum", ParameterFields.RangeMin));
        }

        return ParameterValidationResult.From(errors);
    }
}
