using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Analytics;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Analytics;

/// <summary>
/// T250 [US9] — unit tests for <c>FunnelCalculator</c> (FR-14.2, spec.md § SC-007). The analytics funnel
/// turns four absolute stage counts (Sent → Opened → Started → Finished) into the two derived families the
/// UI renders: each stage as a <b>% of Sent</b>, and each stage as a <b>stage-to-stage conversion</b> against
/// the preceding stage.
/// <para>
/// Contract pinned for the implementer (T255):
/// <list type="bullet">
///   <item><c>FunnelCalculator</c> lives in <c>Application/Analytics/</c> and is pure (no I/O, no clock).</item>
///   <item><c>FunnelResult Compute(FunnelCounts counts)</c>.</item>
///   <item><c>FunnelCounts(long Sent, long Opened, long Started, long Finished)</c> — the raw ES aggregation.</item>
///   <item><c>FunnelResult(decimal OpenedPct, decimal StartedPct, decimal FinishedPct, decimal OpenedToSent,
///   decimal StartedToOpened, decimal FinishedToStarted)</c>.</item>
///   <item>Every ratio is a percentage rounded to <b>2 decimal places, away from zero</b>
///   (<c>120/130 → 92.31</c>). <c>%-of-Sent</c> divides by Sent; a stage-to-stage conversion divides by the
///   preceding stage's count.</item>
///   <item>A zero denominator yields <c>0m</c> (never a divide-by-zero) — a survey with nothing sent has an
///   all-zero funnel, not an error.</item>
/// </list>
/// </para>
/// </summary>
public sealed class FunnelCalculatorTests
{
    private readonly FunnelCalculator _calculator = new();

    [Fact]
    public void Compute_derives_percent_of_sent_and_stage_to_stage_conversions()
    {
        // Spec § SC-007 pinned fixture: 200 → 160 → 130 → 120.
        var result = _calculator.Compute(new FunnelCounts(Sent: 200, Opened: 160, Started: 130, Finished: 120));

        // % of Sent
        result.OpenedPct.Should().Be(80m);    // 160 / 200
        result.StartedPct.Should().Be(65m);   // 130 / 200
        result.FinishedPct.Should().Be(60m);  // 120 / 200

        // stage-to-stage conversion
        result.OpenedToSent.Should().Be(80m);        // 160 / 200
        result.StartedToOpened.Should().Be(81.25m);  // 130 / 160
        result.FinishedToStarted.Should().Be(92.31m); // 120 / 130 = 92.3076… → 92.31 (2 dp, away from zero)
    }

    [Fact]
    public void Compute_rounds_conversions_to_two_decimal_places_away_from_zero()
    {
        // 2 / 3 = 66.666… → 66.67; 1 / 3 = 33.333… → 33.33.
        var result = _calculator.Compute(new FunnelCounts(Sent: 3, Opened: 3, Started: 2, Finished: 1));

        result.StartedToOpened.Should().Be(66.67m);   // 2 / 3
        result.FinishedToStarted.Should().Be(50m);     // 1 / 2
        result.StartedPct.Should().Be(66.67m);         // 2 / 3
        result.FinishedPct.Should().Be(33.33m);        // 1 / 3
    }

    [Fact]
    public void Compute_returns_an_all_zero_funnel_when_nothing_was_sent()
    {
        var result = _calculator.Compute(new FunnelCounts(Sent: 0, Opened: 0, Started: 0, Finished: 0));

        result.OpenedPct.Should().Be(0m);
        result.StartedPct.Should().Be(0m);
        result.FinishedPct.Should().Be(0m);
        result.OpenedToSent.Should().Be(0m);
        result.StartedToOpened.Should().Be(0m);
        result.FinishedToStarted.Should().Be(0m);
    }

    [Fact]
    public void Compute_guards_each_conversion_against_its_own_zero_denominator()
    {
        // Sent > 0 but Opened = 0 (and therefore Started/Finished = 0): OpenedToSent computes,
        // but StartedToOpened and FinishedToStarted must not divide by their zero predecessor.
        var result = _calculator.Compute(new FunnelCounts(Sent: 100, Opened: 0, Started: 0, Finished: 0));

        result.OpenedToSent.Should().Be(0m);          // 0 / 100
        result.StartedToOpened.Should().Be(0m);        // guarded (Opened == 0)
        result.FinishedToStarted.Should().Be(0m);      // guarded (Started == 0)
    }
}
