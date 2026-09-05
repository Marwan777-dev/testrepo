namespace Nabadat.IntegrationHub.Domain.Interfaces;

/// <summary>
/// Resolves <b>which</b> survey applies for a service channel plus the transaction parameters received.
/// M-02's rules own this decision for all five scenarios (BR-19); M-13 only asks the question.
///
/// <para><b>M-13-owned consumer-side port</b> (contracts/published-interfaces.md § M-02, research.md
/// §4.3). M-02 does not exist in this repo yet, so the default binding is
/// <c>NullSurveyResolutionReader</c> — the same dependency-inversion stub pattern M-15's
/// <c>IKpiScoreReader</c> and M-01's <c>IChannelSurveyRulesReader</c> already established. Swapping in the
/// real adapter when M-02 ships is a one-line host registration change with no consumer edits
/// (coordination-log.md C-01).</para>
/// </summary>
public interface ISurveyResolutionReader
{
    /// <summary>
    /// Returns the resolved survey id, or <c>null</c> when no survey resolves. A <c>null</c> is surfaced
    /// as a blocking internal error (<c>E-1500</c>) — <b>never</b> a silent default or a guessed survey.
    /// </summary>
    Task<Guid?> ResolveSurveyIdAsync(
        Guid serviceChannelId,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default);
}
