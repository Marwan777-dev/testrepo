namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// Read projection of a single KPI perspective (sub-dimension), ordered by
/// <see cref="DisplayOrder"/>. <see cref="Id"/> is the stable identifier M-01 question bindings
/// reference.
/// </summary>
public record KpiPerspectiveDto(
    Guid Id,
    string Label,
    short DisplayOrder);
