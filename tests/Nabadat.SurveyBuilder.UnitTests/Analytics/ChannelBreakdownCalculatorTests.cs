using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Analytics;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Analytics;

/// <summary>
/// T252 [US9] — unit tests for <c>ChannelBreakdownCalculator</c> (FR-14.4, acceptance scenario 5). The channel
/// breakdown shows, per delivery channel, its <b>Sent</b> count, its <b>completion rate</b> (Finished ÷ Sent),
/// and the <b>deviation</b> of that rate vs the same channel in the previous period of equal length.
/// <para>
/// Contract pinned for the implementer (T257):
/// <list type="bullet">
///   <item><c>ChannelBreakdownCalculator</c> lives in <c>Application/Analytics/</c> and is pure.</item>
///   <item><c>IReadOnlyList&lt;ChannelBreakdownResult&gt; Compute(IReadOnlyList&lt;ChannelCounts&gt; channels)</c>
///   — one result per input channel, order preserved.</item>
///   <item><c>ChannelCounts(string Channel, long Sent, long Finished, long? PriorSent, long? PriorFinished)</c>
///   — current + previous-period raw counts.</item>
///   <item><c>ChannelBreakdownResult(string Channel, long Sent, decimal CompletionRate, decimal? Delta)</c>.</item>
///   <item>Completion rate = <c>Finished ÷ Sent × 100</c>, 2 dp away from zero; <c>0m</c> when <c>Sent == 0</c>.</item>
///   <item>Delta is the rate deviation in <b>percentage points</b> (current rate − prior rate). It is
///   <b>suppressed</b> (<c>null</c>) when there is no comparable prior period: <c>PriorSent</c> is <c>null</c>
///   or <c>0</c> (FR-14.5).</item>
/// </list>
/// </para>
/// </summary>
public sealed class ChannelBreakdownCalculatorTests
{
    private readonly ChannelBreakdownCalculator _calculator = new();

    [Fact]
    public void Compute_reports_completion_rate_and_a_percentage_point_delta_per_channel()
    {
        // Email: 60/100 = 60% now vs 50/100 = 50% prior → +10 pp.
        var result = _calculator.Compute(new[]
        {
            new ChannelCounts("email", Sent: 100, Finished: 60, PriorSent: 100, PriorFinished: 50),
        });

        result.Should().ContainSingle();
        result[0].Channel.Should().Be("email");
        result[0].Sent.Should().Be(100);
        result[0].CompletionRate.Should().Be(60m);
        result[0].Delta.Should().Be(10m);
    }

    [Fact]
    public void Compute_suppresses_the_delta_when_the_channel_has_no_previous_period()
    {
        var result = _calculator.Compute(new[]
        {
            new ChannelCounts("whatsapp", Sent: 50, Finished: 20, PriorSent: null, PriorFinished: null),
        });

        result[0].CompletionRate.Should().Be(40m); // 20 / 50
        result[0].Delta.Should().BeNull();
    }

    [Fact]
    public void Compute_returns_a_zero_completion_rate_when_the_channel_sent_nothing()
    {
        var result = _calculator.Compute(new[]
        {
            new ChannelCounts("sms", Sent: 0, Finished: 0, PriorSent: 0, PriorFinished: 0),
        });

        result[0].CompletionRate.Should().Be(0m);
        result[0].Delta.Should().BeNull(); // prior sent is 0 → no comparable prior rate
    }

    [Fact]
    public void Compute_preserves_the_order_and_count_of_the_input_channels()
    {
        var result = _calculator.Compute(new[]
        {
            new ChannelCounts("email", Sent: 200, Finished: 130, PriorSent: 200, PriorFinished: 120),
            new ChannelCounts("whatsapp", Sent: 80, Finished: 20, PriorSent: null, PriorFinished: null),
            new ChannelCounts("web", Sent: 40, Finished: 30, PriorSent: 40, PriorFinished: 40),
        });

        result.Select(r => r.Channel).Should().ContainInOrder("email", "whatsapp", "web");
        result[0].CompletionRate.Should().Be(65m);   // 130 / 200
        result[2].CompletionRate.Should().Be(75m);   // 30 / 40
        result[2].Delta.Should().Be(-25m);            // 75% now − 100% prior (40/40) = −25 pp
    }
}
