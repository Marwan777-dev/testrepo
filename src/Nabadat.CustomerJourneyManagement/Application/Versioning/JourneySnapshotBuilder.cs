using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Application.Versioning;

/// <summary>
/// EF <see cref="IJourneySnapshotBuilder"/> over <see cref="ITenantDbContext"/> (T067 / US-3):
/// assembles the full journey tree the version snapshot freezes. Replaces the raw-Npgsql builder.
/// It returns <i>domain entities</i> (not the M-06 <c>JourneyConfigDto</c>) because a snapshot must
/// record touchpoint <c>channels</c>/<c>importance</c>/flags and the detection config that the
/// config DTO does not carry. Returns <c>null</c> when the journey does not exist. All reads are
/// <c>AsNoTracking</c>.
/// </summary>
public sealed class JourneySnapshotBuilder : IJourneySnapshotBuilder
{
    private readonly ITenantDbContext _context;

    public JourneySnapshotBuilder(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<JourneySnapshotInput?> BuildAsync(Guid journeyId, CancellationToken ct = default)
    {
        var journey = await _context.Journeys.AsNoTracking()
            .FirstOrDefaultAsync(j => j.JourneyId == journeyId, ct);
        if (journey is null)
        {
            return null;
        }

        // Scoring is tenant-level (SRS §4.2.9, Q11): one scoring_configs row per tenant, captured into
        // every journey version snapshot so historical recomputation uses the parameters live at publish.
        var scoringConfig = await _context.ScoringConfigs.AsNoTracking()
            .FirstOrDefaultAsync(ct);
        var detectionConfig = await _context.DetectionConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.JourneyId == journeyId, ct);

        var stages = await _context.Stages.AsNoTracking()
            .Where(s => s.JourneyId == journeyId)
            .OrderBy(s => s.SequenceNumber)
            .ToListAsync(ct);

        // Touchpoints of the journey's stages, grouped by stage in memory (one query, no N+1).
        var touchpoints = await _context.Touchpoints.AsNoTracking()
            .Where(t => _context.Stages.Any(s => s.StageId == t.StageId && s.JourneyId == journeyId))
            .OrderBy(t => t.StageId).ThenBy(t => t.CreatedAt).ThenBy(t => t.TouchpointId)
            .ToListAsync(ct);
        var touchpointsByStage = touchpoints
            .GroupBy(t => t.StageId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // KPI bindings across the journey's touchpoints, grouped by touchpoint in memory.
        var bindings = await _context.KpiBindings.AsNoTracking()
            .Where(b => _context.Touchpoints.Any(t => t.TouchpointId == b.TouchpointId
                && _context.Stages.Any(s => s.StageId == t.StageId && s.JourneyId == journeyId)))
            .OrderBy(b => b.TouchpointId).ThenBy(b => b.KpiType)
            .ToListAsync(ct);
        var bindingsByTouchpoint = bindings
            .GroupBy(b => b.TouchpointId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var stageInputs = new List<StageSnapshotInput>(stages.Count);
        foreach (var stage in stages)
        {
            var stageTouchpoints = touchpointsByStage.GetValueOrDefault(stage.StageId) ?? [];
            var touchpointInputs = new List<TouchpointSnapshotInput>(stageTouchpoints.Count);
            foreach (var touchpoint in stageTouchpoints)
            {
                var tpBindings = bindingsByTouchpoint.GetValueOrDefault(touchpoint.TouchpointId) ?? [];
                touchpointInputs.Add(new TouchpointSnapshotInput(touchpoint, tpBindings));
            }

            stageInputs.Add(new StageSnapshotInput(stage, touchpointInputs));
        }

        return new JourneySnapshotInput(journey, scoringConfig, detectionConfig, stageInputs);
    }
}
