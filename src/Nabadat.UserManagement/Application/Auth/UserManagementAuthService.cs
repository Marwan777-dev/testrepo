using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;

using Nabadat.UserManagement.Application.Auth.Interfaces;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>
/// Published <see cref="IUserManagementAuthService"/> implementation — a thin delegate over
/// <see cref="ISessionService.ValidateSessionAsync"/>. This is the only surface
/// other modules and the host pipeline use to authenticate a request (AD-01).
/// </summary>
public sealed class UserManagementAuthService : IUserManagementAuthService
{
    private readonly ISessionService _sessions;

    public UserManagementAuthService(ISessionService sessions) => _sessions = sessions;

    public Task<SessionContext?> ValidateSessionTokenAsync(string token, CancellationToken ct = default) =>
        _sessions.ValidateSessionAsync(token, ct);
}
