using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Sections;
using Nabadat.SurveyBuilder.Application.Sections.Dtos;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Sections;

/// <summary>
/// T127 [US3] — unit tests for <c>SectionValidator</c> (data-model.md §2.2): a section
/// <c>name</c> is required and capped at 200 chars; <c>description</c> is optional. Pure.
/// <para>
/// Contract pinned for the implementer (T137):
/// <list type="bullet">
///   <item><c>SectionValidator</c> lives in <c>Application/Sections/</c> and is pure:
///   <c>SectionValidationResult Validate(SectionDraft draft)</c>.</item>
///   <item><c>SectionDraft</c> (in <c>Application/Sections/Dtos/</c>) carries at least
///   <c>string? Name</c> and <c>string? Description</c> as <c>init</c> properties.</item>
///   <item><c>SectionValidationResult</c> exposes <c>bool IsValid</c> and
///   <c>IReadOnlyList&lt;string&gt; Errors</c> (API-05 codes: <c>section.name.required</c>
///   — the only code the contract pins, see contracts/sections-and-sets.md POST — and
///   <c>section.name.too_long</c> for the 1–200 char cap in data-model.md §2.2).</item>
/// </list>
/// </para>
/// </summary>
public sealed class SectionValidatorTests
{
    private readonly SectionValidator _validator = new();

    private static SectionDraft Draft(string? name, string? description = null) =>
        new() { Name = name, Description = description };

    [Fact]
    public void Validate_returns_invalid_when_the_name_is_empty()
    {
        var result = _validator.Validate(Draft(name: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("section.name.required");
    }

    [Fact]
    public void Validate_returns_invalid_when_the_name_is_whitespace()
    {
        var result = _validator.Validate(Draft(name: "   "));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("section.name.required");
    }

    [Fact]
    public void Validate_returns_invalid_when_the_name_exceeds_200_chars()
    {
        var result = _validator.Validate(Draft(name: new string('x', 201)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("section.name.too_long");
    }

    [Fact]
    public void Validate_accepts_a_name_at_the_200_char_boundary()
    {
        var result = _validator.Validate(Draft(name: new string('x', 200)));

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_a_valid_name_with_an_optional_description()
    {
        var result = _validator.Validate(Draft(name: "General", description: "Section for general questions"));

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_a_valid_name_with_a_null_description()
    {
        var result = _validator.Validate(Draft(name: "General", description: null));

        result.IsValid.Should().BeTrue();
    }
}
