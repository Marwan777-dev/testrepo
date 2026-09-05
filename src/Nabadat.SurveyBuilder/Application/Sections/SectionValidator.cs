using Nabadat.SurveyBuilder.Application.Sections.Dtos;

namespace Nabadat.SurveyBuilder.Application.Sections;

/// <summary>
/// Section field validator (T137, data-model.md §2.2): <c>name</c> is required and capped at 200
/// chars; <c>description</c> is optional. Pure.
/// </summary>
public sealed class SectionValidator
{
    private const int MaxNameLength = 200;

    public SectionValidationResult Validate(SectionDraft draft)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            errors.Add("section.name.required");
        }
        else if (draft.Name.Length > MaxNameLength)
        {
            errors.Add("section.name.too_long");
        }

        return new SectionValidationResult(errors.Count == 0, errors);
    }
}
