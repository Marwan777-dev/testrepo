namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// T255 [US9] — derives the analytics funnel metrics (FR-14.2, spec.md § SC-007) from four absolute
/// stage counts: each stage as a <b>% of Sent</b> and each stage as a <b>stage-to-stage conversion</b>
/// against the preceding stage. Pure — no I/O, no clock. Unit-tested by <c>FunnelCalculatorTests</c>
/// (T250).
/// <para>Every ratio is a percentage rounded to 2 decimal places away from zero
/// (<c>120/130 → 92.31</c>). A zero denominator yields <c>0</c> — a survey with nothing sent has an
/// all-zero funnel rather than a divide-by-zero.</para>
/// </summary>
public sealed class FunnelCalculator
{
    public FunnelResult Compute(FunnelCounts counts) => new(
        OpenedPct: Percent(counts.Opened, counts.Sent),
        StartedPct: Percent(counts.Started, counts.Sent),
        FinishedPct: Percent(counts.Finished, counts.Sent),
        OpenedToSent: Percent(counts.Opened, counts.Sent),
        StartedToOpened: Percent(counts.Started, counts.Opened),
        FinishedToStarted: Percent(counts.Finished, counts.Started));

    private static decimal Percent(long numerator, long denominator) =>
        denominator == 0
            ? 0m
            : Math.Round((decimal)numerator / denominator * 100m, 2, MidpointRounding.AwayFromZero);
}
