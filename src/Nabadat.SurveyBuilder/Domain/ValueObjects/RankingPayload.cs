namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Ranking question payload (no sub-type — research.md §5). Respondents order the
/// <see cref="Items"/>. Not routing-eligible.
/// </summary>
/// <param name="Items">The items to be ranked, in initial display order.</param>
public sealed record RankingPayload(
    IReadOnlyList<string> Items) : QuestionTypePayload;
