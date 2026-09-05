using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Domain.Interfaces;

/// <summary>
/// Data-access service for the tenant's strategic <see cref="ScoringConfig"/> (tenant-schema table
/// <c>scoring_configs</c>, <b>one row per tenant</b> — singleton, SRS §4.2.9 / §11.7, EF-backed over
/// <c>ITenantDbContext</c>). The write is an upsert: the tenant's first save inserts, every later save
/// replaces the same row in place (the original <c>scoring_config_id</c> / <c>created_at</c> survive).
/// </summary>
public interface IScoringConfigDataService
{
    /// <summary>Loads the tenant's single scoring-config row; <c>null</c> when none has been saved yet.</summary>
    Task<ScoringConfig?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Inserts or replaces the tenant's single scoring-config row and returns the persisted entity
    /// (canonical <c>scoring_config_id</c> / <c>created_at</c>). MUST run inside the caller's
    /// <c>ITenantDbContext.ExecuteAsync</c> so the row and the <c>journey.scoring_config.updated</c>
    /// M-17 event commit atomically (FR-015).
    /// </summary>
    Task<ScoringConfig> UpsertAsync(ScoringConfig config, CancellationToken ct = default);
}
