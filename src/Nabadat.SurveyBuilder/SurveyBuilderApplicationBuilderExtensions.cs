using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Nabadat.SurveyBuilder.Api.Middleware;

namespace Nabadat.SurveyBuilder;

/// <summary>
/// Pipeline registration for the M-01 middleware (T026). Companion to
/// <see cref="SurveyBuilderServiceCollectionExtensions"/> (which registers the services these
/// middleware resolve) — pipeline ordering needs an <see cref="IApplicationBuilder"/>, so it lives
/// here and is called from the host <c>Program.cs</c>, mirroring M-10's <c>UseTenantResolution</c>.
/// </summary>
public static class SurveyBuilderApplicationBuilderExtensions
{
    // M-01 owns these route prefixes (research.md §9). The middleware are branched onto them so
    // they never wrap other modules' requests (e.g. M-01's error-envelope must not reshape an
    // M-10 error).
    private static readonly string[] M01Prefixes = ["/api/v1/surveys", "/api/v1/templates"];

    /// <summary>
    /// Adds the M-01 middleware in the order fixed by T026: the host's correlation-id and
    /// tenant-context middleware run first (already in the pipeline before this call), then
    /// <b>error-envelope → idempotency-key → etag</b>. Call it AFTER <c>UseTenantResolution</c> and
    /// before <c>UseAuthentication</c> so the envelope also wraps auth failures on M-01 routes.
    /// </summary>
    public static IApplicationBuilder UseSurveyBuilderModule(this IApplicationBuilder app) =>
        app.UseWhen(
            static ctx => M01Prefixes.Any(p =>
                ctx.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)),
            static branch =>
            {
                branch.UseMiddleware<ApiErrorEnvelopeMiddleware>();
                branch.UseMiddleware<IdempotencyKeyMiddleware>();
                branch.UseMiddleware<EtagMiddleware>();
            });
}
