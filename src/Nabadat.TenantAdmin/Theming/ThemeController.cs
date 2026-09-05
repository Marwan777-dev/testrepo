using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nabadat.TenantAdmin.Theming;

/// <summary>
/// Tenant theming endpoint. The brand seed is served from the <c>tenant-themes.json</c> file
/// (subdomain → colors, via <see cref="TenantThemeProvider"/>); a subdomain with no entry falls
/// back to the in-code default. No database / data-layer involvement.
///
/// Anonymous on purpose: the SPA fetches the theme on boot, BEFORE login, to paint the login page
/// in the tenant brand. The subdomain is read straight off the request (X-Forwarded-Host, set by
/// the dev/prod proxy, then the Host header). Rate-limited (the "theme-current" policy) because it
/// is anonymous and reachable pre-auth.
/// </summary>
[ApiController]
[AllowAnonymous]
//[EnableRateLimiting(ThemeRateLimit.Policy)] to be enabled later
[Route("api/theme")]
public sealed class ThemeController(TenantThemeProvider themes) : ControllerBase
{
    private const string DefaultSlug = "nabadat";

    /// <summary>The 6-color seed + slug for the request's tenant (resolved from the subdomain).</summary>
    [HttpGet("current")]
    public IActionResult Current()
    {
        var slug = TenantFromRequest(Request);
        var colors = themes.Resolve(slug, out var isDefault);

        // Default coloring -> report the "nabadat" slug so the SPA keeps the pinned index.css verbatim;
        // a mapped tenant reports its own slug so the SPA derives the CSS from these colors.
        var reportedSlug = isDefault ? DefaultSlug : slug!;

        return Ok(new
        {
            slug = reportedSlug,
            primary = colors.Primary,
            secondary = colors.Secondary,
            neutral = colors.Neutral,
            sidebar = colors.Sidebar,
            accent = colors.Accent,
            background = colors.Background,
        });
    }

    // Resolve the tenant subdomain from the request. Prefers X-Forwarded-Host (set by the Vite dev
    // proxy / prod reverse proxy via xfwd) and falls back to the Host header. Bare host / IP / www /
    // app / nabadat -> null (the in-code default theme applies).
    private static string? TenantFromRequest(HttpRequest req)
    {
        var raw = req.Headers["X-Forwarded-Host"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw)) raw = req.Host.Host;
        var host = raw.Split(',')[0].Trim().Split(':')[0];
        if (System.Net.IPAddress.TryParse(host, out _)) return null;
        var labels = host.Split('.');
        if (labels.Length < 2) return null;
        var first = labels[0].ToLowerInvariant();
        return first is "www" or "app" or "nabadat" ? null : first;
    }
}
