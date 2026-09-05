namespace Nabadat.UserManagement.Application.Auth.Exceptions;

/// <summary>Thrown when self-service password-reset requests exceed the per-email window limit.</summary>
public sealed class PasswordResetRateLimitExceededException : Exception
{
    public PasswordResetRateLimitExceededException(int retryAfterSeconds)
        : base("Too many password reset requests. Try again later.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int RetryAfterSeconds { get; }
}
