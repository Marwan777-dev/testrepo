namespace Nabadat.CustomerJourneyManagement.Application.Bindings.Dtos;

/// <summary>
/// Binding-usage counts for a KPI: how many touchpoints bind it, and across how many distinct
/// non-archived journeys. Returned by <c>IJourneyBindingQuery.GetKpiBindingUsageAsync</c>; (0, 0)
/// for an unbound KPI.
/// </summary>
public record KpiBindingUsage(int TouchpointCount, int JourneyCount);
