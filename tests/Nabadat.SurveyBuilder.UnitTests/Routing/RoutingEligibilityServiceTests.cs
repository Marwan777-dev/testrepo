using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Routing;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Routing;

/// <summary>
/// T163 [US4] — unit tests for <c>RoutingEligibilityService</c> (FR-9.5). A question may act as a
/// routing source/target only when it is one of the eligible types — Single Select, Scale (but
/// <b>not</b> the Slider sub-type), Yes/No, KPI — <b>and</b> is standalone (not inside a Questions
/// Set). Multi-select, Input Field, Matrix and Ranking are never eligible.
/// <para>
/// Contract pinned for the implementer (T171):
/// <list type="bullet">
///   <item><c>RoutingEligibilityService</c> lives in <c>Application/Routing/</c> and is pure:
///   <c>bool IsEligible(Question question)</c>.</item>
///   <item>Eligibility reads only <see cref="Question.Type"/>, <see cref="Question.Subtype"/> and
///   <see cref="Question.SetId"/> (<c>SetId != null ⇒ inside a set ⇒ ineligible</c>); it delegates
///   to <see cref="QuestionRoutingRules.IsRoutingEligible"/>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class RoutingEligibilityServiceTests
{
    private readonly RoutingEligibilityService _service = new();

    private static Question Question(QuestionType type, QuestionSubType subType = QuestionSubType.None, bool inSet = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Subtype = subType,
            SetId = inSet ? Guid.NewGuid() : null,
        };

    [Fact]
    public void IsEligible_returns_false_when_the_question_is_multi_select()
    {
        _service.IsEligible(Question(QuestionType.MultiSelect)).Should().BeFalse();
    }

    [Fact]
    public void IsEligible_returns_true_when_the_question_is_a_scale()
    {
        _service.IsEligible(Question(QuestionType.Scale, QuestionSubType.Labels)).Should().BeTrue();
    }

    [Fact]
    public void IsEligible_returns_false_when_the_scale_is_a_slider()
    {
        _service.IsEligible(Question(QuestionType.Scale, QuestionSubType.Slider)).Should().BeFalse();
    }

    [Fact]
    public void IsEligible_returns_false_when_a_single_select_is_inside_a_set()
    {
        _service.IsEligible(Question(QuestionType.SingleSelect, QuestionSubType.List, inSet: true)).Should().BeFalse();
    }

    [Theory]
    // The remaining eligible standalone types (FR-9.5).
    [InlineData(QuestionType.SingleSelect)]
    [InlineData(QuestionType.YesNo)]
    [InlineData(QuestionType.Kpi)]
    public void IsEligible_returns_true_for_an_eligible_standalone_type(QuestionType type)
    {
        _service.IsEligible(Question(type)).Should().BeTrue();
    }
}
