using Nabadat.SurveyBuilder.Application.Report;

namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// The composed analytics result produced by <see cref="AnalyticsService"/> (T261) — the
/// Application-layer output the Api's <c>AnalyticsView</c> maps to the wire (keeping Application free
/// of any Api dependency, as with <c>PreviewPayloadBuilder</c>). All percentages are on the 0–100
/// scale; every delta is <c>null</c> when no previous period exists (FR-14.5).
/// </summary>
/// <param name="Period">The resolved current window.</param>
/// <param name="Granularity">The resolved trend granularity (<c>daily</c>/<c>weekly</c>/<c>monthly</c>).</param>
/// <param name="Counts">The current-period absolute funnel counts (stage counts).</param>
/// <param name="Funnel">The current-period derived funnel percentages and conversions.</param>
/// <param name="SentDeltaPct">Percent-change delta of Sent vs the prior period, or <c>null</c>.</param>
/// <param name="OpenedDeltaPp">Percentage-point delta of Opened's % of Sent, or <c>null</c>.</param>
/// <param name="StartedDeltaPp">Percentage-point delta of Started's % of Sent, or <c>null</c>.</param>
/// <param name="FinishedDeltaPp">Percentage-point delta of Finished's % of Sent, or <c>null</c>.</param>
/// <param name="OverallCompletionDeltaPp">Percentage-point delta of the overall completion rate.</param>
/// <param name="Channels">The per-channel breakdown.</param>
/// <param name="Trend">The bucketed responses-trend counts.</param>
public sealed record AnalyticsResult(
    ResolvedPeriod Period,
    string Granularity,
    FunnelCounts Counts,
    FunnelResult Funnel,
    decimal? SentDeltaPct,
    decimal? OpenedDeltaPp,
    decimal? StartedDeltaPp,
    decimal? FinishedDeltaPp,
    decimal? OverallCompletionDeltaPp,
    IReadOnlyList<ChannelBreakdownResult> Channels,
    IReadOnlyList<TrendCounts> Trend);
