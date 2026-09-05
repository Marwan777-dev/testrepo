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
using System.Net.Mail;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>
/// First step of the authentication exchange: validates username/password and,
/// on success, issues a short-lived MFA challenge (no session is created here —
/// FR-AUTH-003). Also provisions a tenant user with a hashed password. A locked
/// account whose cooldown has not elapsed raises <see cref="AccountLockedException"/>.
/// </summary>
public sealed class TenantAuthenticationService
{
    private readonly ITenantUserService _users;
    private readonly IPasswordHasher _hasher;
    private readonly IMfaChallengeService _challenges;
    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public TenantAuthenticationService(
        ITenantUserService users,
        IPasswordHasher hasher,
        IMfaChallengeService challenges,
        IUserManagementEventPublisher events,
        ITenantDbContext context,
        TimeProvider clock)
    {
        _users = users;
        _hasher = hasher;
        _challenges = challenges;
        _events = events;
        _context = context;
        _clock = clock;
    }

    public async Task<CreateUserResult> CreateUserAsync(string username, string password, string persona, CancellationToken ct = default)
    {
        if (!IsValidEmail(username))
        {
            return CreateUserResult.InvalidEmail();
        }

        if (await _users.ExistsAsync(username, ct))
        {
            return CreateUserResult.Conflict();
        }

        var now = _clock.GetUtcNow();
        var user = new TenantUser
        {
            UserId = Guid.NewGuid(),
            Username = username,
            PasswordHash = _hasher.Hash(password),
            Persona = persona,
            Status = UserStatus.PendingEnrollment,
            IsMfaEnrolled = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _context.ExecuteAsync(async () =>
        {
            await _users.AddAsync(user, ct);
            await _events.PublishAsync(new UserManagementEvent
            {
                EventType = "user.created",
                ActorId = user.UserId,
                ActorPersona = persona,
                EntityType = nameof(TenantUser),
                EntityId = user.UserId,
                NewValue = new { user.Username, user.Persona, Status = user.Status.ToWire() },
                OccurredAtUtc = now,
                CorrelationId = Guid.NewGuid(),
            }, ct);
        }, ct);

        return CreateUserResult.Created(user.UserId);
    }

    public async Task<CredentialValidationResult> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await _users.GetByUsernameAsync(username, ct);
        if (user is null)
        {
            return CredentialValidationResult.InvalidCredentials();
        }

        if (user.Status == UserStatus.Locked && user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > _clock.GetUtcNow())
        {
            throw new AccountLockedException(user.UserId, user.LockedUntilUtc.Value);
        }

        if (!_hasher.Verify(password, user.PasswordHash))
        {
            return CredentialValidationResult.InvalidCredentials();
        }

        if (!user.IsMfaEnrolled)
        {
            var enrollChallenge = _challenges.CreateChallenge(user.UserId, requiresEnrollment: true);
            return CredentialValidationResult.RequiresMfaEnrollment(enrollChallenge);
        }

        var challenge = _challenges.CreateChallenge(user.UserId, requiresEnrollment: false);
        return CredentialValidationResult.ChallengeIssued(challenge);
    }

    private static bool IsValidEmail(string value) =>
        !string.IsNullOrWhiteSpace(value) && MailAddress.TryCreate(value, out _);
}
