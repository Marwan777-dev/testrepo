using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.QuestionsSets.Interfaces;

/// <summary>
/// Data-access port for the questions-set aggregate (DB-08). Implemented by
/// <c>QuestionsSetStore</c> (T136) over <c>ITenantDbContext</c>. <see cref="GetBySectionAsync"/>
/// backs the section cascade-delete (T138) and the F10 structure view.
/// </summary>
public interface IQuestionsSetStore
{
    Task<QuestionsSet?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<QuestionsSet>> GetBySectionAsync(Guid sectionId, CancellationToken ct = default);

    Task AddAsync(QuestionsSet set, CancellationToken ct = default);

    Task UpdateAsync(QuestionsSet set, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
