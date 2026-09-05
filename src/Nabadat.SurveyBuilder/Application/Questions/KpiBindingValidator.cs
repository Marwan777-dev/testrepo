using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>
/// Layered KPI-binding validator (T076, FR-8.4 / BR-8.2). With the journey binding ON, a touchpoint
/// requires a stage (<c>kpi.touchpoint.requires_stage</c>); with it OFF, any stage/touchpoint is
/// ignored and stripped (warning <c>kpi.binding_ignored_when_bound_journey_off</c>, not an error).
/// Builds on the domain <see cref="KpiBinding.IsValid"/> shape check. Pure.
/// </summary>
public sealed class KpiBindingValidator
{
    public KpiBindingValidationResult Validate(KpiBinding binding)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var normalised = binding;

        if (!binding.BoundJourneyOn)
        {
            if (binding.StageId is not null || binding.TouchpointId is not null)
            {
                warnings.Add("kpi.binding_ignored_when_bound_journey_off");
                normalised = binding with { StageId = null, TouchpointId = null };
            }

            return new KpiBindingValidationResult(true, errors, warnings, normalised);
        }

        if (binding.TouchpointId is not null && binding.StageId is null)
        {
            errors.Add("kpi.touchpoint.requires_stage");
            return new KpiBindingValidationResult(false, errors, warnings, normalised);
        }

        return new KpiBindingValidationResult(true, errors, warnings, normalised);
    }
}
