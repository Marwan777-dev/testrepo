using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Sections.Interfaces;

/// <summary>
/// Data-access port for the section aggregate (DB-08). Implemented by <c>SectionStore</c> (T064).
/// </summary>
public interface ISectionStore
{
    Task<Section?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Section>> GetBySurveyAsync(Guid surveyId, CancellationToken ct = default);

    Task<int> CountBySurveyAsync(Guid surveyId, CancellationToken ct = default);

    Task AddAsync(Section section, CancellationToken ct = default);

    Task UpdateAsync(Section section, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
