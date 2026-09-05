using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Domain.Interfaces;

/// <summary>
/// <b>Published interface (AD-01).</b> The single contract other modules and the
/// host request pipeline use to authenticate a request. No consumer references
/// M-10 concrete types — only this interface and <see cref="SessionContext"/>.
/// </summary>
public interface IUserManagementAuthService
{
    /// <summary>
    /// Validates an opaque session token (the raw <c>nbd_</c>-prefixed string) and
    /// returns the authenticated <see cref="SessionContext"/>, or <c>null</c> if the
    /// token is unknown, inactive, or expired.
    /// </summary>
    Task<SessionContext?> ValidateSessionTokenAsync(string token, CancellationToken ct = default);
}
