using FluentAssertions;
using Nabadat.KpiManagement.Application.Catalogue;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Xunit;

namespace Nabadat.KpiManagement.UnitTests.Kpis;

/// <summary>
/// T031 [US1] — unit tests for <c>KpiSeedDataProvider</c> (defines the 8 canonical KPIs + their
/// canonical order). Asserts the 8 tuples in canonical order with the right
/// <c>(short_name, full_name, is_composite, calculation_method)</c> values (spec.md US-1 Required
/// case) plus the NPS-specific threshold <c>(x=0, y=30)</c> (Clarifications round 2, 2026-06-21;
/// KpiManagement_Baseline.sql seed).
/// <para>
/// Contract pinned for the implementer (T033): <c>KpiSeedDataProvider.Seed()</c> returns an
/// ordered <c>IReadOnlyList&lt;KpiSeed&gt;</c> where each <c>KpiSeed</c> bundles the seeded
/// <c>Definition</c> (a <c>KpiDefinition</c>) and its <c>Threshold</c> (a <c>KpiThreshold</c>) —
/// the single source of canonical seed truth mirrored by the SQL baseline.
/// </para>
/// </summary>
public sealed class KpiSeedDataProviderTests
{
    [Fact]
    public void Seed_returns_eight_canonical_kpis_in_order_with_expected_tuples()
    {
        var seeds = KpiSeedDataProvider.Seed();

        seeds.Select(s => (
                s.Definition.ShortName,
                s.Definition.FullName,
                s.Definition.IsComposite,
                s.Definition.CalculationMethod))
            .Should().Equal(
                ("NPS", "Net Promoter Score", false, CalculationMethod.NPSStandard),
                ("CSAT", "Customer Satisfaction Score", false, CalculationMethod.WeightedAverage),
                ("CES", "Customer Effort Score", false, CalculationMethod.WeightedAverage),
                ("CXI", "Customer Experience Index", true, CalculationMethod.WeightedComposite),
                ("FCR", "First Contact Resolution", false, CalculationMethod.WeightedAverage),
                ("VFM", "Value for Money", false, CalculationMethod.WeightedAverage),
                ("AgentScore", "Agent Score", false, CalculationMethod.WeightedAverage),
                ("CHS", "Customer Happiness Score", false, CalculationMethod.WeightedAverage));
    }

    [Fact]
    public void Seed_assigns_nps_threshold_of_x_zero_and_y_thirty()
    {
        var nps = KpiSeedDataProvider.Seed().Single(s => s.Definition.ShortName == "NPS");

        nps.Threshold.X.Should().Be(0);
        nps.Threshold.Y.Should().Be(30);
    }
}
