namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// A resolved headline KPI gauge (FR-13.1/13.2): the current <see cref="Value"/>, its
/// <see cref="Target"/> (null when the catalogue sets none), and the period-over-period
/// <see cref="DeltaPp"/> (null when there is no previous-period data — FR-14.5). Application-layer
/// result mapped to the wire <c>HeadlineKpi</c> by the Api layer.
/// </summary>
public sealed record ReportHeadlineKpi(decimal Value, decimal? Target, decimal? DeltaPp);
