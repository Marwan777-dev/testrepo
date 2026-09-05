using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.CrossModule;

/// <summary>
/// Placeholder <see cref="IKpiCatalogReader"/> until M-06 exposes its published reader (T020). Fails
/// loudly (501) rather than fabricating KPI existence — so authoring a KPI question is refused until
/// M-06 wires the real adapter in the host; non-KPI authoring is unaffected.
/// </summary>
public sealed class UnavailableKpiCatalogReader : IKpiCatalogReader
{
    public Task<bool> KpiExistsAsync(string kpiCode, CancellationToken ct = default) =>
        throw new SurveyBuilderException("kpi.catalog_reader_unavailable", 501,
            "KPI catalogue validation (M-06) is not available yet.");

    public Task<IReadOnlyList<string>> ListPerspectivesAsync(string kpiCode, CancellationToken ct = default) =>
        throw new SurveyBuilderException("kpi.catalog_reader_unavailable", 501,
            "KPI catalogue validation (M-06) is not available yet.");
}
