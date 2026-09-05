using FluentAssertions;
using Nabadat.KpiManagement.Domain.Entities;
using Xunit;
using Nabadat.KpiManagement.Application.Kpis.Validators;

namespace Nabadat.KpiManagement.UnitTests.Kpis;

/// <summary>
/// T047 [US2] — unit tests for <c>KpiThresholdValidator</c> (the strictly-ascending band-edge rule),
/// covering the spec.md US-2 Required cases.
/// <para>
/// Contract pinned for the implementer (T053):
/// <list type="bullet">
///   <item><c>KpiThresholdValidator : AbstractValidator&lt;KpiThreshold&gt;</c> (FluentValidation),
///   in <c>Application/Kpis/</c>.</item>
///   <item>The single ordering rule fails with <c>ErrorCode == "threshold.not_ascending"</c> unless
///   <c>LowerBound &lt; X &lt; Y &lt; UpperBound</c> (e.g. NPS <c>(-100, -50, 50, 100)</c> is valid).</item>
/// </list>
/// </para>
/// </summary>
public sealed class KpiThresholdValidatorTests
{
    private static readonly KpiThresholdValidator Validator = new();

    [Fact]
    public void Validate_returns_valid_when_bands_are_strictly_ascending()
    {
        var threshold = new KpiThreshold { LowerBound = 0m, X = 20m, Y = 70m, UpperBound = 100m };

        Validator.Validate(threshold).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_not_ascending_when_x_exceeds_y()
    {
        var threshold = new KpiThreshold { LowerBound = 0m, X = 70m, Y = 20m, UpperBound = 100m };

        var result = Validator.Validate(threshold);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorCode).Should().Contain("threshold.not_ascending");
    }

    [Fact]
    public void Validate_returns_valid_when_bands_are_ascending_across_the_nps_range()
    {
        var threshold = new KpiThreshold { LowerBound = -100m, X = -50m, Y = 50m, UpperBound = 100m };

        Validator.Validate(threshold).IsValid.Should().BeTrue();
    }
}
