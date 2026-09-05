using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Questions;
using Nabadat.SurveyBuilder.Application.Questions.Dtos;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Questions;

/// <summary>
/// T132 [US3] — unit tests for <c>QuestionMoveService</c> (drag-and-drop across sections/sets).
/// A move persists all three placement fields (<c>section_id</c>, <c>set_id</c>, <c>order</c>) via
/// <c>IQuestionStore.MoveAsync</c> inside <c>ITenantDbContext.ExecuteAsync</c>. A move that lands the
/// question <b>inside a set</b> also removes any pre-existing routing for that question — as both a
/// source AND a target — because set questions cannot be routing sources or targets (FR-9.5); a move
/// to a standalone position leaves routing untouched.
/// <para>
/// Contract pinned for the implementer (T142):
/// <list type="bullet">
///   <item><c>QuestionMoveService</c> lives in <c>Application/Questions/</c>.</item>
///   <item>ctor <c>(IQuestionStore questions, IRoutingMapStore routing, ITenantDbContext context,
///   TimeProvider timeProvider)</c>.</item>
///   <item><c>Task MoveAsync(MoveQuestionCommand command, CancellationToken ct = default)</c> where
///   <c>MoveQuestionCommand(Guid QuestionId, Guid TargetSectionId, Guid? TargetSetId, int TargetOrder,
///   Guid ActorId, Guid CorrelationId)</c> (in <c>Application/Questions/Dtos/</c>).</item>
///   <item>Set-move routing cleanup uses the existing
///   <c>IRoutingMapStore.DeleteBySourceQuestionAsync</c> + <c>DeleteByTargetQuestionAsync</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class QuestionMoveServiceTests
{
    private readonly IQuestionStore _questions = Substitute.For<IQuestionStore>();
    private readonly IRoutingMapStore _routing = Substitute.For<IRoutingMapStore>();
    private readonly RecordingTenantDbContext _context = new();

    private readonly Guid _questionId = Guid.NewGuid();
    private readonly Guid _targetSectionId = Guid.NewGuid();

    private QuestionMoveService CreateService() =>
        new(_questions, _routing, _context, TestTime.Provider());

    private void SeedQuestion() =>
        _questions.GetAsync(_questionId, Arg.Any<CancellationToken>())
            .Returns(new Question { Id = _questionId, SurveyId = Guid.NewGuid(), SectionId = Guid.NewGuid(), SetId = null });

    private MoveQuestionCommand Command(Guid? targetSetId, int targetOrder) =>
        new(_questionId, _targetSectionId, targetSetId, targetOrder, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task MoveAsync_persists_section_set_and_order()
    {
        SeedQuestion();
        var targetSetId = Guid.NewGuid();

        await CreateService().MoveAsync(Command(targetSetId, targetOrder: 2));

        await _questions.Received(1).MoveAsync(_questionId, _targetSectionId, targetSetId, 2, Arg.Any<CancellationToken>());
        _context.ExecuteAsyncCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MoveAsync_removes_routing_when_the_question_lands_inside_a_set()
    {
        SeedQuestion();
        var targetSetId = Guid.NewGuid();

        await CreateService().MoveAsync(Command(targetSetId, targetOrder: 1));

        await _routing.Received(1).DeleteBySourceQuestionAsync(_questionId, Arg.Any<CancellationToken>());
        await _routing.Received(1).DeleteByTargetQuestionAsync(_questionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveAsync_leaves_routing_intact_when_the_question_lands_as_standalone()
    {
        SeedQuestion();

        await CreateService().MoveAsync(Command(targetSetId: null, targetOrder: 3));

        await _routing.DidNotReceive().DeleteBySourceQuestionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _routing.DidNotReceive().DeleteByTargetQuestionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
