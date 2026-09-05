using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Questions;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Questions;

/// <summary>
/// T046 [US1] — unit tests for <c>KpiBindingValidator</c> (FR-8.4 / BR-8.2 layered KPI-binding
/// rules). A touchpoint requires a stage; when journey binding is off, any stage/touchpoint is
/// ignored and stripped (a warning, not an error); a stage without a touchpoint is valid.
/// <para>
/// Contract pinned for the implementer (T076):
/// <list type="bullet">
///   <item><c>KpiBindingValidator</c> lives in <c>Application/Questions/</c> and is pure:
///   <c>KpiBindingValidationResult Validate(KpiBinding binding)</c>, building on the domain
///   <see cref="KpiBinding.IsValid"/> shape check.</item>
///   <item><c>KpiBindingValidationResult</c> exposes <c>bool IsValid</c>,
///   <c>IReadOnlyList&lt;string&gt; Errors</c>, <c>IReadOnlyList&lt;string&gt; Warnings</c>, and
///   <c>KpiBinding Normalised</c> (the binding after stripping ignored stage/touchpoint values).</item>
///   <item>Error code <c>kpi.touchpoint.requires_stage</c>; warning code
///   <c>kpi.binding_ignored_when_bound_journey_off</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class KpiBindingValidatorTests
{
    private static readonly Guid Stage = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Touchpoint = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private readonly KpiBindingValidator _validator = new();

    [Fact]
    public void Validate_returns_invalid_when_a_touchpoint_is_set_without_a_stage()
    {
        var binding = new KpiBinding("CSAT", Perspective: null, BoundJourneyOn: true, StageId: null, TouchpointId: Touchpoint);

        var result = _validator.Validate(binding);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("kpi.touchpoint.requires_stage");
    }

    [Fact]
    public void Validate_warns_and_strips_stage_and_touchpoint_when_journey_binding_is_off()
    {
        var binding = new KpiBinding("CSAT", Perspective: null, BoundJourneyOn: false, StageId: Stage, TouchpointId: Touchpoint);

        var result = _validator.Validate(binding);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain("kpi.binding_ignored_when_bound_journey_off");
        result.Normalised.StageId.Should().BeNull();
        result.Normalised.TouchpointId.Should().BeNull();
    }

    [Fact]
    public void Validate_accepts_a_stage_without_a_touchpoint()
    {
        var binding = new KpiBinding("CSAT", Perspective: null, BoundJourneyOn: true, StageId: Stage, TouchpointId: null);

        var result = _validator.Validate(binding);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_a_stage_and_touchpoint_together()
    {
        var binding = new KpiBinding("CSAT", Perspective: null, BoundJourneyOn: true, StageId: Stage, TouchpointId: Touchpoint);

        var result = _validator.Validate(binding);

        result.IsValid.Should().BeTrue();
    }
}
