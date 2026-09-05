using FluentAssertions;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Scenarios;

/// <summary>
/// T159 [US3] — scenario for the FR-10.4 Set → Section → Survey low-response cascade, end-to-end
/// against real Postgres + Elasticsearch. Walks the spec's Independent Test flow: a survey with three
/// sections, each carrying a <c>low_response</c> Questions Set that delivers one question per
/// respondent; response counts are seeded per member; a render plan is requested; and the final
/// state-of-the-world is asserted — the survey-wide least-answered section is served first, AND within
/// the winning section its set samples that section's least-answered member.
///
/// <para>Driven through the published <see cref="ISurveyRenderService"/> (AD-01 seam) — the HTTP
/// <c>render-plan</c> route is still the US1 stub (T150 pending); see
/// <see cref="RenderPlanApplicationFactory"/> / TODO-M01-019.</para>
/// </summary>
[Collection("render-plan")]
public sealed class QuestionsSetLowResponseOrderingScenarioTests
{
    private readonly RenderPlanApplicationFactory _factory;

    public QuestionsSetLowResponseOrderingScenarioTests(RenderPlanApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Survey_wide_lowest_response_section_is_served_first_and_its_set_samples_the_least_answered()
    {
        // Arrange — 3 sections, each a low_response Set delivering 1 of its 2 members.
        var surveyId = await _factory.SeedActiveSurveyAsync();

        var (section1, _, section1Winner) = await SeedSectionAsync(surveyId, order: 0, lowCount: 7, highCount: 15);
        var (section2, _, section2Winner) = await SeedSectionAsync(surveyId, order: 1, lowCount: 4, highCount: 10);
        var (section3, _, _) = await SeedSectionAsync(surveyId, order: 2, lowCount: 12, highCount: 20);

        // Act — request the render plan.
        var plan = await _factory.InScopeAsync(render => render.GetRenderPlanAsync(
            new SurveyId(surveyId), new RespondentContext(Guid.NewGuid(), new LocaleCode("en")), CancellationToken.None));

        // Assert — survey-wide-lowest (section2, min = 4) is first; each section keeps its single set.
        plan.Sections.Select(s => s.SectionId).Should().Equal(section2, section1, section3);

        // The winning section's set sampled its least-answered member (the low-count question), not the high one.
        var winningSample = plan.Sections[0].Items.OfType<RenderSetSample>().Single();
        winningSample.QuestionIds.Should().Equal(section2Winner);

        // The next section (section1) likewise sampled its least-answered member.
        var runnerUpSample = plan.Sections[1].Items.OfType<RenderSetSample>().Single();
        runnerUpSample.QuestionIds.Should().Equal(section1Winner);
    }

    /// <summary>
    /// Seeds a section with one <c>low_response</c> set of <c>count = 1</c> over two members: one
    /// least-answered (<paramref name="lowCount"/>) and one more-answered (<paramref name="highCount"/>).
    /// Returns the section id, set id, and the id of the least-answered member (the one a low-response
    /// sample must pick).
    /// </summary>
    private async Task<(Guid SectionId, Guid SetId, Guid Winner)> SeedSectionAsync(
        Guid surveyId, int order, long lowCount, long highCount)
    {
        var sectionId = await _factory.SeedSectionAsync(surveyId, order);
        var setId = await _factory.SeedSetAsync(sectionId, "low_response", count: 1, order: 0);

        var winner = await _factory.SeedQuestionAsync(surveyId, sectionId, setId, order: 0);
        await _factory.SeedResponseCountAsync(winner, lowCount);
        var loser = await _factory.SeedQuestionAsync(surveyId, sectionId, setId, order: 1);
        await _factory.SeedResponseCountAsync(loser, highCount);

        return (sectionId, setId, winner);
    }
}
