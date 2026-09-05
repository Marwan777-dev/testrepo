using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Nabadat.SurveyBuilder.Application.Analytics;
using Nabadat.SurveyBuilder.Application.Analytics.Interfaces;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Report;

namespace Nabadat.SurveyBuilder.Infrastructure.Elasticsearch;

/// <summary>
/// T260 [US9] — reads the Survey Analytics aggregate from Elasticsearch (AD-04): the
/// <c>tenant_{tenantId}_analytics</c> index holds one funnel document per survey / channel / bucket
/// with sent/opened/started/finished counts (written by M-04's ingest). A single bounded query spans
/// the previous window through the current window; the counts are split, summed, grouped by channel
/// and re-bucketed at the requested granularity in-process (correct at fixture/tenant scale;
/// TODO-M01-026 tracks moving to native ES date-histogram aggregations for large surveys).
/// <para>Read-only and resilient: a missing index, an unreachable cluster, or a query error resolves
/// to <see cref="AnalyticsAggregate.Empty"/>, so analytics degrades to an empty state rather than
/// 500-ing. Analytics is organisation-scoped, so no data-scope filter clause is applied.</para>
/// </summary>
public sealed class AnalyticsAggregator : IAnalyticsAggregator
{
    private const int MaxDocuments = 10_000;

    private readonly ElasticsearchClient _client;
    private readonly ICurrentTenant _tenant;

    public AnalyticsAggregator(ElasticsearchClient client, ICurrentTenant tenant)
    {
        _client = client;
        _tenant = tenant;
    }

    public async Task<AnalyticsAggregate> AggregateAsync(AnalyticsAggregateQuery query, CancellationToken ct = default)
    {
        var docs = await FetchAsync(query, ct);
        if (docs is null)
        {
            return AnalyticsAggregate.Empty;
        }

        // Split the combined result set into the current and previous windows by bucket start.
        var current = docs.Where(d => InWindow(d, query.Current)).ToList();
        var prior = docs.Where(d => InWindow(d, query.Prior)).ToList();

        var currentFunnel = SumFunnel(current);
        // No prior-period documents ⇒ new survey ⇒ deltas suppressed (FR-14.5).
        FunnelCounts? priorFunnel = prior.Count == 0 ? null : SumFunnel(prior);

        var channels = BuildChannels(current, prior, priorFunnel is not null);
        var trend = BuildTrend(current, query.Granularity);

        return new AnalyticsAggregate(currentFunnel, priorFunnel, channels, trend);
    }

    private async Task<IReadOnlyCollection<FunnelDocument>?> FetchAsync(
        AnalyticsAggregateQuery query, CancellationToken ct)
    {
        var index = $"tenant_{_tenant.TenantId:N}_analytics";
        var esQuery = new BoolQuery
        {
            Filter = new List<Query>
            {
                new TermQuery("survey_id", query.SurveyId.ToString()),
                new DateRangeQuery("bucket_start")
                {
                    Gte = query.Prior.From.UtcDateTime,
                    Lt = query.Current.To.UtcDateTime,
                },
            },
        };

        try
        {
            var response = await _client.SearchAsync<FunnelDocument>(
                s => s.Indices(index).Query(esQuery).Size(MaxDocuments), ct);

            return response.IsValidResponse ? response.Documents : null;
        }
        catch
        {
            // ES unavailable / index absent / auth failure — degrade to an empty aggregate.
            return null;
        }
    }

    private static bool InWindow(FunnelDocument doc, ResolvedPeriod window) =>
        doc.BucketStart >= window.From && doc.BucketStart < window.To;

    private static FunnelCounts SumFunnel(IEnumerable<FunnelDocument> docs)
    {
        long sent = 0, opened = 0, started = 0, finished = 0;
        foreach (var d in docs)
        {
            sent += d.Sent;
            opened += d.Opened;
            started += d.Started;
            finished += d.Finished;
        }

        return new FunnelCounts(sent, opened, started, finished);
    }

    private static IReadOnlyList<ChannelCounts> BuildChannels(
        List<FunnelDocument> current, List<FunnelDocument> prior, bool hasPrior)
    {
        var priorByChannel = prior
            .GroupBy(d => d.Channel, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, SumFunnel, StringComparer.Ordinal);

        return current
            .GroupBy(d => d.Channel, StringComparer.Ordinal)
            .Select(g =>
            {
                var c = SumFunnel(g);
                var hasPriorChannel = hasPrior && priorByChannel.TryGetValue(g.Key, out var p);
                var priorCounts = hasPriorChannel ? priorByChannel[g.Key] : null;
                return new ChannelCounts(
                    g.Key,
                    Sent: c.Sent,
                    Finished: c.Finished,
                    PriorSent: priorCounts?.Sent,
                    PriorFinished: priorCounts?.Finished);
            })
            .ToList();
    }

    private static IReadOnlyList<TrendCounts> BuildTrend(List<FunnelDocument> current, string granularity) =>
        current
            .GroupBy(d => BucketKey(d.BucketStart, granularity))
            .Select(g =>
            {
                var f = SumFunnel(g);
                return new TrendCounts(g.Key, f.Sent, f.Finished);
            })
            .OrderBy(t => t.BucketStart)
            .ToList();

    private static DateTimeOffset BucketKey(DateTimeOffset instant, string granularity)
    {
        var day = new DateTimeOffset(instant.Date, TimeSpan.Zero);
        return granularity switch
        {
            "weekly" => day.AddDays(-(int)day.DayOfWeek),           // week anchored on Sunday
            "monthly" => new DateTimeOffset(day.Year, day.Month, 1, 0, 0, 0, TimeSpan.Zero),
            _ => day,                                                // daily (default)
        };
    }

    /// <summary>Shape of a funnel document in the <c>tenant_{id}_analytics</c> index.</summary>
    private sealed class FunnelDocument
    {
        [JsonPropertyName("survey_id")]
        public string SurveyId { get; set; } = string.Empty;

        [JsonPropertyName("channel")]
        public string Channel { get; set; } = string.Empty;

        [JsonPropertyName("bucket_start")]
        public DateTimeOffset BucketStart { get; set; }

        [JsonPropertyName("sent")]
        public long Sent { get; set; }

        [JsonPropertyName("opened")]
        public long Opened { get; set; }

        [JsonPropertyName("started")]
        public long Started { get; set; }

        [JsonPropertyName("finished")]
        public long Finished { get; set; }
    }
}
