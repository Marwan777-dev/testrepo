using FluentAssertions;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Xunit;
using Nabadat.KpiManagement.Application.Kpis.Validators;

namespace Nabadat.KpiManagement.UnitTests.Kpis;

/// <summary>
/// T049 [US2] — unit tests for <c>TopNBoxWarningRule</c> (the TOP-n Box "n" advisory + blocking
/// rules of the calculation-method field, FR-014 / FR-015), covering the spec.md US-2 Required cases.
/// <para>
/// Contract pinned for the implementer (T054):
/// <list type="bullet">
///   <item>Static <c>TopNBoxWarningRule</c> in <c>Application/Kpis/</c>.</item>
///   <item><c>ShouldWarn(Scale scale, int n)</c> → <see langword="true"/> when <c>n</c> exceeds half the
///   scale span (Scale1_7 span 6 → warn when n &gt; 3; Scale0_10 span 10 → warn when n &gt; 5, so n=5
///   does NOT warn).</item>
///   <item><c>IsBlockingError(Scale scale, int n)</c> → <see langword="true"/> when <c>n</c> reaches the
///   scale's box count (Scale1_7 has 7 boxes → n=7 is a blocking error).</item>
/// </list>
/// </para>
/// </summary>
public sealed class TopNBoxWarningRuleTests
{
    [Fact]
    public void ShouldWarn_returns_true_when_n_exceeds_half_of_scale_1_7()
    {
        TopNBoxWarningRule.ShouldWarn(Scale.Scale1_7, n: 4).Should().BeTrue();
    }

    [Fact]
    public void ShouldWarn_returns_false_when_n_is_half_of_scale_0_10()
    {
        TopNBoxWarningRule.ShouldWarn(Scale.Scale0_10, n: 5).Should().BeFalse();
    }

    [Fact]
    public void IsBlockingError_returns_true_when_n_reaches_scale_1_7_max()
    {
        TopNBoxWarningRule.IsBlockingError(Scale.Scale1_7, n: 7).Should().BeTrue();
    }
}
