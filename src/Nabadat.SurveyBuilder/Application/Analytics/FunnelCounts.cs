namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// The four raw funnel stage counts for a survey over one period (F14, FR-14.2), produced by the ES
/// aggregator (T260) and fed to <see cref="FunnelCalculator"/>. Absolute counts only — every derived
/// percentage and conversion is computed by the calculator, never stored here.
/// </summary>
/// <param name="Sent">Surveys dispatched in the window.</param>
/// <param name="Opened">Recipients who opened the survey.</param>
/// <param name="Started">Recipients who answered at least one question.</param>
/// <param name="Finished">Recipients who submitted a completed response.</param>
public sealed record FunnelCounts(long Sent, long Opened, long Started, long Finished);
