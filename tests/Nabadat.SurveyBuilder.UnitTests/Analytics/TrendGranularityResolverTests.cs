using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Analytics;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Analytics;

/// <summary>
/// T253 [US9] — unit tests for <c>TrendGranularityResolver</c> (FR-14.1). The responses-trend chart offers a
/// daily / weekly / monthly granularity segment; the resolver picks the <b>default</b> granularity that suits a
/// named report period (the user may still override it in the UI). Short windows default to <c>daily</c>,
/// mid-length windows to <c>weekly</c>, long windows to <c>monthly</c>.
/// <para>
/// Contract pinned for the implementer (T258):
/// <list type="bullet">
///   <item><c>TrendGranularityResolver</c> lives in <c>Application/Analytics/</c> and is pure.</item>
///   <item><c>string Resolve(string period)</c> — accepts the same wire period values as the report's
///   <c>PeriodResolver</c>: <c>last_1_day</c> / <c>last_7_days</c> / <c>last_month</c> / <c>last_3_months</c> /
///   <c>last_6_months</c> / <c>last_9_months</c> / <c>last_year</c>.</item>
///   <item>Returns the wire granularity string <c>daily</c> / <c>weekly</c> / <c>monthly</c>.</item>
///   <item>An unknown / non-named period throws <c>ArgumentException</c> (mirrors <c>PeriodResolver</c>;
///   <c>custom</c> needs an explicit range and is resolved at the controller, not here).</item>
/// </list>
/// </para>
/// </summary>
public sealed class TrendGranularityResolverTests
{
    private readonly TrendGranularityResolver _resolver = new();

    [Theory]
    [InlineData("last_1_day", "daily")]
    [InlineData("last_7_days", "daily")]
    [InlineData("last_month", "daily")]
    [InlineData("last_3_months", "weekly")]
    [InlineData("last_6_months", "weekly")]
    [InlineData("last_9_months", "monthly")]
    [InlineData("last_year", "monthly")]
    public void Resolve_maps_each_named_period_to_its_default_granularity(string period, string expected)
    {
        _resolver.Resolve(period).Should().Be(expected);
    }

    [Fact]
    public void Resolve_throws_when_the_period_is_not_a_known_named_window()
    {
        var act = () => _resolver.Resolve("last_fortnight");

        act.Should().Throw<ArgumentException>();
    }
}
