using Nabadat.UserManagement.Application.Auth.Dtos;
using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using System.Security.Cryptography;
using System.Text;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>
/// Issues and validates authenticated sessions. The session carries a permission
/// snapshot built at the user's current version; validation enforces both the
/// sliding-inactivity and absolute TTLs and rebuilds the snapshot on a version
/// mismatch (so a permission change propagates by the next request — FR-AUTHZ-021/022).
/// </summary>
public sealed class SessionService : ISessionService
{
    private const string TokenPrefix = "nbd_";
    private const int TokenByteLength = 32;
    private const short DefaultSlidingTtlMinutes = 60;
    private static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(24);

    private readonly ITenantUserService _users;
    private readonly IAuthSessionService _sessions;
    private readonly IPermissionModuleAssignmentService _permissions;
    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public SessionService(
        ITenantUserService users,
        IAuthSessionService sessions,
        IPermissionModuleAssignmentService permissions,
        IUserManagementEventPublisher events,
        ITenantDbContext context,
        TimeProvider clock)
    {
        _users = users;
        _sessions = sessions;
        _permissions = permissions;
        _events = events;
        _context = context;
        _clock = clock;
    }

    public async Task<SessionCreationResult> CreateSessionAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var snapshot = await BuildSnapshotAsync(user, ct);
        var rawToken = GenerateToken();
        var now = _clock.GetUtcNow();

        var session = new AuthSession
        {
            SessionId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = HashToken(rawToken),
            IssuedAtUtc = now,
            AbsoluteExpiresAtUtc = now + AbsoluteLifetime,
            LastActivityAtUtc = now,
            SlidingTtlMinutes = DefaultSlidingTtlMinutes,
            PermissionSnapshotVersion = snapshot.Version,
            PermissionSnapshot = snapshot,
            IsActive = true,
            CreatedAt = now,
        };

        await _context.ExecuteAsync(async () =>
        {
            await _sessions.AddAsync(session, ct);
            await _events.PublishAsync(SessionEvent(user, session, "session.created", now), ct);
        }, ct);

        return new SessionCreationResult { RawToken = rawToken, Session = session };
    }

    public async Task<SessionContext?> ValidateSessionAsync(string rawToken, CancellationToken ct = default)
    {
        var session = await _sessions.GetByTokenHashAsync(HashToken(rawToken), ct);
        if (session is null || !session.IsActive)
        {
            return null;
        }

        var now = _clock.GetUtcNow();
        if (session.AbsoluteExpiresAtUtc <= now)
        {
            return null;
        }

        if (session.LastActivityAtUtc.AddMinutes(session.SlidingTtlMinutes) <= now)
        {
            return null;
        }

        var user = await _users.GetByIdAsync(session.UserId, ct);
        if (user is null)
        {
            return null;
        }

        // Sliding-window reset on activity.
        await _sessions.UpdateActivityAsync(session.SessionId, now, ct);

        var snapshot = session.PermissionSnapshot;
        if (user.LastPermissionSnapshotVersion != session.PermissionSnapshotVersion)
        {
            snapshot = await BuildSnapshotAsync(user, ct);
        }

        return new SessionContext
        {
            SessionId = session.SessionId,
            UserId = user.UserId,
            Persona = user.Persona,
            PermissionSnapshot = snapshot,
        };
    }

    public async Task InvalidateSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        await _context.ExecuteAsync(async () =>
        {
            await _sessions.InvalidateAsync(sessionId, ct);
            await _events.PublishAsync(new UserManagementEvent
            {
                EventType = "session.revoked",
                ActorId = Guid.Empty,
                ActorPersona = string.Empty,
                EntityType = nameof(AuthSession),
                EntityId = sessionId,
                OccurredAtUtc = now,
                CorrelationId = Guid.NewGuid(),
            }, ct);
        }, ct);
    }

    private async Task<PermissionSnapshot> BuildSnapshotAsync(TenantUser user, CancellationToken ct)
    {
        var assignments = await _permissions.GetAssignmentsAsync(user.UserId, ct);
        var modules = assignments.ToDictionary(
            a => a.ModuleId,
            a => (IReadOnlyList<string>)a.AllowedModes.ToList());

        return new PermissionSnapshot
        {
            Version = user.LastPermissionSnapshotVersion,
            Modules = modules,
            CustomActions = [],
            ScopeAssignments = new Dictionary<string, IReadOnlyList<string>>(),
            HierarchyNodeId = user.OrganizationNodeId,
            HierarchyDescendantIds = [],
        };
    }

    private static string GenerateToken() =>
        TokenPrefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenByteLength));

    private static byte[] HashToken(string rawToken) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static UserManagementEvent SessionEvent(TenantUser user, AuthSession session, string eventType, DateTimeOffset now) => new()
    {
        EventType = eventType,
        ActorId = user.UserId,
        ActorPersona = user.Persona,
        EntityType = nameof(AuthSession),
        EntityId = session.SessionId,
        OccurredAtUtc = now,
        CorrelationId = Guid.NewGuid(),
    };
}
