using Nabadat.SurveyBuilder.Application.Questions.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>
/// Per-type + sub-type question validator (T075, FR-8.8). Types with display variants (Scale,
/// Input Field, Single Select, Matrix) require a sub-type; the variant-less types (Multi-select,
/// Yes/No, Ranking, KPI) validate with <see cref="QuestionSubType.None"/>. A Scale/Slider needs a
/// positive step count. Pure.
/// </summary>
public sealed class QuestionValidator
{
    private static readonly HashSet<QuestionType> RequiresSubtype =
    [
        QuestionType.Scale,
        QuestionType.InputField,
        QuestionType.SingleSelect,
        QuestionType.Matrix,
    ];

    public QuestionValidationResult Validate(QuestionDraft draft)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.Text))
        {
            errors.Add("question.text.required");
        }

        var hasSubtype = draft.SubType is not null and not QuestionSubType.None;
        if (RequiresSubtype.Contains(draft.Type) && !hasSubtype)
        {
            errors.Add("question.subtype.required");
        }

        if (draft.Type == QuestionType.Scale && draft.SubType == QuestionSubType.Slider && (draft.SliderSteps ?? 0) < 1)
        {
            errors.Add("scale.slider.steps.min");
        }

        return new QuestionValidationResult(errors.Count == 0, errors);
    }
}
