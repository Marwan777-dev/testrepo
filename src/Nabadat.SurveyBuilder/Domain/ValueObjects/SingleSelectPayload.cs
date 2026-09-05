namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Single Select question payload (List / Dropdown sub-types — research.md §5). Routing-eligible
/// when standalone (FR-9.5); each option's identifier is a valid routing <c>answer_key</c>.
/// </summary>
/// <param name="Options">The selectable options, in display order.</param>
public sealed record SingleSelectPayload(
    IReadOnlyList<string> Options) : QuestionTypePayload;
