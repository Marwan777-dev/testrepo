using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>
/// Applies the "Apply sentiment analysis" toggle (T078, FR-8.11). Sentiment is honoured only for
/// Input Field Text/Paragraph; requesting it on any other type is ignored with the warning
/// <c>sentiment.ignored_for_non_text</c>. Pure.
/// </summary>
public sealed class SentimentFlagPolicy
{
    public SentimentFlagResult Apply(QuestionType type, QuestionSubType subType, bool requested)
    {
        if (!requested)
        {
            return new SentimentFlagResult(false, Array.Empty<string>());
        }

        var eligible = type == QuestionType.InputField
            && subType is QuestionSubType.Text or QuestionSubType.Paragraph;

        return eligible
            ? new SentimentFlagResult(true, Array.Empty<string>())
            : new SentimentFlagResult(false, new[] { "sentiment.ignored_for_non_text" });
    }
}
