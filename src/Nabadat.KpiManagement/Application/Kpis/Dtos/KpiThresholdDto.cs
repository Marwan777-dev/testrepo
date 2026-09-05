namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// Read projection of a KPI's performance-band thresholds (returned via <c>IKpiConfigReader</c>).
/// The four values are strictly ascending; the <c>[LowerBound, X)</c> band is unsatisfactory,
/// <c>[X, Y)</c> average, <c>[Y, UpperBound]</c> satisfactory.
/// </summary>
public record KpiThresholdDto(
    decimal LowerBound,
    decimal X,
    decimal Y,
    decimal UpperBound);
