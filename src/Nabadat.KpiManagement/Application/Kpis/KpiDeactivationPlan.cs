namespace Nabadat.KpiManagement.Application.Kpis;

/// <summary>
/// The mutation set derived when a KPI is deactivated (FR-026), produced by
/// <see cref="KpiDeactivationSideEffects.Compute"/>: the KPI's <see cref="ShowOnDashboard"/> is forced
/// false (an inactive KPI is never shown), and <see cref="CxiSideEffects"/> carries one entry per CXI
/// composite the KPI belonged to (empty when it was in no CXI). The command handler applies this plan
/// inside a single transaction and folds it into the audit event.
/// </summary>
public sealed record KpiDeactivationPlan(
    bool ShowOnDashboard,
    IReadOnlyList<CxiDeactivationSideEffect> CxiSideEffects);
