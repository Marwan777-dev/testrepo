namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// The report's three headline KPI gauges (FR-13.2). Each is <c>null</c> when the survey has no
/// contributing question of that family.
/// </summary>
public sealed record ReportHeadline(
    ReportHeadlineKpi? Csat,
    ReportHeadlineKpi? Nps,
    ReportHeadlineKpi? Ces);
