namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// Raw per-channel funnel counts for one delivery channel, current period plus the comparable
/// previous period (FR-14.4), fed to <see cref="ChannelBreakdownCalculator"/>. When the survey had no
/// previous period for this channel, <see cref="PriorSent"/>/<see cref="PriorFinished"/> are
/// <c>null</c> and the channel's delta is suppressed (FR-14.5).
/// </summary>
/// <param name="Channel">The delivery channel key (e.g. <c>email</c>, <c>whatsapp</c>, <c>web</c>).</param>
/// <param name="Sent">Sends on this channel in the current window.</param>
/// <param name="Finished">Completed responses on this channel in the current window.</param>
/// <param name="PriorSent">Sends on this channel in the previous window, or <c>null</c> when absent.</param>
/// <param name="PriorFinished">Completed responses on this channel in the previous window, or <c>null</c>.</param>
public sealed record ChannelCounts(
    string Channel,
    long Sent,
    long Finished,
    long? PriorSent,
    long? PriorFinished);
