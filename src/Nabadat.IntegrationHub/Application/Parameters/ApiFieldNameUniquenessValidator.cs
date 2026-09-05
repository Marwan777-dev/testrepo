using System.Text.RegularExpressions;

namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// T052 — enforces VR-F06 / BR-11: an API field name is required, <c>snake_case</c>, and unique per tenant
/// <b>across built-in + custom + enabled + disabled</b>. A disabled parameter keeps its field name reserved
/// forever (spec.md Edge Cases: disabling "never frees the API field name for a different purpose").
///
/// <para>Pure by design, mirroring <see cref="ChannelIdUniquenessValidator"/>: the caller
/// (<see cref="ParameterService"/>) supplies the tenant's existing field names — <b>every</b> row, disabled and
/// built-in included, already excluding the row being edited. Expressing "including disabled" as the caller's
/// contract rather than a flag is what makes it impossible for this validator to accidentally filter on
/// <c>enabled</c>; T065's endpoint test pins the full-list behaviour end-to-end.</para>
///
/// <para>Format is validated here too, not only in the database: the baseline's
/// <c>ck_parameters_api_field_format</c> CHECK is the last line of defence, but a caller sending
/// <c>waitTime</c> deserves an inline console error, not a 500.</para>
/// </summary>
public sealed class ApiFieldNameUniquenessValidator
{
    /// <summary>The baseline's <c>ck_parameters_api_field_format</c> CHECK, mirrored so the two cannot drift.</summary>
    private static readonly Regex SnakeCase = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Checks that <paramref name="apiField"/> is present, well formed, and unused.
    /// <paramref name="existingApiFields"/> may be <c>null</c> or empty — that simply means there is nothing to
    /// collide with.
    /// </summary>
    public ParameterValidationResult Validate(IEnumerable<string>? existingApiFields, string? apiField)
    {
        if (string.IsNullOrWhiteSpace(apiField))
        {
            return ParameterValidationResult.Invalid(new ParameterValidationError(
                ParameterErrorCodes.ApiFieldRequired,
                "API field name is required",
                ParameterFields.ApiField));
        }

        var candidate = apiField.Trim();
        var errors = new List<ParameterValidationError>();

        if (!SnakeCase.IsMatch(candidate))
        {
            errors.Add(new ParameterValidationError(
                ParameterErrorCodes.ApiFieldFormat,
                "API field name must be snake_case: lower-case letters, digits and underscores, starting with a letter",
                ParameterFields.ApiField));
        }

        // Checked even when the format already failed — the two rules ACCUMULATE rather than short-circuit, so a
        // client sending "WAIT_TIME" for a name that is already taken learns both things in one round-trip instead
        // of fixing the casing only to be rejected again. The comparison is ordinal-ignore-case for the same
        // reason: it must find the collision independently of whether the format rule passed.
        var collides = existingApiFields is not null
            && existingApiFields.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase));

        if (collides)
        {
            errors.Add(new ParameterValidationError(
                ParameterErrorCodes.DuplicateApiField,
                "This API field name is already in use",
                ParameterFields.ApiField));
        }

        return ParameterValidationResult.From(errors);
    }
}
