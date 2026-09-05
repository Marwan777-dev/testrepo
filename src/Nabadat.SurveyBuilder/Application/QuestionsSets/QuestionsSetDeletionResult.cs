namespace Nabadat.SurveyBuilder.Application.QuestionsSets;

/// <summary>
/// Outcome of <c>QuestionsSetService.DeleteAsync</c> (T139): whether the set was deleted, and if not
/// the API-05 <see cref="ErrorCode"/> (<c>questionsset.delete.requires_confirmation</c> when the set
/// has ≥1 question and confirmation was not supplied, FR-2.6) plus the <see cref="Details"/> the
/// client renders in the confirmation prompt (<c>questions_count</c>) per
/// contracts/sections-and-sets.md DELETE 409.
/// </summary>
public sealed record QuestionsSetDeletionResult(
    bool Deleted,
    string? ErrorCode,
    IReadOnlyDictionary<string, object>? Details = null)
{
    public static QuestionsSetDeletionResult Success() => new(true, null);

    public static QuestionsSetDeletionResult Blocked(string errorCode, int questionsCount) =>
        new(false, errorCode, new Dictionary<string, object> { ["questions_count"] = questionsCount });
}
