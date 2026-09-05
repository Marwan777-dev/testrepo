namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// The raw analytics aggregate for a survey, produced by the ES aggregator (T260) from the
/// <c>tenant_{id}_analytics</c> funnel index over the current and previous windows.
/// <see cref="AnalyticsService"/> turns it into the wire <c>AnalyticsView</c> via the funnel,
/// per-period-delta and channel-breakdown calculators.
/// </summary>
/// <param name="CurrentFunnel">Funnel stage counts for the selected period.</param>
/// <param name="PriorFunnel">
/// Funnel stage counts for the previous equal-length period, or <c>null</c> when the survey has no
/// prior-period data (new survey) — every headline delta is then suppressed (FR-14.5).
/// </param>
/// <param name="Channels">Per-channel current + prior counts (order preserved for display).</param>
/// <param name="Trend">Bucketed responses-trend counts for the selected period + granularity.</param>
public sealed record AnalyticsAggregate(
    FunnelCounts CurrentFunnel,
    FunnelCounts? PriorFunnel,
    IReadOnlyList<ChannelCounts> Channels,
    IReadOnlyList<TrendCounts> Trend)
{
    /// <summary>An empty aggregate — returned when nothing matches or ES is unavailable.</summary>
    public static readonly AnalyticsAggregate Empty = new(
        CurrentFunnel: new FunnelCounts(0, 0, 0, 0),
        PriorFunnel: null,
        Channels: Array.Empty<ChannelCounts>(),
        Trend: Array.Empty<TrendCounts>());
}
