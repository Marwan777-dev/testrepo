using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Scoring;

/// <summary>
/// EF <see cref="IScoringConfigDataService"/> over <see cref="ITenantDbContext"/> for the tenant-schema
/// <c>scoring_configs</c> table (<b>one row per tenant</b> — SRS §4.2.9 / §11.7). The upsert is a
/// load-or-add against the singleton: the original <c>scoring_config_id</c> / <c>created_at</c> survive
/// a replace, and the persisted row is returned so the caller can stamp its M-17 audit event with the
/// canonical id.
/// </summary>
public sealed class ScoringConfigDataService : IScoringConfigDataService
{
    private readonly ITenantDbContext _context;

    public ScoringConfigDataService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<ScoringConfig?> GetAsync(CancellationToken ct = default) =>
        _context.ScoringConfigs.AsNoTracking().FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<ScoringConfig> UpsertAsync(ScoringConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var existing = await _context.ScoringConfigs.FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            _context.ScoringConfigs.Add(config);
            await _context.SaveChangesAsync(ct);
            return config;
        }

        // Replace in place — preserve the original scoring_config_id and created_at.
        existing.Alpha = config.Alpha;
        existing.MotMultiplier = config.MotMultiplier;
        existing.NFloor = config.NFloor;
        existing.FlagPercentile = config.FlagPercentile;
        existing.RollingWindowDays = config.RollingWindowDays;
        existing.UpdatedAt = config.UpdatedAt;
        existing.UpdatedBy = config.UpdatedBy;

        await _context.SaveChangesAsync(ct);
        return existing;
    }
}
