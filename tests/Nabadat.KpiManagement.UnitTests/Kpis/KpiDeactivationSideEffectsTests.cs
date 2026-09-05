using FluentAssertions;
using Nabadat.KpiManagement.Application.Kpis;
using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Xunit;

namespace Nabadat.KpiManagement.UnitTests.Kpis;

/// <summary>
/// T118 [US5] — unit tests for <c>KpiDeactivationSideEffects</c> (the pure function that derives the
/// deactivation mutation set), covering the spec.md US-5 Required cases: Show-on-Dashboard is forced
/// to false; a KPI in no CXI yields no CXI mutation; a CXI member's removal excludes the KPI from that
/// CXI's member list and recomputes the effective percentages on the remaining members.
/// <para>
/// Contract pinned for the implementer (T120):
/// <list type="bullet">
///   <item>static <c>KpiDeactivationPlan KpiDeactivationSideEffects.Compute(KpiDefinition kpi,
///   IReadOnlyList&lt;CxiWeight&gt; affectedCxiMemberships)</c> in <c>Application/Kpis/</c> — pure, no I/O.
///   <c>affectedCxiMemberships</c> is the full current membership of every CXI that includes
///   <c>kpi.Id</c> (each affected CXI's complete row set, including the deactivated member's own row);
///   empty when the KPI is in no CXI.</item>
///   <item><c>KpiDeactivationPlan(bool ShowOnDashboard, IReadOnlyList&lt;CxiDeactivationSideEffect&gt; CxiSideEffects)</c>
///   — <c>ShowOnDashboard</c> is always false (FR-026 cascade); <c>CxiSideEffects</c> is empty when the
///   KPI is in no CXI.</item>
///   <item><c>CxiDeactivationSideEffect(Guid CxiKpiId, Guid RemovedMemberKpiId,
///   IReadOnlyList&lt;CxiMemberEffectivePercentage&gt; RecomputedEffectivePercentages)</c> — one per affected
///   CXI; the recomputed list EXCLUDES the removed member and its percentages are normalised over the
///   remaining members (reuses <c>CxiWeightNormaliser</c>; sums to 100 ±0.1, SC-004).</item>
///   <item><c>CxiMemberEffectivePercentage(Guid MemberKpiId, decimal EffectivePercentage)</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class KpiDeactivationSideEffectsTests
{
    private static readonly Guid Nps = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Csat = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

    [Fact]
    public void Compute_forces_show_on_dashboard_off_even_when_currently_shown()
    {
        var kpi = Kpi(showOnDashboard: true);

        var plan = KpiDeactivationSideEffects.Compute(kpi, []);

        plan.ShowOnDashboard.Should().BeFalse();
    }

    [Fact]
    public void Compute_yields_no_cxi_side_effects_when_kpi_is_not_a_cxi_member()
    {
        var kpi = Kpi(showOnDashboard: false);

        var plan = KpiDeactivationSideEffects.Compute(kpi, []);

        plan.CxiSideEffects.Should().BeEmpty();
    }

    [Fact]
    public void Compute_excludes_the_deactivated_member_and_recomputes_remaining_effective_percentages()
    {
        var kpi = Kpi(showOnDashboard: true);
        var cxi = Guid.NewGuid();
        var memberships = new[]
        {
            Weight(cxi, kpi.Id, 2),
            Weight(cxi, Nps, 3),
            Weight(cxi, Csat, 5),
        };

        var plan = KpiDeactivationSideEffects.Compute(kpi, memberships);

        plan.CxiSideEffects.Should().ContainSingle();
        var effect = plan.CxiSideEffects[0];
        effect.CxiKpiId.Should().Be(cxi);
        effect.RemovedMemberKpiId.Should().Be(kpi.Id);
        effect.RecomputedEffectivePercentages.Select(p => p.MemberKpiId)
            .Should().BeEquivalentTo([Nps, Csat]);
        effect.RecomputedEffectivePercentages.Single(p => p.MemberKpiId == Nps).EffectivePercentage.Should().Be(37.5m);
        effect.RecomputedEffectivePercentages.Single(p => p.MemberKpiId == Csat).EffectivePercentage.Should().Be(62.5m);
    }

    private static KpiDefinition Kpi(bool showOnDashboard) => new()
    {
        Id = Guid.NewGuid(),
        ShortName = "QUAL",
        FullName = "Service Quality",
        KpiType = KpiType.Custom,
        IsComposite = false,
        CalculationMethod = CalculationMethod.WeightedAverage,
        Scale = Scale.Scale1_5,
        RepresentationStyle = RepresentationStyle.Number,
        Target = 80m,
        IsActive = true,
        ShowOnDashboard = showOnDashboard,
    };

    private static CxiWeight Weight(Guid cxiKpiId, Guid memberKpiId, int weight) =>
        new() { CxiKpiId = cxiKpiId, MemberKpiId = memberKpiId, Weight = (short)weight };
}
