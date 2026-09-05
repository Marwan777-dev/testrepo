using FluentAssertions;
using Nabadat.KpiManagement.Application.ScoringConfig;
using Xunit;

namespace Nabadat.KpiManagement.UnitTests.ScoringConfig;

/// <summary>
/// Unit tests for <see cref="AlphaBetaDeriver"/> (T096 / US-4). β is derived as <c>1.000 − α</c> using
/// <see cref="decimal"/> arithmetic so there is no IEEE-754 drift (FR-053 / R6) — the displayed β must
/// be exact to 3 decimal places.
/// </summary>
public sealed class AlphaBetaDeriverTests
{
    [Theory]
    [InlineData(0.500, 0.500)]
    [InlineData(0.700, 0.300)]
    [InlineData(0.123, 0.877)]
    [InlineData(0.000, 1.000)]
    [InlineData(1.000, 0.000)]
    public void Beta_returns_one_minus_alpha_exactly(double alpha, double expectedBeta)
    {
        AlphaBetaDeriver.Beta((decimal)alpha).Should().Be((decimal)expectedBeta);
    }
}
