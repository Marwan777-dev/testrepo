using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Routing;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Routing;

/// <summary>
/// T166 [US4] — unit tests for <c>RoutingDefaultTargeter</c> (research.md §6). Every answer with no
/// explicit route falls through to the default target: the next question in survey order. Defaults
/// are computed, <b>never persisted</b> — only overrides are written to <c>routing_maps</c>, so a
/// reorder recomputes defaults transparently. The last question's default is end-of-survey (null).
/// <para>
/// Contract pinned for the implementer (T174):
/// <list type="bullet">
///   <item><c>RoutingDefaultTargeter</c> lives in <c>Application/Routing/</c> and is pure:
///   <c>Guid? Default(Question question, Question? nextInOrder)</c>.</item>
///   <item>Returns <c>nextInOrder.Id</c> when a next question exists; <c>null</c> (⇒ end-of-survey)
///   when <paramref name="nextInOrder"/> is null.</item>
/// </list>
/// </para>
/// </summary>
public sealed class RoutingDefaultTargeterTests
{
    private readonly RoutingDefaultTargeter _targeter = new();

    private static Question QuestionAt(int order) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = QuestionType.YesNo,
            Subtype = QuestionSubType.None,
            Order = order,
        };

    [Fact]
    public void Default_targets_the_next_question_in_order()
    {
        var question = QuestionAt(0);
        var next = QuestionAt(1);

        _targeter.Default(question, next).Should().Be(next.Id);
    }

    [Fact]
    public void Default_targets_end_of_survey_when_there_is_no_next_question()
    {
        var last = QuestionAt(4);

        _targeter.Default(last, nextInOrder: null).Should().BeNull();
    }
}
