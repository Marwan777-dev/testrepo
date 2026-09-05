using Nabadat.KpiManagement.Application.Catalogue.Dtos;
using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Domain.ValueObjects;

namespace Nabadat.KpiManagement.Application.Catalogue;

/// <summary>
/// Defines the eight platform-standard KPIs and their canonical order (data-model.md §4 / research.md
/// R7): NPS, CSAT, CES, CXI, FCR, VFM, AgentScore, CHS. This is the in-code mirror of the seed block
/// in <c>KpiManagement_Baseline.sql</c> (same deterministic ids, same tuples, same default thresholds
/// — NPS uses <c>(-100, 0, 30, 100)</c>; every other standard uses <c>(0, 20, 70, 100)</c>). The
/// migration performs the actual per-tenant seed; this provider exists so unit tests and any in-code
/// consumer can assert/read the canonical set without a database.
/// </summary>
public static class KpiSeedDataProvider
{
    // Migration-time system actor (all-zero UUID), matching KpiManagement_Baseline.sql.
    private static readonly Guid SystemActor = Guid.Empty;

    /// <summary>The eight standard KPI seeds in canonical order.</summary>
    public static IReadOnlyList<KpiSeed> Seed() => Seeds;

    /// <summary>Canonical short names in order — the single declaration of standard ordering.</summary>
    public static IReadOnlyList<string> CanonicalShortNames =>
        Seeds.Select(s => s.Definition.ShortName).ToList();

    private static readonly IReadOnlyList<KpiSeed> Seeds =
    [
        Standard("00000006-0000-0000-0000-000000000001", "NPS", "Net Promoter Score",
            CalculationMethod.NPSStandard, Scale.Scale0_10, representation: null, target: 50,
            lower: -100, x: 0, y: 30, upper: 100),
        Standard("00000006-0000-0000-0000-000000000002", "CSAT", "Customer Satisfaction Score",
            CalculationMethod.WeightedAverage, Scale.Scale1_5, RepresentationStyle.Number, target: 80),
        Standard("00000006-0000-0000-0000-000000000003", "CES", "Customer Effort Score",
            CalculationMethod.WeightedAverage, Scale.Scale1_7, RepresentationStyle.Number, target: 80),
        Composite("00000006-0000-0000-0000-000000000004", "CXI", "Customer Experience Index", target: 80),
        Standard("00000006-0000-0000-0000-000000000005", "FCR", "First Contact Resolution",
            CalculationMethod.WeightedAverage, Scale.Scale1_3, RepresentationStyle.Number, target: 80),
        Standard("00000006-0000-0000-0000-000000000006", "VFM", "Value for Money",
            CalculationMethod.WeightedAverage, Scale.Scale1_5, RepresentationStyle.Number, target: 80),
        Standard("00000006-0000-0000-0000-000000000007", "AgentScore", "Agent Score",
            CalculationMethod.WeightedAverage, Scale.Scale1_5, RepresentationStyle.Number, target: 80),
        Standard("00000006-0000-0000-0000-000000000008", "CHS", "Customer Happiness Score",
            CalculationMethod.WeightedAverage, Scale.Scale1_5, RepresentationStyle.Number, target: 80),
    ];

    private static KpiSeed Standard(
        string id,
        string shortName,
        string fullName,
        CalculationMethod calculationMethod,
        Scale scale,
        RepresentationStyle? representation,
        decimal target,
        decimal lower = 0,
        decimal x = 20,
        decimal y = 70,
        decimal upper = 100)
    {
        var kpiId = Guid.Parse(id);
        return new KpiSeed(
            new KpiDefinition
            {
                Id = kpiId,
                ShortName = shortName,
                FullName = fullName,
                KpiType = KpiType.Standard,
                IsComposite = false,
                CalculationMethod = calculationMethod,
                Scale = scale,
                RepresentationStyle = representation,
                Target = target,
                IsActive = true,
                ShowOnDashboard = false,
                CreatedBy = SystemActor,
                UpdatedBy = SystemActor,
            },
            new KpiThreshold { KpiId = kpiId, LowerBound = lower, X = x, Y = y, UpperBound = upper });
    }

    private static KpiSeed Composite(string id, string shortName, string fullName, decimal target)
    {
        var kpiId = Guid.Parse(id);
        return new KpiSeed(
            new KpiDefinition
            {
                Id = kpiId,
                ShortName = shortName,
                FullName = fullName,
                KpiType = KpiType.Standard,
                IsComposite = true,
                CalculationMethod = CalculationMethod.WeightedComposite,
                Scale = null,
                RepresentationStyle = null,
                Target = target,
                IsActive = true,
                ShowOnDashboard = false,
                CreatedBy = SystemActor,
                UpdatedBy = SystemActor,
            },
            new KpiThreshold { KpiId = kpiId, LowerBound = 0, X = 20, Y = 70, UpperBound = 100 });
    }
}
