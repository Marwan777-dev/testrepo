using Nabadat.UserManagement.Application.Auth.Exceptions;

namespace Nabadat.UserManagement.Application.Auth.Interfaces;

/// <summary>Enforces the per-email self-service password-reset request limit (sliding window).</summary>
public interface IPasswordResetRateLimiter
{
    /// <summary>
    /// Records a reset request for <paramref name="email"/> and throws
    /// <see cref="PasswordResetRateLimitExceededException"/> if the window limit is exceeded.
    /// </summary>
    Task EnsureWithinLimitAsync(string email, CancellationToken ct = default);
}
