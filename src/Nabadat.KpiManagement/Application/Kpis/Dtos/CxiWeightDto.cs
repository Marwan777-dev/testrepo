namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// Read projection of one CXI member weighting. <see cref="Weight"/> is the relative integer the
/// tenant entered; <see cref="EffectivePercentage"/> is the derived share of the composite (the
/// weights normalised to sum 100), computed at read time — never stored.
/// </summary>
public record CxiWeightDto(
    Guid MemberKpiId,
    string MemberShortName,
    int Weight,
    decimal EffectivePercentage);
