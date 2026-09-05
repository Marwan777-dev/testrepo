using FluentAssertions;
using Xunit;
using Nabadat.KpiManagement.Application.Cxi;

namespace Nabadat.KpiManagement.UnitTests.Cxi;

/// <summary>
/// T078 [US3] — unit tests for <c>CxiWeightNormaliser</c> (relative integer weights → effective %),
/// covering the spec.md US-3 Required cases.
/// <para>
/// Contract pinned for the implementer (T083):
/// <list type="bullet">
///   <item>Static <c>CxiWeightNormaliser</c> in <c>Application/Cxi/</c>.</item>
///   <item><c>Normalise(IReadOnlyList&lt;int&gt; weights)</c> → <c>IReadOnlyList&lt;decimal&gt;</c>: each
///   weight's share of the total, expressed as a percentage rounded to 1 decimal place, in input order.
///   The returned percentages sum to 100 within ±0.1 (rounding tolerance).</item>
///   <item>Empty input → empty output; an all-zero input (no positive weight) → empty output
///   (a CXI with no weighted members has no effective breakdown).</item>
/// </list>
/// </para>
/// </summary>
public sealed class CxiWeightNormaliserTests
{
    [Fact]
    public void Normalise_returns_proportional_percentages_when_weights_are_3_2_1()
    {
        CxiWeightNormaliser.Normalise(new[] { 3, 2, 1 })
            .Should().Equal(50.0m, 33.3m, 16.7m);
    }

    [Fact]
    public void Normalise_sums_to_100_within_tolerance_when_weights_are_3_2_1()
    {
        CxiWeightNormaliser.Normalise(new[] { 3, 2, 1 })
            .Sum().Should().BeApproximately(100m, 0.1m);
    }

    [Fact]
    public void Normalise_splits_evenly_when_weights_are_equal()
    {
        CxiWeightNormaliser.Normalise(new[] { 1, 1 })
            .Should().Equal(50m, 50m);
    }

    [Fact]
    public void Normalise_returns_empty_when_input_is_empty()
    {
        CxiWeightNormaliser.Normalise(Array.Empty<int>())
            .Should().BeEmpty();
    }

    [Fact]
    public void Normalise_returns_empty_when_all_weights_are_zero()
    {
        CxiWeightNormaliser.Normalise(new[] { 0, 0 })
            .Should().BeEmpty();
    }
}
