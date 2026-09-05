using Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;

namespace Nabadat.SurveyBuilder.Application.QuestionsSets;

/// <summary>
/// Questions Set validator (T139, data-model.md §2.3 invariants): a required 1–200-char title, and
/// <c>0 &lt;= count &lt;= size(set)</c>. An empty set with <c>count = 0</c> is a valid no-op. Pure.
/// </summary>
public sealed class QuestionsSetValidator
{
    private const int MaxTitleLength = 200;

    public QuestionsSetValidationResult Validate(QuestionsSetDraft draft)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            errors.Add("questionsset.title.required");
        }
        else if (draft.Title.Length > MaxTitleLength)
        {
            errors.Add("questionsset.title.too_long");
        }

        if (draft.Count < 0)
        {
            errors.Add("questionsset.count.negative");
        }
        else if (draft.Count > draft.SetSize)
        {
            errors.Add("questionsset.count.exceeds_size");
        }

        return new QuestionsSetValidationResult(errors.Count == 0, errors);
    }
}
