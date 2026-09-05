using FluentValidation;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Nabadat.KpiManagement.Application.Kpis.Dtos;

namespace Nabadat.KpiManagement.Application.Kpis.Validators;

/// <summary>
/// Cross-field validation for the KPI Configuration form (US-2 / SRS FR rules). Each rule sets its
/// FluentValidation <c>ErrorCode</c> to a stable token — the API layer maps these onto the API-05
/// envelope codes (contracts/kpi-api.md). The rules:
/// <list type="bullet">
///   <item>Short Name required, ≤ 20 chars, and case-insensitively unique against the tenant's
///   other KPIs (trim-insensitive).</item>
///   <item>Full Name required, ≤ 100 chars.</item>
///   <item>Threshold band edges strictly ascending (mirrors <see cref="KpiThresholdValidator"/>).</item>
///   <item>The <c>Slider</c> representation is permitted only on the 1–3 scale.</item>
///   <item><c>NPSStandard</c> is reserved for the NPS standard KPI; <c>WeightedComposite</c> for CXI.</item>
///   <item>Target is required when the KPI is active, and (when present) within the KPI's scale
///   range — the threshold lower/upper bounds (0..100 ordinarily, −100..100 for NPS).</item>
/// </list>
/// </summary>
public sealed class KpiDefinitionValidator : AbstractValidator<KpiDefinitionInput>
{
    public KpiDefinitionValidator()
    {
        RuleFor(x => x.ShortName)
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithErrorCode("short_name.required")
            .WithMessage("Short Name is required.");

        RuleFor(x => x.ShortName)
            .Must(s => (s ?? string.Empty).Trim().Length <= 20)
            .WithErrorCode("short_name.too_long")
            .WithMessage("Short Name must be 20 characters or fewer.");

        RuleFor(x => x.ShortName)
            .Must((input, shortName) => !IsDuplicate(input))
            .WithErrorCode("short_name.duplicate")
            .WithMessage("A KPI with this Short Name already exists.");

        RuleFor(x => x.FullName)
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithErrorCode("full_name.required")
            .WithMessage("Full Name is required.");

        RuleFor(x => x.FullName)
            .Must(s => (s ?? string.Empty).Trim().Length <= 100)
            .WithErrorCode("full_name.too_long")
            .WithMessage("Full Name must be 100 characters or fewer.");

        RuleFor(x => x)
            .Must(x => x.LowerBound < x.X && x.X < x.Y && x.Y < x.UpperBound)
            .WithErrorCode("threshold.must_be_ascending")
            .WithMessage("Threshold band edges must be strictly ascending: lower_bound < x < y < upper_bound.");

        RuleFor(x => x)
            .Must(x => !(x.RepresentationStyle == RepresentationStyle.Slider && x.Scale != Scale.Scale1_3))
            .WithErrorCode("representation_style.slider_requires_scale_1_3")
            .WithMessage("The slider representation is only available on the 1–3 scale.");

        RuleFor(x => x)
            .Must(x => !(x.CalculationMethod == CalculationMethod.NPSStandard && !x.IsStandard))
            .WithErrorCode("calculation_method.nps_standard_reserved_for_nps")
            .WithMessage("The NPS Standard calculation method is reserved for the NPS KPI.");

        RuleFor(x => x)
            .Must(x => !(x.CalculationMethod == CalculationMethod.WeightedComposite && !x.IsComposite))
            .WithErrorCode("calculation_method.weighted_composite_reserved_for_cxi")
            .WithMessage("The Weighted Composite calculation method is reserved for the CXI KPI.");

        RuleFor(x => x)
            .Must(x => !(x.IsActive && x.Target is null))
            .WithErrorCode("target.required_when_active")
            .WithMessage("A target is required when the KPI is active.");

        // Target must fall inside the KPI's own scale range — i.e. between the threshold lower and
        // upper bounds, which are 0..100 for ordinary KPIs and −100..100 for NPS. (Previously a flat
        // −100..100, which wrongly accepted negatives for non-NPS KPIs.)
        When(x => x.Target.HasValue, () =>
            RuleFor(x => x)
                .Must(x => x.Target!.Value >= x.LowerBound && x.Target!.Value <= x.UpperBound)
                .WithErrorCode("target.out_of_range")
                .WithMessage(x => $"Target must be between {x.LowerBound:0.##} and {x.UpperBound:0.##}."));
    }

    private static bool IsDuplicate(KpiDefinitionInput input)
    {
        var candidate = (input.ShortName ?? string.Empty).Trim();
        if (candidate.Length == 0)
        {
            return false;
        }

        return input.ExistingShortNames
            .Any(existing => string.Equals((existing ?? string.Empty).Trim(), candidate, StringComparison.OrdinalIgnoreCase));
    }
}
