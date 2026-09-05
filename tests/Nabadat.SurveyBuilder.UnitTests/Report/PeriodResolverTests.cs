using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Report;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Report;

/// <summary>
/// T228 [US8] — unit tests for <c>PeriodResolver</c> (FR-13.1). Turns a named report period into a
/// concrete <c>[From, To]</c> window anchored at the caller-supplied <c>now</c>: <c>To</c> is always
/// <c>now</c> and <c>From</c> is <c>now</c> minus the named window. The resolver is a pure function of
/// <c>(period, now)</c> — no <c>DateTime.UtcNow</c> read (CLAUDE.md Unit Test Policy rule 8); the
/// caller injects the clock.
/// <para>
/// Contract pinned for the implementer (T234):
/// <list type="bullet">
///   <item><c>PeriodResolver</c> lives in <c>Application/Report/</c> and is pure.</item>
///   <item><c>ResolvedPeriod Resolve(string period, DateTimeOffset now)</c> — accepts the wire values
///   <c>last_1_day</c> / <c>last_7_days</c> / <c>last_month</c> / <c>last_3_months</c> /
///   <c>last_6_months</c> / <c>last_9_months</c> / <c>last_year</c>.</item>
///   <item><c>ResolvedPeriod(DateTimeOffset From, DateTimeOffset To)</c> lives in
///   <c>Application/Report/</c>; day windows subtract calendar days, month/year windows subtract
///   calendar months/years.</item>
///   <item><c>custom</c> is NOT resolvable here (it needs explicit <c>from</c>/<c>to</c>, supplied at the
///   controller). An unknown period throws <c>ArgumentException</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class PeriodResolverTests
{
    private readonly PeriodResolver _resolver = new();

    // A fixed anchor so day/month/year subtraction is deterministic across a month/year boundary.
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Resolve_last_7_days_spans_the_seven_days_ending_now()
    {
        var period = _resolver.Resolve("last_7_days", Now);

        period.From.Should().Be(Now.AddDays(-7));
        period.To.Should().Be(Now);
    }

    [Theory]
    [InlineData("last_1_day", -1)]
    [InlineData("last_7_days", -7)]
    public void Resolve_day_windows_subtract_calendar_days(string period, int days)
    {
        var resolved = _resolver.Resolve(period, Now);

        resolved.From.Should().Be(Now.AddDays(days));
        resolved.To.Should().Be(Now);
    }

    [Theory]
    [InlineData("last_month", -1)]
    [InlineData("last_3_months", -3)]
    [InlineData("last_6_months", -6)]
    [InlineData("last_9_months", -9)]
    public void Resolve_month_windows_subtract_calendar_months(string period, int months)
    {
        var resolved = _resolver.Resolve(period, Now);

        resolved.From.Should().Be(Now.AddMonths(months));
        resolved.To.Should().Be(Now);
    }

    [Fact]
    public void Resolve_last_year_subtracts_one_calendar_year()
    {
        var resolved = _resolver.Resolve("last_year", Now);

        resolved.From.Should().Be(Now.AddYears(-1));
        resolved.To.Should().Be(Now);
    }

    [Fact]
    public void Resolve_throws_when_the_period_is_not_a_known_named_window()
    {
        var act = () => _resolver.Resolve("last_fortnight", Now);

        act.Should().Throw<ArgumentException>();
    }
}
