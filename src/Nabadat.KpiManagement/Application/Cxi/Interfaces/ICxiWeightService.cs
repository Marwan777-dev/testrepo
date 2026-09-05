using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Cxi.Interfaces;

/// <summary>
/// The single per-entity service for <see cref="CxiWeight"/> (DB-08 — CRUD + CXI weight business
/// logic over <see cref="KpiManagement.Application.Interfaces.ITenantDbContext"/>, NOT a
/// repository). Rows exist only when the CXI KPI has members; this port is the unit-test mock seam.
/// </summary>
public interface ICxiWeightService
{
    /// <summary>Loads the CXI's member weights.</summary>
    Task<IReadOnlyList<CxiWeight>> ListByCxiKpiIdAsync(Guid cxiKpiId, CancellationToken ct = default);

    /// <summary>
    /// Returns every CXI membership row in which <paramref name="memberKpiId"/> participates as a
    /// member — the deactivation-cascade lookup (FR-026 / FR-044): when a KPI is deactivated, the
    /// caller removes these rows and recomputes effective percentages.
    /// </summary>
    Task<IReadOnlyList<CxiWeight>> GetCxiMembershipsForKpiAsync(Guid memberKpiId, CancellationToken ct = default);

    /// <summary>
    /// Full-replace save: deletes all existing member rows for the CXI and inserts the supplied set
    /// in one save (members with weight ≤ 0 must be excluded by the caller per the <c>weight &gt; 0</c>
    /// invariant). Commits atomically when wrapped in the surrounding transaction.
    /// </summary>
    Task ReplaceAllAsync(Guid cxiKpiId, IEnumerable<CxiWeight> weights, CancellationToken ct = default);

    /// <summary>Removes a single member from the CXI (used by the deactivation cascade).</summary>
    Task RemoveMemberAsync(Guid cxiKpiId, Guid memberKpiId, CancellationToken ct = default);
}
