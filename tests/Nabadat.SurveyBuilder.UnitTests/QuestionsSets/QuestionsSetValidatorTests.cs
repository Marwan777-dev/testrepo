using FluentAssertions;
using Nabadat.SurveyBuilder.Application.QuestionsSets;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.QuestionsSets;

/// <summary>
/// T129 [US3] — unit tests for <c>QuestionsSetValidator</c> (data-model.md §2.3 invariants):
/// <c>count &gt;= 0 AND count &lt;= size(set)</c>, and a required 1–200-char title. An empty set
/// with <c>count = 0</c> is a valid (no-op) configuration. Pure.
/// <para>
/// Contract pinned for the implementer (T139):
/// <list type="bullet">
///   <item><c>QuestionsSetValidator</c> lives in <c>Application/QuestionsSets/</c> and is pure:
///   <c>QuestionsSetValidationResult Validate(QuestionsSetDraft draft)</c>.</item>
///   <item><c>QuestionsSetDraft</c> (in <c>Application/QuestionsSets/Dtos/</c>) carries at least
///   <c>string? Title</c>, <c>int Count</c>, and <c>int SetSize</c> (the current member count of the
///   set — the <c>Questions.Count</c> in the task fixture) as <c>init</c> properties.</item>
///   <item><c>QuestionsSetValidationResult</c> exposes <c>bool IsValid</c> and
///   <c>IReadOnlyList&lt;string&gt; Errors</c> (API-05 codes: <c>questionsset.count.exceeds_size</c>
///   from contracts/sections-and-sets.md, plus <c>questionsset.count.negative</c> and
///   <c>questionsset.title.required</c>).</item>
/// </list>
/// </para>
/// </summary>
public sealed class QuestionsSetValidatorTests
{
    private readonly QuestionsSetValidator _validator = new();

    private static QuestionsSetDraft Draft(int count, int setSize, string? title = "Rotating pool") =>
        new() { Title = title, Count = count, SetSize = setSize };

    [Fact]
    public void Validate_returns_invalid_when_count_exceeds_the_set_size()
    {
        var result = _validator.Validate(Draft(count: 6, setSize: 5));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("questionsset.count.exceeds_size");
    }

    [Fact]
    public void Validate_accepts_an_empty_set_with_a_zero_count()
    {
        var result = _validator.Validate(Draft(count: 0, setSize: 0));

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_a_count_within_the_set_size()
    {
        var result = _validator.Validate(Draft(count: 3, setSize: 5));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_accepts_a_count_equal_to_the_set_size()
    {
        var result = _validator.Validate(Draft(count: 5, setSize: 5));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_invalid_when_count_is_negative()
    {
        var result = _validator.Validate(Draft(count: -1, setSize: 5));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("questionsset.count.negative");
    }

    [Fact]
    public void Validate_returns_invalid_when_the_title_is_missing()
    {
        var result = _validator.Validate(Draft(count: 0, setSize: 0, title: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("questionsset.title.required");
    }
}
