using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Kpis.Interfaces;

/// <summary>
/// The single per-entity service for <see cref="KpiThreshold"/> (DB-08 — CRUD + threshold
/// business rules over <see cref="Interfaces.ITenantDbContext"/>, NOT a repository). One threshold
/// row per KPI; this port is the unit-test mock seam.
/// </summary>
public interface IKpiThresholdService
{
    /// <summary>Loads the threshold row for a KPI; null if absent.</summary>
    Task<KpiThreshold?> GetByKpiIdAsync(Guid kpiId, CancellationToken ct = default);

    /// <summary>
    /// Inserts the threshold when absent, otherwise replaces its band edges in place (preserving
    /// the <c>kpi_id</c> primary key). The strictly-ascending invariant is enforced by the SQL
    /// CHECK constraint at write time.
    /// </summary>
    Task UpsertAsync(KpiThreshold threshold, CancellationToken ct = default);
}
