using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Nabadat.SurveyBuilder.Application.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Middleware;

/// <summary>
/// Optimistic-concurrency plumbing for M-01 aggregate endpoints (research.md §2/§9). On ingress it
/// parses a weak <c>If-Match: W/"&lt;n&gt;"</c> into the scoped <see cref="ICurrentETag.IfMatch"/>
/// (a write handler compares it to the aggregate's <c>row_version</c> and, on a mismatch, throws an
/// <c>&lt;aggregate&gt;.conflict</c> error — 412 Precondition Failed — which the error-envelope
/// middleware renders per API-05). On egress it stamps <c>ETag: W/"&lt;n&gt;"</c> from
/// <see cref="ICurrentETag.ResponseRowVersion"/> when a handler set it (aggregate reads/writes only;
/// collection endpoints leave it null and carry no ETag).
/// </summary>
public sealed class EtagMiddleware
{
    private readonly RequestDelegate _next;

    public EtagMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICurrentETag currentETag)
    {
        currentETag.IfMatch = ParseWeakETag(context.Request.Headers.IfMatch);

        // Stamp the fresh ETag just before headers flush, once the handler has set the persisted
        // row_version. OnStarting runs even when the handler short-circuits the body.
        context.Response.OnStarting(() =>
        {
            if (currentETag.ResponseRowVersion is { } version)
            {
                context.Response.Headers.ETag = new EntityTagHeaderValue($"\"{version}\"", isWeak: true).ToString();
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }

    /// <summary>
    /// Parses the integer revision out of a weak ETag header (<c>W/"12"</c> → <c>12</c>). Returns
    /// null for an absent, malformed, or <c>*</c> value — the handler then treats it as
    /// "no precondition supplied".
    /// </summary>
    private static int? ParseWeakETag(Microsoft.Extensions.Primitives.StringValues headerValue)
    {
        var raw = headerValue.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digits = raw.AsSpan().Trim();
        // Strip the weak prefix and surrounding quotes: W/"12" → 12.
        if (digits.StartsWith("W/"))
        {
            digits = digits[2..];
        }

        digits = digits.Trim('"');
        return int.TryParse(digits, out var version) ? version : null;
    }
}
