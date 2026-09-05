using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T043 [US1] — unit tests for <c>RulesCountProjection</c>, the read-only projection that surfaces
/// the count of channel-send rules bound to a survey (used to decide whether Pause needs the
/// FR-1.10 confirmation).
/// <para>
/// Contract pinned for the implementer (T071):
/// <list type="bullet">
///   <item><c>RulesCountProjection</c> lives in <c>Application/Surveys/</c> and depends on the
///   M-02-published <c>IChannelSurveyRulesReader</c> (in <c>Application/Surveys/Interfaces/</c>):
///   <c>Task&lt;int&gt; GetRulesCountAsync(Guid surveyId, CancellationToken ct = default)</c>.</item>
///   <item><c>Task&lt;int&gt; ReadAsync(Guid surveyId, CancellationToken ct = default)</c> returns the
///   reader's count verbatim.</item>
///   <item><c>static bool RequiresPauseConfirmation(int rulesCount)</c> → true iff
///   <c>rulesCount &gt; 0</c> (FR-1.10).</item>
/// </list>
/// </para>
/// </summary>
public sealed class RulesCountProjectionTests
{
    private readonly IChannelSurveyRulesReader _reader = Substitute.For<IChannelSurveyRulesReader>();

    [Fact]
    public async Task ReadAsync_returns_the_count_reported_by_the_rules_reader()
    {
        var surveyId = Guid.NewGuid();
        _reader.GetRulesCountAsync(surveyId, Arg.Any<CancellationToken>()).Returns(3);
        var projection = new RulesCountProjection(_reader);

        var count = await projection.ReadAsync(surveyId);

        count.Should().Be(3);
    }

    [Fact]
    public async Task ReadAsync_returns_zero_when_no_rules_are_bound()
    {
        var surveyId = Guid.NewGuid();
        _reader.GetRulesCountAsync(surveyId, Arg.Any<CancellationToken>()).Returns(0);
        var projection = new RulesCountProjection(_reader);

        var count = await projection.ReadAsync(surveyId);

        count.Should().Be(0);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    public void RequiresPauseConfirmation_is_true_only_when_at_least_one_rule_is_bound(int rulesCount, bool expected)
    {
        RulesCountProjection.RequiresPauseConfirmation(rulesCount).Should().Be(expected);
    }
}
