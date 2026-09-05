using FluentAssertions;
using Xunit;
using Nabadat.KpiManagement.Application.Cxi;

namespace Nabadat.KpiManagement.UnitTests.Cxi;

/// <summary>
/// T080 [US3] — unit tests for <c>CxiMemberMembershipRule</c> (the membership-set transitions: a member
/// is auto-removed when its KPI is deactivated; the CXI may never include itself), covering the spec.md
/// US-3 Required cases.
/// <para>
/// Contract pinned for the implementer (T085):
/// <list type="bullet">
///   <item><c>CxiMemberMembershipRule</c> in <c>Application/Cxi/</c>, constructed with the CXI KPI's own id
///   (<c>new CxiMemberMembershipRule(Guid cxiKpiId)</c>) so it can reject self-membership.</item>
///   <item><c>OnKpiDeactivated(IReadOnlyList&lt;Guid&gt; memberSet, Guid deactivatedKpiId)</c> →
///   the member set with <c>deactivatedKpiId</c> removed, preserving order.</item>
///   <item><c>Add(IReadOnlyList&lt;Guid&gt; memberSet, Guid candidate)</c> → the set with <c>candidate</c>
///   appended; throws <c>CxiCannotIncludeItself</c> when <c>candidate</c> equals the CXI KPI id.</item>
/// </list>
/// </para>
/// </summary>
public sealed class CxiMemberMembershipRuleTests
{
    private static readonly Guid Cxi = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid Nps = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Csat = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Ces = Guid.Parse("00000000-0000-0000-0000-0000000000a3");

    [Fact]
    public void OnKpiDeactivated_removes_the_deactivated_member_and_keeps_the_rest()
    {
        var rule = new CxiMemberMembershipRule(Cxi);

        rule.OnKpiDeactivated(new[] { Nps, Csat, Ces }, deactivatedKpiId: Csat)
            .Should().Equal(Nps, Ces);
    }

    [Fact]
    public void Add_throws_CxiCannotIncludeItself_when_candidate_is_the_cxi_itself()
    {
        var rule = new CxiMemberMembershipRule(Cxi);

        var act = () => rule.Add(Array.Empty<Guid>(), candidate: Cxi);

        act.Should().Throw<CxiCannotIncludeItself>();
    }
}
