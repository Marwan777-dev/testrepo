namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>
/// Result of <c>SentimentFlagPolicy.Apply</c> (T078, FR-8.11): whether sentiment analysis is applied
/// and any warning codes (<c>sentiment.ignored_for_non_text</c> for non Text/Paragraph types).
/// </summary>
public sealed record SentimentFlagResult(bool Applied, IReadOnlyList<string> Warnings);
