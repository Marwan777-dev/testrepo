namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Yes/No (Boolean) question payload with editable labels (research.md §5). Routing-eligible when
/// standalone; the answer keys are <c>"yes"</c> / <c>"no"</c> (FR-9 routing).
/// </summary>
/// <param name="YesLabel">Label for the affirmative choice (default "Yes").</param>
/// <param name="NoLabel">Label for the negative choice (default "No").</param>
public sealed record YesNoPayload(
    string YesLabel = "Yes",
    string NoLabel = "No") : QuestionTypePayload;
