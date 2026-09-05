namespace Nabadat.SurveyBuilder.Application.Report.Interfaces;

/// <summary>
/// T238 [US8] — the ES query port for the Survey Report (F13). Reads exclusively from Elasticsearch
/// (<c>tenant_{tenantId}_analytics</c> + <c>tenant_{tenantId}_responses</c>, AD-04); no PostgreSQL
/// query serves the report. Implemented by <c>ReportAggregator</c> (T239, Infrastructure). All
/// filtering — period range, data scope, per-question — is applied server-side before dispatch
/// (APIs-constitution Article 4.5).
/// </summary>
public interface IReportAggregator
{
    /// <summary>
    /// Aggregates the survey's in-window responses into metric cards, headline KPI raw values, and
    /// per-question aggregates. Returns <see cref="ReportAggregate.Empty"/> when nothing matches or
    /// ES is unavailable (the report degrades to an empty state rather than failing).
    /// </summary>
    Task<ReportAggregate> AggregateAsync(ReportAggregateQuery query, CancellationToken ct = default);

    /// <summary>
    /// Returns the newest-first verbatim sample for a single Text/Paragraph question (FR-13.7),
    /// capped at <see cref="VerbatimQuery.Limit"/>. Empty when nothing matches or ES is unavailable.
    /// </summary>
    Task<IReadOnlyList<VerbatimResponse>> GetVerbatimsAsync(VerbatimQuery query, CancellationToken ct = default);
}
