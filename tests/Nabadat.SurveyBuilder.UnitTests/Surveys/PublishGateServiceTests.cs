using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T042 [US1] — unit tests for <c>PublishGateService</c>, the BR-1.7 (Q9) content gate: a survey may
/// enter Active only when it has ≥1 section AND ≥1 question. Reactivating a Paused survey is NOT
/// gated (Pause does not remove content).
/// <para>
/// Contract pinned for the implementer (T070):
/// <list type="bullet">
///   <item><c>PublishGateService</c> lives in <c>Application/Surveys/</c>. The service composes
///   <c>ISurveyStore</c> to source the counts (T070); the pure gate under test here takes the counts
///   directly so the rule is dependency-free.</item>
///   <item><c>PublishGateResult EnsureContent(SurveyContentCounts counts, SurveyStatus current,
///   SurveyStatus target)</c> where <c>SurveyContentCounts(int SectionsCount, int QuestionsCount)</c>.</item>
///   <item><c>PublishGateResult</c> exposes <c>bool Gated</c> (false when the transition is not a
///   gated entry into Active), <c>bool IsSatisfied</c>, <c>string? ErrorCode</c>
///   (<c>publish.requires_content</c> when unsatisfied), <c>bool MissingSections</c>, and
///   <c>bool MissingQuestions</c>.</item>
///   <item>Only Draft → Active and PendingReview → Active are gated; Paused → Active (Reactivate)
///   is ungated (<c>Gated == false</c>, <c>IsSatisfied == true</c>).</item>
/// </list>
/// </para>
/// </summary>
public sealed class PublishGateServiceTests
{
    private readonly PublishGateService _service = new();

    [Fact]
    public void EnsureContent_rejects_publish_when_the_survey_has_no_sections()
    {
        var result = _service.EnsureContent(new SurveyContentCounts(SectionsCount: 0, QuestionsCount: 0),
            SurveyStatus.Draft, SurveyStatus.Active);

        result.IsSatisfied.Should().BeFalse();
        result.ErrorCode.Should().Be("publish.requires_content");
        result.MissingSections.Should().BeTrue();
    }

    [Fact]
    public void EnsureContent_rejects_publish_when_the_survey_has_a_section_but_no_questions()
    {
        var result = _service.EnsureContent(new SurveyContentCounts(SectionsCount: 1, QuestionsCount: 0),
            SurveyStatus.Draft, SurveyStatus.Active);

        result.IsSatisfied.Should().BeFalse();
        result.ErrorCode.Should().Be("publish.requires_content");
        result.MissingQuestions.Should().BeTrue();
    }

    [Fact]
    public void EnsureContent_allows_publish_when_the_survey_has_a_section_and_a_question()
    {
        var result = _service.EnsureContent(new SurveyContentCounts(SectionsCount: 1, QuestionsCount: 1),
            SurveyStatus.Draft, SurveyStatus.Active);

        result.IsSatisfied.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void EnsureContent_skips_the_gate_when_reactivating_a_paused_survey()
    {
        // Paused → Active is not content-gated even with zero content counts (Pause keeps content).
        var result = _service.EnsureContent(new SurveyContentCounts(SectionsCount: 0, QuestionsCount: 0),
            SurveyStatus.Paused, SurveyStatus.Active);

        result.Gated.Should().BeFalse();
        result.IsSatisfied.Should().BeTrue();
    }
}
