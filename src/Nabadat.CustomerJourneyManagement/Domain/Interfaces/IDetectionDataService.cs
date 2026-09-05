using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Domain.Interfaces;

/// <summary>
/// Data-access service for <see cref="DetectionConfig"/> (one row per journey) and its
/// <see cref="DetectionThresholdOverride"/> children (tenant-schema, EF-backed over
/// <c>ITenantDbContext</c>). The journey-level config is upserted; overrides are saved as a full
/// replace. Multi-step writes commit atomically with their M-17 event when the caller wraps them in
/// <c>ITenantDbContext.ExecuteAsync</c>.
/// </summary>
public interface IDetectionDataService
{
    /// <summary>Loads the journey-level detection config; null when none is configured.</summary>
    Task<DetectionConfig?> GetByJourneyAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>Inserts or updates the journey-level config (<c>detection_configs</c> is UNIQUE per journey).</summary>
    Task UpsertConfigAsync(DetectionConfig config, CancellationToken ct = default);

    /// <summary>
    /// All per-stage / per-touchpoint overrides for a detection config. The override
    /// resolver narrows these to the most specific scope (touchpoint &gt; stage &gt; journey).
    /// </summary>
    Task<IReadOnlyList<DetectionThresholdOverride>> ListOverridesAsync(Guid detectionConfigId, CancellationToken ct = default);

    /// <summary>
    /// Full-replace save of a config's overrides (DELETE all + INSERT the supplied set). MUST run
    /// inside the caller's <c>ITenantDbContext.ExecuteAsync</c>, mirroring the KPI-binding save.
    /// </summary>
    Task ReplaceOverridesAsync(
        Guid detectionConfigId,
        IReadOnlyList<DetectionThresholdOverride> overrides,
        CancellationToken ct = default);
}
