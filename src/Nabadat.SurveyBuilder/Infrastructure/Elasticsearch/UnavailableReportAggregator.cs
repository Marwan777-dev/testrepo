using Nabadat.SurveyBuilder.Application.Report;
using Nabadat.SurveyBuilder.Application.Report.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.Elasticsearch;

/// <summary>
/// Degraded <see cref="IReportAggregator"/> registered when no Elasticsearch cluster is configured
/// (<c>Elasticsearch:Uri</c> absent — dev / E2E). Mirrors the module's other "Unavailable*" port
/// stubs: it lets the report endpoints compose and return a well-formed <b>empty</b> report instead
/// of failing DI resolution. Swapped for <see cref="ReportAggregator"/> automatically once a cluster
/// is configured (TODO-M01-023).
/// </summary>
public sealed class UnavailableReportAggregator : IReportAggregator
{
    public Task<ReportAggregate> AggregateAsync(ReportAggregateQuery query, CancellationToken ct = default) =>
        Task.FromResult(ReportAggregate.Empty);

    public Task<IReadOnlyList<VerbatimResponse>> GetVerbatimsAsync(VerbatimQuery query, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VerbatimResponse>>(Array.Empty<VerbatimResponse>());
}
