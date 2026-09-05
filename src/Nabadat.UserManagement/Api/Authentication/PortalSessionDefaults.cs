namespace Nabadat.UserManagement.Api.Authentication;

/// <summary>
/// Constants for the M-10 opaque-bearer-session authentication scheme. The scheme name is the
/// host's default authentication AND challenge scheme, so <c>[Authorize]</c> on any controller
/// challenges through <see cref="PortalSessionAuthenticationHandler"/> (401 + API-05 envelope).
/// </summary>
public static class PortalSessionDefaults
{
    /// <summary>The authentication scheme name registered via <c>AddAuthentication</c>.</summary>
    public const string AuthenticationScheme = "PortalSession";
}
