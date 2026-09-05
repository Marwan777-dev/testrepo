using FluentAssertions;
using Nabadat.CustomerJourneyManagement.Application.KpiBindings;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.KpiBindings;

/// <summary>
/// Unit tests for <see cref="KpiWeightValidator"/> (T042 / US-2) — the pure weight-rule guard for
/// a touchpoint's KPI binding set (full-replace save, <c>contracts/configuration-api.md §PUT
/// /api/v1/touchpoints/{id}/kpis</c>). These tests are authored FIRST (red→green per the Unit Test
/// Policy) and define the contract the T045 implementation must satisfy:
/// <list type="bullet">
///   <item><c>record KpiBindingInput(string KpiType, decimal Weight)</c> — one requested binding;
///   weight is <see langword="decimal"/> (numeric(5,2)), never <see langword="double"/>.</item>
///   <item><c>KpiWeightValidator(IActiveKpiCatalogReader catalog)</c> — the active bindable catalogue
///   backs the known-type check; the cases below use the platform-standard keys (NPS/CSAT/CES), which
///   the default catalogue supplies.</item>
///   <item><c>Task&lt;ServiceResult&gt; ValidateAsync(IReadOnlyList&lt;KpiBindingInput&gt; bindings,
///   CancellationToken ct = default)</c>.</item>
/// </list>
/// Weight rules under test: an empty set is valid (unmeasured touchpoint); a non-empty set must
/// have each weight in <c>(0, 100]</c>, no duplicate <c>kpiType</c>, and weights summing to exactly
/// <c>100.00m</c>. Every case violates at most one rule so the asserted error code is deterministic
/// regardless of the implementation's internal check order.
/// </summary>
public sealed class KpiWeightValidatorTests
{
    private static KpiWeightValidator CreateSut(IActiveKpiCatalogReader? catalog = null) =>
        new(catalog ?? StandardCatalog());

    /// <summary>A catalogue substitute exposing the platform-standard keys the cases below bind.</summary>
    private static IActiveKpiCatalogReader StandardCatalog()
    {
        var catalog = Substitute.For<IActiveKpiCatalogReader>();
        catalog.GetActiveKpisAsync(Arg.Any<CancellationToken>()).Returns(
            KpiTypeService.PlatformStandardCatalog
                .Select(type => new ActiveKpiCatalogEntry(
                    Guid.NewGuid(), type.TypeKey, type.LabelAr, type.LabelEn, type.ScoringDirection, true))
                .ToList());
        return catalog;
    }

    private static KpiBindingInput Binding(string kpiType, decimal weight) => new(kpiType, weight);

    [Fact]
    public async Task ValidateAsync_succeeds_when_weights_sum_to_100()
    {
        var bindings = new[] { Binding("NPS", 60.00m), Binding("CSAT", 40.00m) };

        var result = await CreateSut().ValidateAsync(bindings);

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_succeeds_when_decimal_weights_sum_to_exactly_100()
    {
        // Proves decimal arithmetic, not double: 33.34 + 33.33 + 33.33 sums to exactly 100.00m in
        // decimal, whereas IEEE-754 double would accumulate representation error and risk a
        // spurious "sum != 100" rejection. All three are platform-standard types (no repo lookup).
        var bindings = new[] { Binding("NPS", 33.34m), Binding("CSAT", 33.33m), Binding("CES", 33.33m) };

        var result = await CreateSut().ValidateAsync(bindings);

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_returns_weight_sum_invalid_when_weights_do_not_sum_to_100()
    {
        // 60 + 30 = 90: each weight is individually valid and there are no duplicates, so only the
        // sum rule fires.
        var bindings = new[] { Binding("NPS", 60.00m), Binding("CSAT", 30.00m) };

        var result = await CreateSut().ValidateAsync(bindings);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("kpi.weight_sum_invalid");
    }

    [Fact]
    public async Task ValidateAsync_succeeds_when_bindings_are_empty()
    {
        // An empty set saves an unmeasured touchpoint (all existing bindings deleted) — valid.
        var result = await CreateSut().ValidateAsync(Array.Empty<KpiBindingInput>());

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_returns_duplicate_type_when_same_kpi_type_appears_twice()
    {
        // Weights sum to exactly 100 and each is in range, so the duplicate rule is the only one
        // violated — forces the implementation to reject duplicates even when the sum is valid.
        var bindings = new[] { Binding("NPS", 50.00m), Binding("NPS", 50.00m) };

        var result = await CreateSut().ValidateAsync(bindings);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("kpi.duplicate_type");
    }

    [Theory]
    [InlineData(0)]    // zero is not strictly positive
    [InlineData(-10)]  // negative weight
    public async Task ValidateAsync_returns_individual_weight_invalid_when_a_weight_is_not_positive(int invalidWeight)
    {
        // Partner weight keeps the sum at 100.00m and there are no duplicates, so the individual
        // weight rule is the only one in play (for -10 both weights are out of range, but both map
        // to the same code, so the assertion stays deterministic).
        var bindings = new[]
        {
            Binding("NPS", invalidWeight),
            Binding("CSAT", 100m - invalidWeight),
        };

        var result = await CreateSut().ValidateAsync(bindings);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("kpi.individual_weight_invalid");
    }
}
