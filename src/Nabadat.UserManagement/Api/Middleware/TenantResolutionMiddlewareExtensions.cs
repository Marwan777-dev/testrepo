using Microsoft.AspNetCore.Builder;

namespace Nabadat.UserManagement.Api.Middleware;

/// <summary>Pipeline registration for <see cref="TenantResolutionMiddleware"/>.</summary>
public static class TenantResolutionMiddlewareExtensions
{
    /// <summary>
    /// Adds subdomain-based tenant resolution (AD-07). Place it BEFORE
    /// <c>UseAuthentication</c> so the tenant schema is bound before the bearer token is validated
    /// against it. Only call this in multi-tenant mode (<c>ENABLE_MULTI_TENANT=true</c>).
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
