using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Stages;

/// <summary>
/// EF <see cref="IStageDataService"/> over <see cref="ITenantDbContext"/> for the tenant-schema
/// <c>stages</c> table. Replaces the raw-Npgsql <c>StageRepository</c>. <see cref="ReorderAsync"/>
/// reassigns the per-journey ordering in two flushes (negate, then assign 1..n) so the
/// <c>(journey_id, sequence_number)</c> unique index is never transiently violated — it MUST run
/// inside the caller's <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>.
/// </summary>
public sealed class StageDataService : IStageDataService
{
    private readonly ITenantDbContext _context;

    public StageDataService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<Stage?> GetByIdAsync(Guid stageId, CancellationToken ct = default) =>
        _context.Stages.AsNoTracking().FirstOrDefaultAsync(s => s.StageId == stageId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Stage>> ListByJourneyAsync(Guid journeyId, CancellationToken ct = default) =>
        await _context.Stages.AsNoTracking()
            .Where(s => s.JourneyId == journeyId)
            .OrderBy(s => s.SequenceNumber)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<int> CountByJourneyAsync(Guid journeyId, CancellationToken ct = default) =>
        _context.Stages.AsNoTracking().CountAsync(s => s.JourneyId == journeyId, ct);

    /// <inheritdoc />
    public async Task<int> GetMaxSequenceNumberAsync(Guid journeyId, CancellationToken ct = default)
    {
        var max = await _context.Stages.AsNoTracking()
            .Where(s => s.JourneyId == journeyId)
            .Select(s => (int?)s.SequenceNumber)
            .MaxAsync(ct);
        return max ?? 0;
    }

    /// <inheritdoc />
    public async Task CreateAsync(Stage stage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stage);
        _context.Stages.Add(stage);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Stage stage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stage);
        _context.Stages.Update(stage);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid stageId, CancellationToken ct = default) =>
        _context.Stages.Where(s => s.StageId == stageId).ExecuteDeleteAsync(ct);

    /// <inheritdoc />
    public async Task ReorderAsync(Guid journeyId, IReadOnlyList<Guid> orderedStageIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orderedStageIds);
        if (orderedStageIds.Count == 0)
        {
            return;
        }

        // Load the targeted rows tracked so the two-phase reassignment is change-tracked.
        var stages = await _context.Stages
            .Where(s => s.JourneyId == journeyId && orderedStageIds.Contains(s.StageId))
            .ToListAsync(ct);

        // Phase 1: vacate the positive range by negating each sequence number (negation preserves
        // distinctness, so no transient collision against the unique index). Flushed on its own.
        foreach (var stage in stages)
        {
            stage.SequenceNumber = -stage.SequenceNumber;
        }

        await _context.SaveChangesAsync(ct);

        // Phase 2: assign the new 1-based positions from the id ordering, then flush again.
        var positionById = new Dictionary<Guid, int>(orderedStageIds.Count);
        for (var i = 0; i < orderedStageIds.Count; i++)
        {
            positionById[orderedStageIds[i]] = i + 1;
        }

        foreach (var stage in stages)
        {
            if (positionById.TryGetValue(stage.StageId, out var position))
            {
                stage.SequenceNumber = position;
            }
        }

        await _context.SaveChangesAsync(ct);
    }
}
