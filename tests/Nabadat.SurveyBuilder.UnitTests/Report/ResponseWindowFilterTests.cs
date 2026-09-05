using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Report;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Report;

/// <summary>
/// T230 [US8] — unit tests for <c>ResponseWindowFilter</c> (FR-13.6, BR-3.1). The live report reflects
/// only responses collected within the survey's active period; a response submitted after the period
/// elapsed (<c>sentAt + activePeriod</c>) is excluded (it lives in the M-07 post-expiry store instead).
/// Pure predicate over timestamps — no clock read.
/// <para>
/// Contract pinned for the implementer (T236):
/// <list type="bullet">
///   <item><c>ResponseWindowFilter</c> lives in <c>Application/Report/</c> and is pure.</item>
///   <item><c>bool Include(DateTimeOffset submittedAt, DateTimeOffset sentAt, TimeSpan activePeriod)</c>
///   — <c>false</c> when <c>submittedAt &gt; sentAt + activePeriod</c>, otherwise <c>true</c>.</item>
///   <item>The expiry boundary is inclusive: a response submitted exactly at <c>sentAt + activePeriod</c>
///   is still in-period (<c>true</c>).</item>
/// </list>
/// </para>
/// </summary>
public sealed class ResponseWindowFilterTests
{
    private readonly ResponseWindowFilter _filter = new();

    private static readonly DateTimeOffset SentAt = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan ActivePeriod = TimeSpan.FromDays(3);

    [Fact]
    public void Include_returns_true_for_a_response_submitted_within_the_active_period()
    {
        var submittedAt = SentAt.AddDays(2); // day 2 of a 3-day window

        _filter.Include(submittedAt, SentAt, ActivePeriod).Should().BeTrue();
    }

    [Fact]
    public void Include_returns_false_for_a_response_submitted_after_the_active_period_elapsed()
    {
        // FR-13.6: one second past expiry → excluded from the live report.
        var submittedAt = SentAt + ActivePeriod + TimeSpan.FromSeconds(1);

        _filter.Include(submittedAt, SentAt, ActivePeriod).Should().BeFalse();
    }

    [Fact]
    public void Include_treats_the_expiry_boundary_as_inclusive()
    {
        var submittedAt = SentAt + ActivePeriod; // exactly at expiry

        _filter.Include(submittedAt, SentAt, ActivePeriod).Should().BeTrue();
    }

    [Fact]
    public void Include_returns_true_for_a_response_submitted_at_the_moment_the_survey_was_sent()
    {
        _filter.Include(SentAt, SentAt, ActivePeriod).Should().BeTrue();
    }
}
