namespace Nabadat.CustomerJourneyManagement.Application.KpiTypes;

/// <summary>
/// One KPI available to bind on a touchpoint, as supplied by <see cref="Interfaces.IActiveKpiCatalogReader"/>.
/// This is M-16's view of a bindable KPI — assembled in the host from M-06's active KPI catalogue
/// (Feature 003), or, when M-16 runs standalone, from its own platform-standard reference data +
/// <c>kpi_type_definitions</c>.
/// </summary>
/// <param name="KpiId">
/// The M-06 <c>kpi_definitions.id</c> this entry refers to, persisted onto <c>kpi_bindings.kpi_id</c>
/// so the binding-usage probe (FR-026 / FR-017) can count touchpoints by KPI id. <see langword="null"/>
/// when the source has no M-06 id (the standalone default reader), leaving the binding's link blank.
/// </param>
/// <param name="Key">Stable type key used on the wire and as <c>kpi_bindings.kpi_type</c> — an M-06 Short Name.</param>
/// <param name="LabelAr">Arabic display label.</param>
/// <param name="LabelEn">English display label.</param>
/// <param name="ScoringDirection"><c>Ascending</c> | <c>Descending</c> — drives scoring orientation (CES is Descending).</param>
/// <param name="IsPlatformStandard"><c>true</c> for platform-standard KPIs, <c>false</c> for tenant-authored ones.</param>
public sealed record ActiveKpiCatalogEntry(
    Guid? KpiId,
    string Key,
    string LabelAr,
    string LabelEn,
    string ScoringDirection,
    bool IsPlatformStandard);
