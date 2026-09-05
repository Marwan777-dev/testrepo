namespace Nabadat.TenantAdmin.Theming;

/// <summary>
/// Names the rate-limiter policy applied to the anonymous, pre-auth theming endpoint.
/// Shared between the policy registration (Program.cs) and the <see cref="ThemeController"/>
/// <c>[EnableRateLimiting]</c> attribute so the two never drift.
/// </summary>
public static class ThemeRateLimit
{
    public const string Policy = "theme-current";
}
