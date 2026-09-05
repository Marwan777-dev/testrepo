namespace Nabadat.SurveyBuilder.Application.Analytics.Interfaces;

/// <summary>
/// T259 [US9] — the ES query port for Survey Analytics (F14). Reads exclusively from Elasticsearch
/// (<c>tenant_{tenantId}_analytics</c>, AD-04); no PostgreSQL query serves analytics. Implemented by
/// <c>AnalyticsAggregator</c> (T260, Infrastructure). Analytics is organisation-scoped
/// (contracts/report-and-analytics.md § GET /analytics), so no per-region/branch scope filter is
/// applied.
/// </summary>
public interface IAnalyticsAggregator
{
    /// <summary>
    /// Aggregates the survey's funnel counts (current + previous window), per-channel counts and the
    /// bucketed trend series. Returns <see cref="AnalyticsAggregate.Empty"/> when nothing matches or
    /// ES is unavailable, so analytics degrades to an empty state rather than failing.
    /// </summary>
    Task<AnalyticsAggregate> AggregateAsync(AnalyticsAggregateQuery query, CancellationToken ct = default);
}
