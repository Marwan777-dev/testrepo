using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Nabadat.UserManagement.Api.Accessors;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Tenancy;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Tenancy;

namespace Nabadat.UserManagement.Api.Middleware;

/// <summary>
/// Resolves the request's tenant from its subdomain (AD-07 / API-02) and freezes it
/// into the request-scoped <see cref="RequestCurrentTenant"/> — BEFORE authentication,
/// because validating the bearer token reads the tenant schema, so the schema must be
/// known first. Registered ONLY in multi-tenant mode (<c>ENABLE_MULTI_TENANT=true</c>);
/// in single-tenant mode the host serves one schema and this middleware is absent.
/// </summary>
/// <remarks>
/// Resolution is subdomain-only (the JWT-claim path of API-02 is a future addition);
/// a query/body tenant is forbidden by API-02 and never consulted. An unknown or
/// missing subdomain is rejected with an API-05 error envelope rather than silently
/// falling back to a default tenant (GP-04: no cross-tenant leakage in edge states).
/// </remarks>
public sealed class TenantResolutionMiddleware
{
    // Platform routes that are not tenant-scoped and must work without a subdomain.
    private static readonly string[] BypassPrefixes = ["/health", "/openapi", "/api/v1/dev"];

    // Null fields (a null tenant_id here, or absent details) are omitted from the envelope.
    private static readonly JsonSerializerOptions ErrorJson =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICurrentTenant currentTenant, ITenantRegistry registry)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (BypassPrefixes.Any(p => IsUnderPrefix(path, p)))
        {
            await _next(context);
            return;
        }

        // Request.Host is the real client host — set directly, or rewritten from
        // X-Forwarded-Host by ForwardedHeaders middleware when behind a (dev or prod) proxy.
        var slug = ExtractSubdomain(context.Request.Host.Host);
        if (slug is null)
        {
            await WriteErrorAsync(
                context, StatusCodes.Status400BadRequest, "tenant.subdomain_missing",
                "Request host carries no tenant subdomain. Address the tenant as {slug}.<host>.");
            return;
        }

        if (!registry.TryResolve(slug, out var info))
        {
            await WriteErrorAsync(
                context, StatusCodes.Status404NotFound, "tenant.not_found",
                $"No tenant is registered for subdomain '{slug}'.");
            return;
        }

        // Multi-tenant mode always registers RequestCurrentTenant behind ICurrentTenant.
        ((RequestCurrentTenant)currentTenant).Resolve(info.Id, slug);

        await _next(context);
    }

    /// <summary>
    /// Leading DNS label as the slug: <c>acme.nabadat.io</c> / <c>acme.localhost</c> → <c>acme</c>.
    /// Returns null for bare hosts (<c>localhost</c>), IP literals, the <c>www</c> label, and any
    /// label that is not a safe tenant slug (<see cref="TenantSlug.IsValid"/>) — the slug names a
    /// schema in a <c>SET search_path</c>, so a malformed label is rejected here rather than
    /// interpolated into SQL (GP-04 / search-path injection).
    /// </summary>
    private static string? ExtractSubdomain(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || IPAddress.TryParse(host, out _))
        {
            return null;
        }

        var labels = host.Split('.');
        if (labels.Length < 2)
        {
            return null;
        }

        var slug = labels[0].Trim().ToLowerInvariant();
        return slug is "www" || !TenantSlug.IsValid(slug) ? null : slug;
    }

    /// <summary>
    /// True when <paramref name="path"/> is the bypass route itself or a child segment of it
    /// — a boundary match, so <c>/health</c> and <c>/health/ready</c> bypass but <c>/healthz</c>
    /// does not (a plain <c>StartsWith</c> would wrongly bypass the latter).
    /// </summary>
    private static bool IsUnderPrefix(string path, string prefix) =>
        path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);

    private static async Task WriteErrorAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        // Shared API-05 envelope. tenant_id is null here — the tenant could not be resolved.
        var payload = new ApiErrorEnvelope
        {
            Error = new ApiErrorDetail
            {
                Code = code,
                Message = message,
                CorrelationId = context.TraceIdentifier,
            },
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, ErrorJson), context.RequestAborted);
    }
}
