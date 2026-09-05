using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Questions.Interfaces;

/// <summary>
/// Data-access port for the question aggregate (DB-08). Implemented by <c>QuestionStore</c> (T065).
/// <see cref="MoveAsync"/> supports the US3 drag-and-drop reorder (contiguous <c>order</c>).
/// </summary>
public interface IQuestionStore
{
    Task<Question?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Question>> GetBySurveyAsync(Guid surveyId, CancellationToken ct = default);

    /// <summary>All questions in a section (standalone + set members), ordered — powers the section cascade (US3).</summary>
    Task<IReadOnlyList<Question>> GetBySectionAsync(Guid sectionId, CancellationToken ct = default);

    Task<int> CountBySurveyAsync(Guid surveyId, CancellationToken ct = default);

    /// <summary>Number of questions currently in a Questions Set — the ceiling for <c>QuestionsSetValidator</c> (US3).</summary>
    Task<int> CountBySetAsync(Guid setId, CancellationToken ct = default);

    Task AddAsync(Question question, CancellationToken ct = default);

    Task UpdateAsync(Question question, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Moves a question to a new section/set at the given target index (US3), then compacts sibling
    /// <c>order</c> values so both the source and destination <c>(section_id, set_id)</c> containers stay
    /// contiguous and unique (FR-8.2, contracts/questions.md). Runs inside the caller's transaction.
    /// </summary>
    Task MoveAsync(Guid questionId, Guid targetSectionId, Guid? targetSetId, int targetOrder, CancellationToken ct = default);
}
