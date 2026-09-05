using FluentAssertions;
using Nabadat.IntegrationHub.Application.Parameters;
using Nabadat.IntegrationHub.Domain.ValueObjects;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Parameters;

/// <summary>
/// T046 [US2] — unit tests for <c>RangeConfigValidator</c>: VR-F07 / FR-S6-03. A Range parameter's Minimum and
/// Maximum are both required and Minimum must be strictly less than Maximum; the Unit label is optional.
///
/// <para>Contract these tests pin for the implementer (T054):
/// <list type="bullet">
///   <item><c>RangeConfigValidator</c> in <c>Application/Parameters/</c> with
///   <c>ParameterValidationResult Validate(DataType dataType, decimal? min, decimal? max, string? unit = null)</c>
///   — pure, no database access.</item>
///   <item>The type is an input because the rule is <b>conditional</b>: the Range card only exists for
///   <c>DataType.Range</c> (AC-S6-01), so min/max are required only there — and, symmetrically, a non-Range
///   parameter must NOT carry range configuration, matching the baseline's
///   <c>ck_parameters_range_only_for_range</c> CHECK. Without that second half, a client switching Range → List
///   without clearing the card would hit a database exception instead of an inline error.</item>
///   <item>Failures <b>accumulate</b> (both bounds missing reports both), consistent with every other M-13
///   validator.</item>
///   <item>The min-&lt;-max message is asserted verbatim — "Minimum must be less than Maximum" is normative
///   shipped copy from spec.md AC (US2 scenario 7).</item>
/// </list></para>
/// </summary>
public sealed class RangeConfigValidatorTests
{
    private static readonly RangeConfigValidator Validator = new();

    [Fact]
    public void Validate_returns_invalid_range_min_max_when_minimum_is_greater_than_maximum()
    {
        // The normative spec.md required case: Validate(min=100, max=50) → Invalid("Minimum must be less than Maximum").
        var result = Validator.Validate(DataType.Range, min: 100m, max: 50m);

        result.IsValid.Should().BeFalse();
        result.HasCode(ParameterErrorCodes.RangeMinMax).Should().BeTrue();
        result.Messages.Should().Contain("Minimum must be less than Maximum");
    }

    [Fact]
    public void Validate_returns_invalid_range_min_max_when_the_bounds_are_equal()
    {
        // VR-F07 is strict: "Minimum < Maximum", so an empty range is rejected too.
        Validator.Validate(DataType.Range, min: 50m, max: 50m)
            .HasCode(ParameterErrorCodes.RangeMinMax).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_invalid_range_min_required_when_the_minimum_is_absent()
    {
        var result = Validator.Validate(DataType.Range, min: null, max: 100m);

        result.IsValid.Should().BeFalse();
        result.HasCode(ParameterErrorCodes.RangeMinRequired).Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == ParameterFields.RangeMin);
    }

    [Fact]
    public void Validate_returns_invalid_range_max_required_when_the_maximum_is_absent()
    {
        var result = Validator.Validate(DataType.Range, min: 0m, max: null);

        result.IsValid.Should().BeFalse();
        result.HasCode(ParameterErrorCodes.RangeMaxRequired).Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == ParameterFields.RangeMax);
    }

    [Fact]
    public void Validate_accumulates_both_failures_when_neither_bound_is_supplied()
    {
        var result = Validator.Validate(DataType.Range, min: null, max: null);

        result.HasCode(ParameterErrorCodes.RangeMinRequired).Should().BeTrue();
        result.HasCode(ParameterErrorCodes.RangeMaxRequired).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_valid_for_a_well_formed_range_with_a_unit()
    {
        Validator.Validate(DataType.Range, min: 0m, max: 120m, unit: "minutes").IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_valid_for_a_well_formed_range_without_a_unit()
    {
        // The Unit label is optional (FR-S6-03 / SCR-06 field details).
        Validator.Validate(DataType.Range, min: -10m, max: 10m, unit: null).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(DataType.Text)]
    [InlineData(DataType.List)]
    [InlineData(DataType.Number)]
    [InlineData(DataType.Percentage)]
    public void Validate_returns_valid_for_a_non_range_type_with_no_range_configuration(DataType dataType)
    {
        Validator.Validate(dataType, min: null, max: null).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(DataType.Text)]
    [InlineData(DataType.List)]
    [InlineData(DataType.Number)]
    public void Validate_returns_invalid_range_not_applicable_when_a_non_range_type_carries_range_configuration(
        DataType dataType)
    {
        // Mirrors ck_parameters_range_only_for_range: switching Range → List without clearing the card must be
        // an inline console error, not a database exception.
        var result = Validator.Validate(dataType, min: 0m, max: 100m, unit: "minutes");

        result.IsValid.Should().BeFalse();
        result.HasCode(ParameterErrorCodes.RangeNotApplicable).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_invalid_range_not_applicable_when_a_non_range_type_carries_only_a_unit()
    {
        Validator.Validate(DataType.Text, min: null, max: null, unit: "minutes")
            .HasCode(ParameterErrorCodes.RangeNotApplicable).Should().BeTrue();
    }
}
