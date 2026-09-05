using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Detection;

/// <summary>
/// EF <see cref="IDetectionDataService"/> over <see cref="ITenantDbContext"/> for the tenant-schema
/// <c>detection_configs</c> (one row per journey) and its child
/// <c>detection_threshold_overrides</c>. Replaces the raw-Npgsql <c>DetectionRepository</c>: the
/// journey-level write is a load-or-add upsert (preserving the stable
/// <c>detection_config_id</c> the override rows reference), and overrides are saved as a full
/// replace (delete all + insert), both inside the caller's
/// <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/> (FR-015).
/// </summary>
public sealed class DetectionDataService : IDetectionDataService
{
    private readonly ITenantDbContext _context;

    public DetectionDataService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<DetectionConfig?> GetByJourneyAsync(Guid journeyId, CancellationToken ct = default) =>
        _context.DetectionConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.JourneyId == journeyId, ct);

    /// <inheritdoc />
    public async Task UpsertConfigAsync(DetectionConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var existing = await _context.DetectionConfigs.FirstOrDefaultAsync(c => c.JourneyId == config.JourneyId, ct);
        if (existing is null)
        {
            _context.DetectionConfigs.Add(config);
        }
        else
        {
            // Replace in place — preserve the original detection_config_id and created_at so the
            // override rows that FK to it stay valid.
            existing.PainThreshold = config.PainThreshold;
            existing.HappyThreshold = config.HappyThreshold;
            existing.UpdatedAt = config.UpdatedAt;
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DetectionThresholdOverride>> ListOverridesAsync(
        Guid detectionConfigId,
        CancellationToken ct = default) =>
        await _context.DetectionThresholdOverrides.AsNoTracking()
            .Where(o => o.DetectionConfigId == detectionConfigId)
            .OrderBy(o => o.CreatedAt)
            .ThenBy(o => o.OverrideId)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task ReplaceOverridesAsync(
        Guid detectionConfigId,
        IReadOnlyList<DetectionThresholdOverride> overrides,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        // DELETE-all + re-INSERT as one unit (the caller's ExecuteAsync transaction) so a config
        // never transiently holds a partial override set.
        await _context.DetectionThresholdOverrides
            .Where(o => o.DetectionConfigId == detectionConfigId)
            .ExecuteDeleteAsync(ct);

        if (overrides.Count > 0)
        {
            _context.DetectionThresholdOverrides.AddRange(overrides);
            await _context.SaveChangesAsync(ct);
        }
    }
}
