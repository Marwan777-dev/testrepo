using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Api.Interfaces;

/// <summary>
/// Request-scoped holder for the authenticated <see cref="SessionContext"/>, set by the
/// PortalSession authentication handler (<c>PortalSessionAuthenticationHandler</c>) after it
/// validates the bearer token. Controllers and services read <see cref="Current"/> for the actor
/// (user id / persona) instead of re-parsing the token; gating an endpoint on authentication is
/// done with <c>[Authorize]</c>, not by null-checking this property. <c>null</c> means the request
/// is unauthenticated.
/// </summary>
public interface ISessionContextAccessor
{
    SessionContext? Current { get; set; }
}
