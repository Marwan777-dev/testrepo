namespace Nabadat.UserManagement.Application.Auth.Exceptions;

/// <summary>Thrown when authentication is attempted against a locked account whose cooldown has not elapsed.</summary>
public sealed class AccountLockedException : Exception
{
    public AccountLockedException(Guid userId, DateTimeOffset lockedUntilUtc)
        : base($"Account {userId} is locked until {lockedUntilUtc:O}.")
    {
        UserId = userId;
        LockedUntilUtc = lockedUntilUtc;
    }

    public Guid UserId { get; }

    public DateTimeOffset LockedUntilUtc { get; }
}
