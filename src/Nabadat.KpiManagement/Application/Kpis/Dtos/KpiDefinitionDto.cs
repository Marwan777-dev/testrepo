using Nabadat.KpiManagement.Domain.ValueObjects;

namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// Read-only projection of a complete KPI configuration returned by <c>IKpiConfigReader</c>.
/// Carries the definition fields plus the joined threshold band, perspectives, and (for the CXI
/// composite only) the member weights — everything a consumer needs to author a question (M-01),
/// render a gauge (M-07), or evaluate an alert (M-09) without reading M-06's tables.
///
/// <para>The enum-typed members reuse M-06's <c>Domain.ValueObjects</c> enums (the canonical KPI
/// vocabulary) so there is a single source of truth for the KPI type/scale/representation
/// vocabulary across the module and its consumers.</para>
/// </summary>
public record KpiDefinitionDto(
    Guid Id,
    string ShortName,
    string FullName,
    KpiType KpiType,
    bool IsComposite,
    CalculationMethod CalculationMethod,
    int? TopNValue,
    Scale? Scale,
    BilingualText? MinScaleDescription,
    BilingualText? MaxScaleDescription,
    RepresentationStyle? RepresentationStyle,
    EmojiSet? EmojiSet,
    decimal? Target,
    bool IsActive,
    bool ShowOnDashboard,
    KpiThresholdDto Thresholds,
    IReadOnlyList<KpiPerspectiveDto> Perspectives,
    IReadOnlyList<CxiWeightDto>? CxiWeights);
