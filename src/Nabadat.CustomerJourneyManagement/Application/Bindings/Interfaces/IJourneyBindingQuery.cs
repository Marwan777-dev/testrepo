using Nabadat.CustomerJourneyManagement.Application.Bindings.Dtos;

namespace Nabadat.CustomerJourneyManagement.Application.Bindings.Interfaces;

/// <summary>
/// Published interface: M-16 → M-06 (Feature 003 / T020). Returns the count of touchpoints and
/// distinct non-archived journeys where the given KPI is bound. M-06 calls this to assemble the
/// FR-026 deactivation confirmation message and to detect when a scale change affects existing
/// bindings (FR-017).
///
/// <para>Placed in M-16's Application layer (not <c>Domain/Interfaces/</c>) per the project's
/// adopted convention for published read interfaces — see [[feedback-published-reader-in-application]].
/// This diverges from M-16's older <c>Nabadat.Platform.Contracts.M16</c> Domain placement
/// (<c>IJourneyConfigReader</c>); worth a constitution amendment if adopted module-wide.</para>
/// </summary>
public interface IJourneyBindingQuery
{
    /// <summary>
    /// Returns binding-usage counts for the given KPI (by M-06 <c>kpi_definitions.id</c>) on the
    /// current tenant. Returns (0, 0) for an unbound KPI. Archived journeys are excluded.
    /// </summary>
    Task<KpiBindingUsage> GetKpiBindingUsageAsync(Guid kpiId, CancellationToken ct = default);
}
