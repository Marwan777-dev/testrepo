using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.UserManagement.Api.Authorization;

/// <summary>
/// The authorization filter produced by <see cref="RequirePermissionAttribute"/>. It runs after the
/// PortalSession authentication gate, so the request-scoped <see cref="ISessionContextAccessor.Current"/>
/// is populated by the time it executes. It reads the <see cref="Domain.ValueObjects.PermissionSnapshot"/>
/// cached on the session row (AD-03 — no DB round-trip per request) and short-circuits with 403 + the
/// API-05 envelope (code <c>PERMISSION_DENIED</c>) unless the actor's grant for <paramref name="module"/>
/// includes <paramref name="mode"/>. Default-deny: a null session, an absent module, or a module held
/// without the required mode all deny.
/// </summary>
internal sealed class RequirePermissionFilter(string module, string mode, ISessionContextAccessor session)
    : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var snapshot = session.Current?.PermissionSnapshot;
        var allowed = snapshot is not null
            && snapshot.Modules.TryGetValue(module, out var modes)
            && modes.Contains(mode);

        if (allowed)
        {
            return;
        }

        context.Result = new ObjectResult(new ApiErrorEnvelope
        {
            Error = new ApiErrorDetail
            {
                Code = "PERMISSION_DENIED",
                Message = $"You do not have the required permission ({module}:{mode}) to perform this action.",
                CorrelationId = context.HttpContext.TraceIdentifier,
            },
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
