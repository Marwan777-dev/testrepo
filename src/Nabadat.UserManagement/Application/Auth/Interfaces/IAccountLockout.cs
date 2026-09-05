namespace Nabadat.UserManagement.Application.Auth.Interfaces;

/// <summary>Failed-attempt counting, automatic cooldown lockout, and unlock for tenant users.</summary>
public interface IAccountLockout
{
    /// <summary>Records a failed auth attempt; locks the account on the configured threshold.</summary>
    Task RecordFailedAttemptAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Unlocks the account if it is locked and the cooldown has elapsed. Returns true if it unlocked.</summary>
    Task<bool> AutoUnlockIfExpiredAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Administratively unlocks a locked account immediately.</summary>
    Task UnlockAsync(Guid userId, CancellationToken ct = default);
}
