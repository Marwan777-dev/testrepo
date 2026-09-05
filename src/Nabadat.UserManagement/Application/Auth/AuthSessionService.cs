using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Auth.Interfaces;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>
/// EF <see cref="IAuthSessionService"/> over <see cref="ITenantDbContext"/>. The set-based
/// invalidations / activity bump use <c>ExecuteUpdateAsync</c> (a LINQ method, not raw
/// SQL); inside a <c>ITenantDbContext.ExecuteAsync</c> they run on the ambient transaction and so
/// commit atomically with the rest of the unit of work.
/// </summary>
public sealed class AuthSessionService : IAuthSessionService
{
    private readonly ITenantDbContext _context;

    public AuthSessionService(ITenantDbContext context) => _context = context;

    public async Task<AuthSession?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default) =>
        await _context.AuthSessions.FirstOrDefaultAsync(s => s.TokenHash == tokenHash, ct);

    public async Task AddAsync(AuthSession session, CancellationToken ct = default)
    {
        _context.AuthSessions.Add(session);
        await _context.SaveChangesAsync(ct);
    }

    public Task UpdateActivityAsync(Guid sessionId, DateTimeOffset lastActivityUtc, CancellationToken ct = default) =>
        _context.AuthSessions
            .Where(s => s.SessionId == sessionId && s.IsActive)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.LastActivityAtUtc, lastActivityUtc), ct);

    public Task InvalidateAsync(Guid sessionId, CancellationToken ct = default) =>
        _context.AuthSessions
            .Where(s => s.SessionId == sessionId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.IsActive, false), ct);

    public Task InvalidateAllForUserAsync(Guid userId, CancellationToken ct = default) =>
        _context.AuthSessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.IsActive, false), ct);
}
