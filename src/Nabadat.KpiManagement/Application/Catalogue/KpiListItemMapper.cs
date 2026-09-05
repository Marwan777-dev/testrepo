using Nabadat.KpiManagement.Application.Catalogue.Dtos;
using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Domain.ValueObjects;

namespace Nabadat.KpiManagement.Application.Catalogue;

/// <summary>
/// Maps a <see cref="KpiDefinition"/> to a <see cref="KpiListItemDto"/> catalogue row, deriving the
/// <see cref="KpiListItemDto.CalculationMethodLabel"/> and <see cref="KpiListItemDto.ScaleLabel"/>
/// display strings (contracts/kpi-api.md). Composite KPIs (NULL scale) render a <c>"—"</c> scale
/// label. Pure function — no I/O.
/// </summary>
public static class KpiListItemMapper
{
    public static KpiListItemDto Map(KpiDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new KpiListItemDto
        {
            Id = definition.Id,
            ShortName = definition.ShortName,
            FullName = definition.FullName,
            KpiType = definition.KpiType.ToString(),
            IsComposite = definition.IsComposite,
            Scale = definition.Scale?.ToString(),
            CalculationMethod = definition.CalculationMethod.ToString(),
            CalculationMethodLabel = CalculationMethodLabel(definition.CalculationMethod),
            ScaleLabel = ScaleLabel(definition),
            Target = definition.Target,
            IsActive = definition.IsActive,
            ShowOnDashboard = definition.ShowOnDashboard,
            CreatedAt = definition.CreatedAt,
        };
    }

    private static string CalculationMethodLabel(CalculationMethod method) => method switch
    {
        CalculationMethod.WeightedAverage => "Weighted Average",
        CalculationMethod.TopNBox => "TOP n Box",
        CalculationMethod.NPSStandard => "NPS Standard",
        CalculationMethod.WeightedComposite => "Weighted Composite",
        _ => method.ToString(),
    };

    // En-dash (U+2013) ranges per the contract (e.g. "0–10").
    private static string ScaleLabel(KpiDefinition definition)
    {
        if (definition.IsComposite || definition.Scale is null)
        {
            return "—";
        }

        return definition.Scale switch
        {
            Scale.Scale0_10 => "0–10",
            Scale.Scale1_3 => "1–3",
            Scale.Scale1_5 => "1–5",
            Scale.Scale1_7 => "1–7",
            Scale.Scale1_10 => "1–10",
            Scale.Scale1_100 => "1–100",
            _ => "—",
        };
    }
}
