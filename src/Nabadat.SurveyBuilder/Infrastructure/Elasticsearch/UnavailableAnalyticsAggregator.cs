using Nabadat.SurveyBuilder.Application.Analytics;
using Nabadat.SurveyBuilder.Application.Analytics.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.Elasticsearch;

/// <summary>
/// Default <see cref="IAnalyticsAggregator"/> for environments with no Elasticsearch configured
/// (dev / E2E): returns <see cref="AnalyticsAggregate.Empty"/>, so the Analytics screen renders its
/// empty state rather than failing. Registered via <c>TryAddScoped</c> and replaced by the real
/// <c>AnalyticsAggregator</c> when <c>Elasticsearch:Uri</c> is configured (see the DI extension and
/// TODO-M01-026).
/// </summary>
public sealed class UnavailableAnalyticsAggregator : IAnalyticsAggregator
{
    public Task<AnalyticsAggregate> AggregateAsync(AnalyticsAggregateQuery query, CancellationToken ct = default) =>
        Task.FromResult(AnalyticsAggregate.Empty);
}
