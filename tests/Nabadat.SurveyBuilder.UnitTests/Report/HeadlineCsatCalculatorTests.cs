using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Report;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Report;

/// <summary>
/// T227 [US8] — unit tests for <c>HeadlineCsatCalculator</c> (FR-13.2, spec.md § SC-007). The headline
/// CSAT gauge is the arithmetic mean of the survey's contributing CSAT question values; with no
/// contributing questions there is nothing to average, so the result is <c>null</c> (the gauge renders
/// empty rather than <c>0</c>).
/// <para>
/// Contract pinned for the implementer (T233):
/// <list type="bullet">
///   <item><c>HeadlineCsatCalculator</c> lives in <c>Application/Report/</c> and is pure.</item>
///   <item><c>decimal? Compute(IReadOnlyList&lt;decimal&gt; questionValues)</c> — the arithmetic mean of
///   <paramref name="questionValues"/>, or <c>null</c> when the list is empty.</item>
///   <item>The mean is exact (no premature rounding): <c>[81, 76] → 78.5</c> per SC-007's 0.01 tolerance.</item>
/// </list>
/// </para>
/// </summary>
public sealed class HeadlineCsatCalculatorTests
{
    private readonly HeadlineCsatCalculator _calculator = new();

    [Fact]
    public void Compute_returns_the_average_when_the_survey_has_contributing_csat_questions()
    {
        // Two CSAT questions scoring 81% and 76% → headline 78.5% (FR-13.2, acceptance scenario 3).
        _calculator.Compute(new[] { 81m, 76m }).Should().Be(78.5m);
    }

    [Fact]
    public void Compute_returns_null_when_there_are_no_contributing_questions()
    {
        _calculator.Compute(Array.Empty<decimal>()).Should().BeNull();
    }

    [Fact]
    public void Compute_returns_the_single_value_when_only_one_question_contributes()
    {
        _calculator.Compute(new[] { 90m }).Should().Be(90m);
    }

    [Fact]
    public void Compute_averages_three_values_exactly()
    {
        // (70 + 80 + 90) / 3 = 80 — no rounding drift.
        _calculator.Compute(new[] { 70m, 80m, 90m }).Should().Be(80m);
    }
}
