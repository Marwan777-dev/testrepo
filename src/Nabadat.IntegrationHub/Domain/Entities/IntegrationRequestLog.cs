namespace Nabadat.IntegrationHub.Domain.Entities;

/// <summary>
/// One inbound request and its outcome — immutable, append-only, and <b>DB-04 monthly-partitioned</b>
/// on <see cref="Timestamp"/> (data-model.md §8). Backs SCR-08's investigation view. Retention is 90
/// days (NFR-8) enforced by detaching old partitions, not row-level deletes, so the table's PK is
/// <c>(id, timestamp)</c> — a partitioned table's key must include its partition column.
/// </summary>
public sealed class IntegrationRequestLog
{
    public Guid Id { get; set; }

    /// <summary>
    /// FK → <see cref="Integration"/>, <b>nullable</b>: an auth-rejected request can fail before the
    /// integration or credential is even resolved, and that request must still be logged.
    /// </summary>
    public Guid? IntegrationId { get; set; }

    /// <summary>UTC. Also the partition key.</summary>
    public DateTimeOffset Timestamp { get; set; }

    public string Method { get; set; } = string.Empty;

    /// <summary>Logged exactly as received (the documented paths in FR-F0-01 are illustrative).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>The scenario's wire value, or <c>null</c> when the request was rejected before scenario resolution.</summary>
    public string? Scenario { get; set; }

    /// <summary>
    /// <c>jsonb</c> — <b>all</b> parameters received, registered and unregistered alike, stored
    /// <b>raw</b>. PII (mobile, email, customer name) is masked only at display/export time, never at
    /// write time (FR-S8-03): the raw value must stay usable for reprocessing and audit, so masking is
    /// strictly a read-side concern handled by <c>PiiMaskingFormatter</c> (US5).
    /// </summary>
    public string ParametersReceived { get; set; } = "{}";

    /// <summary><c>jsonb</c> — the full response body: status, result code, request id, message, or the scenario's artifact.</summary>
    public string ResponseReturned { get; set; } = "{}";

    public int HttpStatus { get; set; }

    /// <summary>
    /// The normative wire code exactly as returned to the caller — <c>E-1001</c>…<c>E-1500</c>,
    /// <c>202</c>, or <c>200</c> (FR-F0-03). Deliberately a string, not the
    /// <c>ResultCode</c> enum: a log row must stay readable and stable independently of the enum.
    /// </summary>
    public string ResultCode { get; set; } = string.Empty;

    public int LatencyMs { get; set; }

    /// <summary>
    /// Denormalised copy of the credential's label at request time, so a later revocation or
    /// regeneration cannot rewrite history.
    /// </summary>
    public string? CredentialLabel { get; set; }

    /// <summary>
    /// e.g. <c>"authentication"</c> — populated only when the request was rejected <i>before</i>
    /// parameter parsing. Drives SCR-08's "request rejected before parameter parsing" detail notice
    /// (AC-S8-03).
    /// </summary>
    public string? RejectionStage { get; set; }
}
