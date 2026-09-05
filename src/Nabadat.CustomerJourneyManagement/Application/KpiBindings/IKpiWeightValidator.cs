using Nabadat.CustomerJourneyManagement.Application.Common;

namespace Nabadat.CustomerJourneyManagement.Application.KpiBindings;

/// <summary>
/// One requested KPI binding in a touchpoint's full-replace save
/// (<c>contracts/configuration-api.md §PUT /api/v1/touchpoints/{id}/kpis</c>). <see cref="Weight"/>
/// is <see langword="decimal"/> (numeric(5,2)) — never <see langword="double"/> — so a set such as
/// 33.34 + 33.33 + 33.33 sums to exactly <c>100.00m</c> without IEEE-754 representation drift.
/// </summary>
/// <param name="KpiType">Platform-standard key (<c>NPS</c>/<c>CSAT</c>/<c>CES</c>/<c>FCR</c>/
/// <c>AgentSatisfaction</c>/<c>VFM</c>) or a tenant-defined <c>kpi_type_definitions.type_key</c>.</param>
/// <param name="Weight">Relative contribution of this KPI; must sit in <c>(0, 100]</c>.</param>
public sealed record KpiBindingInput(string KpiType, decimal Weight);

/// <summary>
/// Pure weight-rule guard for a touchpoint's KPI binding set, run before any persistence by
/// <c>KpiBindingService</c> (T047). An empty set is valid (an unmeasured touchpoint — all existing
/// bindings are deleted); a non-empty set must have each weight in <c>(0, 100]</c>, no duplicate
/// <c>kpiType</c>, every <c>kpiType</c> resolvable (platform-standard or tenant-defined), and weights
/// summing to exactly <c>100.00m</c>. Failures map to the API-05 codes in
/// <c>contracts/configuration-api.md</c>.
/// </summary>
public interface IKpiWeightValidator
{
    /// <summary>
    /// Validates <paramref name="bindings"/> against the touchpoint KPI weight rules, returning
    /// <see cref="ServiceResult.Success()"/> when valid or a failure carrying one of
    /// <c>kpi.individual_weight_invalid</c>, <c>kpi.duplicate_type</c>, <c>kpi.unknown_type</c>, or
    /// <c>kpi.weight_sum_invalid</c>. Each input violates at most one rule, so the returned code is
    /// deterministic.
    /// </summary>
    Task<ServiceResult> ValidateAsync(
        IReadOnlyList<KpiBindingInput> bindings,
        CancellationToken ct = default);
}
