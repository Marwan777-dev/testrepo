using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Domain.Interfaces;

/// <summary>
/// Data-access service for <see cref="Stage"/> (tenant-schema, EF-backed over
/// <c>ITenantDbContext</c>). Stages are ordered within a journey by
/// <see cref="Stage.SequenceNumber"/> (1-based, unique per journey). Multi-step writes
/// commit atomically with their M-17 event when the caller wraps them in
/// <c>ITenantDbContext.ExecuteAsync</c>.
/// </summary>
public interface IStageDataService
{
    /// <summary>Loads a single stage by id; null when it does not exist.</summary>
    Task<Stage?> GetByIdAsync(Guid stageId, CancellationToken ct = default);

    /// <summary>All stages for a journey, ordered by <see cref="Stage.SequenceNumber"/> ascending.</summary>
    Task<IReadOnlyList<Stage>> ListByJourneyAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>Number of stages on a journey; used by the stage-limit enforcer.</summary>
    Task<int> CountByJourneyAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>
    /// Highest <see cref="Stage.SequenceNumber"/> on a journey, or 0 when it has no
    /// stages. Lets the service append a new stage at the end.
    /// </summary>
    Task<int> GetMaxSequenceNumberAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>Inserts a new stage (tracks + saves; flushes within an ambient transaction).</summary>
    Task CreateAsync(Stage stage, CancellationToken ct = default);

    /// <summary>Updates a stage's mutable fields (tracks + saves; flushes within an ambient transaction).</summary>
    Task UpdateAsync(Stage stage, CancellationToken ct = default);

    /// <summary>
    /// Deletes a stage. Callers MUST first verify the stage has no touchpoints
    /// (the delete-guard lives in the service layer).
    /// </summary>
    Task DeleteAsync(Guid stageId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new ordering by assigning <see cref="Stage.SequenceNumber"/> from the
    /// position of each id in <paramref name="orderedStageIds"/>. MUST run inside the
    /// caller's <c>ITenantDbContext.ExecuteAsync</c> so the unique
    /// <c>(journey_id, sequence_number)</c> index is never transiently violated.
    /// </summary>
    Task ReorderAsync(Guid journeyId, IReadOnlyList<Guid> orderedStageIds, CancellationToken ct = default);
}
