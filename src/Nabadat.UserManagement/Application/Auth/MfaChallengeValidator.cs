using Nabadat.UserManagement.Application.Auth.Dtos;
using Nabadat.UserManagement.Application.Auth.Exceptions;
using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Infrastructure.Crypto;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>
/// Second step of the authentication exchange: verifies a TOTP code against the
/// challenge's user and, on success, creates the session (FR-AUTH-003). Every
/// outcome is audited (<c>authentication.mfa.succeeded</c> / <c>.failed</c>); a
/// failure also records a lockout attempt. Anti-replay is enforced by passing the
/// user's last accepted TOTP step to the verifier and persisting the matched step.
/// </summary>
public sealed class MfaChallengeValidator
{
    private readonly IMfaChallengeService _challenges;
    private readonly ITenantUserService _users;
    private readonly ITotpService _totp;
    private readonly IMfaSecretEncryptionService _encryption;
    private readonly ISessionService _sessions;
    private readonly IAccountLockout _lockout;
    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public MfaChallengeValidator(
        IMfaChallengeService challenges,
        ITenantUserService users,
        ITotpService totp,
        IMfaSecretEncryptionService encryption,
        ISessionService sessions,
        IAccountLockout lockout,
        IUserManagementEventPublisher events,
        ITenantDbContext context,
        TimeProvider clock)
    {
        _challenges = challenges;
        _users = users;
        _totp = totp;
        _encryption = encryption;
        _sessions = sessions;
        _lockout = lockout;
        _events = events;
        _context = context;
        _clock = clock;
    }

    public async Task<MfaChallengeResult> VerifyAsync(string challengeId, string totpCode, CancellationToken ct = default)
    {
        var challenge = _challenges.ResolveChallenge(challengeId)
            ?? throw new MfaValidationException("The challenge is unknown or has expired.");

        var user = await _users.GetByIdAsync(challenge.UserId, ct)
            ?? throw new MfaValidationException("The challenge is unknown or has expired.");

        if (user.MfaSecretEncrypted is null || user.MfaSecretKeyRef is null)
        {
            throw new MfaValidationException("MFA is not enrolled for this user.");
        }

        var secret = await _encryption.DecryptAsync(user.MfaSecretEncrypted, user.MfaSecretKeyRef, ct);
        var verification = _totp.VerifyCode(secret, totpCode, user.LastUsedTotpStep);

        if (!verification.IsValid)
        {
            await _lockout.RecordFailedAttemptAsync(user.UserId, ct);
            await _context.ExecuteAsync(() => _events.PublishAsync(MfaEvent(user, "authentication.mfa.failed"), ct), ct);
            throw new MfaValidationException();
        }

        var now = _clock.GetUtcNow();
        user.LastUsedTotpStep = verification.MatchedStep;
        user.FailedAttemptCount = 0;
        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(MfaEvent(user, "authentication.mfa.succeeded"), ct);
        }, ct);

        var session = await _sessions.CreateSessionAsync(user.UserId, ct);
        _challenges.ConsumeChallenge(challengeId);

        return new MfaChallengeResult
        {
            SessionToken = session.RawToken,
            UserId = user.UserId,
            ExpiresAtUtc = session.Session.AbsoluteExpiresAtUtc,
            PermissionSnapshot = session.Session.PermissionSnapshot,
        };
    }

    private UserManagementEvent MfaEvent(TenantUser user, string eventType) => new()
    {
        EventType = eventType,
        ActorId = user.UserId,
        ActorPersona = user.Persona,
        EntityType = nameof(TenantUser),
        EntityId = user.UserId,
        OccurredAtUtc = _clock.GetUtcNow(),
        CorrelationId = Guid.NewGuid(),
    };
}
