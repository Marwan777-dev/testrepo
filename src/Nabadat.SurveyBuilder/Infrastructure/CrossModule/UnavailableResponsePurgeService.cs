using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.CrossModule;

/// <summary>
/// Placeholder <see cref="IResponsePurgeService"/> until M-04 ships the port (T021, TODO-M01-001).
/// Fails loudly with the documented 501 <c>survey.return_to_draft.purge_service_unavailable</c> so a
/// destructive Return-to-Draft is refused rather than silently skipping the purge; the caller
/// (<c>DestructiveReturnToDraftService</c>) compensates by reverting the status. Swap for the real
/// M-04 adapter in the host when M-04 lands.
/// </summary>
public sealed class UnavailableResponsePurgeService : IResponsePurgeService
{
    public Task<int> PurgeSurveyResponsesAsync(Guid surveyId, Guid actorId, Guid correlationId, CancellationToken ct = default) =>
        throw new SurveyBuilderException(
            "survey.return_to_draft.purge_service_unavailable", 501,
            "Response purge (M-04) is not available yet; destructive Return-to-Draft is temporarily disabled.");
}
