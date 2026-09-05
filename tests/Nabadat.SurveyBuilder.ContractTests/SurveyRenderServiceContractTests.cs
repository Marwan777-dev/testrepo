using FluentAssertions;
using Nabadat.SurveyBuilder.Application.QuestionsSets;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Interfaces;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.RenderPlan;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nabadat.SurveyBuilder.ContractTests;

/// <summary>
/// T160 [US3] — contract tests for the M-01 published <see cref="ISurveyRenderService"/> (AD-01), the
/// in-process seam M-02 (dispatch) and M-04 (response collection) consume. Pure-logic verification of
/// the value-type return shape those consumers bind to (no I/O — the store ports are substituted):
/// the <see cref="RenderPlan"/> echoes the survey id + layout, standalone questions surface as
/// <see cref="RenderQuestion"/> and Questions Sets as <see cref="RenderSetSample"/> (its pre-selected
/// subset), and the sparse routing overrides project into <c>question_id → answer_key →
/// <see cref="RoutingTarget"/></c> with the end-of-survey flag set when a target is null.
/// </summary>
public sealed class SurveyRenderServiceContractTests
{
    private readonly ISurveyStore _surveys = Substitute.For<ISurveyStore>();
    private readonly ISectionStore _sections = Substitute.For<ISectionStore>();
    private readonly IQuestionStore _questions = Substitute.For<IQuestionStore>();
    private readonly IQuestionsSetStore _sets = Substitute.For<IQuestionsSetStore>();
    private readonly IRoutingMapStore _routing = Substitute.For<IRoutingMapStore>();
    private readonly IResponseCountReader _responseCounts = Substitute.For<IResponseCountReader>();

    private ISurveyRenderService CreateService() =>
        new SurveyRenderService(
            _surveys, _sections, _questions, _sets, _routing, _responseCounts,
            new LowResponseOrderingService(), new SurveyDefinitionAssembler(_surveys));

    [Fact]
    public async Task GetRenderPlanAsync_returns_the_published_shape_M02_and_M04_consume()
    {
        var surveyId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var standaloneId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        _surveys.GetAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new Survey { Id = surveyId, Status = SurveyStatus.Active, Layout = LayoutMode.Section, ShuffleMode = "random" });
        _sections.GetBySurveyAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new[] { new Section { Id = sectionId, SurveyId = surveyId, Name = "S", Order = 0 } });
        _questions.GetBySurveyAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new Question { Id = standaloneId, SurveyId = surveyId, SectionId = sectionId, SetId = null, Order = 0 },
                new Question { Id = memberId, SurveyId = surveyId, SectionId = sectionId, SetId = setId, Order = 0 },
            });
        _sets.GetBySectionAsync(sectionId, Arg.Any<CancellationToken>())
            .Returns(new[] { new QuestionsSet { Id = setId, SectionId = sectionId, SelectionMode = QuestionsSetSelectionMode.Random, Count = 1, Order = 1 } });
        _responseCounts.GetResponseCountsAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, long>());
        _routing.GetBySurveyAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new[] { new RoutingMap { Id = Guid.NewGuid(), SurveyId = surveyId, SourceQuestionId = standaloneId, AnswerKey = "1", TargetQuestionId = null } });

        var plan = await CreateService().GetRenderPlanAsync(
            new SurveyId(surveyId), new RespondentContext(Guid.NewGuid(), new LocaleCode("en")), CancellationToken.None);

        plan.SurveyId.Value.Should().Be(surveyId);
        plan.Layout.Should().Be(LayoutMode.Section);
        plan.Sections.Should().ContainSingle();
        var section = plan.Sections[0];
        section.SectionId.Should().Be(sectionId);
        section.Items.OfType<RenderQuestion>().Should().ContainSingle(q => q.QuestionId == standaloneId);
        section.Items.OfType<RenderSetSample>().Should().ContainSingle(s => s.SetId == setId && s.QuestionIds.Contains(memberId));

        plan.RoutingMap.Should().ContainKey(standaloneId);
        var target = plan.RoutingMap[standaloneId]["1"];
        target.TargetQuestionId.Should().BeNull();
        target.EndsSurvey.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveSurveyDefinitionAsync_returns_the_definition_when_the_survey_is_active()
    {
        var surveyId = Guid.NewGuid();
        _surveys.GetAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new Survey
            {
                Id = surveyId,
                Status = SurveyStatus.Active,
                Layout = LayoutMode.Question,
                WelcomeHtml = "<p>Welcome</p>",
                ThanksHtml = "<p>Thanks</p>",
            });

        var definition = await CreateService().GetActiveSurveyDefinitionAsync(
            new SurveyId(surveyId), new LocaleCode("ar"), CancellationToken.None);

        definition.Should().NotBeNull();
        definition!.SurveyId.Value.Should().Be(surveyId);
        definition.Status.Should().Be(SurveyStatus.Active);
        definition.Locale.Value.Should().Be("ar");
        definition.Layout.Should().Be(LayoutMode.Question);
        definition.WelcomeHtml.Should().Be("<p>Welcome</p>");
        definition.ThanksHtml.Should().Be("<p>Thanks</p>");
    }

    [Fact]
    public async Task GetActiveSurveyDefinitionAsync_returns_null_when_the_survey_is_not_active()
    {
        var surveyId = Guid.NewGuid();
        _surveys.GetAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new Survey { Id = surveyId, Status = SurveyStatus.Draft, Layout = LayoutMode.Section });

        var definition = await CreateService().GetActiveSurveyDefinitionAsync(
            new SurveyId(surveyId), new LocaleCode("en"), CancellationToken.None);

        definition.Should().BeNull();
    }
}
