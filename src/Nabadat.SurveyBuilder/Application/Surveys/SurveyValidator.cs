using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// F3 Survey Settings validator (T067): <c>name_en</c> is required and ≤ 200 chars, and the derived
/// <see cref="SurveyType"/> follows the bound-journey invariant (BR-3.3). Pure — no dependencies.
/// </summary>
public sealed class SurveyValidator
{
    private const int NameMaxLength = 200;

    public SurveyValidationResult Validate(SurveyDraft draft)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.NameEn))
        {
            errors.Add("survey.name_en.required");
        }
        else if (draft.NameEn.Length > NameMaxLength)
        {
            errors.Add("survey.name_en.max_length");
        }

        var surveyType = draft.BoundJourney is null ? SurveyType.SeasonalRelational : SurveyType.Transactional;

        return errors.Count == 0
            ? SurveyValidationResult.Valid(surveyType)
            : SurveyValidationResult.Invalid(errors, surveyType);
    }
}
