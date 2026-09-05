namespace Nabadat.IntegrationHub.Domain.Interfaces;

/// <summary>
/// Forwards an SCN-05 payload — transaction details plus the survey response — for validation, dedup, and
/// storage (CMC-03). <b>M-04 MUST save every payload this call succeeds with, unconditionally</b>: there
/// is no discretionary rejection path (Clarifications 2026-07-27, SC-016).
///
/// <para><b>M-13-owned consumer-side port</b> (contracts/published-interfaces.md § M-04, research.md
/// §4.4). Default binding is <c>NullResponseIngestionGateway</c> until M-04 ships (coordination-log.md
/// C-02).</para>
///
/// <para><b>Not</b> this port's job: the respondent-facing, unauthenticated, origin-checked rendering
/// surface that SCN-04's embed URL points at. That surface is not built by M-13 at all — it belongs to
/// M-04 or a dedicated survey-renderer frontend, tracked under the same C-02 item.</para>
/// </summary>
public interface IResponseIngestionGateway
{
    /// <summary>
    /// Forwards the response for the given channel. <paramref name="transactionId"/> is the caller's own
    /// transaction identifier — the value the <c>(tenant, channelId, transaction_id)</c> idempotency key is
    /// built from (BR-18), so downstream dedup can recognise a retry.
    /// </summary>
    Task ForwardResponseAsync(
        Guid serviceChannelId,
        string transactionId,
        IReadOnlyDictionary<string, string> parameters,
        object surveyResponse,
        CancellationToken ct = default);
}
