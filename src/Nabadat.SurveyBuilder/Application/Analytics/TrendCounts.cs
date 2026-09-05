namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// Raw counts for one bucket of the responses-trend series (FR-14.4), produced by the ES aggregator
/// (T260) already bucketed at the requested granularity. <see cref="AnalyticsService"/> derives the
/// bucket's completion rate for the wire; only absolute counts are carried here.
/// </summary>
/// <param name="BucketStart">The inclusive start instant of this trend bucket.</param>
/// <param name="Sent">Sends within the bucket.</param>
/// <param name="Finished">Completed responses within the bucket.</param>
public sealed record TrendCounts(DateTimeOffset BucketStart, long Sent, long Finished);
