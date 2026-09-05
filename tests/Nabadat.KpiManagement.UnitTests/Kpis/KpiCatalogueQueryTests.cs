using FluentAssertions;
using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Xunit;
using Nabadat.KpiManagement.Application.Kpis.Dtos;
using Nabadat.KpiManagement.Application.Kpis.Services;

namespace Nabadat.KpiManagement.UnitTests.Kpis;

/// <summary>
/// T029 [US1] — unit tests for <c>KpiCatalogueQuery</c> (filter + search + ordering composition),
/// covering the four spec.md US-1 Required cases (spec.md §"Unit Test Coverage" for US-1).
/// <para>
/// Contract these tests pin for the implementer (T035 / T033):
/// <list type="bullet">
///   <item><c>KpiCatalogueQuery.Build(IQueryable&lt;KpiDefinition&gt; source, KpiTypeFilter type, bool activeOnly, string? search)</c>
///   returns the filtered + canonically ordered sequence (no pagination applied at this layer).</item>
///   <item><c>KpiTypeFilter</c> is the <c>All | Standard | Custom</c> filter enum from kpi-api.md
///   §"GET /api/v1/kpis" (the <c>type</c> query param).</item>
/// </list>
/// Standards sort in canonical order [NPS, CSAT, CES, CXI, FCR, VFM, AgentScore, CHS], then
/// customs by <c>created_at DESC</c> (kpi-api.md ordering rule / research.md R7).
/// </para>
/// </summary>
public sealed class KpiCatalogueQueryTests
{
    private static readonly string[] CanonicalStandardOrder =
        ["NPS", "CSAT", "CES", "CXI", "FCR", "VFM", "AgentScore", "CHS"];

    private static readonly DateTimeOffset SeedInstant =
        new(2026, 6, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_returns_eight_standards_in_canonical_order_when_all_and_active_only()
    {
        var source = EightStandardsAndOneInactiveCustom();

        var result = KpiCatalogueQuery
            .Build(source, KpiTypeFilter.All, activeOnly: true, search: null)
            .ToList();

        result.Should().HaveCount(8);
        result.Select(k => k.ShortName).Should().Equal(CanonicalStandardOrder);
    }

    [Fact]
    public void Build_returns_single_inactive_custom_when_custom_and_active_only_is_false()
    {
        var source = EightStandardsAndOneInactiveCustom();

        var result = KpiCatalogueQuery
            .Build(source, KpiTypeFilter.Custom, activeOnly: false, search: null)
            .ToList();

        result.Should().ContainSingle().Which.ShortName.Should().Be("QUAL");
    }

    [Fact]
    public void Build_matches_nps_only_when_search_is_lowercase_nps()
    {
        var source = EightStandardsAndOneInactiveCustom();

        var result = KpiCatalogueQuery
            .Build(source, KpiTypeFilter.All, activeOnly: false, search: "nps")
            .ToList();

        result.Should().ContainSingle().Which.ShortName.Should().Be("NPS");
    }

    [Fact]
    public void Build_returns_all_rows_when_search_is_whitespace_only()
    {
        var source = EightStandardsAndOneInactiveCustom();

        var result = KpiCatalogueQuery
            .Build(source, KpiTypeFilter.All, activeOnly: false, search: "  ")
            .ToList();

        result.Should().HaveCount(9);
    }

    // Eight active standard KPIs (deliberately inserted out of canonical order to prove the
    // query sorts them) plus one inactive custom KPI ("QUAL").
    private static IQueryable<KpiDefinition> EightStandardsAndOneInactiveCustom() =>
        new List<KpiDefinition>
        {
            Standard("CSAT", "Customer Satisfaction Score", Scale.Scale1_5),
            Standard("CHS", "Customer Happiness Score", Scale.Scale1_5),
            Standard("NPS", "Net Promoter Score", Scale.Scale0_10, CalculationMethod.NPSStandard),
            Standard("AgentScore", "Agent Score", Scale.Scale1_5),
            Standard("CES", "Customer Effort Score", Scale.Scale1_7),
            Standard("FCR", "First Contact Resolution", Scale.Scale1_3),
            Composite("CXI", "Customer Experience Index"),
            Standard("VFM", "Value for Money", Scale.Scale1_5),
            InactiveCustom("QUAL", "Service Quality"),
        }.AsQueryable();

    private static KpiDefinition Standard(
        string shortName,
        string fullName,
        Scale scale,
        CalculationMethod method = CalculationMethod.WeightedAverage) => new()
        {
            Id = Guid.NewGuid(),
            ShortName = shortName,
            FullName = fullName,
            KpiType = KpiType.Standard,
            IsComposite = false,
            CalculationMethod = method,
            Scale = scale,
            Target = 80,
            IsActive = true,
            CreatedAt = SeedInstant,
        };

    private static KpiDefinition Composite(string shortName, string fullName) => new()
    {
        Id = Guid.NewGuid(),
        ShortName = shortName,
        FullName = fullName,
        KpiType = KpiType.Standard,
        IsComposite = true,
        CalculationMethod = CalculationMethod.WeightedComposite,
        Scale = null,
        Target = 80,
        IsActive = true,
        CreatedAt = SeedInstant,
    };

    private static KpiDefinition InactiveCustom(string shortName, string fullName) => new()
    {
        Id = Guid.NewGuid(),
        ShortName = shortName,
        FullName = fullName,
        KpiType = KpiType.Custom,
        IsComposite = false,
        CalculationMethod = CalculationMethod.WeightedAverage,
        Scale = Scale.Scale1_5,
        Target = 80,
        IsActive = false,
        CreatedAt = SeedInstant.AddDays(1),
    };
}
