using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.UserManagement.Domain.Interfaces;

namespace Nabadat.UserManagement.Api.Authentication;

/// <summary>
/// ASP.NET Core authentication handler for the opaque-bearer-session scheme
/// (<see cref="PortalSessionDefaults.AuthenticationScheme"/>). It reads
/// <c>Authorization: Bearer &lt;token&gt;</c>, validates it through the published
/// <see cref="IUserManagementAuthService"/>, and on success both builds the request
/// <see cref="ClaimsPrincipal"/> AND populates the request-scoped
/// <see cref="ISessionContextAccessor"/> — so controllers can rely on either <c>[Authorize]</c>
/// (this handler challenges with 401) or, where they still need the actor, the session accessor.
/// Challenges and forbids render the shared API-05 error envelope so a 401/403 is never an empty body.
/// </summary>
public sealed class PortalSessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string BearerPrefix = "Bearer ";

    private readonly IUserManagementAuthService _authService;
    private readonly ISessionContextAccessor _sessionAccessor;

    public PortalSessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserManagementAuthService authService,
        ISessionContextAccessor sessionAccessor)
        : base(options, logger, encoder)
    {
        _authService = authService;
        _sessionAccessor = sessionAccessor;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.Ordinal))
        {
            // No bearer credentials — anonymous. [Authorize] turns this into a 401 challenge.
            return AuthenticateResult.NoResult();
        }

        var token = header[BearerPrefix.Length..].Trim();
        if (token.Length == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var session = await _authService.ValidateSessionTokenAsync(token, Context.RequestAborted);
        if (session is null)
        {
            return AuthenticateResult.Fail("Invalid or expired session token.");
        }

        // Keep the legacy accessor populated so existing controllers/services that read the actor
        // from the session continue to work unchanged.
        _sessionAccessor.Current = session;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim("persona", session.Persona),
            new Claim("session_id", session.SessionId.ToString()),
        };
        // Passing the scheme as the authentication type makes Identity.IsAuthenticated true.
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Response.WriteAsJsonAsync(Envelope("auth.required", "Authentication required."));
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Response.WriteAsJsonAsync(Envelope("permission.denied", "You do not have permission to perform this action."));
    }

    private ApiErrorEnvelope Envelope(string code, string message) => new()
    {
        Error = new ApiErrorDetail
        {
            Code = code,
            Message = message,
            CorrelationId = Context.TraceIdentifier,
        },
    };
}
