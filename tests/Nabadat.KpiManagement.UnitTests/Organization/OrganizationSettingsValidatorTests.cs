using FluentAssertions;
using Nabadat.KpiManagement.Application.Organization;
using Nabadat.KpiManagement.Application.Organization.Dtos;
using Xunit;

namespace Nabadat.KpiManagement.UnitTests.Organization;

/// <summary>
/// T128 [US6] — unit tests for <c>OrganizationSettingsValidator</c> (FR-050 Name + Industry
/// validation), covering the spec.md US-6 Required cases.
/// <para>
/// Contract pinned for the implementer (T133):
/// <list type="bullet">
///   <item><c>OrganizationSettingsValidator : AbstractValidator&lt;OrganizationSettingsUpdate&gt;</c>
///   (FluentValidation), in <c>Application/Organization/</c>; constructed with an
///   <c>IIndustryEnumProvider</c> (the single source of truth for the canonical industry list).</item>
///   <item><c>OrganizationSettingsUpdate(string? Name, string? Industry)</c> in
///   <c>Application/Organization/Dtos/</c>.</item>
///   <item>Name null/empty/whitespace fails with <c>ErrorCode == "organization.name.required"</c>.</item>
///   <item>Name &gt; 150 chars fails with <c>ErrorCode == "organization.name.too_long"</c>.</item>
///   <item>Industry not in the canonical six fails with <c>ErrorCode == "organization.industry.unknown"</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class OrganizationSettingsValidatorTests
{
    private static readonly OrganizationSettingsValidator Validator = new(new IndustryEnumProvider());

    [Fact]
    public void Validate_returns_valid_when_name_and_industry_are_present_and_canonical()
    {
        var update = new OrganizationSettingsUpdate(Name: "Acme", Industry: "Banking");

        Validator.Validate(update).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_name_required_when_name_is_empty()
    {
        var update = new OrganizationSettingsUpdate(Name: "", Industry: "Banking");

        var result = Validator.Validate(update);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorCode).Should().Contain("organization.name.required");
    }

    [Fact]
    public void Validate_returns_name_too_long_when_name_exceeds_150_chars()
    {
        var update = new OrganizationSettingsUpdate(Name: new string('a', 151), Industry: "Banking");

        var result = Validator.Validate(update);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorCode).Should().Contain("organization.name.too_long");
    }

    [Fact]
    public void Validate_returns_industry_unknown_when_industry_is_not_in_canonical_list()
    {
        var update = new OrganizationSettingsUpdate(Name: "Acme", Industry: "Aerospace");

        var result = Validator.Validate(update);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorCode).Should().Contain("organization.industry.unknown");
    }
}
