using System.Security.Cryptography;
using System.Text;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Exceptions;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Auth.Interfaces;

namespace Nabadat.UserManagement.Application.Users;

/// <summary>
/// Tenant-user lifecycle service (US2, T077): create, (de)activate, unlock, and
/// admin MFA / password reset. Every action is gated by the P-01/P-07 data-layer
/// authority check (<see cref="UserCreationPolicy.EnsureCanManageUsers"/>) and
/// co-writes its M-17 audit event in the same transaction as the state change
/// (FR-015). Creation is delegated to <see cref="UserCreationPolicy"/> so baseline
/// provisioning and the create-authority rule live in one place.
/// </summary>
public sealed class UserManagementService
{
    private const int TokenByteLength = 32;
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);

    private readonly UserCreationPolicy _policy;
    private readonly ITenantUserService _users;
    private readonly IAuthSessionService _sessions;
    private readonly IPasswordResetTokenService _resetTokens;
    private readonly IM09NotificationService _notifications;
    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public UserManagementService(
        UserCreationPolicy policy,
        ITenantUserService users,
        IAuthSessionService sessions,
        IPasswordResetTokenService resetTokens,
        IM09NotificationService notifications,
        IUserManagementEventPublisher events,
        ITenantDbContext context,
        TimeProvider clock)
    {
        _policy = policy;
        _users = users;
        _sessions = sessions;
        _resetTokens = resetTokens;
        _notifications = notifications;
        _events = events;
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Data-layer read-authority check (<c>UserManagement.View</c>): throws
    /// <see cref="ForbiddenException"/> unless the actor is P-01/P-07. The list and
    /// user-detail read paths call this so the contract's persona restriction is
    /// enforced at the service layer, not just the UI.
    /// </summary>
    public void EnsureCanViewUsers(string actorPersona) => _policy.EnsureCanViewUsers(actorPersona);

    /// <summary>Creates a user (P-01/P-07 only) with an admin-set password and persona baseline permissions applied.</summary>
    public Task<TenantUser> CreateUserAsync(
        Guid tenantId,
        Guid actorId,
        string actorPersona,
        string newUsername,
        string newUserPersona,
        string password,
        CancellationToken ct = default) =>
        _policy.CreateUserAsync(tenantId, actorId, actorPersona, newUsername, newUserPersona, password, ct);

    /// <summary>
    /// Updates a user's profile. Only P-01 may change the <paramref name="newPersona"/>;
    /// a P-07 actor attempting a persona change is rejected with
    /// <see cref="ForbiddenException"/>. The organization node is replaced as supplied
    /// (a <c>null</c> clears it). Publishes <c>user.updated</c>.
    /// </summary>
    public async Task UpdateProfileAsync(
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        string? newPersona,
        Guid? newOrganizationNodeId,
        CancellationToken ct = default)
    {
        _policy.EnsureCanManageUsers(actorPersona);
        var user = await LoadAsync(targetUserId, ct);

        if (newPersona is not null && newPersona != user.Persona && actorPersona != "P-01")
        {
            throw new ForbiddenException("Only P-01 may change a user's persona.", "users.persona_change_forbidden");
        }

        var now = _clock.GetUtcNow();
        // Capture the prior profile before mutating, for the audit event's oldValue.
        var oldValue = new { user.Persona, user.OrganizationNodeId };

        if (newPersona is not null)
        {
            user.Persona = newPersona;
        }

        user.OrganizationNodeId = newOrganizationNodeId;
        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(LifecycleEvent("user.updated", actorId, actorPersona, user, now, oldValue), ct);
        }, ct);
    }

    /// <summary>Soft-deletes a user (status <c>inactive</c>) and revokes all active sessions.</summary>
    public async Task DeactivateUserAsync(Guid actorId, string actorPersona, Guid targetUserId, CancellationToken ct = default)
    {
        _policy.EnsureCanManageUsers(actorPersona);
        var user = await LoadAsync(targetUserId, ct);
        var now = _clock.GetUtcNow();
        var oldValue = new { Status = user.Status.ToWire() };
        user.Status = UserStatus.Inactive;
        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _users.UpdateAsync(user, ct);
            // A deactivated user must not retain an exercisable session.
            await _sessions.InvalidateAllForUserAsync(targetUserId, ct);
            await _events.PublishAsync(LifecycleEvent("user.deactivated", actorId, actorPersona, user, now, oldValue), ct);
        }, ct);
    }

    /// <summary>Re-activates an inactive user (status <c>active</c>).</summary>
    public async Task ReactivateUserAsync(Guid actorId, string actorPersona, Guid targetUserId, CancellationToken ct = default)
    {
        _policy.EnsureCanManageUsers(actorPersona);
        var user = await LoadAsync(targetUserId, ct);
        var now = _clock.GetUtcNow();
        var oldValue = new { Status = user.Status.ToWire() };
        user.Status = UserStatus.Active;
        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(LifecycleEvent("user.reactivated", actorId, actorPersona, user, now, oldValue), ct);
        }, ct);
    }

    /// <summary>
    /// Manually unlocks a locked account before the cooldown expires. Throws
    /// <see cref="InvalidOperationException"/> (mapped to 409) when the account is not locked.
    /// </summary>
    public async Task UnlockUserAsync(Guid actorId, string actorPersona, Guid targetUserId, CancellationToken ct = default)
    {
        _policy.EnsureCanManageUsers(actorPersona);
        var user = await LoadAsync(targetUserId, ct);
        if (user.Status != UserStatus.Locked && user.LockedUntilUtc is null)
        {
            throw new InvalidOperationException($"User {targetUserId} is not locked.");
        }

        var now = _clock.GetUtcNow();
        var oldValue = new { Status = user.Status.ToWire(), user.FailedAttemptCount, user.LockedUntilUtc };
        user.Status = UserStatus.Active;
        user.FailedAttemptCount = 0;
        user.LockedUntilUtc = null;
        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(LifecycleEvent("authentication.account.unlocked", actorId, actorPersona, user, now, oldValue), ct);
        }, ct);
    }

    /// <summary>
    /// Admin-triggered MFA reset: clears the user's enrolled authenticator and forces
    /// re-enrollment on next login (status <c>pending-enrollment</c>); revokes active sessions.
    /// </summary>
    public async Task AdminMfaResetAsync(Guid actorId, string actorPersona, Guid targetUserId, CancellationToken ct = default)
    {
        _policy.EnsureCanManageUsers(actorPersona);
        var user = await LoadAsync(targetUserId, ct);
        var now = _clock.GetUtcNow();
        var oldValue = new { user.IsMfaEnrolled, Status = user.Status.ToWire() };
        user.IsMfaEnrolled = false;
        user.MfaSecretEncrypted = null;
        user.MfaSecretKeyRef = null;
        user.LastUsedTotpStep = null;
        user.Status = UserStatus.PendingEnrollment;
        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _users.UpdateAsync(user, ct);
            // Force re-authentication so the cleared MFA secret can't be bypassed by a live session.
            await _sessions.InvalidateAllForUserAsync(targetUserId, ct);
            await _events.PublishAsync(LifecycleEvent("mfa.reset", actorId, actorPersona, user, now, oldValue), ct);
        }, ct);
    }

    /// <summary>
    /// Admin-triggered password reset: flags <c>requiresPasswordChange</c>, issues an
    /// admin-scoped reset token, and delivers it via M-09 synchronously inside the
    /// transaction. If M-09 is unavailable the delivery throws and the whole unit of
    /// work rolls back — no state change persists (mapped to 503).
    /// </summary>
    public async Task AdminPasswordResetAsync(Guid actorId, string actorPersona, Guid targetUserId, CancellationToken ct = default)
    {
        _policy.EnsureCanManageUsers(actorPersona);
        var user = await LoadAsync(targetUserId, ct);
        var now = _clock.GetUtcNow();

        var rawToken = GenerateToken();
        var token = new PasswordResetToken
        {
            TokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = HashToken(rawToken),
            ExpiresAtUtc = now + ResetTokenLifetime,
            Revoked = false,
            IssuedBy = "admin",
            IssuedVia = "email",
            CreatedAt = now,
        };

        user.RequiresPasswordChange = true;
        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _resetTokens.AddAsync(token, ct);
            await _users.UpdateAsync(user, ct);
            // Synchronous delivery inside the transaction: M-09 failure rolls everything back.
            await _notifications.SendPasswordResetAsync(user.Username, rawToken, ct);
            await _events.PublishAsync(LifecycleEvent("password.reset.requested", actorId, actorPersona, user, now), ct);
        }, ct);
    }

    /// <summary>
    /// Right to Erasure (GP-03): nulls PII on the user (<c>Username</c>,
    /// <c>PasswordHash</c>, <c>MfaSecretEncrypted</c>, <c>MfaSecretKeyRef</c>),
    /// hard-deletes related <c>AuthSession</c> and <c>PasswordResetToken</c> rows,
    /// and publishes a <c>user.erased</c> event — all in one unit of work. Distinct from
    /// soft-deactivation, which retains PII for audit history. Full implementation depends
    /// on hard-delete support (tracked separately); stub retained until then.
    /// </summary>
    public Task EraseUserAsync(Guid userId, CancellationToken ct = default) =>
        throw new NotImplementedException(
            "GP-03 Right to Erasure requires repository hard-delete support (tracked separately).");

    private async Task<TenantUser> LoadAsync(Guid userId, CancellationToken ct) =>
        await _users.GetByIdAsync(userId, ct)
        ?? throw new KeyNotFoundException($"User {userId} does not exist.");

    private static UserManagementEvent LifecycleEvent(
        string eventType, Guid actorId, string actorPersona, TenantUser user, DateTimeOffset now, object? oldValue = null) => new()
    {
        EventType = eventType,
        ActorId = actorId,
        ActorPersona = actorPersona,
        EntityType = nameof(TenantUser),
        EntityId = user.UserId,
        OldValue = oldValue,
        NewValue = new { user.Username, user.Persona, Status = user.Status.ToWire() },
        OccurredAtUtc = now,
        CorrelationId = Guid.NewGuid(),
    };

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenByteLength))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] HashToken(string rawToken) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
}
