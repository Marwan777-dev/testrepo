using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Surveys.Interfaces;

/// <summary>
/// Data-access port for the survey aggregate (DB-08 — the store is the only EF seam; it depends on
/// <c>ITenantDbContext</c>). Implemented by <c>SurveyStore</c> (T063). Multi-write atomicity is the
/// caller's concern via <c>ITenantDbContext.ExecuteAsync</c>.
/// </summary>
public interface ISurveyStore
{
    Task<Survey?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Section + total-question counts for the BR-1.7 publish gate.</summary>
    Task<SurveyContentCounts> GetContentCountsAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Survey survey, CancellationToken ct = default);

    Task UpdateAsync(Survey survey, CancellationToken ct = default);

    /// <summary>F1 Library listing with filters + cursor pagination (contracts/surveys.md).</summary>
    Task<SurveySearchResult> SearchAsync(SurveySearchQuery query, CancellationToken ct = default);
}
