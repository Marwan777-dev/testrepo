using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Scores;

/// <summary>
/// EF <see cref="IJourneyScoreDataService"/> over <see cref="ITenantDbContext"/> for the
/// tenant-schema <c>journey_scores</c> table (one row per journey). Replaces the raw-Npgsql
/// <c>JourneyScoreRepository</c>; the old <c>INSERT … ON CONFLICT (journey_id) DO UPDATE</c> upsert
/// becomes a load-or-add (the stable <c>journey_score_id</c> survives a refresh). The M-06-shaped
/// <c>stage_scores</c> / <c>touchpoint_scores</c> jsonb trees are stored verbatim. Runs inside the
/// caller's <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/> so the row
/// and its <c>journey.score.updated</c> event commit atomically (FR-015).
/// </summary>
public sealed class JourneyScoreDataService : IJourneyScoreDataService
{
    private readonly ITenantDbContext _context;

    public JourneyScoreDataService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task UpsertAsync(JourneyScore score, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(score);

        var existing = await _context.JourneyScores.FirstOrDefaultAsync(s => s.JourneyId == score.JourneyId, ct);
        if (existing is null)
        {
            _context.JourneyScores.Add(score);
        }
        else
        {
            // Refresh in place — preserve the stable journey_score_id.
            existing.ComputedAt = score.ComputedAt;
            existing.CompositeScore = score.CompositeScore;
            existing.StageScores = score.StageScores;
            existing.TouchpointScores = score.TouchpointScores;
        }

        await _context.SaveChangesAsync(ct);
    }
}
