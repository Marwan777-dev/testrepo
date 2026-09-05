using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Questions;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Questions;

/// <summary>
/// T047 [US1] — unit tests for <c>KpiBindingChangePolicy</c> (BR-8.5): when a KPI question's KPI code
/// changes, the touchpoint is retained iff it is still valid for the new KPI + journey + stage (as
/// judged by the M-16 <c>IJourneyReader</c>), else it is cleared; a stage that is no longer valid for
/// the new KPI is cleared too.
/// <para>
/// Contract pinned for the implementer (T077):
/// <list type="bullet">
///   <item><c>KpiBindingChangePolicy</c> lives in <c>Application/Questions/</c>; ctor
///   <c>(IJourneyReader journeys)</c>.</item>
///   <item><c>Task&lt;KpiBinding&gt; OnKpiChangedAsync(KpiBinding current, string newKpiCode,
///   CancellationToken ct = default)</c> returns the adjusted binding (new KPI code applied;
///   stage/touchpoint retained or cleared per validity).</item>
///   <item><c>IJourneyReader</c> (M-16 port, in <c>Domain/Interfaces/</c>):
///   <c>Task&lt;bool&gt; IsBindingValidAsync(string kpiCode, Guid? stageId, Guid? touchpointId,
///   CancellationToken ct = default)</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class KpiBindingChangePolicyTests
{
    private static readonly Guid Stage = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Touchpoint = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private readonly IJourneyReader _journeys = Substitute.For<IJourneyReader>();

    [Fact]
    public async Task OnKpiChangedAsync_retains_stage_and_touchpoint_when_still_valid_for_the_new_kpi()
    {
        _journeys.IsBindingValidAsync("NPS", Stage, Touchpoint, Arg.Any<CancellationToken>()).Returns(true);
        var current = new KpiBinding("CSAT", null, BoundJourneyOn: true, Stage, Touchpoint);
        var policy = new KpiBindingChangePolicy(_journeys);

        var result = await policy.OnKpiChangedAsync(current, "NPS");

        result.KpiCode.Should().Be("NPS");
        result.StageId.Should().Be(Stage);
        result.TouchpointId.Should().Be(Touchpoint);
    }

    [Fact]
    public async Task OnKpiChangedAsync_clears_the_touchpoint_when_invalid_for_the_new_kpi()
    {
        // Touchpoint invalid for the new KPI; the stage alone is still valid.
        _journeys.IsBindingValidAsync("NPS", Stage, Touchpoint, Arg.Any<CancellationToken>()).Returns(false);
        _journeys.IsBindingValidAsync("NPS", Stage, null, Arg.Any<CancellationToken>()).Returns(true);
        var current = new KpiBinding("CSAT", null, BoundJourneyOn: true, Stage, Touchpoint);
        var policy = new KpiBindingChangePolicy(_journeys);

        var result = await policy.OnKpiChangedAsync(current, "NPS");

        result.KpiCode.Should().Be("NPS");
        result.TouchpointId.Should().BeNull();
        result.StageId.Should().Be(Stage);
    }

    [Fact]
    public async Task OnKpiChangedAsync_clears_stage_and_touchpoint_when_neither_is_valid_for_the_new_kpi()
    {
        _journeys.IsBindingValidAsync("NPS", Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var current = new KpiBinding("CSAT", null, BoundJourneyOn: true, Stage, Touchpoint);
        var policy = new KpiBindingChangePolicy(_journeys);

        var result = await policy.OnKpiChangedAsync(current, "NPS");

        result.StageId.Should().BeNull();
        result.TouchpointId.Should().BeNull();
    }
}
