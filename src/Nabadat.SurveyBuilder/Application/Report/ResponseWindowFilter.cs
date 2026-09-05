namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// T236 [US8] — decides whether a response falls inside the survey's active-period window (FR-13.6,
/// BR-3.1). The live report reflects only responses collected within the active period; a response
/// submitted after the period elapsed (<c>sentAt + activePeriod</c>) is excluded — it lives in the
/// M-07 post-expiry store instead, never in the live report. Pure predicate; unit-tested by
/// <c>ResponseWindowFilterTests</c> (T230).
/// </summary>
public sealed class ResponseWindowFilter
{
    /// <summary>
    /// <c>true</c> when <paramref name="submittedAt"/> is at or before <c>sentAt + activePeriod</c>
    /// (the expiry boundary is inclusive); <c>false</c> once the active period has elapsed.
    /// </summary>
    public bool Include(DateTimeOffset submittedAt, DateTimeOffset sentAt, TimeSpan activePeriod) =>
        submittedAt <= sentAt + activePeriod;
}
