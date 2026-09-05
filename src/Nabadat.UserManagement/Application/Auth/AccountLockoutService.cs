using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>
/// Tracks failed authentication attempts, locks an account on the 5th consecutive
/// failure for a fixed cooldown, and unlocks it (automatically once the cooldown
/// elapses, or manually by an admin). Each lock/unlock is audited via M-17.
/// </summary>
public sealed class AccountLockoutService : IAccountLockout
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly ITenantUserService _users;
    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public AccountLockoutService(
        ITenantUserService users,
        IUserManagementEventPublisher events,
        ITenantDbContext context,
        TimeProvider clock)
    {
        _users = users;
        _events = events;
        _context = context;
        _clock = clock;
    }

    public async Task RecordFailedAttemptAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return;
        }

        var now = _clock.GetUtcNow();
        user.FailedAttemptCount = (short)(user.FailedAttemptCount + 1);
        var locking = user.FailedAttemptCount >= MaxFailedAttempts && user.Status != UserStatus.Locked;
        if (locking)
        {
            user.Status = UserStatus.Locked;
            user.LockedUntilUtc = now + LockoutDuration;
        }

        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _users.UpdateAsync(user, ct);
            if (locking)
            {
                await _events.PublishAsync(LockEvent(user, "authentication.account.locked", now), ct);
            }
        }, ct);
    }

    public async Task<bool> AutoUnlockIfExpiredAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return false;
        }

        var now = _clock.GetUtcNow();
        if (user.Status != UserStatus.Locked || user.LockedUntilUtc is null || user.LockedUntilUtc > now)
        {
            return false;
        }

        await ClearLockAsync(user, now, ct);
        return true;
    }

    public async Task UnlockAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || user.Status != UserStatus.Locked)
        {
            return;
        }

        await ClearLockAsync(user, _clock.GetUtcNow(), ct);
    }

    private async Task ClearLockAsync(Domain.Entities.TenantUser user, DateTimeOffset now, CancellationToken ct)
    {
        user.Status = UserStatus.Active;
        user.FailedAttemptCount = 0;
        user.LockedUntilUtc = null;
        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(LockEvent(user, "authentication.account.unlocked", now), ct);
        }, ct);
    }

    private static UserManagementEvent LockEvent(Domain.Entities.TenantUser user, string eventType, DateTimeOffset now) => new()
    {
        EventType = eventType,
        ActorId = user.UserId,
        ActorPersona = user.Persona,
        EntityType = nameof(Domain.Entities.TenantUser),
        EntityId = user.UserId,
        NewValue = new { user.Status, user.LockedUntilUtc, user.FailedAttemptCount },
        OccurredAtUtc = now,
        CorrelationId = Guid.NewGuid(),
    };
}
