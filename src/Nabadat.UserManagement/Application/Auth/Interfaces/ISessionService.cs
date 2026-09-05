using Nabadat.UserManagement.Application.Auth.Dtos;
using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Application.Auth.Interfaces;

/// <summary>Creates and validates authenticated sessions (opaque token + permission snapshot).</summary>
public interface ISessionService
{
    /// <summary>Builds a permission snapshot, issues an opaque token, and persists a new session.</summary>
    Task<SessionCreationResult> CreateSessionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Validates a raw session token: returns the <see cref="SessionContext"/> when the
    /// session is active and within both the sliding and absolute TTLs (refreshing the
    /// snapshot on a version mismatch), or <c>null</c> otherwise.
    /// </summary>
    Task<SessionContext?> ValidateSessionAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Invalidates a session (logout / admin revoke); publishes <c>session.revoked</c>.</summary>
    Task InvalidateSessionAsync(Guid sessionId, CancellationToken ct = default);
}
