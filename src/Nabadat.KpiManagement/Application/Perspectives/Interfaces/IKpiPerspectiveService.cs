using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Perspectives.Interfaces;

/// <summary>
/// The single per-entity service for <see cref="KpiPerspective"/> (DB-08 — CRUD + business logic
/// over <see cref="KpiManagement.Application.Interfaces.ITenantDbContext"/>, NOT a repository and
/// NOT a separate <c>*DataService</c>). 0..10 perspectives per KPI; this port is the unit-test
/// mock seam.
/// </summary>
public interface IKpiPerspectiveService
{
    /// <summary>Loads a KPI's perspectives ordered by <c>display_order</c>.</summary>
    Task<IReadOnlyList<KpiPerspective>> ListByKpiIdAsync(Guid kpiId, CancellationToken ct = default);

    /// <summary>
    /// Full-replace save (FR-028): deletes all existing perspectives for the KPI and inserts the
    /// supplied set in one save. Invoked inside the KPI-save transaction so the replacement is
    /// atomic with the rest of the write.
    /// </summary>
    Task ReplaceAllAsync(Guid kpiId, IEnumerable<KpiPerspective> perspectives, CancellationToken ct = default);
}
