using System.Text.Json.Serialization;
using Nabadat.SurveyBuilder.Application.Analytics;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// T263 [US9] — the Survey Analytics payload on the wire (F14, contracts/report-and-analytics.md
/// § GET /analytics): the resolved period + granularity, the Sent → Opened → Started → Finished
/// funnel, the headline overall completion rate, the per-channel breakdown and the responses-trend
/// series. Every deviation is <c>null</c> (not <c>0</c>) when no previous period exists (FR-14.5).
/// Built by <c>AnalyticsService</c> (T261) from the calculators; read-only, no ETag.
/// </summary>
public sealed record AnalyticsView(
    [property: JsonPropertyName("period")] AnalyticsPeriodView Period,
    [property: JsonPropertyName("funnel")] AnalyticsFunnelView Funnel,
    [property: JsonPropertyName("overall_completion_rate")] OverallCompletionRateView OverallCompletionRate,
    [property: JsonPropertyName("channels")] IReadOnlyList<ChannelBreakdown> Channels,
    [property: JsonPropertyName("trend")] IReadOnlyList<TrendBucket> Trend)
{
    /// <summary>
    /// Maps the Application-layer <see cref="AnalyticsResult"/> onto the wire. Funnel and overall
    /// completion stay on the 0–100 percentage scale; per-channel and per-bucket completion rates are
    /// emitted as ratios in <c>[0,1]</c> (rounded to 4 dp) per the contract.
    /// </summary>
    public static AnalyticsView From(AnalyticsResult r) => new(
        Period: new AnalyticsPeriodView(r.Period.From, r.Period.To, r.Granularity),
        Funnel: new AnalyticsFunnelView(
            Sent: new FunnelStage(r.Counts.Sent, PctOfSent: null, DeltaPct: r.SentDeltaPct, DeltaPp: null, ConversionFromPrevStagePct: null),
            Opened: new FunnelStage(r.Counts.Opened, r.Funnel.OpenedPct, DeltaPct: null, r.OpenedDeltaPp, r.Funnel.OpenedToSent),
            Started: new FunnelStage(r.Counts.Started, r.Funnel.StartedPct, DeltaPct: null, r.StartedDeltaPp, r.Funnel.StartedToOpened),
            Finished: new FunnelStage(r.Counts.Finished, r.Funnel.FinishedPct, DeltaPct: null, r.FinishedDeltaPp, r.Funnel.FinishedToStarted)),
        OverallCompletionRate: new OverallCompletionRateView(r.Funnel.FinishedPct, r.OverallCompletionDeltaPp),
        Channels: r.Channels
            .Select(c => new ChannelBreakdown(c.Channel, c.Sent, Ratio(c.CompletionRate), c.Delta))
            .ToList(),
        Trend: r.Trend
            .Select(t => new TrendBucket(t.BucketStart, t.Sent, t.Finished, CompletionRatio(t.Finished, t.Sent)))
            .ToList());

    /// <summary>A 0–100 percentage as a <c>[0,1]</c> ratio, 4 dp.</summary>
    private static decimal Ratio(decimal percentage) => Math.Round(percentage / 100m, 4);

    /// <summary>Finished ÷ Sent as a <c>[0,1]</c> ratio (4 dp); <c>0</c> when nothing was sent.</summary>
    private static decimal CompletionRatio(long finished, long sent) =>
        sent == 0 ? 0m : Math.Round((decimal)finished / sent, 4);
}
