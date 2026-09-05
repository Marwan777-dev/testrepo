namespace Nabadat.SurveyBuilder.Application.Sections;

/// <summary>
/// Outcome of <c>SectionCascadeService.DeleteAsync</c> (T138): whether the section was deleted, and
/// if not, the API-05 <see cref="ErrorCode"/> (e.g. <c>section.delete.requires_confirmation</c>)
/// plus the <see cref="Details"/> the caller needs to render a confirmation prompt — the child
/// counts (<c>standalone_questions</c>, <c>questions_sets</c>, <c>set_questions</c>) per
/// contracts/sections-and-sets.md DELETE 409.
/// </summary>
public sealed record SectionCascadeResult(
    bool Deleted,
    string? ErrorCode,
    IReadOnlyDictionary<string, object>? Details = null)
{
    public static SectionCascadeResult Success() => new(true, null);

    public static SectionCascadeResult Blocked(
        string errorCode,
        int standaloneQuestions,
        int questionsSets,
        int setQuestions) =>
        new(false, errorCode, new Dictionary<string, object>
        {
            ["standalone_questions"] = standaloneQuestions,
            ["questions_sets"] = questionsSets,
            ["set_questions"] = setQuestions,
        });
}
