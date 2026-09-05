using FluentAssertions;
using Xunit;
using Nabadat.KpiManagement.Application.Cxi;

namespace Nabadat.KpiManagement.UnitTests.Cxi;

/// <summary>
/// T079 [US3] — unit tests for <c>CxiActivationRule</c> (the FR-043 "CXI needs at least two weighted
/// members" gate), covering the spec.md US-3 Required cases.
/// <para>
/// Contract pinned for the implementer (T084):
/// <list type="bullet">
///   <item>Static <c>CxiActivationRule</c> in <c>Application/Cxi/</c>.</item>
///   <item><c>CanActivate(IReadOnlyList&lt;int&gt; weights)</c> → <see langword="true"/> iff at least two
///   weights are positive (weight &gt; 0). Fewer than two positive weights → <see langword="false"/>:
///   a composite of zero or one member is not a composite.</item>
/// </list>
/// </para>
/// </summary>
public sealed class CxiActivationRuleTests
{
    [Fact]
    public void CanActivate_returns_false_when_there_are_no_weights()
    {
        CxiActivationRule.CanActivate(Array.Empty<int>()).Should().BeFalse();
    }

    [Fact]
    public void CanActivate_returns_false_when_only_one_member_has_a_positive_weight()
    {
        CxiActivationRule.CanActivate(new[] { 5 }).Should().BeFalse();
    }

    [Fact]
    public void CanActivate_returns_true_when_two_members_have_positive_weights()
    {
        CxiActivationRule.CanActivate(new[] { 1, 1 }).Should().BeTrue();
    }
}
