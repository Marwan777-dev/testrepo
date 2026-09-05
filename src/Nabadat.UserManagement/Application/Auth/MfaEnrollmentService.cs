using Nabadat.UserManagement.Application.Auth.Dtos;
using Nabadat.UserManagement.Application.Auth.Exceptions;
using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.Crypto;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>
/// First-time (or post-reset) TOTP enrollment. Initiation generates a secret and
/// provisioning URI; confirmation verifies the first code, envelope-encrypts and
/// persists the secret (GP-02), marks the user enrolled, and creates the session.
/// </summary>
public sealed class MfaEnrollmentService
{
    private readonly IMfaChallengeService _challenges;
    private readonly ITenantUserService _users;
    private readonly ITotpService _totp;
    private readonly IMfaSecretEncryptionService _encryption;
    private readonly ISessionService _sessions;
    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public MfaEnrollmentService(
        IMfaChallengeService challenges,
        ITenantUserService users,
        ITotpService totp,
        IMfaSecretEncryptionService encryption,
        ISessionService sessions,
        IUserManagementEventPublisher events,
        ITenantDbContext context,
        TimeProvider clock)
    {
        _challenges = challenges;
        _users = users;
        _totp = totp;
        _encryption = encryption;
        _sessions = sessions;
        _events = events;
        _context = context;
        _clock = clock;
    }

    public async Task<EnrollmentInitiation> InitiateEnrollmentAsync(string challengeId, CancellationToken ct = default)
    {
        var challenge = _challenges.ResolveChallenge(challengeId)
            ?? throw new MfaValidationException("The challenge is unknown or has expired.");

        var user = await _users.GetByIdAsync(challenge.UserId, ct)
            ?? throw new MfaValidationException("The challenge is unknown or has expired.");

        var secret = _totp.GenerateSecret();
        var uri = _totp.GetOtpUri(user.Username, secret);
        var enrollmentToken = _challenges.CreateEnrollment(user.UserId, secret);

        return new EnrollmentInitiation
        {
            OtpauthUri = uri,
            Base32Secret = secret,
            EnrollmentToken = enrollmentToken,
        };
    }

    public async Task<MfaChallengeResult> ConfirmEnrollmentAsync(string enrollmentToken, string totpCode, CancellationToken ct = default)
    {
        var enrollment = _challenges.ResolveEnrollment(enrollmentToken)
            ?? throw new MfaValidationException("The enrollment is unknown or has expired.");

        var verification = _totp.VerifyCode(enrollment.Base32Secret, totpCode, lastUsedStep: null);
        if (!verification.IsValid)
        {
            throw new MfaValidationException();
        }

        var user = await _users.GetByIdAsync(enrollment.UserId, ct)
            ?? throw new MfaValidationException("The enrollment references a missing user.");

        var encrypted = await _encryption.EncryptAsync(enrollment.Base32Secret, ct);
        var now = _clock.GetUtcNow();

        user.MfaSecretEncrypted = encrypted.Cipher;
        user.MfaSecretKeyRef = encrypted.KeyRef;
        user.IsMfaEnrolled = true;
        user.Status = UserStatus.Active;
        user.LastUsedTotpStep = verification.MatchedStep;
        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(new UserManagementEvent
            {
                EventType = "mfa.enrolled",
                ActorId = user.UserId,
                ActorPersona = user.Persona,
                EntityType = nameof(TenantUser),
                EntityId = user.UserId,
                OccurredAtUtc = now,
                CorrelationId = Guid.NewGuid(),
            }, ct);
        }, ct);

        var session = await _sessions.CreateSessionAsync(user.UserId, ct);
        _challenges.ConsumeEnrollment(enrollmentToken);

        return new MfaChallengeResult
        {
            SessionToken = session.RawToken,
            UserId = user.UserId,
            ExpiresAtUtc = session.Session.AbsoluteExpiresAtUtc,
            PermissionSnapshot = session.Session.PermissionSnapshot,
        };
    }
}
