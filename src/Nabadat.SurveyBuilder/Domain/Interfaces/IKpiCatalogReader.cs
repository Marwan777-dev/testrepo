namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// Cross-module port M-01 consumes from <b>M-06 (KPI Management)</b> to validate a KPI question's
/// <c>kpi_code</c> and list its perspectives (research.md §4.2, data-model.md §4). Published-interface
/// only.
/// <para><b>Declared here per T020;</b> the concrete implementation is supplied by M-06 and wired in
/// the host composition root.</para>
/// </summary>
public interface IKpiCatalogReader
{
    /// <summary>Returns <c>true</c> when <paramref name="kpiCode"/> is an active KPI in the tenant catalogue.</summary>
    Task<bool> KpiExistsAsync(string kpiCode, CancellationToken ct = default);

    /// <summary>The perspectives available for <paramref name="kpiCode"/> (empty when none).</summary>
    Task<IReadOnlyList<string>> ListPerspectivesAsync(string kpiCode, CancellationToken ct = default);
}
