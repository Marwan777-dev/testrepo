namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// How a Questions Set picks the subset it delivers per respondent per dispatch (tenant-schema
/// column <c>questions_sets.selection_mode</c>, data-model.md §2.3, F10). Wire form is the
/// lowercase DDL token (<c>random</c> / <c>low_response</c>) — see <c>QuestionsSetConfiguration</c>.
/// </summary>
public enum QuestionsSetSelectionMode
{
    /// <summary>Deterministic-random subset seeded by <c>respondent_id + survey_id</c>.</summary>
    Random,

    /// <summary>Prioritise the least-answered eligible questions (FR-10.4).</summary>
    LowResponse,
}
