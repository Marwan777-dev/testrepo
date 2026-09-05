namespace Nabadat.SurveyBuilder.Application.Interfaces;

/// <summary>
/// A stored response snapshot for an <c>Idempotency-Key</c> (APIs-constitution Article 7.1). The
/// <see cref="RequestHash"/> pins the key to the original request payload so a reuse of the same
/// key with a different body is detected and rejected rather than silently replayed.
/// </summary>
/// <param name="RequestHash">Hash of the original request (method + path + body).</param>
/// <param name="StatusCode">The captured HTTP status to replay.</param>
/// <param name="ContentType">The captured response content type.</param>
/// <param name="Body">The captured response body to replay verbatim.</param>
public sealed record IdempotencyRecord(
    string RequestHash,
    int StatusCode,
    string ContentType,
    string Body);
