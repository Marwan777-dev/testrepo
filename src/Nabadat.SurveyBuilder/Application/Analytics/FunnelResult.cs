namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// The derived funnel metrics for one period (FR-14.2), produced by <see cref="FunnelCalculator"/>
/// from a <see cref="FunnelCounts"/>. Two families: each stage as a <b>% of Sent</b>
/// (<see cref="OpenedPct"/>/<see cref="StartedPct"/>/<see cref="FinishedPct"/>) and each stage as a
/// <b>stage-to-stage conversion</b> against the preceding stage
/// (<see cref="OpenedToSent"/>/<see cref="StartedToOpened"/>/<see cref="FinishedToStarted"/>). All
/// values are percentages rounded to 2 decimal places. <see cref="FinishedPct"/> is also the survey's
/// overall completion rate.
/// </summary>
public sealed record FunnelResult(
    decimal OpenedPct,
    decimal StartedPct,
    decimal FinishedPct,
    decimal OpenedToSent,
    decimal StartedToOpened,
    decimal FinishedToStarted);
