using Nabadat.UserManagement.Application.Auth.Dtos;
using Nabadat.UserManagement.Application.Auth.Exceptions;
using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Infrastructure.Crypto;
using System.Security.Cryptography;
using System.Text;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>
/// Self-service password reset. Requesting a reset is rate-limited, then the token
/// write and the synchronous M-09 delivery share one transaction — if delivery
/// fails the token is never persisted (FR-021). Redeeming validates the token
/// (expiry / used / revoked) and the new password's complexity before re-hashing.
/// </summary>
public sealed class PasswordResetService
{
    private const int TokenByteLength = 32;
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    private readonly IPasswordResetTokenService _tokens;
    private readonly ITenantUserService _users;
    private readonly IPasswordResetRateLimiter _rateLimiter;
    private readonly IM09NotificationService _notifications;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IPasswordHasher _hasher;
    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public PasswordResetService(
        IPasswordResetTokenService tokens,
        ITenantUserService users,
        IPasswordResetRateLimiter rateLimiter,
        IM09NotificationService notifications,
        IPasswordValidator passwordValidator,
        IPasswordHasher hasher,
        IUserManagementEventPublisher events,
        ITenantDbContext context,
        TimeProvider clock)
    {
        _tokens = tokens;
        _users = users;
        _rateLimiter = rateLimiter;
        _notifications = notifications;
        _passwordValidator = passwordValidator;
        _hasher = hasher;
        _events = events;
        _context = context;
        _clock = clock;
    }

    public async Task RequestResetAsync(string email, CancellationToken ct = default)
    {
        await _rateLimiter.EnsureWithinLimitAsync(email, ct);

        // No user enumeration: a non-existent email returns normally without a token.
        var user = await _users.GetByUsernameAsync(email, ct);
        if (user is null)
        {
            return;
        }

        var now = _clock.GetUtcNow();
        var rawToken = GenerateToken();
        var token = new PasswordResetToken
        {
            TokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = HashToken(rawToken),
            ExpiresAtUtc = now + TokenLifetime,
            Revoked = false,
            IssuedBy = "self-service",
            IssuedVia = "email",
            CreatedAt = now,
        };

        await _context.ExecuteAsync(async () =>
        {
            await _tokens.AddAsync(token, ct);
            // Synchronous delivery inside the transaction: a failure rolls the token write back.
            await _notifications.SendPasswordResetAsync(email, rawToken, ct);
            await _events.PublishAsync(ResetEvent(user, "password.reset.requested", now), ct);
        }, ct);
    }

    public async Task<bool> RedeemResetAsync(string rawToken, string newPassword, CancellationToken ct = default)
    {
        var token = await _tokens.GetByTokenHashAsync(HashToken(rawToken), ct);
        if (token is null)
        {
            throw new TokenExpiredException("The reset token is unknown.");
        }

        if (token.Revoked)
        {
            throw new TokenRevokedException();
        }

        if (token.UsedAtUtc is not null)
        {
            throw new TokenAlreadyUsedException();
        }

        var now = _clock.GetUtcNow();
        if (token.ExpiresAtUtc <= now)
        {
            throw new TokenExpiredException();
        }

        var validation = _passwordValidator.ValidatePassword(newPassword);
        if (!validation.IsValid)
        {
            throw new WeakPasswordException(validation.Errors);
        }

        var user = await _users.GetByIdAsync(token.UserId, ct)
            ?? throw new TokenExpiredException("The reset token references a missing user.");

        user.PasswordHash = _hasher.Hash(newPassword);
        user.RequiresPasswordChange = false;
        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _tokens.MarkUsedAsync(token.TokenId, now, ct);
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(ResetEvent(user, "password.reset.completed", now), ct);
        }, ct);

        // Phase 1 does not force MFA re-enrollment on password reset.
        return false;
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenByteLength))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] HashToken(string rawToken) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

    private static UserManagementEvent ResetEvent(TenantUser user, string eventType, DateTimeOffset now) => new()
    {
        EventType = eventType,
        ActorId = user.UserId,
        ActorPersona = user.Persona,
        EntityType = nameof(TenantUser),
        EntityId = user.UserId,
        OccurredAtUtc = now,
        CorrelationId = Guid.NewGuid(),
    };
}
