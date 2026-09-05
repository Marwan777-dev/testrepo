using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Domain.Interfaces;

/// <summary>
/// Data-access service for <see cref="Touchpoint"/> (tenant-schema, EF-backed over
/// <c>ITenantDbContext</c>). A touchpoint with no <c>kpi_bindings</c> rows is "unmeasured"; the
/// service derives that flag. Multi-step writes commit atomically with their M-17 event when the
/// caller wraps them in <c>ITenantDbContext.ExecuteAsync</c>.
/// </summary>
public interface ITouchpointDataService
{
    /// <summary>Loads a single touchpoint by id; null when it does not exist.</summary>
    Task<Touchpoint?> GetByIdAsync(Guid touchpointId, CancellationToken ct = default);

    /// <summary>All touchpoints belonging to a stage.</summary>
    Task<IReadOnlyList<Touchpoint>> ListByStageAsync(Guid stageId, CancellationToken ct = default);

    /// <summary>
    /// Number of touchpoints on a stage; backs both the touchpoint-per-stage limit
    /// enforcer and the stage-delete guard (a stage with touchpoints cannot be deleted).
    /// </summary>
    Task<int> CountByStageAsync(Guid stageId, CancellationToken ct = default);

    /// <summary>
    /// True when the touchpoint carries at least one <c>kpi_bindings</c> row. Backs the
    /// derived "measured" flag (FR-008): a touchpoint with no bindings is unmeasured
    /// (<c>isMeasured: false</c>) and excluded from score computation.
    /// </summary>
    Task<bool> HasKpiBindingsAsync(Guid touchpointId, CancellationToken ct = default);

    /// <summary>
    /// Every <c>kpi_bindings</c> row across all touchpoints of <paramref name="journeyId"/>'s
    /// stages, in one set-based query (no N+1). The caller groups by <c>TouchpointId</c> to attach
    /// each touchpoint's bindings to the journey tree (<c>GET /api/v1/journeys/{id}</c>), deriving
    /// the per-touchpoint <c>isMeasured</c> flag from whether its group is non-empty.
    /// </summary>
    Task<IReadOnlyList<KpiBinding>> ListKpiBindingsByJourneyAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>Inserts a new touchpoint (tracks + saves; flushes within an ambient transaction).</summary>
    Task CreateAsync(Touchpoint touchpoint, CancellationToken ct = default);

    /// <summary>Updates a touchpoint's mutable fields (tracks + saves; flushes within an ambient transaction).</summary>
    Task UpdateAsync(Touchpoint touchpoint, CancellationToken ct = default);

    /// <summary>
    /// Deletes a touchpoint. Child <c>kpi_bindings</c> are removed by the
    /// ON DELETE CASCADE foreign key.
    /// </summary>
    Task DeleteAsync(Guid touchpointId, CancellationToken ct = default);

    /// <summary>
    /// Full-replace save of a touchpoint's KPI binding set: deletes every existing
    /// <c>kpi_bindings</c> row for the touchpoint, then inserts <paramref name="bindings"/>
    /// (which may be empty — leaving the touchpoint unmeasured). MUST run inside the caller's
    /// <c>ITenantDbContext.ExecuteAsync</c> so the DELETE + INSERTs and the
    /// <c>journey.kpi_bindings.updated</c> event commit atomically (FR-015); a partial replace
    /// would transiently violate the 100%-weight invariant. The caller
    /// (<c>KpiBindingService</c>) validates the set first, so no validation happens here.
    /// </summary>
    Task ReplaceKpiBindingsAsync(Guid touchpointId, IReadOnlyList<KpiBinding> bindings, CancellationToken ct = default);
}
