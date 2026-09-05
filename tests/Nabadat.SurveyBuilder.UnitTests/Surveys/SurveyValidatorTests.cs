using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T039 [US1] — unit tests for <c>SurveyValidator</c> (F3 Settings validation + the journey↔type
/// invariant), covering the spec.md US1 Required cases for survey-draft validation.
/// <para>
/// Contract pinned for the implementer (T067):
/// <list type="bullet">
///   <item><c>SurveyValidator</c> lives in <c>Application/Surveys/</c> and is a pure, dependency-free
///   validator: <c>SurveyValidationResult Validate(SurveyDraft draft)</c>.</item>
///   <item><c>SurveyDraft</c> (in <c>Application/Surveys/Dtos/</c>) carries at least
///   <c>string? NameEn</c> and <c>Guid? BoundJourney</c> (the F3 settings fields; other fields are
///   irrelevant to these cases).</item>
///   <item><c>SurveyValidationResult</c> exposes <c>bool IsValid</c>, <c>IReadOnlyList&lt;string&gt; Errors</c>
///   (API-05 error codes), and the derived <c>SurveyType SurveyType</c> (BR-3.3 — a draft with no
///   bound journey is <see cref="SurveyType.SeasonalRelational"/>, otherwise
///   <see cref="SurveyType.Transactional"/>).</item>
///   <item><c>name_en</c> is required (<c>survey.name_en.required</c>) and capped at 200 chars
///   (<c>survey.name_en.max_length</c>).</item>
///   <item><see cref="SurveyType"/> is a new Domain value object with members
///   <c>Transactional</c> and <c>SeasonalRelational</c> (survey_type enum, data-model.md §2.1).</item>
/// </list>
/// </para>
/// </summary>
public sealed class SurveyValidatorTests
{
    private readonly SurveyValidator _validator = new();

    [Fact]
    public void Validate_returns_invalid_when_name_en_is_empty()
    {
        var result = _validator.Validate(new SurveyDraft { NameEn = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("survey.name_en.required");
    }

    [Fact]
    public void Validate_returns_valid_and_seasonal_type_when_no_journey_is_bound()
    {
        var result = _validator.Validate(new SurveyDraft { NameEn = "Post-visit", BoundJourney = null });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.SurveyType.Should().Be(SurveyType.SeasonalRelational);
    }

    [Fact]
    public void Validate_returns_transactional_type_when_a_journey_is_bound()
    {
        var result = _validator.Validate(new SurveyDraft { NameEn = "Post-visit", BoundJourney = Guid.NewGuid() });

        result.IsValid.Should().BeTrue();
        result.SurveyType.Should().Be(SurveyType.Transactional);
    }

    [Fact]
    public void Validate_returns_invalid_when_name_en_exceeds_max_length()
    {
        var result = _validator.Validate(new SurveyDraft { NameEn = new string('x', 201) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("survey.name_en.max_length");
    }
}
