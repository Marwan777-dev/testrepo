namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// The derived per-channel breakdown for one channel (FR-14.4), produced by
/// <see cref="ChannelBreakdownCalculator"/>: its send count, its completion rate as a percentage
/// (2 dp), and the deviation of that rate vs the previous period in percentage points
/// (<c>null</c> when there is no comparable prior period).
/// </summary>
public sealed record ChannelBreakdownResult(
    string Channel,
    long Sent,
    decimal CompletionRate,
    decimal? Delta);
