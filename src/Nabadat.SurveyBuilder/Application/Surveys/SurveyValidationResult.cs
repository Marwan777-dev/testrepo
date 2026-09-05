using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Result of <c>SurveyValidator.Validate</c> (T067): whether the draft is valid, the API-05 error
/// codes for any failures, and the derived <see cref="SurveyType"/> (BR-3.3).
/// </summary>
public sealed record SurveyValidationResult(bool IsValid, IReadOnlyList<string> Errors, SurveyType SurveyType)
{
    public static SurveyValidationResult Valid(SurveyType surveyType) =>
        new(true, Array.Empty<string>(), surveyType);

    public static SurveyValidationResult Invalid(IReadOnlyList<string> errors, SurveyType surveyType) =>
        new(false, errors, surveyType);
}
