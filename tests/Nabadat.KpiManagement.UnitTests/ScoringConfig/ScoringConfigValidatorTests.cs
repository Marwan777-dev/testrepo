using FluentAssertions;
using Nabadat.KpiManagement.Application.ScoringConfig;
using Xunit;

namespace Nabadat.KpiManagement.UnitTests.ScoringConfig;

/// <summary>
/// Unit tests for <see cref="ScoringConfigValidator"/> (T095 / US-4) — the per-field truth table for
/// the five tenant scoring parameters (spec.md US-4 Required cases). Error codes are the domain codes
/// the controller maps to the API-05 wire codes (<c>INVALID_ALPHA_BETA_SUM</c>, …).
/// </summary>
public sealed class ScoringConfigValidatorTests
{
    private static readonly ScoringConfigValidator Sut = new();

    private static ScoringConfigInput Valid() => new(
        Alpha: 0.500m, MotMultiplier: 1.5m, NFloor: 100, FlagPercentile: 25, RollingWindowDays: 30);

    [Fact]
    public void Validate_passes_when_all_fields_are_in_range()
    {
        Sut.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Validate_rejects_alpha_out_of_range(double alpha)
    {
        var result = Sut.Validate(Valid() with { Alpha = (decimal)alpha });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(ScoringConfigValidator.AlphaOutOfRangeCode);
    }

    [Theory]
    [InlineData(0.9)]
    [InlineData(2.01)]
    public void Validate_rejects_mot_multiplier_out_of_range(double mot)
    {
        var result = Sut.Validate(Valid() with { MotMultiplier = (decimal)mot });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(ScoringConfigValidator.MotOutOfRangeCode);
    }

    [Fact]
    public void Validate_rejects_n_floor_below_minimum()
    {
        var result = Sut.Validate(Valid() with { NFloor = 0 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(ScoringConfigValidator.NFloorBelowMinimumCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public void Validate_rejects_flag_percentile_out_of_range(int percentile)
    {
        var result = Sut.Validate(Valid() with { FlagPercentile = percentile });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(ScoringConfigValidator.FlagPercentileOutOfRangeCode);
    }

    [Fact]
    public void Validate_rejects_rolling_window_below_minimum()
    {
        var result = Sut.Validate(Valid() with { RollingWindowDays = 6 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(ScoringConfigValidator.RollingWindowBelowMinimumCode);
    }
}
