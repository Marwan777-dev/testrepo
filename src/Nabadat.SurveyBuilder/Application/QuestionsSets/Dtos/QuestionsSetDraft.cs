namespace Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;

/// <summary>
/// Validation input for a Questions Set (T139, data-model.md §2.3). <see cref="SetSize"/> is the
/// set's current member count (the <c>Questions.Count</c> in the task fixture) — the ceiling for
/// <see cref="Count"/>. On create the set is empty, so callers pass <c>SetSize = int.MaxValue</c> to
/// skip the ceiling (the contract validates it only on update / as questions are added).
/// </summary>
public sealed record QuestionsSetDraft
{
    public string? Title { get; init; }

    public int Count { get; init; }

    public int SetSize { get; init; }
}
