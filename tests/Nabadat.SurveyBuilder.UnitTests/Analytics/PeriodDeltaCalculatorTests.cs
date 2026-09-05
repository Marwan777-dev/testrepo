using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Analytics;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Analytics;

/// <summary>
/// T251 [US9] — unit tests for <c>PeriodDeltaCalculator</c> (FR-14.3, FR-14.5). Every headline analytics
/// number carries a deviation vs the previous period of equal length. The <b>unit</b> of that deviation
/// depends on what the number is:
/// <list type="bullet">
///   <item>a <b>rate</b> (a percentage such as a completion rate) deviates in <b>percentage points</b> —
///   the arithmetic difference <c>current − prior</c>.</item>
///   <item>a <b>count</b> (an absolute total such as sends or responses) deviates in <b>percent change</b> —
///   <c>(current − prior) / prior × 100</c>.</item>
/// </list>
/// <para>
/// Contract pinned for the implementer (T256):
/// <list type="bullet">
///   <item><c>PeriodDeltaCalculator</c> lives in <c>Application/Analytics/</c> and is pure.</item>
///   <item><c>decimal? Delta(decimal current, decimal? prior, DeltaKind kind)</c>.</item>
///   <item><c>enum DeltaKind { Rate, Count }</c> in <c>Application/Analytics/</c>.</item>
///   <item>Percent-change results round to 2 decimal places, away from zero; percentage-point results are the
///   exact difference.</item>
///   <item><b>Suppression (FR-14.5):</b> a <c>null</c> prior (new survey — no previous period) returns
///   <c>null</c>. A <c>Count</c> delta against a zero prior also returns <c>null</c> — a percent change from a
///   zero base is undefined and must be suppressed, not shown as a misleading number.</item>
/// </list>
/// </para>
/// </summary>
public sealed class PeriodDeltaCalculatorTests
{
    private readonly PeriodDeltaCalculator _calculator = new();

    [Fact]
    public void Delta_of_a_rate_is_the_difference_in_percentage_points()
    {
        // Overall completion rate 50% → 60% is +10 pp (spec Independent Test).
        _calculator.Delta(current: 60m, prior: 50m, kind: DeltaKind.Rate).Should().Be(10m);
    }

    [Fact]
    public void Delta_of_a_count_is_the_percent_change()
    {
        // 100 sends → 200 sends is a +100% change.
        _calculator.Delta(current: 200m, prior: 100m, kind: DeltaKind.Count).Should().Be(100m);
    }

    [Fact]
    public void Delta_is_negative_when_a_rate_declines()
    {
        _calculator.Delta(current: 40m, prior: 50m, kind: DeltaKind.Rate).Should().Be(-10m);
    }

    [Fact]
    public void Delta_rounds_a_percent_change_to_two_decimal_places_away_from_zero()
    {
        // (130 − 120) / 120 × 100 = 8.333… → 8.33.
        _calculator.Delta(current: 130m, prior: 120m, kind: DeltaKind.Count).Should().Be(8.33m);
    }

    [Theory]
    [InlineData(nameof(DeltaKind.Rate))]
    [InlineData(nameof(DeltaKind.Count))]
    public void Delta_is_suppressed_when_there_is_no_previous_period(string kindName)
    {
        var kind = Enum.Parse<DeltaKind>(kindName);

        _calculator.Delta(current: 42m, prior: null, kind: kind).Should().BeNull();
    }

    [Fact]
    public void Delta_of_a_count_is_suppressed_when_the_prior_is_zero()
    {
        // Percent change from a zero base is undefined — suppress rather than mislead.
        _calculator.Delta(current: 50m, prior: 0m, kind: DeltaKind.Count).Should().BeNull();
    }

    [Fact]
    public void Delta_of_a_rate_against_a_zero_prior_is_the_current_value_in_points()
    {
        // A rate deviation is a plain subtraction — no division — so a zero prior is fine.
        _calculator.Delta(current: 50m, prior: 0m, kind: DeltaKind.Rate).Should().Be(50m);
    }
}
