using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// M-01 published reader (constitution AD-01) consumed by <b>M-04</b> to enforce the active-period
/// lifecycle before accepting a response. See <c>contracts/published-interface.md</c>.
/// </summary>
public interface IActiveSurveyReader
{
    /// <summary>
    /// Returns whether the survey is currently Active AND within its active period, as of
    /// <paramref name="asOf"/>. M-04 uses this at response-submission time to enforce BR-3.4
    /// (before-start refuse) and the tenant <c>post_expiry_feedback_collection</c> handling (Q5 —
    /// M-04 reads that setting live from M-11 and combines it with the returned state).
    /// <list type="bullet">
    ///   <item><c>Status == Active</c> AND (<c>asOf &lt; ExpiresAt</c> OR <c>ExpiresAt is null</c>)
    ///   → accept the response.</item>
    ///   <item>Otherwise M-04 handles rejection / post-expiry routing per BR-3.1.</item>
    /// </list>
    /// </summary>
    Task<ActiveSurveyState> GetStateAsync(SurveyId surveyId, DateTimeOffset asOf, CancellationToken ct);
}

/// <summary>
/// The lifecycle snapshot M-04 needs to decide acceptance: the survey's <see cref="SurveyStatus"/>,
/// when it was activated, and when it expires (<c>null</c> ⇒ never auto-expires, FR-3.4).
/// </summary>
public sealed record ActiveSurveyState(
    SurveyStatus Status,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? ExpiresAt);
