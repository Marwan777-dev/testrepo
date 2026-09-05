using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Domain.Interfaces;

/// <summary>
/// Data-access service for <see cref="JourneyScore"/> (tenant-schema <c>journey_scores</c>, one row
/// per journey — <c>uq_journey_scores_journey_id</c> UNIQUE, EF-backed over <c>ITenantDbContext</c>).
/// The score is refreshed wholesale on every <c>IJourneyScoreProvider.GetScoresAsync()</c> call, so
/// the only write is an upsert keyed on <c>journey_id</c>. It MUST run inside the caller's
/// <c>ITenantDbContext.ExecuteAsync</c> so the score row and its <c>journey.score.updated</c> event
/// commit atomically (FR-015).
/// </summary>
public interface IJourneyScoreDataService
{
    /// <summary>Inserts or refreshes the single <c>journey_scores</c> row for the journey.</summary>
    Task UpsertAsync(JourneyScore score, CancellationToken ct = default);
}
