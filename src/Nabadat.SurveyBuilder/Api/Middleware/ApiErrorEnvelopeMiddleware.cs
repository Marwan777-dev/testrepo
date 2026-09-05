using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Middleware;

/// <summary>
/// Catches M-01 exceptions and renders the API-05 envelope
/// <c>{"error":{"code","message","correlation_id","tenant_id","details?"}}</c> (research.md §9). A
/// <see cref="SurveyBuilderException"/> maps to its own <c>Code</c>/<c>StatusCode</c>/<c>Details</c>;
/// any other exception is a 500 <c>survey_builder.internal_error</c> (message not leaked). Placed
/// first among the M-01 middleware so it wraps idempotency, ETag, and handler execution.
/// </summary>
public sealed class ApiErrorEnvelopeMiddleware
{
    private static readonly JsonSerializerOptions ErrorJson =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiErrorEnvelopeMiddleware> _logger;

    public ApiErrorEnvelopeMiddleware(RequestDelegate next, ILogger<ApiErrorEnvelopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenant currentTenant)
    {
        try
        {
            await _next(context);
        }
        catch (SurveyBuilderException ex)
        {
            await WriteAsync(context, ex.StatusCode, ex.Code, ex.Message, currentTenant, ex.Details);
        }
        catch (Exception ex)
        {
            // Never leak an unexpected exception's message across the API boundary.
            _logger.LogError(ex, "Unhandled M-01 exception on {Method} {Path}.", context.Request.Method, context.Request.Path);
            await WriteAsync(
                context, StatusCodes.Status500InternalServerError, "survey_builder.internal_error",
                "An unexpected error occurred.", currentTenant, details: null);
        }
    }

    private static async Task WriteAsync(
        HttpContext context, int status, string code, string message,
        ICurrentTenant currentTenant, IReadOnlyDictionary<string, object>? details)
    {
        // The response may have already started streaming (e.g. an exception mid-body); nothing
        // safe to write then — let it surface as a truncated response rather than corrupt headers.
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var payload = new ApiErrorEnvelope
        {
            Error = new ApiErrorDetail
            {
                Code = code,
                Message = message,
                CorrelationId = context.TraceIdentifier,
                TenantId = currentTenant.IsResolved ? currentTenant.TenantId.ToString() : null,
                Details = details,
            },
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, ErrorJson), context.RequestAborted);
    }
}
