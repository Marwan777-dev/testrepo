namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// A headline KPI's raw values for the report (FR-13.1/13.2): the current-period
/// <see cref="Value"/>, its <see cref="Target"/> (from the M-06 catalogue when available), and the
/// equal-length previous-period <see cref="PreviousValue"/> used to compute the delta. A <c>null</c>
/// <see cref="PreviousValue"/> suppresses the delta (FR-14.5 — no misleading <c>+0</c> placeholder).
/// </summary>
public sealed record HeadlineKpiRaw(decimal Value, decimal? Target, decimal? PreviousValue);
