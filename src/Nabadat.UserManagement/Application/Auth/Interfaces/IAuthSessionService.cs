using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Application.Auth.Interfaces;

/// <summary>
/// Context-holding data-access service over <c>auth_sessions</c> (EF / <c>TenantDbContext</c>),
/// replacing the raw-Npgsql <c>IAuthSessionRepository</c>. Sessions are append-only except
/// for activity bumps and invalidation. Write methods persist immediately; compose them
/// inside <c>ITenantDbContext.ExecuteAsync</c> to commit atomically with other writes.
/// </summary>
public interface IAuthSessionService
{
    Task<AuthSession?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default);

    /// <summary>Inserts a new session and saves.</summary>
    Task AddAsync(AuthSession session, CancellationToken ct = default);

    /// <summary>Bumps the sliding-window activity timestamp on an active session (standalone update).</summary>
    Task UpdateActivityAsync(Guid sessionId, DateTimeOffset lastActivityUtc, CancellationToken ct = default);

    /// <summary>Marks a single session inactive.</summary>
    Task InvalidateAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Marks every active session for a user inactive.</summary>
    Task InvalidateAllForUserAsync(Guid userId, CancellationToken ct = default);
}
