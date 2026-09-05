namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// T257 [US9] — derives the per-channel breakdown (FR-14.4, acceptance scenario 5): for each channel,
/// its completion rate (Finished ÷ Sent, as a percentage rounded to 2 dp) and the deviation of that
/// rate vs the same channel in the previous period, in percentage points. Order is preserved. Pure —
/// no I/O, no clock. Unit-tested by <c>ChannelBreakdownCalculatorTests</c> (T252).
/// <para>Completion rate is <c>0</c> when the channel sent nothing. The delta is <b>suppressed</b>
/// (<c>null</c>) when there is no comparable prior period — <see cref="ChannelCounts.PriorSent"/> is
/// <c>null</c> or <c>0</c> (FR-14.5).</para>
/// </summary>
public sealed class ChannelBreakdownCalculator
{
    public IReadOnlyList<ChannelBreakdownResult> Compute(IReadOnlyList<ChannelCounts> channels) =>
        channels.Select(Map).ToList();

    private static ChannelBreakdownResult Map(ChannelCounts c)
    {
        var completionRate = Rate(c.Finished, c.Sent);

        // Suppressed unless there is a comparable prior period (prior sent > 0) — FR-14.5.
        decimal? delta = c.PriorSent is { } priorSent && priorSent > 0
            ? completionRate - Rate(c.PriorFinished ?? 0, priorSent)
            : null;

        return new ChannelBreakdownResult(c.Channel, c.Sent, completionRate, delta);
    }

    private static decimal Rate(long finished, long sent) =>
        sent == 0
            ? 0m
            : Math.Round((decimal)finished / sent * 100m, 2, MidpointRounding.AwayFromZero);
}
