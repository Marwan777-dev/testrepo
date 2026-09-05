namespace Nabadat.IntegrationHub.Domain.ValueObjects;

/// <summary>
/// Every outcome the inbound request-validation pipeline can produce — the normative result-code
/// catalogue (FR-F0-03, data-model.md §10). This enum is the pipeline's <b>internal vocabulary</b>;
/// US4's <c>ResultCodeMapper</c> (T103) maps each member to its wire code (<c>E-1001</c>…,
/// <c>202</c>, <c>200</c>), HTTP status, and exact message-copy pattern.
///
/// <para><b>Why the log column is a string, not this enum:</b>
/// <c>IntegrationRequestLog.ResultCode</c> stores the wire code <i>exactly as returned to the
/// caller</i> (<c>"E-1002"</c>, <c>"202"</c>) per data-model.md §8, so a log row stays readable and
/// stable even if this enum is later extended. Do not add a value converter that would persist the
/// member name instead.</para>
///
/// <para>The pipeline is ordered and short-circuits on the first failure (FR-F0-02) — it never
/// returns a combined or ambiguous code — so the declaration order below is the evaluation order.</para>
/// </summary>
public enum ResultCode
{
    /// <summary><c>401 E-1401 INVALID_CREDENTIALS</c> — authentication failed, or the credential was revoked/suspended.</summary>
    InvalidCredentials = 1,

    /// <summary><c>429 E-1429 RATE_LIMIT_EXCEEDED</c> — the per-integration rate limit was exceeded (NFR-4, default 100 req/s).</summary>
    RateLimitExceeded = 2,

    /// <summary><c>413 E-1413 PAYLOAD_TOO_LARGE</c> — body exceeded the 2MB cap (NFR-3), rejected before parsing.</summary>
    PayloadTooLarge = 3,

    /// <summary><c>404 E-1001 UNKNOWN_SERVICE_CHANNEL</c> — the <c>{channelId}</c> path segment resolves to no channel in this tenant.</summary>
    UnknownServiceChannel = 4,

    /// <summary><c>409 E-1004 CHANNEL_INACTIVE</c> — the channel exists but is deactivated (BR-07).</summary>
    ChannelInactive = 5,

    /// <summary><c>400 E-1002 MISSING_REQUIRED_PARAMETER</c> — a parameter the channel contract marks required is absent (BR-08).</summary>
    MissingRequiredParameter = 6,

    /// <summary><c>422 E-1003 INVALID_PARAMETER_VALUE</c> — a value failed its per-type validator or the parameter's validation rule.</summary>
    InvalidParameterValue = 7,

    /// <summary><c>202 ACCEPTED</c> — accepted for downstream processing (SCN-01, SCN-05).</summary>
    Accepted = 8,

    /// <summary><c>200 OK</c> — accepted and answered inline with the scenario's artifact (SCN-02, SCN-03, SCN-04).</summary>
    Ok = 9,

    /// <summary>
    /// <c>500 E-1500 INTERNAL_ERROR</c> — any unexpected failure, including a downstream module being
    /// unavailable. M-13 never surfaces the downstream error directly; the message tells the caller the
    /// retry is idempotent with the same <c>transaction_id</c>.
    /// </summary>
    InternalError = 10,
}
