namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// VR-F05 — the bilingual parameter names: both required, both ≤ 50 characters. Mirrors
/// <c>ChannelNameValidator</c> (US1/T033) so the two catalogues validate their names identically.
///
/// <para>Unlike a channel's EN name, a parameter's display names are <b>not</b> required to be unique — VR-F06
/// scopes uniqueness to the <c>api_field</c> alone, which is the value that actually has to be unambiguous on the
/// wire. Two parameters may legitimately both be labelled "Branch" while carrying different API fields.</para>
/// </summary>
public sealed class ParameterNameValidator
{
    /// <summary>
    /// Matches the baseline's <c>ck_parameters_name_en_length</c> / <c>..._name_ar_length</c> CHECKs so a valid
    /// input can never be rejected by the database instead of by this validator.
    /// </summary>
    public const int MaxNameLength = 50;

    /// <summary>Validates both names, accumulating every failure.</summary>
    public ParameterValidationResult Validate(string? nameEn, string? nameAr)
    {
        var errors = new List<ParameterValidationError>();

        if (string.IsNullOrWhiteSpace(nameEn))
        {
            errors.Add(new ParameterValidationError(
                ParameterErrorCodes.NameEnRequired, "Parameter name · EN is required", ParameterFields.NameEn));
        }
        else if (nameEn.Trim().Length > MaxNameLength)
        {
            errors.Add(new ParameterValidationError(
                ParameterErrorCodes.NameEnTooLong,
                $"Parameter name · EN must be {MaxNameLength} characters or fewer",
                ParameterFields.NameEn));
        }

        if (string.IsNullOrWhiteSpace(nameAr))
        {
            errors.Add(new ParameterValidationError(
                ParameterErrorCodes.NameArRequired, "Parameter name · AR is required", ParameterFields.NameAr));
        }
        else if (nameAr.Trim().Length > MaxNameLength)
        {
            errors.Add(new ParameterValidationError(
                ParameterErrorCodes.NameArTooLong,
                $"Parameter name · AR must be {MaxNameLength} characters or fewer",
                ParameterFields.NameAr));
        }

        return ParameterValidationResult.From(errors);
    }
}
