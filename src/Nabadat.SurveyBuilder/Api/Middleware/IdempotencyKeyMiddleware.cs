using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Middleware;

/// <summary>
/// Honours the <c>Idempotency-Key</c> header on sensitive writes (APIs-constitution Article 7.1;
/// the M-01 endpoint subset is enumerated in research.md §9). On a first call it captures the
/// response and stores it under the key with a 24-hour TTL; on a repeat within the window it
/// replays the stored response verbatim (no re-execution ⇒ no double-audit). Reusing the same key
/// with a different request payload is rejected with 409 <c>idempotency.key_reuse</c>.
/// </summary>
public sealed class IdempotencyKeyMiddleware
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly RequestDelegate _next;

    public IdempotencyKeyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key) || !IsWrite(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var requestHash = await ComputeRequestHashAsync(context.Request);

        var existing = await store.TryGetAsync(key, context.RequestAborted);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                throw new SurveyBuilderException(
                    "idempotency.key_reuse", StatusCodes.Status409Conflict,
                    "This Idempotency-Key was already used with a different request payload.");
            }

            await ReplayAsync(context, existing);
            return;
        }

        // First call: buffer the response so it can be captured, then copied back to the client.
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await _next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;
        var responseBody = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync(context.RequestAborted);
        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody, context.RequestAborted);

        // Only successful responses are replayable — a failed write should be retryable afresh.
        if (context.Response.StatusCode is >= 200 and < 300)
        {
            var record = new IdempotencyRecord(
                requestHash,
                context.Response.StatusCode,
                context.Response.ContentType ?? "application/json",
                responseBody);
            await store.SaveAsync(key, record, Ttl, context.RequestAborted);
        }
    }

    private static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static async Task<string> ComputeRequestHashAsync(HttpRequest request)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(request.HttpContext.RequestAborted);
        request.Body.Position = 0;

        var material = $"{request.Method}\n{request.Path}{request.QueryString}\n{body}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash);
    }

    private static async Task ReplayAsync(HttpContext context, IdempotencyRecord record)
    {
        context.Response.StatusCode = record.StatusCode;
        context.Response.ContentType = record.ContentType;
        await context.Response.WriteAsync(record.Body, context.RequestAborted);
    }
}
