namespace Nabadat.IntegrationHub.Domain.Interfaces;

/// <summary>
/// Hands a resolved survey plus its transaction context off for delivery through the suitable channel
/// (SCN-01, CMC-01). <b>Fire-and-forget from M-13's perspective</b>: a downstream delivery failure never
/// surfaces as an M-13 API error — the caller has already been told <c>202 ACCEPTED</c>.
///
/// <para><b>M-13-owned consumer-side port</b> (contracts/published-interfaces.md § M-02, research.md
/// §4.3). Default binding is <c>NullSurveyDispatchGateway</c> until M-02 ships (coordination-log.md
/// C-01).</para>
/// </summary>
public interface ISurveyDispatchGateway
{
    /// <summary>
    /// Dispatches <paramref name="surveyId"/> for the given channel and parameters.
    /// <paramref name="requestId"/> is M-13's own request identifier, carried through so a dispatch can be
    /// traced back to its <c>integration_request_logs</c> row.
    /// </summary>
    Task DispatchAsync(
        Guid surveyId,
        Guid serviceChannelId,
        IReadOnlyDictionary<string, string> parameters,
        Guid requestId,
        CancellationToken ct = default);
}
