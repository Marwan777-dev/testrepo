using Nabadat.KpiManagement.Domain.ValueObjects;
using Nabadat.KpiManagement.Application.Kpis.Services;
using Nabadat.KpiManagement.Application.Kpis.Validators;

namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// The validation model for the KPI Configuration form (US-2), carrying every field the
/// cross-field <see cref="KpiDefinitionValidator"/> rules read. It is assembled by
/// <c>KpiSaveService</c> from the incoming <see cref="Domain.Entities.KpiDefinition"/> +
/// <see cref="Domain.Entities.KpiThreshold"/> plus the tenant's existing Short Names (for the
/// case-insensitive duplicate check). Kept separate from the entity so validation can run before
/// the write transaction opens, with no persistence concerns.
/// </summary>
public sealed record KpiDefinitionInput
{
    public string ShortName { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    /// <summary>True for the eight platform-seeded standard KPIs; gates the NPSStandard reservation.</summary>
    public bool IsStandard { get; init; }

    /// <summary>True only for the CXI composite KPI; gates the WeightedComposite reservation.</summary>
    public bool IsComposite { get; init; }

    public CalculationMethod CalculationMethod { get; init; }

    public short? TopNValue { get; init; }

    public Scale? Scale { get; init; }

    public RepresentationStyle? RepresentationStyle { get; init; }

    public decimal? Target { get; init; }

    public bool IsActive { get; init; }

    /// <summary>Threshold band edges — validated for the strictly-ascending invariant.</summary>
    public decimal LowerBound { get; init; }

    public decimal X { get; init; }

    public decimal Y { get; init; }

    public decimal UpperBound { get; init; }

    /// <summary>
    /// The tenant's other KPIs' Short Names (excluding this KPI on edit) — the duplicate check
    /// compares the trimmed, case-insensitive Short Name against this set.
    /// </summary>
    public IReadOnlyList<string> ExistingShortNames { get; init; } = [];
}
