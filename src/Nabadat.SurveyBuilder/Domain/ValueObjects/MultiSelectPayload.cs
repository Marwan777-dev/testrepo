namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Multi-select question payload (no sub-type — research.md §5). Not routing-eligible.
/// </summary>
/// <param name="Options">The selectable options, in display order.</param>
/// <param name="MinSelections">Optional minimum number of selections.</param>
/// <param name="MaxSelections">Optional maximum number of selections.</param>
public sealed record MultiSelectPayload(
    IReadOnlyList<string> Options,
    int? MinSelections = null,
    int? MaxSelections = null) : QuestionTypePayload;
