using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Questions;
using Nabadat.SurveyBuilder.Application.Questions.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Questions;

/// <summary>
/// T045 [US1] — unit tests for <c>QuestionValidator</c> (per-type + sub-type invariants from the
/// authoritative Question Type Catalogue, FR-8.8). Types that offer display variants (Scale,
/// Input Field, Single Select, Matrix) require a sub-type; the variant-less types (Multi-select,
/// Yes/No, Ranking, KPI) validate with <see cref="QuestionSubType.None"/>. A Scale/Slider needs a
/// positive step count.
/// <para>
/// Contract pinned for the implementer (T075):
/// <list type="bullet">
///   <item><c>QuestionValidator</c> lives in <c>Application/Questions/</c> and is pure:
///   <c>QuestionValidationResult Validate(QuestionDraft draft)</c>.</item>
///   <item><c>QuestionDraft</c> (in <c>Application/Questions/Dtos/</c>) carries at least
///   <c>string? Text</c>, <c>QuestionType Type</c>, <c>QuestionSubType? SubType</c>, and
///   <c>int? SliderSteps</c>.</item>
///   <item><c>QuestionValidationResult</c> exposes <c>bool IsValid</c> and
///   <c>IReadOnlyList&lt;string&gt; Errors</c> (API-05 codes: <c>question.subtype.required</c>,
///   <c>scale.slider.steps.min</c>, …).</item>
/// </list>
/// </para>
/// </summary>
public sealed class QuestionValidatorTests
{
    private readonly QuestionValidator _validator = new();

    private static QuestionDraft Draft(QuestionType type, QuestionSubType? subType, int? sliderSteps = null) =>
        new() { Text = "Question", Type = type, SubType = subType, SliderSteps = sliderSteps };

    [Fact]
    public void Validate_returns_invalid_when_a_scale_has_no_subtype()
    {
        var result = _validator.Validate(Draft(QuestionType.Scale, subType: null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("question.subtype.required");
    }

    [Fact]
    public void Validate_returns_invalid_when_a_slider_scale_has_zero_steps()
    {
        var result = _validator.Validate(Draft(QuestionType.Scale, QuestionSubType.Slider, sliderSteps: 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("scale.slider.steps.min");
    }

    [Theory]
    // One positive case per type from the catalogue (variant-less types take None).
    [InlineData(QuestionType.Scale, QuestionSubType.Labels)]
    [InlineData(QuestionType.Scale, QuestionSubType.Stars)]
    [InlineData(QuestionType.InputField, QuestionSubType.Text)]
    [InlineData(QuestionType.InputField, QuestionSubType.Paragraph)]
    [InlineData(QuestionType.SingleSelect, QuestionSubType.List)]
    [InlineData(QuestionType.SingleSelect, QuestionSubType.Dropdown)]
    [InlineData(QuestionType.MultiSelect, QuestionSubType.None)]
    [InlineData(QuestionType.YesNo, QuestionSubType.None)]
    [InlineData(QuestionType.Matrix, QuestionSubType.CustomColumns)]
    [InlineData(QuestionType.Matrix, QuestionSubType.KpiScale)]
    [InlineData(QuestionType.Ranking, QuestionSubType.None)]
    [InlineData(QuestionType.Kpi, QuestionSubType.None)]
    public void Validate_accepts_a_valid_type_and_subtype_pairing(QuestionType type, QuestionSubType subType)
    {
        var steps = subType == QuestionSubType.Slider ? (int?)5 : null;

        var result = _validator.Validate(Draft(type, subType, steps));

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_a_slider_scale_with_a_positive_step_count()
    {
        var result = _validator.Validate(Draft(QuestionType.Scale, QuestionSubType.Slider, sliderSteps: 5));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    // Types that require a display sub-type reject the missing sub-type (FR-8.8).
    [InlineData(QuestionType.InputField)]
    [InlineData(QuestionType.SingleSelect)]
    [InlineData(QuestionType.Matrix)]
    public void Validate_returns_invalid_when_a_variant_type_has_no_subtype(QuestionType type)
    {
        var result = _validator.Validate(Draft(type, subType: null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("question.subtype.required");
    }

    [Fact]
    public void Validate_returns_invalid_when_the_question_text_is_missing()
    {
        var result = _validator.Validate(new QuestionDraft
        {
            Text = "",
            Type = QuestionType.YesNo,
            SubType = QuestionSubType.None,
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("question.text.required");
    }
}
