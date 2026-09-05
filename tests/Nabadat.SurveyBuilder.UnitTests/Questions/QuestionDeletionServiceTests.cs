using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Questions;
using Nabadat.SurveyBuilder.Application.Questions.Dtos;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Application.Translations.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Questions;

/// <summary>
/// T130 [US3] — unit tests for <c>QuestionDeletionService</c>: deleting a question (FR-2.7) removes
/// every routing override that pointed at it so the next-in-order default reapplies, and (FR-2.8)
/// purges its translation keys across all locales — atomically inside
/// <c>ITenantDbContext.ExecuteAsync</c>.
/// <para>
/// Contract pinned for the implementer (T140):
/// <list type="bullet">
///   <item><c>QuestionDeletionService</c> lives in <c>Application/Questions/</c>.</item>
///   <item>ctor <c>(IQuestionStore questions, IRoutingMapStore routing, ITranslationStore translations,
///   ITenantDbContext context, TimeProvider timeProvider)</c>.</item>
///   <item><c>Task DeleteAsync(QuestionDeletionCommand command, CancellationToken ct = default)</c>
///   where <c>QuestionDeletionCommand(Guid QuestionId, Guid ActorId, Guid CorrelationId)</c>
///   (in <c>Application/Questions/Dtos/</c>).</item>
///   <item>Uses the existing <c>IRoutingMapStore.DeleteByTargetQuestionAsync</c> and the new
///   <c>ITranslationStore.PurgeQuestionKeysAsync</c> port.</item>
/// </list>
/// </para>
/// </summary>
public sealed class QuestionDeletionServiceTests
{
    private readonly IQuestionStore _questions = Substitute.For<IQuestionStore>();
    private readonly IRoutingMapStore _routing = Substitute.For<IRoutingMapStore>();
    private readonly ITranslationStore _translations = Substitute.For<ITranslationStore>();
    private readonly RecordingTenantDbContext _context = new();

    private readonly Guid _questionId = Guid.NewGuid();

    private QuestionDeletionService CreateService() =>
        new(_questions, _routing, _translations, _context, TestTime.Provider());

    private void SeedQuestion() =>
        _questions.GetAsync(_questionId, Arg.Any<CancellationToken>())
            .Returns(new Question { Id = _questionId, SurveyId = Guid.NewGuid(), SectionId = Guid.NewGuid() });

    private QuestionDeletionCommand Command() => new(_questionId, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task DeleteAsync_deletes_the_question()
    {
        SeedQuestion();

        await CreateService().DeleteAsync(Command());

        await _questions.Received(1).DeleteAsync(_questionId, Arg.Any<CancellationToken>());
        _context.ExecuteAsyncCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteAsync_resets_inbound_routing_targets_to_default()
    {
        SeedQuestion();

        await CreateService().DeleteAsync(Command());

        await _routing.Received(1).DeleteByTargetQuestionAsync(_questionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_purges_all_locale_translations_for_the_question()
    {
        SeedQuestion();

        await CreateService().DeleteAsync(Command());

        await _translations.Received(1).PurgeQuestionKeysAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(_questionId)), Arg.Any<CancellationToken>());
    }
}
