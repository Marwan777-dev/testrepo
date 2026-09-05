using FluentAssertions;
using Nabadat.KpiManagement.Application.Catalogue;
using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Xunit;

namespace Nabadat.KpiManagement.UnitTests.Kpis;

/// <summary>
/// T030 [US1] — unit tests for <c>KpiListItemMapper</c> (KpiDefinition → list-row DTO),
/// covering the two spec.md US-1 Required cases: NPS mapping
/// (<c>CalculationMethodLabel="NPS Standard"</c>, <c>ScaleLabel="0–10"</c>) and the composite
/// case (<c>ScaleLabel="—"</c>).
/// <para>
/// Contract pinned for the implementer (T034): <c>KpiListItemMapper.Map(KpiDefinition)</c> returns
/// a <c>KpiListItemDto</c> whose <c>CalculationMethodLabel</c> / <c>ScaleLabel</c> are the
/// human-readable strings from kpi-api.md §"GET /api/v1/kpis" (en-dash scale labels; <c>"—"</c>
/// for composite KPIs whose scale is NULL).
/// </para>
/// </summary>
public sealed class KpiListItemMapperTests
{
    [Fact]
    public void Map_produces_nps_labels_when_mapping_the_nps_definition()
    {
        var nps = new KpiDefinition
        {
            Id = Guid.NewGuid(),
            ShortName = "NPS",
            FullName = "Net Promoter Score",
            KpiType = KpiType.Standard,
            IsComposite = false,
            CalculationMethod = CalculationMethod.NPSStandard,
            Scale = Scale.Scale0_10,
            Target = 50,
            IsActive = true,
        };

        var row = KpiListItemMapper.Map(nps);

        row.ShortName.Should().Be("NPS");
        row.CalculationMethodLabel.Should().Be("NPS Standard");
        row.ScaleLabel.Should().Be("0–10");
    }

    [Fact]
    public void Map_renders_dash_scale_label_when_definition_is_composite()
    {
        var cxi = new KpiDefinition
        {
            Id = Guid.NewGuid(),
            ShortName = "CXI",
            FullName = "Customer Experience Index",
            KpiType = KpiType.Standard,
            IsComposite = true,
            CalculationMethod = CalculationMethod.WeightedComposite,
            Scale = null,
            Target = 80,
            IsActive = true,
        };

        var row = KpiListItemMapper.Map(cxi);

        row.ScaleLabel.Should().Be("—");
    }
}
