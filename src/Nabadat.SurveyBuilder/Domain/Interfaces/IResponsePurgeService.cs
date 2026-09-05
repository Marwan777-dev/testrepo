namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// Cross-module port M-01 consumes from <b>M-04 (Response Collection)</b> to hard-purge every
/// response for a survey during a destructive Return-to-Draft (BR-1.6, research.md §4.5). Invoked
/// after the M-01 status transition commits; a failure triggers M-01 compensation.
/// <para><b>New port — declared here per T021;</b> the concrete implementation is supplied by M-04
/// (which does not exist under <c>src/</c> yet) and wired in the host composition root. Until then
/// no runtime path resolves it and the destructive Return-to-Draft endpoint returns 501
/// <c>survey.return_to_draft.purge_service_unavailable</c> (see TODO-M01-001).</para>
/// </summary>
public interface IResponsePurgeService
{
    /// <summary>
    /// Hard-deletes every response (live + M-07 post-expiry) for <paramref name="surveyId"/> and
    /// invalidates in-flight sessions, returning the number of responses purged (for the audit trail).
    /// </summary>
    Task<int> PurgeSurveyResponsesAsync(Guid surveyId, Guid actorId, Guid correlationId, CancellationToken ct = default);
}
