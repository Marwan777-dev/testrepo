using FluentAssertions;
using FluentValidation.Results;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Xunit;
using Nabadat.KpiManagement.Application.Kpis.Dtos;
using Nabadat.KpiManagement.Application.Kpis.Validators;

namespace Nabadat.KpiManagement.UnitTests.Kpis;

/// <summary>
/// T046 [US2] — unit tests for <c>KpiDefinitionValidator</c> (the cross-field FluentValidation
/// rules of the KPI Configuration form), covering the spec.md US-2 "Unit Test Coverage" Required
/// cases.
/// <para>
/// Contract these tests pin for the implementer (T055):
/// <list type="bullet">
///   <item><c>KpiDefinitionValidator : AbstractValidator&lt;KpiDefinitionInput&gt;</c> (FluentValidation),
///   in <c>Application/Kpis/</c>.</item>
///   <item><c>KpiDefinitionInput</c> — the validation model carrying every field the cross-field
///   rules read (Short Name + the tenant's <c>ExistingShortNames</c> for the duplicate check,
///   <c>IsStandard</c>, calculation method, scale, representation style, target, active flag, and
///   the four threshold band edges). One type per file, in <c>Application/Kpis/</c>.</item>
///   <item>Each rule sets its FluentValidation <c>ErrorCode</c> via <c>.WithErrorCode(...)</c> to the
///   exact code asserted below (e.g. <c>"short_name.duplicate"</c>) — the codes are the stable
///   contract the API layer maps to the API-05 envelope.</item>
/// </list>
/// Invalid cases assert only that the expected code is <em>present</em> (other rules may also fire on
/// a deliberately-minimal input); Valid cases assert <see cref="ValidationResult.IsValid"/>.
/// </para>
/// </summary>
public sealed class KpiDefinitionValidatorTests
{
    private static readonly KpiDefinitionValidator Validator = new();

    [Fact]
    public void Validate_returns_valid_when_input_is_the_seeded_nps_row()
    {
        var input = new KpiDefinitionInput
        {
            ShortName = "NPS",
            FullName = "Net Promoter Score",
            IsStandard = true,
            IsComposite = false,
            CalculationMethod = CalculationMethod.NPSStandard,
            Scale = Scale.Scale0_10,
            RepresentationStyle = null,
            Target = 42m,
            IsActive = true,
            LowerBound = -100m,
            X = -50m,
            Y = 50m,
            UpperBound = 100m,
            ExistingShortNames = [],
        };

        Validator.Validate(input).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_threshold_must_be_ascending_when_x_exceeds_y()
    {
        var input = new KpiDefinitionInput
        {
            ShortName = "QUAL",
            FullName = "Service Quality",
            IsStandard = false,
            CalculationMethod = CalculationMethod.WeightedAverage,
            Scale = Scale.Scale1_5,
            Target = 80m,
            IsActive = true,
            LowerBound = 0m,
            X = 70m,
            Y = 20m,
            UpperBound = 100m,
            ExistingShortNames = [],
        };

        ErrorCodes(Validator.Validate(input)).Should().Contain("threshold.must_be_ascending");
    }

    [Fact]
    public void Validate_returns_short_name_duplicate_when_short_name_matches_case_insensitively()
    {
        var input = new KpiDefinitionInput
        {
            ShortName = "qual",
            FullName = "Service Quality",
            ExistingShortNames = ["QUAL"],
        };

        ErrorCodes(Validator.Validate(input)).Should().Contain("short_name.duplicate");
    }

    [Fact]
    public void Validate_returns_short_name_duplicate_when_short_name_matches_after_trim()
    {
        var input = new KpiDefinitionInput
        {
            ShortName = " QUAL ",
            FullName = "Service Quality",
            ExistingShortNames = ["QUAL"],
        };

        ErrorCodes(Validator.Validate(input)).Should().Contain("short_name.duplicate");
    }

    [Fact]
    public void Validate_returns_slider_requires_scale_1_3_when_slider_on_scale_1_5()
    {
        var input = new KpiDefinitionInput
        {
            ShortName = "X",
            FullName = "Custom",
            RepresentationStyle = RepresentationStyle.Slider,
            Scale = Scale.Scale1_5,
        };

        ErrorCodes(Validator.Validate(input))
            .Should().Contain("representation_style.slider_requires_scale_1_3");
    }

    [Fact]
    public void Validate_returns_nps_standard_reserved_when_custom_kpi_picks_nps_standard()
    {
        var input = new KpiDefinitionInput
        {
            ShortName = "X",
            FullName = "Custom",
            CalculationMethod = CalculationMethod.NPSStandard,
            IsStandard = false,
        };

        ErrorCodes(Validator.Validate(input))
            .Should().Contain("calculation_method.nps_standard_reserved_for_nps");
    }

    [Fact]
    public void Validate_returns_target_required_when_active_and_target_is_null()
    {
        var input = new KpiDefinitionInput
        {
            ShortName = "X",
            FullName = "Custom",
            IsActive = true,
            Target = null,
        };

        ErrorCodes(Validator.Validate(input)).Should().Contain("target.required_when_active");
    }

    [Fact]
    public void Validate_returns_target_out_of_range_when_non_nps_target_is_negative()
    {
        var input = new KpiDefinitionInput
        {
            ShortName = "QUAL",
            FullName = "Service Quality",
            CalculationMethod = CalculationMethod.WeightedAverage,
            Scale = Scale.Scale1_5,
            Target = -50m,
            IsActive = true,
            LowerBound = 0m,
            X = 20m,
            Y = 70m,
            UpperBound = 100m,
            ExistingShortNames = [],
        };

        ErrorCodes(Validator.Validate(input)).Should().Contain("target.out_of_range");
    }

    [Fact]
    public void Validate_returns_target_out_of_range_when_target_exceeds_upper_bound()
    {
        var input = new KpiDefinitionInput
        {
            ShortName = "QUAL",
            FullName = "Service Quality",
            CalculationMethod = CalculationMethod.WeightedAverage,
            Scale = Scale.Scale1_5,
            Target = 120m,
            IsActive = true,
            LowerBound = 0m,
            X = 20m,
            Y = 70m,
            UpperBound = 100m,
            ExistingShortNames = [],
        };

        ErrorCodes(Validator.Validate(input)).Should().Contain("target.out_of_range");
    }

    [Fact]
    public void Validate_returns_valid_when_nps_target_is_negative_within_range()
    {
        var input = new KpiDefinitionInput
        {
            ShortName = "NPS",
            FullName = "Net Promoter Score",
            IsStandard = true,
            CalculationMethod = CalculationMethod.NPSStandard,
            Scale = Scale.Scale0_10,
            Target = -50m,
            IsActive = true,
            LowerBound = -100m,
            X = -60m,
            Y = 50m,
            UpperBound = 100m,
            ExistingShortNames = [],
        };

        Validator.Validate(input).IsValid.Should().BeTrue();
    }

    private static IEnumerable<string> ErrorCodes(ValidationResult result) =>
        result.Errors.Select(e => e.ErrorCode);
}
