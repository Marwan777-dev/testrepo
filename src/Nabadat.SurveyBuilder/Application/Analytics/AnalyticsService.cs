using Nabadat.SurveyBuilder.Application.Analytics.Interfaces;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Report;

namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// T261 [US9] — the Survey Analytics entry point (F14). Resolves the requested period + granularity,
/// asks the ES aggregator for the current + previous-period funnel/channel/trend counts, and composes
/// the pure calculators (<see cref="FunnelCalculator"/>, <see cref="PeriodDeltaCalculator"/>,
/// <see cref="ChannelBreakdownCalculator"/>) into an <see cref="AnalyticsResult"/>. Reuses the
/// report's <see cref="PeriodResolver"/> so both surfaces resolve named periods identically; the
/// clock is injected (CLAUDE.md Unit Test Policy rule 8).
/// <para>The previous window is the equal-length span immediately preceding the current one
/// (FR-14.3). When the aggregator reports no prior-period data, every delta is suppressed
/// (FR-14.5).</para>
/// </summary>
public sealed class AnalyticsService
{
    private static readonly string[] ValidGranularities = ["daily", "weekly", "monthly"];

    private readonly IAnalyticsAggregator _aggregator;
    private readonly PeriodResolver _periodResolver;
    private readonly FunnelCalculator _funnel;
    private readonly PeriodDeltaCalculator _delta;
    private readonly ChannelBreakdownCalculator _channels;
    private readonly TrendGranularityResolver _granularityResolver;
    private readonly TimeProvider _clock;

    public AnalyticsService(
        IAnalyticsAggregator aggregator,
        PeriodResolver periodResolver,
        FunnelCalculator funnel,
        PeriodDeltaCalculator delta,
        ChannelBreakdownCalculator channels,
        TrendGranularityResolver granularityResolver,
        TimeProvider clock)
    {
        _aggregator = aggregator;
        _periodResolver = periodResolver;
        _funnel = funnel;
        _delta = delta;
        _channels = channels;
        _granularityResolver = granularityResolver;
        _clock = clock;
    }

    public async Task<AnalyticsResult> GetAsync(
        Guid surveyId,
        string? period,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? granularity,
        CancellationToken ct = default)
    {
        var resolvedPeriod = string.IsNullOrWhiteSpace(period) ? "last_7_days" : period;

        var current = ResolveWindow(resolvedPeriod, from, to);
        var granularityValue = ResolveGranularity(resolvedPeriod, granularity);

        // Previous period of equal length, immediately preceding the current window (FR-14.3).
        var length = current.To - current.From;
        var prior = new ResolvedPeriod(current.From - length, current.From);

        var aggregate = await _aggregator.AggregateAsync(
            new AnalyticsAggregateQuery(surveyId, current, prior, granularityValue), ct);

        var currentFunnel = _funnel.Compute(aggregate.CurrentFunnel);
        var priorFunnel = aggregate.PriorFunnel is null ? null : _funnel.Compute(aggregate.PriorFunnel);
        decimal? priorSent = aggregate.PriorFunnel?.Sent;

        return new AnalyticsResult(
            Period: current,
            Granularity: granularityValue,
            Counts: aggregate.CurrentFunnel,
            Funnel: currentFunnel,
            SentDeltaPct: _delta.Delta(aggregate.CurrentFunnel.Sent, priorSent, DeltaKind.Count),
            OpenedDeltaPp: _delta.Delta(currentFunnel.OpenedPct, priorFunnel?.OpenedPct, DeltaKind.Rate),
            StartedDeltaPp: _delta.Delta(currentFunnel.StartedPct, priorFunnel?.StartedPct, DeltaKind.Rate),
            FinishedDeltaPp: _delta.Delta(currentFunnel.FinishedPct, priorFunnel?.FinishedPct, DeltaKind.Rate),
            OverallCompletionDeltaPp: _delta.Delta(currentFunnel.FinishedPct, priorFunnel?.FinishedPct, DeltaKind.Rate),
            Channels: _channels.Compute(aggregate.Channels),
            Trend: aggregate.Trend);
    }

    private ResolvedPeriod ResolveWindow(string period, DateTimeOffset? from, DateTimeOffset? to)
    {
        if (period == "custom")
        {
            if (from is null || to is null || to <= from)
            {
                throw new SurveyBuilderException(
                    "analytics.period.invalid", 400,
                    "A custom analytics period requires valid 'from' and 'to' timestamps.");
            }

            return new ResolvedPeriod(from.Value, to.Value);
        }

        try
        {
            return _periodResolver.Resolve(period, _clock.GetUtcNow());
        }
        catch (ArgumentException)
        {
            throw new SurveyBuilderException(
                "analytics.period.invalid", 400, $"Unknown analytics period '{period}'.");
        }
    }

    private string ResolveGranularity(string period, string? granularity)
    {
        if (string.IsNullOrWhiteSpace(granularity))
        {
            // Custom ranges default to daily; named periods use the resolver's default.
            return period == "custom" ? "daily" : _granularityResolver.Resolve(period);
        }

        if (Array.IndexOf(ValidGranularities, granularity) < 0)
        {
            throw new SurveyBuilderException(
                "analytics.granularity.invalid", 400,
                $"Unknown analytics granularity '{granularity}' (expected daily, weekly or monthly).");
        }

        return granularity;
    }
}
