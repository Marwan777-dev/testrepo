using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Application.Sections;
using Nabadat.SurveyBuilder.Application.Sections.Dtos;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.SurveyBuilder.Application.Translations.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Sections;

/// <summary>
/// T128 [US3] — unit tests for <c>SectionDeletionGuard</c> + <c>SectionCascadeService</c>.
/// <para>FR-2.3: the last section CAN be deleted — no minimum-count invariant (the guard never
/// blocks on count). FR-2.5: deleting a <b>non-empty</b> section requires an explicit client
/// confirmation, else it is blocked with <c>section.delete.requires_confirmation</c>; an
/// already-empty section deletes without confirmation. On a confirmed cascade the service deletes
/// every child standalone question and Questions Set, then (FR-2.7) removes every routing override
/// pointing at a deleted question so the next-in-order default reapplies, and (FR-2.8) purges the
/// child questions' translation keys — all atomically inside <c>ITenantDbContext.ExecuteAsync</c>.</para>
/// <para>
/// Contract pinned for the implementer (T138):
/// <list type="bullet">
///   <item><c>SectionDeletionGuard</c> (pure): <c>bool CanDelete(int sectionCountInSurvey)</c> —
///   always <c>true</c> (documents FR-2.3, no minimum count).</item>
///   <item><c>SectionCascadeService</c> ctor <c>(ISectionStore sections, IQuestionStore questions,
///   IQuestionsSetStore sets, IRoutingMapStore routing, ITranslationStore translations,
///   ITenantDbContext context, TimeProvider timeProvider)</c>.</item>
///   <item><c>Task&lt;SectionCascadeResult&gt; DeleteAsync(SectionCascadeCommand command,
///   CancellationToken ct = default)</c> where
///   <c>SectionCascadeCommand(Guid SectionId, bool Confirmed, Guid ActorId, Guid CorrelationId)</c>.</item>
///   <item><c>SectionCascadeResult</c> exposes <c>bool Deleted</c> + <c>string? ErrorCode</c> with
///   factories <c>Success()</c> / <c>Blocked(string errorCode)</c>.</item>
///   <item>Uses the existing <c>IRoutingMapStore.DeleteByTargetQuestionAsync</c> (FR-2.7 reset) and
///   the new <c>IQuestionsSetStore.GetBySectionAsync</c> / <c>IQuestionStore.GetBySectionAsync</c>
///   / <c>ITranslationStore.PurgeQuestionKeysAsync</c> ports.</item>
/// </list>
/// </para>
/// </summary>
public sealed class SectionCascadeServiceTests
{
    private readonly ISectionStore _sections = Substitute.For<ISectionStore>();
    private readonly IQuestionStore _questions = Substitute.For<IQuestionStore>();
    private readonly IQuestionsSetStore _sets = Substitute.For<IQuestionsSetStore>();
    private readonly IRoutingMapStore _routing = Substitute.For<IRoutingMapStore>();
    private readonly ITranslationStore _translations = Substitute.For<ITranslationStore>();
    private readonly RecordingTenantDbContext _context = new();

    private readonly Guid _sectionId = Guid.NewGuid();
    private readonly Guid _surveyId = Guid.NewGuid();

    private SectionCascadeService CreateService() =>
        new(_sections, _questions, _sets, _routing, _translations, _context, TestTime.Provider());

    private void SeedSection() =>
        _sections.GetAsync(_sectionId, Arg.Any<CancellationToken>())
            .Returns(new Section { Id = _sectionId, SurveyId = _surveyId, Name = "General" });

    private SectionCascadeCommand Command(bool confirmed) =>
        new(_sectionId, confirmed, Guid.NewGuid(), Guid.NewGuid());

    [Theory]
    [InlineData(1)] // the last section is still deletable (FR-2.3)
    [InlineData(3)]
    public void CanDelete_never_blocks_on_section_count(int sectionCount)
    {
        var guard = new SectionDeletionGuard();

        guard.CanDelete(sectionCount).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_blocks_a_non_empty_section_without_confirmation()
    {
        SeedSection();
        _questions.GetBySectionAsync(_sectionId, Arg.Any<CancellationToken>())
            .Returns(new[] { new Question { Id = Guid.NewGuid(), SectionId = _sectionId } });

        var result = await CreateService().DeleteAsync(Command(confirmed: false));

        result.Deleted.Should().BeFalse();
        result.ErrorCode.Should().Be("section.delete.requires_confirmation");
        await _sections.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _questions.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_deletes_an_empty_section_without_confirmation()
    {
        SeedSection();
        _questions.GetBySectionAsync(_sectionId, Arg.Any<CancellationToken>()).Returns(Array.Empty<Question>());
        _sets.GetBySectionAsync(_sectionId, Arg.Any<CancellationToken>()).Returns(Array.Empty<QuestionsSet>());

        var result = await CreateService().DeleteAsync(Command(confirmed: false));

        result.Deleted.Should().BeTrue();
        await _sections.Received(1).DeleteAsync(_sectionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_cascades_all_child_questions_and_sets_when_confirmed()
    {
        SeedSection();
        var q1 = new Question { Id = Guid.NewGuid(), SectionId = _sectionId, SurveyId = _surveyId };
        var q2 = new Question { Id = Guid.NewGuid(), SectionId = _sectionId, SurveyId = _surveyId };
        var set = new QuestionsSet { Id = Guid.NewGuid(), SectionId = _sectionId };
        _questions.GetBySectionAsync(_sectionId, Arg.Any<CancellationToken>()).Returns(new[] { q1, q2 });
        _sets.GetBySectionAsync(_sectionId, Arg.Any<CancellationToken>()).Returns(new[] { set });

        var result = await CreateService().DeleteAsync(Command(confirmed: true));

        result.Deleted.Should().BeTrue();
        await _questions.Received(1).DeleteAsync(q1.Id, Arg.Any<CancellationToken>());
        await _questions.Received(1).DeleteAsync(q2.Id, Arg.Any<CancellationToken>());
        await _sets.Received(1).DeleteAsync(set.Id, Arg.Any<CancellationToken>());
        await _sections.Received(1).DeleteAsync(_sectionId, Arg.Any<CancellationToken>());
        _context.ExecuteAsyncCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteAsync_resets_inbound_routing_for_the_deleted_questions_when_confirmed()
    {
        SeedSection();
        var q1 = new Question { Id = Guid.NewGuid(), SectionId = _sectionId, SurveyId = _surveyId };
        _questions.GetBySectionAsync(_sectionId, Arg.Any<CancellationToken>()).Returns(new[] { q1 });
        _sets.GetBySectionAsync(_sectionId, Arg.Any<CancellationToken>()).Returns(Array.Empty<QuestionsSet>());

        await CreateService().DeleteAsync(Command(confirmed: true));

        await _routing.Received(1).DeleteByTargetQuestionAsync(q1.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_purges_translation_keys_for_the_deleted_questions_when_confirmed()
    {
        SeedSection();
        var q1 = new Question { Id = Guid.NewGuid(), SectionId = _sectionId, SurveyId = _surveyId };
        _questions.GetBySectionAsync(_sectionId, Arg.Any<CancellationToken>()).Returns(new[] { q1 });
        _sets.GetBySectionAsync(_sectionId, Arg.Any<CancellationToken>()).Returns(Array.Empty<QuestionsSet>());

        await CreateService().DeleteAsync(Command(confirmed: true));

        await _translations.Received(1).PurgeQuestionKeysAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(q1.Id)), Arg.Any<CancellationToken>());
    }
}
