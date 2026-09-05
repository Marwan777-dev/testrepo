using FluentAssertions;
using Nabadat.IntegrationHub.Application.Parameters;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Parameters;

/// <summary>
/// T044 [US2] — unit tests for <c>ApiFieldNameUniquenessValidator</c>: VR-F06 / BR-11. An API field name is
/// required, <c>snake_case</c>, and unique per tenant <b>across built-in + custom + enabled + disabled</b> — a
/// disabled parameter still reserves its field name permanently (spec.md Edge Cases: "disabling never frees the
/// API field name for a different purpose").
///
/// <para>Contract these tests pin for the implementer (T052):
/// <list type="bullet">
///   <item><c>ApiFieldNameUniquenessValidator</c> in <c>Application/Parameters/</c> with
///   <c>ParameterValidationResult Validate(IEnumerable&lt;string&gt;? existingApiFields, string? apiField)</c>.</item>
///   <item><b>Pure</b>, mirroring <c>ChannelIdUniquenessValidator</c>: the caller (<c>ParameterService</c>, T057)
///   supplies the tenant's existing field names — <b>every</b> row, disabled and built-in included, and already
///   excluding the row being edited. That is how VR-F06's "including disabled" clause is expressed: the
///   validator never queries, so it cannot accidentally filter on <c>enabled</c>. A caller that passes only the
///   enabled subset is the defect, and T065's endpoint test pins the full-list behaviour end-to-end.</item>
///   <item>Format is validated here too (not just uniqueness): the value must satisfy the baseline's
///   <c>ck_parameters_api_field_format</c> CHECK so a bad field name surfaces as an inline console error rather
///   than a database exception.</item>
///   <item>Comparison is <b>case-insensitive</b> even though the format CHECK already forces lower case —
///   otherwise a client sending <c>Wait_Time</c> would slip past the collision check and then fail the CHECK.</item>
/// </list></para>
/// </summary>
public sealed class ApiFieldNameUniquenessValidatorTests
{
    private static readonly ApiFieldNameUniquenessValidator Validator = new();

    [Fact]
    public void Validate_returns_invalid_duplicate_api_field_when_the_name_is_already_in_use()
    {
        // The normative spec.md required case:
        // Validate(existingFields=["wait_time"], field="wait_time", includeDisabled=true)
        //   → Invalid("This API field name is already in use").
        // "includeDisabled" is the CALLER's contract — the list below is the tenant's full catalogue.
        var result = Validator.Validate(new[] { "wait_time" }, "wait_time");

        result.IsValid.Should().BeFalse();
        result.HasCode(ParameterErrorCodes.DuplicateApiField).Should().BeTrue();
        result.Messages.Should().Contain("This API field name is already in use");
    }

    [Fact]
    public void Validate_returns_invalid_duplicate_api_field_when_a_disabled_parameter_holds_the_name()
    {
        // VR-F06's "including disabled" clause: a disabled custom parameter still reserves its field name.
        // The caller passes disabled rows in the same list, so the collision is found identically.
        Validator.Validate(new[] { "legacy_wait_time", "wait_time" }, "wait_time")
            .HasCode(ParameterErrorCodes.DuplicateApiField).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_invalid_duplicate_api_field_when_a_built_in_holds_the_name()
    {
        // BR-09/BR-23: the 23 seeded built-ins are in the same namespace — "branch" is taken from day one.
        Validator.Validate(new[] { "branch", "region" }, "branch")
            .HasCode(ParameterErrorCodes.DuplicateApiField).Should().BeTrue();
    }

    [Fact]
    public void Validate_matches_an_existing_name_case_insensitively()
    {
        Validator.Validate(new[] { "wait_time" }, "WAIT_TIME")
            .HasCode(ParameterErrorCodes.DuplicateApiField).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_returns_invalid_api_field_required_when_the_value_is_missing(string? apiField)
    {
        var result = Validator.Validate(new[] { "branch" }, apiField);

        result.IsValid.Should().BeFalse();
        result.HasCode(ParameterErrorCodes.ApiFieldRequired).Should().BeTrue();
        result.Messages.Should().Contain("API field name is required");
    }

    [Theory]
    [InlineData("Wait Time")]   // whitespace
    [InlineData("wait-time")]   // hyphen — snake_case only
    [InlineData("waitTime")]    // camelCase
    [InlineData("2nd_visit")]   // leading digit
    [InlineData("_wait_time")]  // leading underscore
    [InlineData("wait.time")]   // dot
    public void Validate_returns_invalid_api_field_format_when_the_value_is_not_snake_case(string apiField)
    {
        var result = Validator.Validate(existingApiFields: null, apiField);

        result.IsValid.Should().BeFalse();
        result.HasCode(ParameterErrorCodes.ApiFieldFormat).Should().BeTrue();
    }

    [Theory]
    [InlineData("wait_time")]
    [InlineData("branch")]
    [InlineData("queue_2_wait")]
    [InlineData("a")]
    public void Validate_returns_valid_for_a_well_formed_unused_snake_case_name(string apiField)
    {
        Validator.Validate(new[] { "branch_x", "region" }, apiField).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_valid_when_the_existing_field_list_is_null_or_empty()
    {
        Validator.Validate(existingApiFields: null, "wait_time").IsValid.Should().BeTrue();
        Validator.Validate(Array.Empty<string>(), "wait_time").IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_reports_the_api_field_wire_name_so_the_console_can_render_the_error_inline()
    {
        var result = Validator.Validate(new[] { "wait_time" }, "wait_time");

        result.Errors.Should().OnlyContain(e => e.Field == ParameterFields.ApiField);
    }
}
