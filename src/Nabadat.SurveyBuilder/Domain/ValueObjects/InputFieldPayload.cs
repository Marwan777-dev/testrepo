namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Input Field question payload (Text / Paragraph / Number / Date / Time / Date-Time / Month
/// sub-types — research.md §5). Sentiment analysis is available only on Text/Paragraph (FR-8.11).
/// </summary>
/// <param name="MaxLength">Optional maximum character length (Text/Paragraph).</param>
/// <param name="Placeholder">Optional placeholder text.</param>
public sealed record InputFieldPayload(
    int? MaxLength = null,
    string? Placeholder = null) : QuestionTypePayload;
