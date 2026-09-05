using FluentAssertions;
using Xunit;
using Nabadat.KpiManagement.Application.Kpis.Validators;

namespace Nabadat.KpiManagement.UnitTests.Kpis;

/// <summary>
/// Unit tests for <see cref="TargetBandRule"/> — the non-blocking advisory raised when a KPI's
/// Target falls below the Satisfactory band (&lt; y, the average/satisfactory boundary).
/// </summary>
public sealed class TargetBandRuleTests
{
    [Fact]
    public void IsBelowSatisfactory_returns_true_when_target_is_below_y()
    {
        TargetBandRule.IsBelowSatisfactory(60m, 70m).Should().BeTrue();
    }

    [Fact]
    public void IsBelowSatisfactory_returns_false_when_target_is_above_y()
    {
        TargetBandRule.IsBelowSatisfactory(80m, 70m).Should().BeFalse();
    }

    [Fact]
    public void IsBelowSatisfactory_returns_false_when_target_equals_y()
    {
        TargetBandRule.IsBelowSatisfactory(70m, 70m).Should().BeFalse();
    }
}
